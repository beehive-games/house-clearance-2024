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
	[Export] private bool _canTeleport;					// Will the NPC go through teleport locations when pursuing
	[Export] private float _health = 100f;
	
	public enum MoveState { Idle, Move, Fall, Slide, Cover, Dead, Stop = -1 };

	private MoveState _moveState = MoveState.Idle;
	

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	private float _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
	
	public void Hit(Projectile projectile, Vector2 direction, float damage)
	{
		Vector2 velocity = Velocity;
		velocity += projectile.HitForce * direction;
		Velocity = velocity;
		MoveAndSlide();
		Debug.WriteLine("Hit! "+Velocity +", "+damage);
		TakeDamage(damage);
		projectile.Kill();
		Kill();
	}

	private void Kill()
	{
		_moveState = MoveState.Dead;
	}
	
	private void TakeDamage(float damage)
	{
		_health -= damage;
		if (_health <= 0f)
		{
			Debug.WriteLine("Damage taken - person is dead!");
		}
	}

	public override void _Ready()
	{
		
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
		/*
		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.
		Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		if (direction != Vector2.Zero)
		{
			velocity.X = direction.X * _speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, _speed);
		}
		*/
		velocity.X = Mathf.MoveToward(Velocity.X, 0, _speed);
		
		Velocity = velocity;
		MoveAndSlide();
	}
}