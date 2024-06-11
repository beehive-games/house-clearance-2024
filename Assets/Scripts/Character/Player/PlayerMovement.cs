using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Utils;

namespace Player
{
    public class PlayerMovement : MonoBehaviour
    {
    
        [SerializeField] private InputActionAsset actions;
        private InputAction _moveAction;

        [Header("Player Controls")]
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float acceleration = 8f;
        [SerializeField] private float airMoveForce = 400f;
        [SerializeField] private float jumpForce = 500f;
        [SerializeField] private float transitionalAirVelocity = 50f;
        [SerializeField] private float slideBoostForce = 750f;
        [SerializeField] private Vector2 slideMinimumVelocity = new Vector2(0.1f, 1f);
        [SerializeField] private float fallVelocityToSlide = 20f;
        [SerializeField] private float fallVelocityToDie = 30f;
        [SerializeField] private float floorCheckDistance = 0.0125f;
        [SerializeField] private LayerMask groundCheckLayers;
        private bool _queueJump = false;
        private bool _queueSlide = false;
        private bool _waitForMoveActionDepress = false;
        private readonly string _groundTag = "Ground";
        private readonly string _coverTag = "Cover";
        private float airSpeedTracker = 0f;
        private bool previouslyGrounded = false;
        private float pseudoVelocity = 0f;
        private bool isGamepad = false;
        
    
        [Header("Art")]
        [SerializeField] private Transform spriteObject;
        private Rigidbody2D _rigidbody2D;
        private Collider2D _collider2D;
        private SpriteRenderer _spriteRenderer;
        private Animator _spriteAnimCtrl;
        private readonly int _baseColor = Shader.PropertyToID("_BaseColor");


        [Header("Debug Options")] [SerializeField]
        private bool showFloorDetectionRaycasts = true;

        private enum PlayerState
        {
            Idling,
            Running,
            Falling,
            Sliding,
            Hiding,
            Executing,
            DeadShot,
            DeadFall,
            Immobile
        };
        private PlayerState _playerState;

        private void OnEnable()
        {
            actions.FindActionMap("gameplay").Enable();
        }
        private void OnDisable()
        {
            actions.FindActionMap("gameplay").Disable();
        }
    
        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = spriteObject.GetComponent<SpriteRenderer>();
            _spriteAnimCtrl = spriteObject.GetComponent<Animator>();
            _collider2D = GetComponent<Collider2D>();

            if (!_rigidbody2D)
            {
                Debug.LogError("Rigidbody2D missing from PlayerMovement!");
                enabled = false;
                return;
            }
            
            if (!actions)
            {
                Debug.LogError("InputActionAsset missing from PlayerMovement!");
                enabled = false;
                return;
            }

            if (!spriteObject)
            {
                enabled = false;
                Debug.LogError("_spriteObject missing from PlayerMovement!");
                return;
            }
        
            if (!_spriteRenderer)
            {
                Debug.LogError("SpriteRenderer missing from PlayerMovement!");
                enabled = false;
                return; 
            }

            if (!_spriteAnimCtrl)
            {
                Debug.LogError("Animator missing from PlayerMovement");
                enabled = false;
                return;
            }

            if (!_collider2D)
            {
                Debug.LogError("Collider2D missing from PlayerMovement");
                enabled = false;
                return;
            }

            

            if (fallVelocityToSlide > fallVelocityToDie)
            {
                Debug.LogWarning("Fall Velocity to Slide is greater than Fall Velocity to Die. Please check this is the behaviour you intended");
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
            _queueJump = true;
        }
        
