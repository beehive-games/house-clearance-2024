using System;
using Godot;
using System.Diagnostics;

namespace HouseClearance.scripts;

public partial class PlayerMovement : CharacterBody2D
{
	[Export] private float _speed = 100.0f;
	[Export] private float _jumpVelocity = -250.0f;
	[Export] private float _slideFriction = 1.0f;
	[Export] private float _gravityMultiplier = 1.0f;
	[Export] private float _fallAutoSlideVelocity = 500.0f;
	[Export] private float _fallDeathVelocity = 750.0f;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	private float _gravity = -9.81f;
	private bool _queueSliding = false;
	private float _previousDirection = 0f;
	private bool _waitOnInputRelease = false;
	private bool _slideFromAFall = false;
	private Skeleton2D _weaponRig;
	private Gun _gun;
	public enum MoveState { Idle, Move, Fall, Slide, Cover, Dead, Stop = -1 };

	private MoveState _moveState = MoveState.Idle;

	private AnimatedSprite2D _spriteNodePath;
	
	private void SlidePlayer(ref Vector2 velocity)
	{
		velocity.X = Mathf.MoveToward(Velocity.X, 0f, _slideFriction);
		
		if (!IsOnFloor()) return;
		
		_moveState = MoveState.Slide;
		_spriteNodePath.Animation = "slide";
	}
	
	private void MovePlayer(float direction, ref Vector2 velocity)
	{
		velocity.X = direction * _speed;
		if(Math.Abs(Mathf.Sign(direction) - _previousDirection) > 0.001f && Mathf.Abs(direction) > 0.0001f)
		{
			_spriteNodePath.FlipH = direction < 0f;
		}

		if (_weaponRig != null)
		{
			_weaponRig.Scale = new Vector2(Scale.X * (_spriteNodePath.FlipH ? -1 : 1), Scale.Y);
		}
		
		if(IsOnFloor())
		{
			_spriteNodePath.Animation = "run";
		}
		_moveState = MoveState.Move;
	}
	
	private void StopPlayer(ref Vector2 velocity)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0, _speed);
		if(IsOnFloor())
		{
			_spriteNodePath.Animation = "idle";
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
		PlayerInAir();
	}
	
	private void PlayerInAir()
	{
		_moveState = MoveState.Fall;
		_spriteNodePath.Animation = "jump";
	}

	public void HitCover()
	{
		if(_moveState == MoveState.Slide)
		{
			_moveState = MoveState.Cover;
			_waitOnInputRelease = true;
			_queueSliding = false;
			Debug.WriteLine("COVER");
		}
	}

	// TODO: When we can change weapons, the gun script will need to use this
	// and not be hard-coded
	private void StopFiring()
	{
		_gun ??= GetNodeOrNull<Gun>("pistol");
		_gun?.DisableFiring();
	}

	private void StartFiring()
	{
		_gun ??= GetNodeOrNull<Gun>("pistol");
		_gun?.EnableFiring();
	}
	
	public void Kill(bool fall = true)
	{
		_moveState = MoveState.Dead;
		if(fall)
			_spriteNodePath.Animation = "dead_fall";
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

	public override void _Ready()
	{
		_spriteNodePath = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle() * _gravityMultiplier;
		_weaponRig = GetNodeOrNull<Skeleton2D>("WeaponRig");
	}
	
	public override void _PhysicsProcess(double delta)
	{
		// Reset the game?
		if (Input.IsActionJustPressed("ui_cancel"))
		{
			GetTree().ReloadCurrentScene();
			return;
		}

		if (_moveState == MoveState.Cover || _moveState == MoveState.Dead)
		{
			StopFiring();
		}
		else
		{
			StartFiring();
		}
		
		Vector2 velocity = Velocity;
		bool onFloor = IsOnFloor();
		// Add the gravity.
		if (!onFloor)
		{
			velocity.Y += _gravity * (float)delta;
			PlayerInAir();
		}
		
		// Handle Dead.
		if (PlayerIsDead(ref velocity))
		{
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("ui_accept") && IsOnFloor() && !_slideFromAFall)
		{
			JumpPlayer(ref velocity);
		}
		
		// As good practice, you should replace UI actions with custom gameplay actions.
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_down", "ui_up");
		
		if (_slideFromAFall)
		{
			direction.X = 0f;
		}
		
		//Handle sliding.
		if (direction.Y < 0f)
		{
			_queueSliding = _moveState != MoveState.Cover;
		}
		else
		{
			//Handle in-cover.
			//We want to remain in cover until you have released and re-pressed a move button.
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
				if (Mathf.Sign(direction.X) != Mathf.Sign(velocity.X))
				{
					_queueSliding = false;
					MovePlayer(direction.X, ref velocity);
				}
				else
				{
					SlidePlayer(ref velocity);
				}
			}
			else
			{
				SlidePlayer(ref velocity);
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
		Vector2 position = _spriteNodePath.Position;
		if (_moveState == MoveState.Cover)
		{
			_spriteNodePath.Modulate = new Color(0.5f, 0.5f, 0.5f);
			position.Y = -20f;
		}
		else
		{
			_spriteNodePath.Modulate = new Color(1f, 1f, 1f);
			position.Y = -16f;
		}

		// Update player positions, etc.
		_spriteNodePath.Position = position;
		Velocity = velocity;
		
		MoveAndSlide();
		
		// Handle impacts.
		if (!onFloor && IsOnFloorOnly())
		{
			if (velocity.Y > _fallDeathVelocity)
			{
				Kill();
			}
			else if (Mathf.Abs(velocity.Y) > Mathf.Abs(_fallAutoSlideVelocity))
			{
				_moveState = MoveState.Slide;
				float facingDirection = _spriteNodePath.FlipH ? -1f : 1f;
				
				float autoSlideVelocity = velocity.Y / 4f * facingDirection;
				float autoSlideMagnitude = Mathf.Abs(autoSlideVelocity);
				if (autoSlideMagnitude > Mathf.Abs(velocity.X))
				{
					velocity.X = autoSlideVelocity;
				}
				
				Debug.WriteLine("slide! autoSlideVelocity:" + autoSlideVelocity +" v: " + velocity.X+"," + velocity.Y);
				Debug.WriteLine("    : Mathf.Abs(velocity.Y)" + Mathf.Abs(velocity.Y) +" Mathf.Abs(_fallAutoSlideVelocity) " + Mathf.Abs(_fallAutoSlideVelocity));
				Velocity = velocity;
				_waitOnInputRelease = true;
				_slideFromAFall = true;
			}
			Debug.WriteLine("FLOOR " + velocity.Y);
		}
	
	}
}