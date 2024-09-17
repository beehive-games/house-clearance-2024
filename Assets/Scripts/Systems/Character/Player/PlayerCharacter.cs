using System;
using System.Collections;
using System.Numerics;
using BeehiveGames.HouseClearance;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Character.Player
{
    public class PlayerCharacter : CharacterBase
    {
        [Space]
        [Header("Player controls")]
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private UIDocument playerGUIDocument;

        public Vector3 rigidbodyVelocity;

        private Label _healthLabel;
        private Label _ammoLabel;
        private InputAction _moveAction;
        private bool _waitForMoveActionDepress;
        private float _xInput;
        private bool _queueJump;
        private bool _queueSlide;
        private bool _queueRotate;
        private bool _rotating;
        private float _slideToJumpMaxVX;
        private bool _shooting = false;
        private Coroutine _rotationRunnerCo;
        
        public Collider Collider { private set; get; }
        

        // -------------------------
        // Unity-based events
        //--------------------------
        private void OnEnable()
        {
            Collider = GetComponent<Collider>();
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
            GameRoot.RegisterPlayer(this);
        
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
    
        private void OnDestroy()
        {
            GameRoot.DeregisterPlayer();
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

        protected override void StartSlide()
        {
            base.StartSlide();
            float direction = _spriteRenderer.flipX ? 1 : -1;
            var velocity = _rigidbody.velocity;
            velocity = transform.right * (_slideBoost * _rigidbody.mass * direction);
            velocity.y = _rigidbody.velocity.y;
            _rigidbody.velocity = velocity;
            
        }

        private void DuringSlide(bool isGrounded)
        {
            _queueSlide = false;
            var velocity = _rigidbody.velocity;
            var maxV = Mathf.Max(Mathf.Abs(velocity.x), Mathf.Abs(velocity.z));
            if (!isGrounded || maxV < 0.1f)
            {
                StopSlide();
            }
            else
            {

                float deltaV = _slideFriction * Time.fixedDeltaTime;


                var rbDirection = _rigidbody.velocity;
                rbDirection.y = 0f;
                var speed = rbDirection.magnitude;
                rbDirection.Normalize();
                speed -= deltaV;
                rbDirection *= speed;
                rbDirection.y = _rigidbody.velocity.y;
                _rigidbody.velocity = rbDirection;
                /*
                 * _movementState = MovementState.Slide;
                    float direction = _spriteRenderer.flipX ? -1 : 1;
                    var velocity = _rigidbody.velocity;
                    velocity = transform.right * (_slideBoost * _rigidbody.mass * direction);
                    velocity.y = _rigidbody.velocity.y;
                    _rigidbody.velocity = velocity;
                 */
            }
        }

        protected override void HitCover(Vector3 coverPosition, Transform coverTransform)
        {
            base.HitCover(coverPosition, coverTransform);
            _waitForMoveActionDepress = true;
        }
        
        private void SlideToJump()
        {
            _slideToJumpMaxVX = Mathf.Abs(_rigidbody.velocity.x);
        }
        
        private void StopSlide()
        {
            _movementState = MovementState.Walk;
            _waitForMoveActionDepress = true;
        }

        private IEnumerator RotationRunner()
        {
            
            _movementState = MovementState.Rotating;

            /*
             * This needs a bit of explanation
             *
             * We need to rotate the player so that they are aligned with the same axis as NPCs/Enemies
             * Those NPCs are aligned based on NPCMovementLine.points.
             *
             * We therefore need to find out which point on that movement line are we currently on and which
             * we are "rotating on to".
             *
             * To do this, we first go through the line points and find the point tha matches our _activeCorner
             * This lets us find out which points are the previous and next points in those line.
             *
             * The problem is, we don't always know which direction is the right one. If we get this wrong, we end
             * up with inverse left/right controls.
             *
             * So, we take a dot product of our transform.right against the "current corner and the next corner",
             * and again, our transform.right against the "current corner and previous corner".
             * 
             * Whichever returns the highest absolute value dot product _must_ be the line we are currently "on". So we
             * rotate to the _other_ line.
             *
             * We also have to re-align our position to be centered on the line, so we don't get x/z offset as this would
             * cause issues as NPCs are bound to these lines.
             * 
             */
            
            // Get corners
            float counter = 0f;
            var cornerTransform = _activeCorner.transform;
            var locations = movementLine.locations;
            var points = movementLine.points;

            int currentIndex = 0, nextIndex = 0, previousIndex = 0;
            
            for (int i = 0; i < locations.Length; i++)
            {
                var location = locations[i];
                
                if (location == cornerTransform)
                {
                    currentIndex = i;
                }
            }
            
            nextIndex = currentIndex >= points.Length - 1 ? 0 : currentIndex + 1;
            previousIndex = currentIndex <= 0 ? points.Length - 2 : currentIndex - 1;

            // get normals
            var normalNext = (points[currentIndex] - points[nextIndex]).normalized;
            var normalPrev = (points[currentIndex] - points[previousIndex]).normalized;
            
            // get correct orientation of normals
            var dotProductNormalNext = Vector3.Dot(transform.right, normalNext);
            var dotProductNormalPrevious = Vector3.Dot(transform.right, normalPrev);
            if (Mathf.Abs(dotProductNormalNext) < Mathf.Abs(dotProductNormalPrevious))
            {
                (normalNext, normalPrev) = (normalPrev, normalNext);
            }
            
            // set up movement & rotation
            var initialRotation = _rigidbody.rotation;
            var targetRotation = Quaternion.FromToRotation(normalPrev, normalNext) * initialRotation;

            var turnTime = _activeCorner.turnTime;

            var targetPosition = points[currentIndex];
            var currentPos = _rigidbody.position;
            targetPosition.y = currentPos.y;
            var direction = (targetPosition - currentPos).normalized;
            var distance = Vector3.Distance(targetPosition, currentPos);

            // Perform the movement & rotation
            while (counter < turnTime)
            {
                
                Debug.DrawRay(_rigidbody.position, normalPrev, Color.red);
                Debug.DrawRay(_rigidbody.position, normalNext, Color.green);
                var increment = counter / turnTime;
                _rigidbody.MoveRotation(Quaternion.Slerp(initialRotation, targetRotation, increment));
                _rigidbody.MovePosition(currentPos + direction * (distance * increment));
                counter += Time.deltaTime;
                
                yield return 0;
            }
            
            // Timer finished, clean up everything
            _rigidbody.MoveRotation(targetRotation);
            _rigidbody.MovePosition(targetPosition);
            
            _rotating = false;
            _rotationRunnerCo = null;
            _movementState = MovementState.Walk;

        }
        
        private void XDirection(bool isGrounded)
        {
            // Rotation code, could do this better, but it should work for now
            bool activeCornerCheck = _activeCorner != null;
            if ((_rotating || !activeCornerCheck) && _queueRotate)
            {
                _queueRotate = false;
            }
            if (_queueRotate && isGrounded && !_rotating)
            {
                if (activeCornerCheck && _rotationRunnerCo == null)
                {
                    //Debug.LogError("Need to rotate");
                    
                    // Truthy rotation states
                    _rotating = true;
                    _rotationRunnerCo = StartCoroutine(RotationRunner());
                    //_towerRotationService.Rotate(_activeCorner.towerCorner, _activeCorner.transform.position, _rigidbody.position, _activeCorner.turnTime );
                    
                    // Falsify other states
                    _queueRotate = false;
                    _queueSlide = false;
                    _queueJump = false;
                    _xInput = 0f;
                }                
            }

            if (_movementState == MovementState.Rotating && !_rotating &&
                _aliveState is AliveState.Alive or AliveState.Wounded)
            {
                _movementState = isGrounded ? MovementState.Walk : MovementState.Jump;
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
      
        public override void BeginRotation()
        {
            base.BeginRotation();
        }

        public override void Rotation()
        {
            base.Rotation();

        }
        
        public override void EndRotation()
        {
            base.EndRotation();
            /*currentTowerSide = _towerRotationService.TOWER_DIRECTION;
            var targetPos = _activeCorner.transform.position;
            var newPos = new Vector3(targetPos.x, _rigidbody.position.y, targetPos.z);
            
            _rigidbody.MovePosition(newPos);*/
        }

        protected override void UpdateSprite()
        {
            base.UpdateSprite();
            _spriteObject.LookAt(_spriteObject.position - transform.forward);
            var inputDirectionFlipX = _xInput > 0f;
            var currentlyFlipX = _spriteRenderer.flipX;

            if (!Mathf.Approximately(_xInput, 0) && inputDirectionFlipX != currentlyFlipX)
            {
                _spriteRenderer.flipX = !_spriteRenderer.flipX;
                var weaponTransform = _weaponInstance.transform;
                
                weaponTransform.RotateAround(weaponTransform.position, weaponTransform.up, 180f);

            }
        }
        
        // Called in FixedUpdate in parent class, only if we can move based on states
        protected override void Move()
        {
            base.Move();

            if (_movementState is MovementState.Teleporting) return;
            if (TeleportCheck()) return;
            
            bool isGrounded = IsGrounded();

            XDirection(isGrounded);
            YDirection(isGrounded);
            rigidbodyVelocity = _rigidbody.velocity;
            if (!_shooting) return;
            OnShootingHold();
        }
    }
}
