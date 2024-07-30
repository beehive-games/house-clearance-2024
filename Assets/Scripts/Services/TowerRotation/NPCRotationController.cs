using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using UnityEngine;

public class NPCRotationController : MonoBehaviour
{
    private TowerRotationNPCService _service;
    private void OnEnable()
    {
      _service = ServiceLocator.GetService<TowerRotationNPCService>();

        var _npc = GetComponent<NPCCharacter>();
        if(_npc != null)
            _service.Register(_npc);
    }

    private void OnDisable()
    {
        _service = ServiceLocator.GetService<TowerRotationNPCService>();
        
        var _npc = GetComponent<NPCCharacter>();
        if(_npc != null)
            _service.Deregister(_npc);
    }
}