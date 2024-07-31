using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using UnityEngine;
using UnityEngine;

public class TowerRotationEnvironmentService : Service, IService
{
    private readonly List<Transform> _gameObjects = new();
    private readonly Dictionary<Transform, Vector3> _startAngles = new();
    private readonly Dictionary<Transform, Vector3> _endAngles = new();
    private readonly Dictionary<Transform, Vector3> _endPositions = new();
    private TowerRotationService _service;
    private float _targetRotation;

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
        if (_gameObjects.Contains(e))
        {
            _gameObjects.Remove(e);
        }
        if (_startAngles.ContainsKey(e))
        {
            _startAngles.Remove(e);
        }
        if (_endAngles.ContainsKey(e))
        {
            _endAngles.Remove(e);
        }
        if (_endPositions.ContainsKey(e))
        {
            _endPositions.Remove(e);
        }
    }

    float RoundToNearest(float value, float nearest)
    {
        return Mathf.Round(value /nearest) * nearest;
    }
    
    public void OnBeforeTurn()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        var targetAngleGO = new GameObject();
        var tf = targetAngleGO.transform;

        foreach (var e in _gameObjects)
        {
            var eTransform = e.transform;
            if (_startAngles.ContainsKey(e))
            {
                _startAngles[e] = eTransform.eulerAngles;
            }
            else
            {
                _startAngles.Add(e, eTransform.eulerAngles);
            }



            tf.position = eTransform.position;
            tf.rotation = eTransform.rotation;

            tf.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_AMOUNT);

            if (_endAngles.ContainsKey(e))
            {
                _endAngles[e] = tf.eulerAngles;
            }
            else
            {
                _endAngles.Add(e, tf.eulerAngles);
            }

            if (_endPositions.ContainsKey(e))
            {
                _endPositions[e] = tf.position;
            }
            else
            {
                _endPositions.Add(e, tf.position);
            }
        }
        Object.Destroy(targetAngleGO);
    }
    
    public void OnTurn()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        foreach(var e in _gameObjects)
        {
            e.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_THIS_FRAME );
        }
    }
    
    public void OnPostTurn()
    {
        foreach(var e in _gameObjects)
        {
            e.transform.position = _endPositions[e];
            e.transform.eulerAngles = _endAngles[e];
        }
    }
}