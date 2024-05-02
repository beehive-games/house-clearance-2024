using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace HouseClearance.scripts;

public partial class Gun : Sprite2D
{
	[Export] private PackedScene _projectile;
	[Export] private PackedScene _fireVfx;
	[Export] private PackedScene _shellVfx;
	[Export] private Node2D _muzzlePosition;
	[Export] private Node2D _ejectionPosition;
	[Export] private float _shotsPerMinute = 500f;
	[Export] private float _spreadAngle = 20f;

	private CpuParticles2D _fireVfxAsset;
	private CpuParticles2D _shellVfxAsset;
	private bool _preventShot;
	private float _count = 0f;
	private float _timer = 0f;
	private float _timeBetweenShots;

	public void DisableFiring()
	{
		_preventShot = true;
	}
	
	public void EnableFiring()
	{
		_preventShot = false;
	}

	public void ToggleFiring()
	{
		_preventShot = !_preventShot;
	}
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_timeBetweenShots = 1f / (_shotsPerMinute / 60f);
		if (_fireVfx != null)
		{
			_fireVfxAsset = (CpuParticles2D)_fireVfx.Instantiate();
			_fireVfxAsset.Position = _muzzlePosition.Position;
			_fireVfxAsset.Rotation = _muzzlePosition.Rotation;
			_fireVfxAsset.Emitting = false;
		}
		if (_shellVfx != null)
		{
			_shellVfxAsset = (CpuParticles2D)_shellVfx.Instantiate();
			_shellVfxAsset.Position = _ejectionPosition.Position;
			_shellVfxAsset.Rotation = _ejectionPosition.Rotation;
			_shellVfxAsset.Emitting = false;
		}
	}

	
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// on shoot
		var root = GetTree().Root;
		var randomAngle = new RandomNumberGenerator();
		float d = (float)delta;
		if (_count > _timeBetweenShots)
		{
			if (Input.IsActionPressed("ui_up"))
			{
				if (!_preventShot)
				{
					if (_projectile != null)
					{
						RigidBody2D bullet = (RigidBody2D)ResourceLoader.Load<PackedScene>(_projectile.ResourcePath).Instantiate();
						root.AddChild(bullet);
						bullet.Position = _muzzlePosition.GlobalPosition;

						var angle = randomAngle.RandfRange(0, _spreadAngle) - (0.5 * _spreadAngle);
						var spreadForward = Vector2.Right.Rotated(GlobalRotation + Mathf.DegToRad((float)angle));
						float xVelocity = bullet.LinearVelocity.X;
						Vector2 newDirection = spreadForward * xVelocity;
						
						bullet.LinearVelocity = newDirection;
					}

					if (_fireVfxAsset != null)
					{
						_fireVfxAsset.Emitting = true;
						_fireVfxAsset.Restart();
					}

					if (_shellVfxAsset != null)
					{
						_shellVfxAsset.Emitting = true;
						_shellVfxAsset.Restart();
					}

					_count = 0f;
				}
			}
		}
		else
		{
			_count += d;
		}
	}
}
