using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class Teleport : Area2D
{
	[Export] public Teleport TeleportTo;
	[Export] public float TeleportTime = 1f;
	private void _on_body_entered(Node2D body)
	{

		if (body is not PlayerMovement playerMovement) return;
	
		playerMovement.HitTeleport(this);
	}

	private void _on_body_exited(Node2D body)
	{
		if (body is not PlayerMovement playerMovement) return;
		playerMovement.ExitTeleport();
	}
}