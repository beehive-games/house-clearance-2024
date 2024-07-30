using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using UnityEngine;

public class TowerRotationNPCService : IService
{
    private readonly List<NPCCharacter> _npcs = new();
    private TowerRotationService _service;
    
    public void HookUpToTransitionService()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        ServiceLocator.GetService<TowerRotationService>().OnBeforeTurn += OnBeforeTurn;
        ServiceLocator.GetService<TowerRotationService>().OnTurn += OnTurn;
        ServiceLocator.GetService<TowerRotationService>().OnPostTurn += OnPostTurn;
    }
    
    public void CleanUpTransitionService()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        ServiceLocator.GetService<TowerRotationService>().OnBeforeTurn -= OnBeforeTurn;
        ServiceLocator.GetService<TowerRotationService>().OnTurn -= OnTurn;
        ServiceLocator.GetService<TowerRotationService>().OnPostTurn -= OnPostTurn;
    }

    public void Register(NPCCharacter e)
    {
        if (_npcs.Contains(e)) return;
        _npcs.Add(e);
    }

    public void Deregister(NPCCharacter e)
    {
        if (!_npcs.Contains(e)) return;
        _npcs.Remove(e);
    }

    public void OnBeforeTurn()
    {
        foreach(var e in _npcs)
        {
            e.BeginRotation();
        }
    }
    
    public void OnTurn()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        foreach(var e in _npcs)
        {
            float rotationInterpolation = 1f / _service.ROTATION_TIME * _service.ROTATION_PROGRESSION;
            e.transform.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_AMOUNT * rotationInterpolation*Time.deltaTime);
        }
    }
    
    public void OnPostTurn()
    {
        foreach(var e in _npcs)
        {
            Vector3 euler = e.transform.eulerAngles;
            e.transform.rotation = Quaternion.Euler(euler.x, Mathf.Round(euler.y), euler.z);
            e.EndRotation();
        }
    }
}