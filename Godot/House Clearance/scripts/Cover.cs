using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class Cover : Area2D
{
	private void _on_body_entered(Node2D body)
	{
		Debug.WriteLine("Entered body - cover");
		if(body is PlayerMovement playerMovement)
		{
			playerMovement.HitCover();
		}
	}
}