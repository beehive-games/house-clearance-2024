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
        private float _slideToJumpMaxVX;
    
    
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
        
        protected override void Jump()
        {
            base.Jump();
            if (_movementState == MovementState.Slide)
            {
                SlideToJump();
            }
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
            var moveXInput = _moveAction.ReadValue<Vector2>().x;
            ReleasedMoveKey(moveXInput);
            _xInput = _waitForMoveActionDepress ? 0f : moveXInput;
        }
    
        private void ReleasedMoveKey(float xInput)
        {
            if (!_waitForMoveActionDepress) return;
            var inputMatchesFacingDirection = _spriteRenderer.flipX ? xInput < 0f  : xInput > 0f;
            if (_moveAction.WasReleasedThisFrame() || !inputMatchesFacingDirection )
            {
                _waitForMoveActionDepress = false;
            }
        }

        
        
        private void OnSlide(InputAction.CallbackContext context)
        {
            if(_movementState != MovementState.Slide) _queueSlide = true;
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

        protected override void HitCover()
        {
            base.HitCover();
            _waitForMoveActionDepress = true;
        }
        
        private void SlideToJump()
        {
            _slideToJumpMaxVX = Mathf.Abs(_rigidbody2D.velocity.x);
        }
        
        private void StopSlide()
        {
            _movementState = MovementState.Walk;
            _waitForMoveActionDepress = true;
        }

        private void XDirection(bool isGrounded)
        {
            if (isGrounded)
            {
                _slideToJumpMaxVX = -1f;
            }
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
                        MoveMechanics(isGrounded, _xInput, _slideToJumpMaxVX);
                    }
                }
            }

            if (!isGrounded && _aliveState is AliveState.Alive or AliveState.Wounded)
            {
                _movementState = MovementState.Jump;
            }

            if (isGrounded && _movementState is MovementState.Jump)
            {
                _movementState = MovementState.Walk;
            }

            if (_movementState == MovementState.Cover && Mathf.Abs(_xInput) > 0.001f)
            {
                _movementState = MovementState.Walk;
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

        private bool TeleportCheck()
        {
            if (_canTeleport)
            {
                var moveYInput = _moveAction.ReadValue<Vector2>().y;
                if (moveYInput > 0.3f)
                {
                    Teleport(_teleportLocation);
                    return true;
                }
            }
            return false;
        }
      
        // Called in FixedUpdate in parent class, only if we can move based on states
        protected override void Move()
        {
            base.Move();

            if (_movementState == MovementState.Teleporting)
            {
                return;
            }
            
            if(TeleportCheck()) return;
            
            bool isGrounded = IsGrounded();

            XDirection(isGrounded);
            YDirection(isGrounded);

        }
    }
}
