using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class HitBox : Area2D
{
	[Export] private float _damageMultiplier = 1f;

	private NpcMovement _npcMovement;
	private PlayerMovement _playerMovement;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_npcMovement = GetNodeOrNull<NpcMovement>("../../CharacterBody2D");
		_playerMovement = GetNodeOrNull<PlayerMovement>("../../CharacterBody2D");
	}

	private void _on_body_entered(Node2D body)
	{
		Projectile projectile = (Projectile)body;
		if (projectile != null)
		{
			if (projectile.HitVfx != null)
			{
				GpuParticles2D vfx =
					(GpuParticles2D)ResourceLoader.Load<PackedScene>(projectile.HitVfx.ResourcePath).Instantiate();
				GetTree().Root.AddChild(vfx);
				vfx.GlobalPosition = GlobalPosition;
				vfx.Emitting = true;

			}

			Vector2 direction = (GlobalPosition - body.GlobalPosition).Normalized();
			direction.Y = 0;

			if (_npcMovement != null)
			{
				_npcMovement.Hit(projectile, -direction, projectile.Damage * _damageMultiplier);
			}
			else
			{
				_playerMovement?.Hit(projectile, -direction, projectile.Damage * _damageMultiplier);
			}
		}
	}
}