using System;
using Godot;
using System.Diagnostics;

namespace HouseClearance.scripts;

public partial class PlayerMovement : CharacterBody2D
{
	[Export] private float _speed = 100.0f;
	[Export] private float _jumpVelocity = -250.0f;
	[Export] private float _slideFriction = 1.0f;
	[Export] private float _slideBoost = 100.0f;
	[Export] private float _slideCooldown = 1.0f;
	[Export] private float _gravityMultiplier = 1.0f;
	[Export] private float _fallAutoSlideVelocity = 500.0f;
	[Export] private float _fallDeathVelocity = 750.0f;
	[Export] private float _spriteDarkenCover = 0.5f;
	[Export] private float _spriteDarkenTeleport = 0.75f;
	[Export] private float _health = 100f;
	

	public enum MoveState { Idle, Move, Fall, Slide, Cover, Dead, Stop = -1 };
	private MoveState _moveState = MoveState.Idle;
	public enum DeadState { Fall, Shot, HeadShot };
	private DeadState _deadState = DeadState.Fall;
	private float _gravity = -9.81f;
	private bool _queueSliding;
	private float _slideCooldownTimer = 0f;
	private float _previousDirection;
	private bool _waitOnInputRelease;
	private bool _waitOnSlideRelease;
	private bool _waitOnTeleportRelease;
	private bool _slideFromAFall;
	private Skeleton2D _weaponRig;
	private Gun _gun;
	public AnimatedSprite2D SpriteNodePath;
	private float _widthHalf;
	private Teleport _activeTeleport;
	private float _teleportTimer;
	private float _teleportWaitTime;
	private bool _isTeleporting;
	private CollisionShape2D _movementCollider;
	
	private void SlidePlayer(ref Vector2 velocity, bool boost)
	{
		
		if (boost)
		{
			_slideCooldownTimer = 0f;
		}
		
		if (Mathf.IsEqualApprox(velocity.X, 0f) && boost)
		{
			velocity.X = SpriteNodePath.FlipH ? -_speed : _speed;
		}
		
		float dir = velocity.X < 0f ? -1f : 1f;
		
		velocity.X = velocity.X + (boost ? dir * _slideBoost : 0f) - (_slideFriction * dir);
		
		velocity.X = (dir < 0f && velocity.X > 0f) ? 0f : (dir > 0f && velocity.X < 0f) ? 0f : velocity.X;
		
		if (!IsOnFloor()) return;
		
		_moveState = MoveState.Slide;
		SpriteNodePath.Animation = "slide";
	}
	
	private void MovePlayer(float direction, ref Vector2 velocity)
	{
		velocity.X = direction * _speed;
		if(Math.Abs(Mathf.Sign(direction) - _previousDirection) > 0.001f && Mathf.Abs(direction) > 0.0001f)
		{
			SpriteNodePath.FlipH = direction < 0f;
		}

		if (_weaponRig != null)
		{
			_weaponRig.Scale = new Vector2(Scale.X * (SpriteNodePath.FlipH ? -1 : 1), Scale.Y);
		}
		
		if(IsOnFloor())
		{
			SpriteNodePath.Animation = "run";
		}
		_moveState = MoveState.Move;
	}

	public bool InvulnerableProjectile()
	{
		return _isTeleporting || _moveState == MoveState.Cover;
	}
	
	public bool InvulnerableAoe()
	{
		return false;
	}
	
