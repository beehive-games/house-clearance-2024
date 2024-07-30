using Environment;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Utils;
using Vector3 = System.Numerics.Vector3;

namespace Character.Player
{
    public class PlayerCharacter : CharacterBase
    {
        [Space]
        [Header("Player controls")]
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private UIDocument playerGUIDocument;
        private Label _healthLabel;
        private Label _ammoLabel;
        private InputAction _moveAction;
        private bool _waitForMoveActionDepress;
        private float _xInput;
        private bool _queueJump;
        private bool _queueSlide;
        private bool _queueRotate;
        private float _slideToJumpMaxVX;
        private bool _shooting = false;
        private TowerCorner _activeCorner;

        public void InTowerCorner(TowerCorner currentCorner)
        {
            _activeCorner = currentCorner;
        }
        
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
        
        private void OnShootHold(InputAction.CallbackContext context)
        {
            _shooting = true;
            OnShootingHold();
        }
        
        private void OnShootingHold()
        {
            _weaponInstance.Fire(true);
        }
        
        private void OnShootCancel(InputAction.CallbackContext context)
        {
            _shooting = false;
        }
        
        private void OnShoot(InputAction.CallbackContext context)
        {
            if (!_shooting) return;
            
            _weaponInstance.Fire();
        }
        
        private void OnSpecial(InputAction.CallbackContext context)
        {
            Debug.Log("Special!");
            _queueJump = true;
        }
        
        private void OnTowerRotation(InputAction.CallbackContext context)
        {
            Debug.Log("Rotate!");
            _queueRotate = true;
        }
        
        private void OnMelee(InputAction.CallbackContext context)
        {
            if (_movementState is MovementState.Walk or MovementState.Slide or MovementState.Cover &&
                _aliveState is AliveState.Alive or AliveState.Wounded)
            {
                TryMelee();
            }
        }
        
        protected override void Jump()
        {
            base.Jump();
            if (_movementState == MovementState.Slide)
            {
                SlideToJump();
            }
        }

        private void OnRestart(InputAction.CallbackContext context)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
            actions.FindActionMap("gameplay").FindAction("shoot1").performed += OnShoot;
            actions.FindActionMap("gameplay").FindAction("shoot1").started += OnShootHold;
            actions.FindActionMap("gameplay").FindAction("shoot1").canceled += OnShootCancel;
            actions.FindActionMap("gameplay").FindAction("slide").performed += OnSlide;
            actions.FindActionMap("gameplay").FindAction("melee").performed += OnMelee;
            actions.FindActionMap("gameplay").FindAction("restart").performed += OnRestart;
            actions.FindActionMap("gameplay").FindAction("towerTurn").performed += OnTowerRotation;
            
            _healthLabel = playerGUIDocument.rootVisualElement.Q<Label>("healthValue");
            _ammoLabel = playerGUIDocument.rootVisualElement.Q<Label>("ammoValue");
        }

        private void UpdateUI()
        {
            _healthLabel.text = _currentHealth + "/"+_startingHealth;
            var ammoState = _weaponInstance.GetAmmo();
            _ammoLabel.text = ammoState.currentAmmo +"/" + ammoState.magazineCapacity;
        }
    
        protected override void Update()
        {
            base.Update();
        
            GetXAxisInput();

            UpdateUI();
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
            // Rotation code, could do this better, but it should work for now
            if (_queueRotate && isGrounded && !_towerRotationService.ROTATING)
            {
                if (_activeCorner != null)
                {
                    Debug.LogError("Need to rotate");
                    _queueRotate = false;
                    _xInput = 0;
                    _queueSlide = false;
                    _queueJump = false;
                    if (_movementState != MovementState.Dead && _movementState != MovementState.Immobile)
                    {
                        _movementState = MovementState.Rotating;
                    }
                    
                    _towerRotationService.Rotate(_activeCorner.towerCorner, _activeCorner.transform.position, _activeCorner.turnTime );
                }                
            }

            if (_movementState == MovementState.Rotating && !_towerRotationService.ROTATING &&
                _aliveState is AliveState.Alive or AliveState.Wounded)
            {
                _movementState = isGrounded ? MovementState.Jump : MovementState.Walk;
            }
            
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
                    if (_movementState != MovementState.Slide && 
                        _movementState != MovementState.Dead && 
                        _movementState != MovementState.Immobile &&
                        _movementState != MovementState.Rotating)
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
                LeaveCover();
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

            if (_shooting)
            {
                OnShootingHold();
            }

        }
    }
}
