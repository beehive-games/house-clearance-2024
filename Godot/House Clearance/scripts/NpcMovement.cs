using System.Diagnostics;
using Godot;
using Godot.Collections;

namespace HouseClearance.scripts;

public partial class NpcMovement : CharacterBody2D
{
	[Export] private float _speed = 300.0f;
	[Export] private float _aggroNoiseLevel = 3.0f;	// Will move to AggroDistance if noise is above this level at this point
	[Export] private float _aggroDistance = 200.0f;	// At what distance, with line of sight to the player, will the NPC pursue player
	[Export] private float _pursueDistance = 200.0f;	// When spotted, how far will NPC pursue player before returning to starting point
	[Export] private float _patrolRadius = 200.0f;	// How far will NPC patrol from spawn location
	[Export] private float _patrolWaitTime = 2.0f;	// How long can the NPC wait before finding a new target position?
	[Export] private bool _canTeleport;					// Will the NPC go through teleport locations when pursuing
	[Export] private float _health = 100f;
	[Export] private float _fallDeathVelocity = 10f;
	[Export] private float _stunnedTime = 4f;
	private AnimatedSprite2D _spriteNodePath;
	[Export(PropertyHint.Layers2DPhysics)] private uint _floorCollisionCheckLayer;
	[Export(PropertyHint.Layers2DPhysics)] private uint _playerVisibilityCheckLayer;

	
	public enum MoveState { Idle, Move, Fall, Slide, Cover, Dead, StunnedFloor, Stop = -1 };
	public MoveState _moveState = MoveState.Idle;
	public enum DeadState { Fall, Shot, HeadShot };
	private DeadState _deadState = DeadState.Fall;
	private Vector2 _startPosition;
	private Vector2 _targetPosition;
	private Timer _patrolWaitTimer;
	private Timer _stunnedWaitTimer;
	private Vector2 _previousFloorPosition;
	[Export] private Line2D _debugLine;
	private bool _pursuing;
	

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	private float _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
	
	public void Hit(Projectile projectile, Vector2 direction, float damage, DeadState damagedBy, ref bool lostHead)
	{
		TakeDamage(damage, damagedBy);
		if (_moveState == MoveState.Dead) return;
		Vector2 velocity = Velocity;
		velocity += projectile.HitForce * direction;
		Velocity = velocity;
		MoveAndSlide();
		projectile.Kill();
		lostHead = _deadState == DeadState.HeadShot;
	}

	private void PlayerCheck()
	{
		if (CheckPlayerState() && CheckDistanceToPlayer() && CheckVisibilityToPlayer())
		{
			Debug.WriteLine(Name + ": Player found!");
			_pursuing = true;
		}
	}

	private bool CheckPlayerState()
	{
		PlayerMovement player = PlayerMovement.Player;
		if(player == null) return false;
		var moveState = player._moveState;

		bool validTargetToPursue = moveState switch
		{
			PlayerMovement.MoveState.Move => true,
			PlayerMovement.MoveState.Idle => true,
			PlayerMovement.MoveState.Cover => _pursuing ? true : false,
			PlayerMovement.MoveState.Dead => false,
			PlayerMovement.MoveState.Slide => true,
			PlayerMovement.MoveState.Fall => true,
			PlayerMovement.MoveState.Stop => false,
			_ => false
		};
		Debug.WriteLine(Name + ": CheckPlayerState >> "+validTargetToPursue);

		return validTargetToPursue;
	}
	
	private bool CheckDistanceToPlayer()
	{
		if (PlayerMovement.Player == null) return false;
		bool distanceCheck = PlayerMovement.Player.GlobalPosition.DistanceTo(GlobalPosition) < _aggroDistance;
		Debug.WriteLine(Name + ": CheckDistanceToPlayer >> "+distanceCheck);
		return distanceCheck;
	}

