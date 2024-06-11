using UnityEngine;
using UnityEngine.InputSystem;
using Utils;

namespace Character.Player
{
    public class PlayerCharacter : CharacterBase
    {
        [Space]
        [Header("Player controls")]
        [SerializeField] private InputActionAsset actions;
        private InputAction _moveAction;
        private bool _waitForMoveActionDepress;
        private float _xInput;
    
    
        // -------------------------
        // Unity-based events
        //--------------------------
        private void OnEnable()
        {
            actions.FindActionMap("gameplay").Enable();
        }
        private void OnDisable()
        {
            actions.FindActionMap("gameplay").Disable();
        }
    
        protected override void Awake()
        {
            base.Awake();
        
            if (!actions)
            {
                Debug.LogError("InputActionAsset missing from PlayerMovement!");
                enabled = false;
                return;
            }
            _moveAction = actions.FindActionMap("gameplay").FindAction("move");
            if (_moveAction == null)
            {
                Debug.LogError("Move Action is NULL!!");
            }
        }
    
        protected override void Update()
        {
            base.Update();
        
            GetXAxisInput();
        }
    
    
        // -------------------------
        // PlayerCharacter-based methods
        //--------------------------
        private void GetXAxisInput()
        {
            ReleasedMoveKey();
            _xInput = _waitForMoveActionDepress ? 0f : _moveAction.ReadValue<Vector2>().x;
        }
    
        private void ReleasedMoveKey()
        {
            if (!_waitForMoveActionDepress) return;

            var inputMatchesFacingDirection = Flipped() ? _xInput < 0f  : _xInput > 0f;
            if (_moveAction.WasReleasedThisFrame() || !inputMatchesFacingDirection )
            {
                _waitForMoveActionDepress = false;
            }
        }
    
        // Called in FixedUpdate in parent class
        protected override void Move()
        {
            base.Move();
        
            _xInput = _moveAction.ReadValue<Vector2>().x;
            _rigidbody2D.SetVelocityX(_moveSpeed * _xInput);
            _rigidbody2D.SetVelocityX(_moveSpeed * _xInput);
        }
    }
}
