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
	[Export] private float _magazineCapacity = 20f;
	[Export] private float _criticalHitChance = 0.1f;
	[Export(PropertyHint.Layers2DPhysics)] private uint _enemyPhysicsMask;
	
	private CpuParticles2D _fireVfxAsset;
	private CpuParticles2D _shellVfxAsset;
	private bool _preventShot;
	private float _count = 0f;
	private float _timeBetweenShots;
	private float _currentAmmo;
	private Timer _reloadTimer;

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


	public void Reloaded()
	{
		_currentAmmo = _magazineCapacity;
		Debug.WriteLine("Reloaded!");
		_reloadTimer.Stop();
	}
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_timeBetweenShots = 1f / (_shotsPerMinute / 60f);
		_currentAmmo = _magazineCapacity;
		_reloadTimer = GetNodeOrNull<Timer>("ReloadTimer");
		_reloadTimer.Timeout += Reloaded;
		
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
			if (Input.IsActionPressed("fire1"))
			{
				if (!_preventShot && _currentAmmo > 0f && _reloadTimer.IsStopped())
				{
					
					if (_projectile != null)
					{
						var angle = randomAngle.RandfRange(0, _spreadAngle) - (0.5 * _spreadAngle);
						var targetDir = Mathf.DegToRad((float)angle);
						float critical = new RandomNumberGenerator().RandfRange(0f,1f);
						if (critical < _criticalHitChance)
						{
							Debug.WriteLine("Critical shot!");
							var spaceState = GetWorld2D().DirectSpaceState;
							// TODO: cast forward in horizontal straight line - find enemy - find head hit box - calculate angle to that - use for angle
							var forwardDir = Vector2.Right.Rotated(GlobalRotation) * 1000f;
							var rayQuery = PhysicsRayQueryParameters2D.Create(_muzzlePosition.GlobalPosition,
								_muzzlePosition.GlobalPosition + forwardDir);
							rayQuery.CollisionMask = _enemyPhysicsMask;
							var rayResult = spaceState.IntersectRay(rayQuery);


							if (rayResult.Count > 0)
							{
								var rayRes = rayResult["collider"];
								Debug.WriteLine("rayResult set! ");

								if (rayRes.Obj is CharacterBody2D character)
								{
									Debug.WriteLine("Hit a character at " + character.Name);
									// now get head hitbox!
									var a = character.GetNodeOrNull<Area2D>("HeadHB");
									var gPos = forwardDir;
									if (a != null)
									{
										gPos = a.GlobalPosition;
									}

									var directionToTarget = (gPos - GlobalPosition).Normalized();
									var radians = Mathf.Atan2(directionToTarget.Y, directionToTarget.X);
									var characterForward = Vector2.Right.Rotated(GlobalRotation);
									var characterFacingRadians = Mathf.Atan2(characterForward.Y, characterForward.X);
									var relativeRadians = radians - characterFacingRadians;

									if (relativeRadians < -Mathf.Pi)
										relativeRadians += 2 * Mathf.Pi;
									else if (relativeRadians >= -Mathf.Pi)
										relativeRadians -= 2 * Mathf.Pi;

									targetDir = relativeRadians;
								}
								
								
							}
						}

						Projectile bullet = (Projectile)ResourceLoader.Load<PackedScene>(_projectile.ResourcePath).Instantiate();
						root.AddChild(bullet);
						
						//bullet.GlobalPosition = _muzzlePosition.GlobalPosition;
						bullet.Position = _muzzlePosition.GlobalPosition;
						bullet.SetUpLineRenderer( _muzzlePosition.GlobalPosition);
						
						
						var spreadForward = Vector2.Right.Rotated(GlobalRotation + targetDir);
						
						float xVelocity = bullet.LinearVelocity.X;
						Vector2 newDirection = spreadForward * xVelocity;
						
						bullet.LinearVelocity = newDirection;
						_currentAmmo--;
						
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

			if (Input.IsActionPressed("reload") && _reloadTimer.IsStopped())
			{
				_reloadTimer.Start();
				Debug.WriteLine("Reloading! ...  ");
			}
		}
		else
		{
			_count += d;
		}
	}
}