	private bool CheckVisibilityToPlayer()
	{
		Vector2 rayStartPosition = GlobalPosition;
		rayStartPosition.Y -= 10;
		
		Vector2 direction = new Vector2(_spriteNodePath.FlipH ? -1f : 1f, 0f);
		float distance = _aggroDistance;
		
		Vector2 rayDirection = direction * distance;
		var spaceState = GetWorld2D().DirectSpaceState;
		var rayQuery = PhysicsRayQueryParameters2D.Create(rayStartPosition, rayStartPosition + rayDirection);
		rayQuery.CollideWithAreas = true;
		rayQuery.CollisionMask = _playerVisibilityCheckLayer;
		Array<Rid> excludeList = new Array<Rid> { GetRid() };
		rayQuery.Exclude = excludeList;
		var rayResult = spaceState.IntersectRay(rayQuery);
		
		var root = GetTree().Root;
		_debugLine ??= root.GetNodeOrNull<Line2D>("DebugLine");
		if (_debugLine != null)
		{
			Vector2[] points = _debugLine.Points;
			points[0] = rayStartPosition;
			points[1] = rayStartPosition + rayDirection;
			_debugLine.Points = points;
			Gradient colors = _debugLine.Gradient;
			colors.SetColor(1, Colors.Red);
			colors.SetColor(0, Colors.Red);
			_debugLine.Gradient = colors;
		}
		
		if (rayResult.Count > 0f)
		{
			var rayRes = rayResult["collider"];
			var rayPos = (Vector2)rayResult["position"];
			
			if (_debugLine != null)
			{
				Vector2[] points = _debugLine.Points;
				points[0] = rayStartPosition;
				points[1] = rayPos;
				_debugLine.Points = points;
				Gradient colors = _debugLine.Gradient;
				colors.SetColor(1, Colors.Pink);
				colors.SetColor(0, Colors.Pink);
				_debugLine.Gradient = colors;
				Debug.WriteLine("found " + _debugLine.Points[0] +","+_debugLine.Points[1]);
			}
			
			if (rayRes.Obj is PlayerMovement player)
			{
				Debug.WriteLine(Name + ": CheckVisibilityToPlayer >> "+true);

				if (_debugLine != null)
				{
					Gradient colors = _debugLine.Gradient;
					colors.SetColor(1, Colors.Green);
					colors.SetColor(0, Colors.Green);
					_debugLine.Gradient = colors;
				}

				return true;
			}
			else
			{
				if (rayRes.Obj is Node2D n2d)
				{
					Debug.WriteLine(n2d.Name + ": rayRes");

				}

				if (_debugLine != null)
				{
					Gradient colors = _debugLine.Gradient;
					colors.SetColor(1, Colors.Blue);
					colors.SetColor(0, Colors.Blue);
					_debugLine.Gradient = colors;
				}
			}
		}

		Debug.WriteLine(Name + ": CheckVisibilityToPlayer >> "+false + ", count = "+rayResult.Count);

		return false;
	}
	
	private void Kill(DeadState killedBy)
	{
		_moveState = MoveState.Dead;
		_deadState = killedBy;
		if (_spriteNodePath != null)
		{
			switch (killedBy)
			{
				case DeadState.Fall :
					_spriteNodePath.Animation = "dead_fall"; break;
				case DeadState.Shot :
					_spriteNodePath.Animation = "dead_shot"; break;
				case DeadState.HeadShot :
					_spriteNodePath.Animation = "dead_headshot";break;
			}
		}
		var bodyShape = GetNodeOrNull<Area2D>("BodyHB");
		var headShape = GetNodeOrNull<Area2D>("HeadHB");
		CollisionLayer = 0;
		//collShape?.QueueFree();
		bodyShape?.QueueFree();
		headShape?.QueueFree();
	}
	
	private void TakeDamage(float damage, DeadState damagedBy)
	{
		if (_moveState == MoveState.Dead) return;
		
		_health -= damage;
		if (_health <= 0f)
		{
			Debug.WriteLine("Damage taken - person is dead!");
			Kill(damagedBy);
		}
	}

	private void CancelStunnedTimer()
	{
		_stunnedWaitTimer?.Stop();
		_stunnedWaitTimer?.QueueFree();
		_stunnedWaitTimer = null;
	}

	private void FinishedStunnedTimer()
	{
		CancelStunnedTimer();
		if (_moveState == MoveState.StunnedFloor)
		{
			_moveState = MoveState.Idle;
			_spriteNodePath.Animation = "idle";
		}
	}
	
	public void BeginStunnedTimer()
	{
		_stunnedWaitTimer = new Timer();
		_stunnedWaitTimer = new Timer();
		_stunnedWaitTimer = new Timer();
		_stunnedWaitTimer.WaitTime = _stunnedTime;
		_stunnedWaitTimer.OneShot = true;
		_stunnedWaitTimer.Autostart = true;
		_stunnedWaitTimer.Timeout += FinishedStunnedTimer;	
		var root = GetTree().Root;
		root.AddChild(_stunnedWaitTimer);
		_moveState = MoveState.StunnedFloor;
		_spriteNodePath.Animation = "stunned_floor";
	}

