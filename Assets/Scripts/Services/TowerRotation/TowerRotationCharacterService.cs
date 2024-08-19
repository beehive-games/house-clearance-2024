using System;
using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using Character.Player;
using UnityEngine;
using Object = UnityEngine.Object;

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

    public bool AxesMatch(TowerDirection tower, TowerDirection character)
    {
        bool northSouthTower = tower is TowerDirection.North or TowerDirection.South;
        bool northSouthCharacter = character is TowerDirection.North or TowerDirection.South;

        return northSouthTower && northSouthCharacter;
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
            
            

            var player = e as PlayerCharacter;
            var npc = e as NPCCharacter;
            if (npc != null)
            {
                tf.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_AMOUNT);

                Debug.Log("NPC = " +  tf.eulerAngles);
            }
            else// if (player != null)
            {
                
                Debug.Log("player1 = " +  tf.eulerAngles);
                tf.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_AMOUNT);
                tf.eulerAngles = Vector3.zero;
                Debug.Log("player2 = " +  tf.eulerAngles);

                tf.position = new Vector3(_service.ROTATION_ORIGIN.x, eTransform.position.y, _service.ROTATION_ORIGIN.z);
                
            }
            
            var targetRotation = tf.eulerAngles;
            var targetPosition = tf.position;
            
            
            
            
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
        GameObject tempTransform = new GameObject();
        var tf = tempTransform.transform;
        foreach(var e in _characters)
        {
            var eTransform = e.GetComponent<Rigidbody>();
            tf.position = eTransform.position;
            tf.rotation = eTransform.rotation;
            
            var rotation = eTransform.rotation.eulerAngles;
            var lerpAngle = Vector3.Lerp(rotation, _endAngles[eTransform],
                _service.ROTATION_PROGRESSION);
            var lerpPosition = Vector3.Lerp(eTransform.position, _endPositions[eTransform],
                _service.ROTATION_PROGRESSION);
            
            var rotationQuaterion = new Quaternion
            {
                eulerAngles = lerpAngle
            };
            tf.RotateAround(_service.ROTATION_ORIGIN, Vector3.up, _service.ROTATION_THIS_FRAME );
            eTransform.Move(tf.position, rotationQuaterion);
            
            Vector3 targetPos = new Vector3(e.transform.position.x, e._spriteObject.position.y, e.transform.position.z);
            e._spriteObject.position = targetPos;
            e.Rotation();
        }

        Object.Destroy(tempTransform);
    }
    
    public void OnPostTurn()
    {
        foreach(var e in _characters)
        {
            var eTransform = e.GetComponent<Rigidbody>();
            //eTransform.position = _endPositions[eTransform];
            var rotationQuaterion = new Quaternion();
            rotationQuaterion.eulerAngles = _endAngles[eTransform];
            
            var npc = e as NPCCharacter;
            if (npc != null)
            {
                Debug.Log("end NPC = " + _endAngles[eTransform] +", "+eTransform.rotation.eulerAngles);
                eTransform.Move(_endPositions[eTransform],rotationQuaterion);

            }
            else
            {
                //eTransform.rotation = rotationQuaterion;
                Debug.Log("end player = " + _endAngles[eTransform] +", "+eTransform.rotation.eulerAngles);
                //eTransform.Move(_endPositions[eTransform], eTransform.rotation);
                Debug.Log("end player2 = " + _endAngles[eTransform] +", "+eTransform.rotation.eulerAngles);
            }
            

            
            

            //e.transform.rotation = rotationQuaterion;
            e.EndRotation();
        }
    }
}