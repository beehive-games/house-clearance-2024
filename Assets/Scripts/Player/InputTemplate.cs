using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class InputTemplate : MonoBehaviour
{
    
    [SerializeField] private InputActionAsset actions;
    private InputAction _moveAction;

    void OnEnable()
    {
        actions.FindActionMap("gameplay").Enable();
    }
    void OnDisable()
    {
        actions.FindActionMap("gameplay").Disable();
    }
    
    
    void Awake()
    {
        if (!actions)
        {
            Debug.LogError("InputActionAsset missing from InputTemplate!");
            enabled = false;
            return;
        }
           
        _moveAction = actions.FindActionMap("gameplay").FindAction("move");
        actions.FindActionMap("gameplay").FindAction("special").performed += OnSpecial;
        actions.FindActionMap("gameplay").FindAction("slide").performed += OnSlide;
        actions.FindActionMap("gameplay").FindAction("shoot1").performed += OnShoot1;
        actions.FindActionMap("gameplay").FindAction("shoot2").performed += OnShoot2;
        actions.FindActionMap("gameplay").FindAction("reload").performed += OnReload;
    }
    
    private void OnSpecial(InputAction.CallbackContext context)
    {
        Debug.Log("Special!");
    }
        
    private void OnSlide(InputAction.CallbackContext context)
    {
        Debug.Log("Slide!");
    }
        
    private void OnShoot1(InputAction.CallbackContext context)
    {
        Debug.Log("Shoot1!");
    }
        
    private void OnShoot2(InputAction.CallbackContext context)
    {
        Debug.Log("Shoot2!");
    }
        
    private void OnReload(InputAction.CallbackContext context)
    {
        Debug.Log("Reload!");
    }
}
