using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class Projectile : RigidBody2D
{

	[Export] private float _maxLifetime = 1f;
	[Export] private float _maxDistance = 50f;
	[Export] private float _minVelocity = 20f;
	[Export] public float HitForce = 20f;
	[Export] public float Damage = 20f;
	[Export] public PackedScene HitVfx;

	private Vector2 _startPos;
	private TrailRenderer _trailRenderer;
	private float _countdown;
	public override void _Ready()
	{
		_countdown = _maxLifetime;
		
	}

	public void SetUpLineRenderer(Vector2 firePos)
	{
		_trailRenderer = GetNodeOrNull<TrailRenderer>("TrailRenderer");
		if (_trailRenderer != null)
		{
			_trailRenderer.StartPosition = firePos;
			_trailRenderer.MaxVelocity = LinearVelocity.X;
		}

		_startPos = GlobalPosition;
	}

	private float Distance(Vector2 a, Vector2 b)
	{
		var x = Mathf.Pow(a.X, 2f) + Mathf.Pow(b.X, 2);
		var y = Mathf.Pow(a.Y, 2f) + Mathf.Pow(b.Y, 2);
		return Mathf.Abs(Mathf.Sqrt(x + y));
	}

	public void Kill()
	{
		QueueFree();
	}


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
		var distance = GlobalPosition.DistanceTo(_startPos);
		if (distance > _maxDistance || _countdown <= 0f || Mathf.Abs(LinearVelocity.X) < _minVelocity)
		{
			Kill();
		}
		else
		{
			_countdown -= (float)delta;
		}

		if (_trailRenderer != null)
		{
			_trailRenderer.Velocity = LinearVelocity.X;
		}
	}
}