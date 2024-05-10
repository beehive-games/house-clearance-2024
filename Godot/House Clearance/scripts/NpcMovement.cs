using System.Diagnostics;
using Godot;

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
	public AnimatedSprite2D SpriteNodePath;

	
	public enum MoveState { Idle, Move, Fall, Slide, Cover, Dead, Stop = -1 };
	public MoveState _moveState = MoveState.Idle;
	public enum DeadState { Fall, Shot, HeadShot };
	private DeadState _deadState = DeadState.Fall;
	private Vector2 _startPosition;
	private Vector2 _targetPosition;
	private Timer _patrolWaitTimer;
	

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	private float _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
	
	public void Hit(Projectile projectile, Vector2 direction, float damage, DeadState damagedBy, ref bool lostHead)
	{
		if (_moveState == MoveState.Dead) return;
		Vector2 velocity = Velocity;
		velocity += projectile.HitForce * direction;
		Velocity = velocity;
		MoveAndSlide();
		TakeDamage(damage, damagedBy);
		projectile.Kill();
		lostHead = _deadState == DeadState.HeadShot;
	}

	private void Kill(DeadState killedBy)
	{
		_moveState = MoveState.Dead;
		_deadState = killedBy;
		if (SpriteNodePath != null)
		{
			switch (killedBy)
			{
				case DeadState.Fall :
					SpriteNodePath.Animation = "dead_fall"; break;
				case DeadState.Shot :
					SpriteNodePath.Animation = "dead_shot"; break;
				case DeadState.HeadShot :
					SpriteNodePath.Animation = "dead_headshot";break;
			}
		}
	}
	
	private void TakeDamage(float damage, DeadState damagedBy)
	{
		_health -= damage;
		if (_health <= 0f)
		{
			Debug.WriteLine("Damage taken - person is dead!");
			Kill(damagedBy);
		}
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
		var offsetGlobal = GlobalPosition;
		offsetGlobal.Y += 10;
		newPos.Y = GlobalPosition.Y - 3;
		// Check for walls and door...
		var rayQuery = PhysicsRayQueryParameters2D.Create(offsetGlobal, newPos);
		var rayResult = spaceState.IntersectRay(rayQuery);
		// Check for ground underneath
		var pointQuery = new PhysicsPointQueryParameters2D();
		pointQuery.Position = newPos;
		
		pointQuery.CollideWithAreas = true;
		var pointResult = spaceState.IntersectPoint(pointQuery);
		
		Debug.WriteLine(rayResult);
		Debug.WriteLine(pointResult);
		
		return newPos;
	}
	
	public override void _Ready()
	{
		SpriteNodePath = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_startPosition = GlobalPosition;
		FinishedWaiting();
	}
	
	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		
		
		// Add the gravity.
		if (!IsOnFloor())
			velocity.Y += _gravity * (float)delta;

		if (_moveState == MoveState.Dead)
		{
			velocity.X = 0;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}
		
		var dir = Mathf.Sign((_targetPosition-GlobalPosition).X);
		var positionDelta = Mathf.Abs(GlobalPosition.X - _targetPosition.X);
		bool atDestination = positionDelta < 2;
		if (atDestination && _patrolWaitTimer == null )
		{
			ReachedDestination();
			velocity.X = Mathf.MoveToward(Velocity.X, 0, _speed);
		}
		else
		{
			velocity.X = _speed * dir;
		}
		
		Velocity = velocity;
		MoveAndSlide();
	}
}