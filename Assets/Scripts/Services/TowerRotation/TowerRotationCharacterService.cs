using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using Character.Player;
using UnityEngine;

public class TowerRotationCharacterService : IService
{
    private readonly List<CharacterBase> _characters = new();
    private readonly Dictionary<Rigidbody, Vector3> _startAngles = new();
    private readonly Dictionary<Rigidbody, Vector3> _endAngles = new();
    private readonly Dictionary<Rigidbody, Vector3> _endPositions = new();
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

    public void Register(CharacterBase e)
    {
        if (_characters.Contains(e)) return;
        _characters.Add(e);
    }

    public void Deregister(CharacterBase e)
    {
        if (_characters.Contains(e))
        {
            _characters.Remove(e);
        }

        var eTransform = e.GetComponent<Rigidbody>();
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
        
        foreach(var e in _characters)
        {
            var eTransform = e.GetComponent<Rigidbody>();
            if (_startAngles.ContainsKey(eTransform))
            {
                _startAngles[eTransform] = eTransform.rotation.eulerAngles;
            }
            else
            {
                _startAngles.Add(eTransform, eTransform.rotation.eulerAngles);
            }
            
            tf.position = eTransform.position;
            tf.rotation = eTransform.rotation;

            
            
            tf.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_AMOUNT);
            var targetRotation = tf.eulerAngles;
            var targetPosition = tf.position;
            
            
            
            var player = e as PlayerCharacter;
            if (player != null)
            {
                targetPosition = new Vector3(_service.ROTATION_ORIGIN.x, eTransform.position.y, _service.ROTATION_ORIGIN.z);
            }
            
            if (_endAngles.ContainsKey(eTransform))
            {
                _endAngles[eTransform] = targetRotation;
            }
            else
            {
                _endAngles.Add(eTransform, targetRotation);
            }

            if (_endPositions.ContainsKey(eTransform))
            {
                _endPositions[eTransform] = targetPosition;
            }
            else
            {
                _endPositions.Add(eTransform, targetPosition);
            }
            e.BeginRotation();
        }
        Object.Destroy(targetAngleGO);
        
    }
    
    public void OnTurn()
    {
        _service ??= ServiceLocator.GetService<TowerRotationService>();
        
        foreach(var e in _characters)
        {
            var eTransform = e.GetComponent<Rigidbody>();
            var rotation = eTransform.rotation.eulerAngles;
            var lerpAngle = Vector3.Lerp(rotation, _endAngles[eTransform],
                _service.ROTATION_PROGRESSION);
            var lerpPosition = Vector3.Lerp(eTransform.position, _endPositions[eTransform],
                _service.ROTATION_PROGRESSION);
            
            var rotationQuaterion = new Quaternion
            {
                eulerAngles = lerpAngle
            };
            //e.transform.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_THIS_FRAME );
            eTransform.Move(lerpPosition, rotationQuaterion);
            Vector3 targetPos = new Vector3(e.transform.position.x, e._spriteObject.position.y, e.transform.position.z);
            e._spriteObject.position = targetPos;
            e.Rotation();
        }
    }
    
    public void OnPostTurn()
    {
        foreach(var e in _characters)
        {
            var eTransform = e.GetComponent<Rigidbody>();
            eTransform.position = _endPositions[eTransform];
            var rotation = new Quaternion
            {
                eulerAngles = _endAngles[eTransform]
            };

            eTransform.rotation = rotation;
            e.EndRotation();
        }
    }
}