        private void OnSlide(InputAction.CallbackContext context)
        {
            Debug.Log("Slide!");
            _queueSlide = true;
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

        private void DrawDebugRays(Vector2 origin, Vector2 direction, RaycastHit2D hit)
        {
            if (hit.collider)
            {
                Debug.DrawLine(origin, hit.point, Color.green);
                Vector2 perpLine = new Vector2(hit.point.x - 0.25f, hit.point.y);
                Vector2 perpLine2 = new Vector2(hit.point.x + 0.25f, hit.point.y);
                Debug.DrawLine(origin, hit.point, Color.green);
                Debug.DrawLine(perpLine, perpLine2, Color.green);  
            }
            else
            {
                Vector2 o = new Vector2(origin.x, origin.y);
                Vector2 perpLine3 = o + (direction * (floorCheckDistance + 1)) + new Vector2( - 0.25f, 0f);
                Vector2 perpLine4 = o + (direction *(floorCheckDistance + 1)) + new Vector2( 0.25f, 0f);
                Debug.DrawLine(o + direction *(floorCheckDistance + 1), perpLine3, Color.red);
                Debug.DrawLine(o + direction *(floorCheckDistance + 1), perpLine4, Color.red);
                Debug.DrawRay(origin, direction *(floorCheckDistance + 1), Color.red ); 
            }
        }


        private RaycastHit2D Raycast2D(Vector2 origin, Vector2 direction, float distance, Vector2 dotVector, out float nDotUp)
        {
            var hit = Physics2D.Raycast(origin, direction, distance, groundCheckLayers);
            var normal = hit.normal;
            nDotUp = Vector2.Dot(normal, dotVector);

            return hit;
        }
        
        private bool CheckForFloor()
        {
            float colliderWidth = _collider2D.bounds.size.x / 2f;
            Vector3 position = transform.position;
            var originCenter = position + (Vector3.up);
            var originL = position + (Vector3.up) + (Vector3.right * colliderWidth);
            var originR = position + (Vector3.up) - (Vector3.right * colliderWidth);
            var direction = Vector2.down;
            var distance = floorCheckDistance + Vector3.up.magnitude;

            var hitCenter = Raycast2D(originCenter, direction, distance, Vector2.up, out var nDotUpCenter);
            var hitLeft = Raycast2D(originL, direction, distance, Vector2.up, out var nDotUpLeft);
            var hitRight = Raycast2D(originR, direction, distance, Vector2.up, out var nDotUpRight);
            
            if (showFloorDetectionRaycasts)
            {
                DrawDebugRays(originCenter, direction, hitCenter);
                DrawDebugRays(originL, direction, hitLeft);
                DrawDebugRays(originR, direction, hitRight);
            }

            if (!hitCenter && !hitLeft && !hitRight) return false;
            float dotProduct = Mathf.Max( Mathf.Max(nDotUpCenter, nDotUpLeft), nDotUpRight);
            bool playerHitCheck = hitCenter ? hitCenter.transform.CompareTag("Player") : false;
            if (!playerHitCheck)
            {
                playerHitCheck = hitLeft ? hitLeft.transform.CompareTag("Player") : false;

            }
            if (!playerHitCheck)
            {
                playerHitCheck = hitRight? hitRight.transform.CompareTag("Player")  : false;
            }
                                  
            if (dotProduct < Mathf.Epsilon || playerHitCheck)
            {
                return false;
            }
            
            //transform.position = hitCenter.point;
            return true;

        }

        private void UpdateSprites(Vector2 moveVector)
        {
            spriteObject.position = _rigidbody2D.position;
            _spriteRenderer.flipX = FlipSprite(moveVector.x);
            var material = _spriteRenderer.material;
            if (!material)
            {
                Debug.LogError("Sprite Renderer missing material!");
                enabled = false;
            }
        
            // TODO: Fill this out
            switch (_playerState)
            {
                case PlayerState.Idling :  _spriteAnimCtrl.Play("Idle"); material.SetColor(_baseColor, Color.white); break;
                case PlayerState.Running :  _spriteAnimCtrl.Play("Idle"); material.SetColor(_baseColor, Color.white); break;
                case PlayerState.Falling : _spriteAnimCtrl.Play("Fall"); material.SetColor(_baseColor, Color.white); break;
                case PlayerState.Sliding : _spriteAnimCtrl.Play("Slide"); material.SetColor(_baseColor, Color.white); break;
                case PlayerState.Hiding : _spriteAnimCtrl.Play("Idle"); material.SetColor(_baseColor, Color.grey); break;
                case PlayerState.DeadFall : _spriteAnimCtrl.Play("Dead_Fall"); material.SetColor(_baseColor, Color.white); break;
            }
        }
    
        private bool FlipSprite(float moveX)
        {
            if (moveX == 0f) return _spriteRenderer.flipX;
            
            return moveX < 0f;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.CompareTag(_coverTag))
            {
                if (_playerState == PlayerState.Sliding)
                {
                    _playerState = PlayerState.Hiding;
                    _waitForMoveActionDepress = true;
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (_playerState is PlayerState.DeadFall or PlayerState.DeadFall) return;
            
            if (other.collider.CompareTag(_groundTag))
            {
                if (other.relativeVelocity.y >= fallVelocityToDie)
                {
                    _playerState = PlayerState.DeadFall;
                }
                else if (other.relativeVelocity.y >= fallVelocityToSlide)
                {
                    _queueSlide = true;
                    // slide with a bit more oomf, based on Y velocity
                    var newVeloX = _rigidbody2D.VelocityX() + _rigidbody2D.VelocityY() / 2f;
                    _rigidbody2D.SetVelocityY(newVeloX);
                }
                
            }
        }

        private void ReleasedMoveKey()
        {
            if (_waitForMoveActionDepress)
            {
                var xInput = _moveAction.ReadValue<Vector2>().x;
                
                var inputMatchesFacingDirection = _spriteRenderer.flipX ? xInput < 0f  : xInput > 0f;
                if (_moveAction.WasReleasedThisFrame() || !inputMatchesFacingDirection )
                {
                    _waitForMoveActionDepress = false;
                }
            }
        }
        
        // Sync inputs that may change on Update to FixedUpdate knows they've changed
        private void Update()
        {
            ReleasedMoveKey();
        }

        // Kludge to get us a correct value for "does velocity = input direction?"
        private bool MovementSign(float a, float b)
        {
            if (Mathf.Approximately(a, 0f) && Mathf.Approximately(b, 0f))
                return true;
            if (Mathf.Approximately(a, 0f))
            {
                a += 0.0001f * b;
            }
            if (a > 0f && b > 0f)
                return true;
            return a < 0f && b < 0f;
        }

        
        // Ew, but just want it working
        private bool AnyGamepadButtonPressed()
        {
            var currentGamepad = Gamepad.current;
            if (!Mathf.Approximately(currentGamepad.aButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.bButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.xButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.yButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.buttonNorth.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.buttonEast.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.buttonSouth.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.buttonWest.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.leftShoulder.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.leftTrigger.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.rightShoulder.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.rightTrigger.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.rightStick.ReadValue().x, 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.rightStick.ReadValue().y, 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.leftStick.ReadValue().x, 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.leftStick.ReadValue().y, 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.rightStickButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.leftStickButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.selectButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.startButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.triangleButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.circleButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.crossButton.ReadValue(), 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.dpad.ReadValue().x, 0f))
                return true;
            if (!Mathf.Approximately(currentGamepad.dpad.ReadValue().y, 0f))
                return true;
            return false;
        }

        private void DetectInputType()
        {
            var hasGamepad = Gamepad.current != null;
            if (hasGamepad)
            {
                var currentKb = Keyboard.current;
                var currentGamepad = Gamepad.current;
                if (currentKb != null && !Mathf.Approximately(currentKb.anyKey.ReadValue(), 0f))
                {
                    isGamepad = false;
                }
                else if (currentGamepad != null)
                {
                    if (!isGamepad && AnyGamepadButtonPressed())
                    {
                        isGamepad = true;
                    }
                }
            }
            else
            {
                isGamepad = false;
            }
        }
        private void FixedUpdate()
        {
            // 1. get direction input
            var moveVector = _moveAction.ReadValue<Vector2>();

            //1a. we slightly modify input for keyboards later on, so detect input type
            DetectInputType();
            
            // 2. Check if we're dead - just update sprites if we are. Be sure to kill velocity.
            if (_playerState is PlayerState.DeadFall or PlayerState.DeadShot or PlayerState.Immobile)
            {
                UpdateSprites(moveVector);
                _rigidbody2D.SetVelocityX(0f);
                _rigidbody2D.SetVelocityY(0f);
                return;
            }
            
            
            // Get input states
            ReleasedMoveKey();
            
            // Used to force player to release direction key after sliding into cover,
            // so we dont immediately walk out of cover automatically if they're holding
            // the move key down still
            if (_waitForMoveActionDepress)
            {
                moveVector = new Vector2(0f, moveVector.y);
            }
            
            var vX = _rigidbody2D.VelocityX();
            var minVx = slideMinimumVelocity.x;
            var stopThreshold = vX < minVx && vX > -minVx;
        
            // Get physical states
            var grounded = CheckForFloor();
        
            // Figure out if we're supposed to be sliding, as controls are different in this state to regular moving
            if (grounded)
            {
                // Only start slide if we're moving faster than the minimum velocity or are hiding already
                if (_queueSlide)
                {
                    _playerState = PlayerState.Sliding;
                }
                // No sliding is queued, we are grounded - update locomotion to match state
                else
                {
                    if (_playerState == PlayerState.Hiding)
                    {
                        // do nothing atm
                    }
                    // Sliding is still ACTIVE, return to idling if we're slow enough
                    else if (_playerState == PlayerState.Sliding && stopThreshold)
                    {
                        _playerState = PlayerState.Idling;
                    }
                    else if (_playerState != PlayerState.Sliding)
                    {
                        _playerState = stopThreshold ? PlayerState.Idling : PlayerState.Running;
                    }
                }
            }
            else
            {
                _playerState = PlayerState.Falling;
            }
        
            // Perform move actions
            if (grounded)
            {
                
                if (_queueJump)
                {
                    _queueJump = false;
                    _rigidbody2D.AddForce(Vector2.up * jumpForce);
                    _rigidbody2D.SetVelocityX(moveVector.x * moveSpeed);
                }
                else
                {
                    if (_playerState != PlayerState.Sliding)
                    {
                        if (!Mathf.Approximately(moveVector.x, 0f))
                        {
                            _playerState = PlayerState.Running;
                        }
                        else
                        {
                            if (_playerState == PlayerState.Hiding)
                            {
                                
                            }
                        }

                        if (isGamepad)
                        {
                            pseudoVelocity = moveSpeed * moveVector.x;
                        }
                        else
                        {
                            if (!Mathf.Approximately(moveVector.x, 0f))
                            {
                                if (MovementSign(pseudoVelocity, moveVector.x))
                                {
                                    pseudoVelocity += acceleration * Time.fixedDeltaTime * moveVector.x;
                                }
                                else
                                {
                                    pseudoVelocity = 0f;
                                }

                            }
                            else
                            {
                                pseudoVelocity = 0f;
                            }

                            pseudoVelocity = Mathf.Clamp(pseudoVelocity, -moveSpeed, moveSpeed);

                        }

                        _rigidbody2D.SetVelocityX(pseudoVelocity);

                    }
                    else // sliding
                    {
                        if (_queueSlide)
                        {
                            _queueSlide = false;
                            if (_rigidbody2D.VelocityX() < minVx)
                            {
                                var veloX = (slideBoostForce * (_spriteRenderer.flipX ? -1 : 1)) * Time.fixedDeltaTime;
                                _rigidbody2D.SetVelocityX(veloX);
                            }
                            else
                            {
                                // If we're already moving we want to slide faster than stationary, but full speed is a bit too much
                                var veloX =_rigidbody2D.VelocityX() / 2f;
                                _rigidbody2D.SetVelocityX(veloX);
                                _rigidbody2D.AddForce(Vector2.right * (slideBoostForce * (_spriteRenderer.flipX ? -1 : 1)));

                            }
                        }// not a fresh slide, stop if we input-move the opposite direction to travel
                        else if (!Mathf.Approximately(moveVector.x, 0f) &&
                                 (int)Mathf.Sign(_rigidbody2D.VelocityX()) != (int)Mathf.Sign(moveVector.x))
                        {
                            _rigidbody2D.SetVelocityX(0f);
                        }
                    }
                }
            } 
            else // falling
            {
                float moveForce = moveVector.x * airMoveForce * Time.fixedDeltaTime;
                _rigidbody2D.AddForce(Vector2.right * moveForce);
                _queueJump = false;
            }

            previouslyGrounded = grounded;

            // Update Art
            UpdateSprites(moveVector);

        }
    }
}