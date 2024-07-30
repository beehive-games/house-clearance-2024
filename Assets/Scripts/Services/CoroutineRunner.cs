using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoroutineRunner : MonoBehaviour
{
    private void OnEnable()
    {
        var service = ServiceLocator.GetService<TowerRotationService>();
        service._internalCoroutine = StartCoroutine(service.TurnWait(service.ROTATION_AMOUNT, service.ROTATION_ORIGIN));
    }
}

