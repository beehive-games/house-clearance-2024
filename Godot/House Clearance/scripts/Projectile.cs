using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class Projectile : RigidBody2D
{

	[Export] private float _maxLifetime = 1f;
	[Export] private float _maxDistance = 50f;
	[Export] private float _minVelocity = 20f;

	private Vector2 _startPos;
	private float _countdown;
	public override void _Ready()
	{
		_startPos = GlobalPosition;
		_countdown = _maxLifetime;
	}

	private float Distance(Vector2 a, Vector2 b)
	{
		var x = Mathf.Pow(a.X, 2f) + Mathf.Pow(b.X, 2);
		var y = Mathf.Pow(a.Y, 2f) + Mathf.Pow(b.Y, 2);
		return Mathf.Abs(Mathf.Sqrt(x + y));
	}


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var distance = Distance(GlobalPosition, _startPos);
		if (distance > _maxDistance || _countdown <= 0f || Mathf.Abs(LinearVelocity.X) < _minVelocity)
		{
			QueueFree();
		}
		else
		{
			_countdown -= (float)delta;
		}
	}
}