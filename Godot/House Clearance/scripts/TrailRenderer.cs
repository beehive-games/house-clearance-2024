using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class TrailRenderer : Line2D
{
	[Export] private float _maxLength = 20;
	public float Velocity;
	public float MaxVelocity;
	public Vector2 StartPosition;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		var start = ToLocal(StartPosition);
		var end = ToLocal(GlobalPosition);
		Vector2 direction = (end - start).Normalized();
		float currentLength = (GlobalPosition - StartPosition).Length();

		var proportionalLength = (1f / MaxVelocity * Velocity) * _maxLength;
		if (currentLength > proportionalLength)
		{
			start = end - direction * proportionalLength;
		}
		var points = new[] {start, end };
		Points = points;
	}
}