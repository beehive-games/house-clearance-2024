using System;
using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using UnityEngine;

public class TowerRotationEnvironmentService : IService
{
    private readonly List<Transform> _gameObjects = new();
    private TowerRotationService _service;

    public void HookUpToTransitionService()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        _service.OnBeforeTurn += OnBeforeTurn;
        _service.OnTurn += OnTurn;
        _service.OnPostTurn += OnPostTurn;
    }

    public void CleanUpTransitionService()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        _service.OnBeforeTurn -= OnBeforeTurn;
        _service.OnTurn -= OnTurn;
        _service.OnPostTurn -= OnPostTurn;
    }

    public void Register(Transform e)
    {
        if (_gameObjects.Contains(e)) return;
        _gameObjects.Add(e);
    }

    public void Deregister(Transform e)
    {
        if (!_gameObjects.Contains(e)) return;
        _gameObjects.Remove(e);
    }

    public void OnBeforeTurn()
    {
        foreach(var e in _gameObjects)
        {
            //Do logic here
        }
    }
    
    public void OnTurn()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        foreach(var e in _gameObjects)
        {
            float rotationInterpolation = 1f / _service.ROTATION_TIME * _service.ROTATION_PROGRESSION;
            e.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_AMOUNT * rotationInterpolation*Time.deltaTime);
        }
    }
    
    public void OnPostTurn()
    {
        foreach(var e in _gameObjects)
        {
            Vector3 euler = e.rotation.eulerAngles;
            e.rotation = Quaternion.Euler(euler.x, Mathf.Round(euler.y), euler.z);
        }
    }
}