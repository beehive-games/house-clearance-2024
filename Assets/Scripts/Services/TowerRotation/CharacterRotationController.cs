using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using UnityEngine;

public class CharacterRotationController : MonoBehaviour
{
    private TowerRotationCharacterService _service;
    private void OnEnable()
    {
      _service = ServiceLocator.GetService<TowerRotationCharacterService>();

        var character = GetComponent<CharacterBase>();
        if(character != null)
            _service.Register(character);
    }

    private void OnDisable()
    {
        _service = ServiceLocator.GetService<TowerRotationCharacterService>();
        
        var character = GetComponent<CharacterBase>();
        if(character != null)
            _service.Deregister(character);
    }
}