	private void StopPlayer(ref Vector2 velocity)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0, _speed);
		if(IsOnFloor())
		{
			SpriteNodePath.Animation = "idle";
		}
		if (_moveState != MoveState.Cover)
		{
			_moveState = MoveState.Idle;
		}
		else
		{
			_queueSliding = false;
		}
		_slideFromAFall = false;
	}
	
	private void JumpPlayer(ref Vector2 velocity)
	{
		velocity.Y = _jumpVelocity;
		_slideCooldownTimer = 0f;
		_waitOnInputRelease = false;
		_waitOnSlideRelease = false;
		PlayerInAir();
	}
	
	private void PlayerInAir()
	{
		_moveState = MoveState.Fall;
		SpriteNodePath.Animation = "jump";
	}

	public void HitCover(float xPos = 0f)
	{
		if(_moveState == MoveState.Slide)
		{
			_moveState = MoveState.Cover;
			_waitOnInputRelease = true;
			_queueSliding = false;
			var velocity = Velocity;
			velocity.X = 0;
			Velocity = velocity;
			StopPlayer(ref velocity);

			var position = GlobalPosition;
			position.X = xPos + (SpriteNodePath.FlipH ? _widthHalf : -_widthHalf);
			GlobalPosition = position;
			MoveAndSlide();
		}
	}

	// TODO: When we can change weapons, the gun script will need to use this
	// and not be hard-coded
	private void StopFiring()
	{
		_gun?.DisableFiring();
	}

	private void StartFiring()
	{
		_gun?.EnableFiring();
		_gun?.EnableFiring();
	}
	
	public void Hit(Projectile projectile, Vector2 direction, float damage, DeadState damagedBy)
	{
		if (_moveState == MoveState.Dead) return;

		Vector2 velocity = Velocity;
		velocity += projectile.HitForce * direction;
		Velocity = velocity;
		

		TakeDamage(damage, damagedBy);
		projectile.Kill();
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
	public void Kill(DeadState killedBy)
	{
		_moveState = MoveState.Dead;
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

	private bool PlayerIsDead(ref Vector2 velocity)
	{
		if (_moveState == MoveState.Dead)
		{
			velocity.X = 0f;
			Velocity = velocity;
			
			return true;
		}

		return false;
	}
	
	public void HitTeleport(Teleport teleport)
	{
		SpriteNodePath.Modulate = new Color(_spriteDarkenTeleport,_spriteDarkenTeleport,_spriteDarkenTeleport);
		_activeTeleport = teleport;
	}

	public void ExitTeleport()
	{
		if (!_isTeleporting)
		{
			_activeTeleport = null;
			SpriteNodePath.Modulate = new Color(1f, 1f, 1f);

		}
	}

	public override void _Ready()
	{
		SpriteNodePath = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle() * _gravityMultiplier;
		_weaponRig = GetNodeOrNull<Skeleton2D>("WeaponRig");
		_gun = (Gun)GetNodeOrNull<Sprite2D>("WeaponRig/root/left_hand/pistol");
		var shape = GetNodeOrNull<CollisionShape2D>("MovementCollider");
		if(shape != null) _widthHalf = 0.5f * shape.Shape.GetRect().Size.X;
		_movementCollider = GetNodeOrNull<CollisionShape2D>("MovementCollider");
	}
	
	public override void _PhysicsProcess(double delta)
	{
		// Reset the game?
		if (Input.IsActionJustPressed("ui_cancel"))
		{
			GetTree().ReloadCurrentScene();
			return;
		}

		// Teleporting overrides all other input
		if (_isTeleporting)
		{
			_teleportTimer += (float)delta;
			
			if (_teleportTimer >= 0.5 * _teleportWaitTime)
			{
				_movementCollider?.SetDeferred("disabled", true);
				GlobalPosition = _activeTeleport.TeleportTo.GlobalPosition;
			}
			
			if (_teleportTimer >= _teleportWaitTime)
			{
				_isTeleporting = false;
				SpriteNodePath.Modulate = new Color(_spriteDarkenTeleport,_spriteDarkenTeleport,_spriteDarkenTeleport);
				_movementCollider?.SetDeferred("disabled", false);
				StartFiring();
			}

			var transitionAmount = (1f / _teleportWaitTime) * _teleportTimer;
			var lerpVal = Mathf.Sin(Mathf.DegToRad(transitionAmount) * 180 );
			var col = new Color(_spriteDarkenTeleport, _spriteDarkenTeleport, _spriteDarkenTeleport);
			SpriteNodePath.Modulate = new Color(col * ( 1- lerpVal));

			return;
		}
		

		_slideCooldownTimer += (float)delta;

		// OK, this isn't movement, BUT we need to tie firing abilities
		// into movement state
		if (_moveState is MoveState.Cover or MoveState.Dead or MoveState.Slide)
		{
			StopFiring();
		}
		else
		{
			StartFiring();
		}
		
		Vector2 velocity = Velocity;
		bool onFloor = IsOnFloor();
		
		// Add gravity.
		if (!onFloor)
		{
			velocity.Y += _gravity * (float)delta;
			PlayerInAir();
		}
		else // can only teleport on the floor
		{
			bool teleportPressed = Input.IsActionPressed("teleport");

			if (teleportPressed && _activeTeleport != null && !_isTeleporting && !_waitOnTeleportRelease)
			{
				if (_activeTeleport.TeleportTo != null)
				{
					_isTeleporting = true;
					_teleportWaitTime = _activeTeleport.TeleportTime;
					_teleportTimer = 0f;
					_moveState = MoveState.Idle;
					StopFiring();
					StopPlayer(ref velocity);
					_waitOnTeleportRelease = true;
					return;
				}
			}

			if (!teleportPressed && !_isTeleporting )
			{
				_waitOnTeleportRelease = false;
			}
		}
		
		// Handle Dead.
		if (PlayerIsDead(ref velocity))
		{
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("jump") && IsOnFloor() && !_slideFromAFall)
		{
			JumpPlayer(ref velocity);
		}
		
		// As good practice, you should replace UI actions with custom gameplay actions.
		float dX = Input.GetAxis("move_left", "move_right");
		float dY = Input.IsActionPressed("slide") ? 1 : 0; //Input.GetAxis("ui_down", "ui_up");
		bool slidePressed = Input.IsActionPressed("slide");

		Vector2 direction = new Vector2(dX, dY);
		if (_slideFromAFall)
		{
			direction.X = 0f;
		}
		
		//Handle sliding.
		bool slideBoost = false;
		if (slidePressed)
		{
			if (!_waitOnSlideRelease && _waitOnInputRelease)
			{
				_waitOnInputRelease = false;
			}
			_queueSliding = !_waitOnInputRelease && !_waitOnSlideRelease;
			
			slideBoost = (_moveState == MoveState.Move || _moveState == MoveState.Idle) && !_waitOnSlideRelease;
			if (slideBoost)
			{
				_waitOnSlideRelease = true;
			}
		}
		else
		{
			//Handle in-cover.
			//We want to remain in cover until you have released and re-pressed a move button.
			if (_waitOnSlideRelease)
			{
				if (_slideCooldownTimer >= _slideCooldown)
				{
					_waitOnSlideRelease = false;
					_slideCooldownTimer = 0f;
				}
			}
			if (_waitOnInputRelease)
			{
				if (Mathf.Abs(direction.X) < 0.0001f)
				{
					_waitOnInputRelease = false;
				}
				else
				{
					direction.X = 0f;
					StopPlayer(ref velocity);
				}
			}
		}
		
		// Sliding removes player input _unless_ its in the opposite direction of travel.
		if (_queueSliding || _moveState == MoveState.Slide)
		{
			if (Mathf.Abs(direction.X) != 0f)
			{
				bool dir = direction.X >= 0f;
				bool vel = velocity.X >= 0f;

				if (dir != vel)
				{
					_queueSliding = false;
					MovePlayer(direction.X, ref velocity);
				}
				else
				{
					SlidePlayer(ref velocity, slideBoost);
				}
			}
			else
			{
				SlidePlayer(ref velocity, slideBoost);
			}
		}
		else
		{
			if(_moveState != MoveState.Slide)
			{
				if (_moveState == MoveState.Cover && _waitOnInputRelease)
				{
					StopPlayer(ref velocity);
				}
				else
				{
					if (Mathf.Abs(direction.X) > 0.001f)
					{
						MovePlayer(direction.X, ref velocity);
					}
					else
					{
						StopPlayer(ref velocity);
					}
				}
			}
		}
		
		// Also, if we stop sliding due to velocity.X == 0, we need to return to idle.
		if (Mathf.Abs(velocity.X) < 1.1f && _moveState == MoveState.Slide)
		{
			_moveState = MoveState.Idle;
			_slideFromAFall = false;
			StopPlayer(ref velocity);
		}

		_previousDirection = Mathf.Round(direction.X);
		
		// Handle in-cover sprite visual changes.
		Vector2 position = SpriteNodePath.Position;
		if (_moveState == MoveState.Cover)
		{
			SpriteNodePath.Modulate = new Color(_spriteDarkenCover, _spriteDarkenCover, _spriteDarkenCover);
			position.Y = -20f;
		}
		else
		{
			float col = _activeTeleport == null ? 1 : _spriteDarkenTeleport;
			SpriteNodePath.Modulate = new Color(col, col, col);
			position.Y = -16f;
		}

		// Update player positions, etc.
		SpriteNodePath.Position = position;
		Velocity = velocity;
		
		MoveAndSlide();
		
		// Handle impacts.
		if (!onFloor && IsOnFloorOnly())
		{
			if (velocity.Y > _fallDeathVelocity)
			{
				Kill(DeadState.Fall);
			}
			else if (Mathf.Abs(velocity.Y) > Mathf.Abs(_fallAutoSlideVelocity) && _moveState != MoveState.Slide)
			{
				_moveState = MoveState.Slide;
				float facingDirection = SpriteNodePath.FlipH ? -1f : 1f;
				
				float autoSlideVelocity = velocity.Y / 4f * facingDirection;
				float autoSlideMagnitude = Mathf.Abs(autoSlideVelocity);
				if (autoSlideMagnitude > Mathf.Abs(velocity.X))
				{
					velocity.X = autoSlideVelocity;
				}

				velocity.X += facingDirection * _slideBoost;
				Velocity = velocity;
				_waitOnInputRelease = true;
				_slideFromAFall = true;
				MoveAndSlide();
			}
		}
	}
}