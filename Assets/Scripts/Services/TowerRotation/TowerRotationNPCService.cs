using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using UnityEngine;

public class TowerRotationNPCService : IService
{
    private readonly List<NPCCharacter> _npcs = new();
    private readonly Dictionary<Transform, Vector3> _startAngles = new();
    private readonly Dictionary<Transform, Vector3> _endAngles = new();
    private readonly Dictionary<Transform, Vector3> _endPositions = new();
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
        if (_npcs.Contains(e))
        {
            _npcs.Remove(e);
        }

        var eTransform = e.transform;
        if (_startAngles.ContainsKey(eTransform))
        {
            _startAngles.Remove(eTransform);
        }
        if (_endAngles.ContainsKey(eTransform))
        {
            _endAngles.Remove(eTransform);
        }
        if (_endPositions.ContainsKey(eTransform))
        {
            _endPositions.Remove(eTransform);
        }
    }

    
    public void OnBeforeTurn()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        var targetAngleGO = new GameObject();
        var tf = targetAngleGO.transform;
        
        foreach(var e in _npcs)
        {
            e.BeginRotation();

            var eTransform = e.transform;
            if (_startAngles.ContainsKey(eTransform))
            {
                _startAngles[eTransform] = eTransform.eulerAngles;
            }
            else
            {
                _startAngles.Add(eTransform, eTransform.eulerAngles);
            }
            
            tf.position = eTransform.position;
            tf.rotation = eTransform.rotation;

            tf.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_AMOUNT);

            if (_endAngles.ContainsKey(eTransform))
            {
                _endAngles[eTransform] = tf.eulerAngles;
            }
            else
            {
                _endAngles.Add(eTransform, tf.eulerAngles);
            }

            if (_endPositions.ContainsKey(eTransform))
            {
                _endPositions[eTransform] = tf.position;
            }
            else
            {
                _endPositions.Add(eTransform, tf.position);
            }
        }
        Object.Destroy(targetAngleGO);
    }
    
    public void OnTurn()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        
        foreach(var e in _npcs)
        {
            e.transform.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_THIS_FRAME );
            //e._spriteObject.RotateAround(e.transform.position, Vector3.up, -_service.ROTATION_THIS_FRAME);
            
            Vector3 targetPos = new Vector3(e.transform.position.x, e._spriteObject.position.y, e.transform.position.z);
            e._spriteObject.position = targetPos;
        }
    }
    
    public void OnPostTurn()
    {
        foreach(var e in _npcs)
        {
            var eTransform = e.transform;
            eTransform.position = _endPositions[eTransform];
            eTransform.eulerAngles = _endAngles[eTransform];
            e.EndRotation();
        }
    }
}