	private void CancelWaitTimer()
	{
		_patrolWaitTimer?.Stop();
		_patrolWaitTimer?.QueueFree();
		_patrolWaitTimer = null;
	}
	
	private void FinishedWaiting()
	{
		CancelWaitTimer();
		_targetPosition = GetNewTargetPosition();
	}

	private void ReachedDestination()
	{
		CancelWaitTimer();
		_patrolWaitTimer = new Timer();
		float randomWait = new RandomNumberGenerator().RandfRange(0f,_patrolWaitTime);
		_patrolWaitTimer.WaitTime = randomWait;
		_patrolWaitTimer.OneShot = true;
		_patrolWaitTimer.Autostart = true;
		_patrolWaitTimer.Timeout += FinishedWaiting;	
		var root = GetTree().Root;
		root.AddChild(_patrolWaitTimer);
	}

	private Vector2 GetNewTargetPosition()
	{
		float randomPosOffset = new RandomNumberGenerator().RandfRange(-_patrolRadius,_patrolRadius);
		Vector2 newPos = _startPosition;
		newPos.X += randomPosOffset;

		var spaceState = GetWorld2D().DirectSpaceState;
		var startPosition = newPos;
		startPosition.Y -= 10;

		var distance = 13f;
		var direction = Vector2.Down * distance;
		
		var root = GetTree().Root.GetNode("Game");
		
		// Check for walls and door...
		var rayQuery = PhysicsRayQueryParameters2D.Create(startPosition, startPosition + direction);
		rayQuery.CollisionMask = _floorCollisionCheckLayer;
		Array<Rid> excludeList = new Array<Rid> { GetRid() };
		rayQuery.Exclude = excludeList;
		var rayResult = spaceState.IntersectRay(rayQuery);
		
		
		if (rayResult.Count > 0)
		{
			var rayRes = rayResult["collider"];
			if (rayRes.Obj is StaticBody2D sbody)
			{
				//Debug.WriteLine(sbody.Name);
				/*if (_debugLine != null)
				{
					Vector2[] points = _debugLine.Points;
					points[0] = startPosition;
					points[1] = (Vector2)rayResult["position"];
					_debugLine.Points = points;
					Gradient colors = _debugLine.Gradient;
					colors.SetColor(1, Colors.Aqua);
					_debugLine.Gradient = colors;
				}*/
			}
		}
		else
		{
			// not a valid surface we can expect to walk on, return current position
			// this results in calling this method again next frame
			return GlobalPosition;
		}

		
		return newPos;
	}
	
	public override void _Ready()
	{
		_spriteNodePath = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_startPosition = GlobalPosition;
		FinishedWaiting();
		Debug.WriteLine("RID: " +GetRid());
	}
	
	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		bool onFloor = IsOnFloor();
		
		// Add the gravity.
		if (!onFloor)
		{
			velocity.Y += _gravity * (float)delta;
		}

		if (_moveState == MoveState.Dead || _moveState == MoveState.StunnedFloor)
		{
			velocity.X = 0;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		PlayerCheck();
		
		var dir = Mathf.Sign((_targetPosition-GlobalPosition).X);
		_spriteNodePath.FlipH = dir < 0 ? true : dir > 0 ? false : _spriteNodePath.FlipH;
		var positionDelta = Mathf.Abs(GlobalPosition.X - _targetPosition.X);
		var atDestination = positionDelta < 2;
		
		if (atDestination && _patrolWaitTimer == null )
		{
			ReachedDestination();
			velocity.X = Mathf.MoveToward(Velocity.X, 0, _speed);
		}
		else
		{
			if (IsOnWall())
			{
				ReachedDestination();
				velocity.X = Mathf.MoveToward(Velocity.X, 0, _speed);
			}
			else
			{
				velocity.X = _speed * dir;
			}
		}
		
		float vY = Velocity.Y;
		Velocity = velocity;
		MoveAndSlide();

		if (!onFloor && IsOnFloorOnly())
		{
			if (Mathf.Abs(vY) > _fallDeathVelocity)
			{
				Kill(DeadState.Fall);
			}
			Debug.WriteLine("Landed with vY of " +vY);
		}
	}
}