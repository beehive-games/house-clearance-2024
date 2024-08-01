using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerServicesSetup : MonoBehaviour
{
    private void OnEnable()
    {
        var envService = ServiceLocator.GetService<TowerRotationEnvironmentService>();
        envService.HookUpToTransitionService();
        
        var npcService = ServiceLocator.GetService<TowerRotationCharacterService>();
        npcService.HookUpToTransitionService();
        DontDestroyOnLoad(gameObject);
    }
    
    private void OnDisable()
    {
        var envService = ServiceLocator.GetService<TowerRotationEnvironmentService>();
        envService.CleanUpTransitionService();
        
        var npcService = ServiceLocator.GetService<TowerRotationCharacterService>();
        npcService.CleanUpTransitionService();
    }
}
