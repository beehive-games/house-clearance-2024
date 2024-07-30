using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerEnvironmentServiceSetup : MonoBehaviour
{
    private void OnEnable()
    {
        var service = ServiceLocator.GetService<TowerRotationEnvironmentService>();
        service.HookUpToTransitionService();
        DontDestroyOnLoad(gameObject);
    }
    
    private void OnDisable()
    {
        var service = ServiceLocator.GetService<TowerRotationEnvironmentService>();
        service.CleanUpTransitionService();
    }
}
