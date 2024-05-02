using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class HitBox : Area2D
{
	[Export] private float _health = 100f;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	private void _on_body_entered(Node2D body)
	{
		Debug.WriteLine("Body entered a hitbox!");
		// 1: Get body type - i.e. AOE or bullet
		// 2: If Bullet:
		//	2a: Get bullet damage amount
		// 3: Else:
		//	3a: Get distance to transform center
		//	3b: Get damage from AOE, use 1/r^2 as damage amount
		// 4: Subtract from health
		//	4a: If health <= 0f, signal to movement controller
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_health <= 0f)
		{
			var playerController = GetNodeOrNull<PlayerMovement>("PlayerMovement");
			if (playerController != null)
			{
				playerController.Kill(false);
			}
		}
	}
}