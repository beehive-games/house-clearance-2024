using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TriggerVolumeEvents : MonoBehaviour
{
    [SerializeField] private UnityEvent stayEvents;
    [SerializeField] private UnityEvent enterEvents;
    [SerializeField] private UnityEvent exitEvents;
    
    private void OnTriggerStay(Collider other)
    {
        stayEvents.Invoke();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        enterEvents.Invoke();
    }
    
    private void OnTriggerExit(Collider other)
    {
        exitEvents.Invoke();

    }
}
