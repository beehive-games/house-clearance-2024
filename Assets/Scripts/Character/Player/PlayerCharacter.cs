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
        [SerializeField] private float inAirControlForce = 100f;
        private InputAction _moveAction;
        private bool _waitForMoveActionDepress;
        private float _xInput;
        private bool _queueJump;
    
    
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
        
        private void OnSpecial(InputAction.CallbackContext context)
        {
            Debug.Log("Special!");
            _queueJump = true;
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
            
            actions.FindActionMap("gameplay").FindAction("special").performed += OnSpecial;

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

        private void XDirection(bool isGrounded)
        {
            var velocity = _rigidbody2D.velocity;
            var acceleration = isGrounded ? _maxAcceleration : _maxAirAcceleration;
            var maxSpeedDelta = acceleration * Time.fixedDeltaTime;
            velocity.x = Mathf.MoveTowards(velocity.x, _moveSpeed * _xInput, maxSpeedDelta);
            _rigidbody2D.velocity = velocity;
        }
        
        private void YDirection(bool isGrounded)
        {
            if (_queueJump)
            {
                _queueJump = false;
                if (isGrounded)
                {
                    Jump();
                }
            }
        }
        
        // Called in FixedUpdate in parent class, only if we can move based on states
        protected override void Move()
        {
            base.Move();
            
            _xInput = _moveAction.ReadValue<Vector2>().x;
            
            bool isGrounded = IsGrounded();

            XDirection(isGrounded);
            YDirection(isGrounded);

        }
    }
}
