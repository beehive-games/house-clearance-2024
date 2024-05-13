using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class Cover : Area2D
{
	private void _on_body_entered(Node2D body)
	{
		//Debug.WriteLine("Entered body - cover");
		if (body is not PlayerMovement playerMovement) return;
		
		var shape = GetNode<CollisionShape2D>("CollisionShape2D");
		var widthHalf = 0.5f * shape.Shape.GetRect().Size.X;
		var direction = playerMovement.SpriteNodePath.FlipH;
		var finalOffset = (GlobalPosition.X) + (direction ? widthHalf : -widthHalf);
		
		playerMovement.HitCover(finalOffset);
	}
}