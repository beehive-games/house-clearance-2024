using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnvironmentRotationController : MonoBehaviour
{
    private TowerRotationEnvironmentService _service;
    private GameObject _towerEnvironmentSetup;
    private void OnEnable()
    {
        
        if(_towerEnvironmentSetup != null) Destroy(_towerEnvironmentSetup);
        _towerEnvironmentSetup = new GameObject();
        _towerEnvironmentSetup.AddComponent<TowerServicesSetup>();
        
        _service = ServiceLocator.GetService<TowerRotationEnvironmentService>();
        _service.Register(transform);
    }

    private void OnDisable()
    {
        if(_towerEnvironmentSetup != null) Destroy(_towerEnvironmentSetup);
        _service = ServiceLocator.GetService<TowerRotationEnvironmentService>();
        _service.Deregister(transform);
    }
}
