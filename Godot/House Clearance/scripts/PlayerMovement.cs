using System;
using Godot;

namespace HouseClearance.scripts;

public partial class PlayerMovement : CharacterBody2D
{
	public const float Speed = 100.0f;
	public const float JumpVelocity = -250.0f;
	private const float SlideFriction = 1.0f;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	private float _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
	private bool _queueSliding = false;
	private float _previousDirection = 0f;
	private bool _waitOnInputRelease = false;

	public enum MoveState { Idle, Move, Fall, Slide, Cover, Dead, Stop = -1 };

	private MoveState _moveState = MoveState.Idle;

	private AnimatedSprite2D _spriteNodePath;
	
	private void SlidePlayer(ref Vector2 velocity)
	{
		velocity.X = Mathf.MoveToward(Velocity.X, 0f, SlideFriction);
		
		if (!IsOnFloor()) return;
		
		_moveState = MoveState.Slide;
		_spriteNodePath.Animation = "slide";
	}
	
	private void MovePlayer(float direction, ref Vector2 velocity)
	{
		velocity.X = direction * Speed;
		if(Math.Abs(Mathf.Sign(direction) - _previousDirection) > 0.001f && Mathf.Abs(direction) > 0.0001f)
		{
			_spriteNodePath.FlipH = direction < 0f;
		}
		
		if(IsOnFloor())
		{
			_spriteNodePath.Animation = "run";
		}
		_moveState = MoveState.Move;
	}
	
	private void StopPlayer(ref Vector2 velocity)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0, Speed);
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
	}
	
	private void JumpPlayer(ref Vector2 velocity)
	{
		velocity.Y = JumpVelocity;
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
		}
	}

	public override void _Ready()
	{
		_spriteNodePath = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
	}
	
	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity.Y += _gravity * (float)delta;
			PlayerInAir();
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
		{
			JumpPlayer(ref velocity);
		}
		
		// As good practice, you should replace UI actions with custom gameplay actions.
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_down", "ui_up");
		
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
		
		//Handle sliding.
		if (direction.Y < 0f)
		{
			_queueSliding = true;
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
		
		// Also, if we stop sliding due to velocity.X == 0, we need to return to idle.
		if (Mathf.Abs(velocity.X) < 1.1f && _moveState == MoveState.Slide)
		{
			_moveState = MoveState.Idle;
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
	}
}
