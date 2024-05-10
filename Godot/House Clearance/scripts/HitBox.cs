using System.Diagnostics;
using Godot;

namespace HouseClearance.scripts;

public partial class HitBox : Area2D
{
	[Export] private float _damageMultiplier = 1f;
	[Export] private bool _isHead = false;
	[Export] private GpuParticles2D _bloodSpurt;

	private NpcMovement _npcMovement;
	private PlayerMovement _playerMovement;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var parent = GetParent<CharacterBody2D>();

		_npcMovement = parent as NpcMovement;// GetNodeOrNull<NpcMovement>("../../CharacterBody2D");
		_playerMovement = parent as PlayerMovement;//GetNodeOrNull<PlayerMovement>("../../CharacterBody2D");
		if (_npcMovement == null && _playerMovement == null)
		{
			Debug.WriteLine("no movement not found on "+parent.Name);
		}

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
			bool lostHead = false;
			if (_npcMovement != null)
			{
				_npcMovement.Hit(projectile, -direction, projectile.Damage * _damageMultiplier, _isHead ? NpcMovement.DeadState.HeadShot : NpcMovement.DeadState.Shot, ref lostHead);
			}
			else
			{
				_playerMovement?.Hit(projectile, -direction, projectile.Damage * _damageMultiplier, _isHead ? PlayerMovement.DeadState.HeadShot : PlayerMovement.DeadState.Shot, ref lostHead);
			}
			Debug.WriteLine("lostHead! "+ lostHead);

			if (lostHead && _isHead)
			{
				_bloodSpurt.Emitting = true;
				Debug.WriteLine("SPURT!");
			}
		}
	}
}