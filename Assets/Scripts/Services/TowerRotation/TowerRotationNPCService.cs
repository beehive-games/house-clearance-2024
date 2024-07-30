using System.Collections;
using System.Collections.Generic;
using Character.NPC;
using UnityEngine;

public class TowerRotationNPCService : IService
{
    private readonly List<NPCCharacter> _npcs = new();

    public void HookUpToTransitionService()
    {
        ServiceLocator.GetService<TowerRotationService>().OnBeforeTurn += OnBeforeTurn;
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
            //Do logic here
        }
    }
}