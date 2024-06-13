using UnityEngine;
using UnityEngine.InputSystem;
using Utils;
using Vector3 = System.Numerics.Vector3;

namespace Character.Player
{
    public class PlayerCharacter : CharacterBase
    {
        [Space]
        [Header("Player controls")]
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private float inAirControlForce = 100f;
        [SerializeField] private float gridSquaresPerUnit = 10f;
        private InputAction _moveAction;
        private bool _waitForMoveActionDepress;
        private float _xInput;
        private bool _queueJump;
        private bool _queueSlide;
    
    
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
            actions.FindActionMap("gameplay").FindAction("slide").performed += OnSlide;


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

            var inputMatchesFacingDirection = _spriteRenderer.flipX ? _xInput < 0f  : _xInput > 0f;
            if (_moveAction.WasReleasedThisFrame() || !inputMatchesFacingDirection )
            {
                _waitForMoveActionDepress = false;
            }
        }

        private void SetRigidbody2DVelocityX(float x)
        {
            _rigidbody2D.velocity = new Vector2(x, _rigidbody2D.VelocityY());
        }
        
        private void OnSlide(InputAction.CallbackContext context)
        {
            Debug.Log("Slide!");
            _queueSlide = true;
        }

        private void StartSlide()
        {
            _movementState = MovementState.Slide;
            float direction = _spriteRenderer.flipX ? -1 : 1;
            SetRigidbody2DVelocityX(_slideBoost * _rigidbody2D.mass * direction);
        }

        private void DuringSlide(bool isGrounded)
        {
            _queueSlide = false;
            if (!isGrounded || Mathf.Abs(_rigidbody2D.velocity.x) < 0.1f)
            {
                StopSlide();
            }
            else
            {
                float x = _rigidbody2D.velocity.x;
                bool sign = Mathf.Sign(x) < 0f;
                float deltaV = _slideFriction * Time.fixedDeltaTime;
                var clamped1 = Mathf.Clamp(x + deltaV, x, 0f);
                var clamped2 = Mathf.Clamp(x - deltaV, 0f, x);
                SetRigidbody2DVelocityX(sign ? clamped1 : clamped2);
            }
        }
        
        private void StopSlide()
        {
            _movementState = MovementState.Walk;
        }

        private void XDirection(bool isGrounded)
        {
            if (_movementState == MovementState.Slide)
            {
                DuringSlide(isGrounded);
            }
            else
            {
                if (isGrounded && _queueSlide)
                {
                    _queueSlide = false;
                    StartSlide();
                }
                else
                {
                    if (_movementState is not MovementState.Slide or MovementState.Dead or MovementState.Immobile)
                    {
                        var velocity = _rigidbody2D.velocity;
                        var acceleration = isGrounded ? _maxAcceleration : _maxAirAcceleration;
                        var maxSpeedDelta = acceleration * Time.fixedDeltaTime;
                        velocity.x = Mathf.MoveTowards(velocity.x, _moveSpeed * _xInput, maxSpeedDelta);
                        _rigidbody2D.velocity = velocity;
                    }
                }
            }
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
