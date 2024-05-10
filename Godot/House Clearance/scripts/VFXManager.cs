using Godot;
using System;

public partial class VFXManager : GpuParticles2D
{
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	[Export] private float _safeTime = 1f;
	[Export] private bool _waitUntilFirstEmission = false;
	private float _count = 0f;
	public override void _Process(double delta)
	{
		if (_waitUntilFirstEmission)
		{
			if (Emitting == false)
			{
				return;
			}
			_waitUntilFirstEmission = false;
		}
		if (Emitting == false)
		{
			if (_count > _safeTime)
			{
				QueueFree();
			}
			else
			{
				_count += (float)delta;
			}
		}
	}
}
