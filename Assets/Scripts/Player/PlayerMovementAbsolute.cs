using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Player
{
    public class PlayerMovementAbsolute : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private float _moveSpeed = 0.01666f;
        [SerializeField] private float _airSpeed = 5f;
        [SerializeField] private float _jumpSpeed = 20f;
        [SerializeField] private float _gridSnap = 0;
        [SerializeField] private float _moveHoldTime = 0.0166f;

        [Header("Physics")]

        [SerializeField] private float _maxAirVelocity = 50f;
        [SerializeField] private float _playerGravity = -9.81f;
        [SerializeField] private string _groundLayerName = "Ground";
        [SerializeField] private string _wallLayerName = "Wall";
        [SerializeField] private float _deathFallSpeed = 20f;
        [SerializeField] private float _slideFallSpeed = 0.5f;
        [SerializeField] private float _floorCheckDistance = 0.5f;
        
        [Header("Art")]
        [SerializeField] private Transform _spriteObject;

        
        // Debugging
        public float vX;
        
        private enum CrouchState
        {
            Off,
            Moving,
            Crouching
        };

        private bool _grounded = false;
        private bool _holdingMove = false;
        private bool _moveEnabled = true;
        private bool _jumping = false;
        private float _interpolatedVelocity;
        private float _count = 0f;
        private int _blockMovementDirection = 0;
        
        private InputAction _moveAction;
        private Rigidbody2D _rigidbody2D;
        private SpriteRenderer _spriteRenderer;
        private Coroutine _moveCoroutine;
        private CrouchState _crouchstate;
        
        // Public Setters/Getters
        public bool IsGrounded()
        {
            return _grounded;
        }
        
        // Public Setters/Getters
        public void DisableMoveInput()
        {
            _moveEnabled = false;
        }
        
        void OnEnable()
        {
            _actions.FindActionMap("gameplay").Enable();
        }
        void OnDisable()
        {
            _actions.FindActionMap("gameplay").Disable();
        }
        
        void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            if (!_rigidbody2D)
            {
                Debug.LogError("Rigidbody2D missing from PlayerMovementAbsolute!");
                enabled = false;
                return;
            }
            
            if (!_actions)
            {
                Debug.LogError("InputActionAsset missing from PlayerMovementAbsolute!");
                enabled = false;
                return;
            }

            if (!_spriteObject)
            {
                enabled = false;
                Debug.LogError("_spriteObject missing from PlayerMovementAbsolute!");
                return;
            }
            _spriteRenderer = _spriteObject.GetComponent<SpriteRenderer>();
            if (!_spriteRenderer)
            {
                Debug.LogError("SpriteRenderer missing from PlayerMovementAbsolute!");
                enabled = false;
                return; 
            }
            
            _moveAction = _actions.FindActionMap("gameplay").FindAction("move");
            _actions.FindActionMap("gameplay").FindAction("special").performed += OnSpecial;
            _actions.FindActionMap("gameplay").FindAction("slide").performed += OnSlide;
            _actions.FindActionMap("gameplay").FindAction("shoot1").performed += OnShoot1;
            _actions.FindActionMap("gameplay").FindAction("shoot2").performed += OnShoot2;
            _actions.FindActionMap("gameplay").FindAction("reload").performed += OnReload;
        }
        
        void FixedUpdate()
        {
            HorizontalMovement();
        }
        
        bool FlipSprite(float moveX)
        {
            if (moveX == 0f) return _spriteRenderer.flipX;
            
            return moveX < 0f;
        }

        private void Update()
        {
            
        }

        private Vector2 WallMovementBlocking(Vector2 moveVector)
        {
            var x =  moveVector.x;
            if (_blockMovementDirection > 0)
            {
                x = x > 0f ? 0 : x;
                if (x < 0f)
                {
                    _blockMovementDirection = 0;
                }
            }
            else if (_blockMovementDirection < 0)
            {
                x = x < 0f ? 0 : x;
                if (x > 0f)
                {
                    _blockMovementDirection = 0;
                }
            }
            return new Vector2(x, moveVector.y);
        }

        
        private void HorizontalMovement()
        {
            
            
            Vector2 moveVector = _moveAction.ReadValue<Vector2>();
            
            moveVector = WallMovementBlocking(moveVector);
            if (CheckForFloor())  GroundPlayer();
            else _grounded = false;
            
            _spriteRenderer.flipX = FlipSprite(moveVector.x);

            // TODO
            // If there's a crouch point set, move to it
            // note: if you press the direction _away_ from the current crouch direction, cancel the crouch 

            if(_grounded)
            {
                _rigidbody2D.velocityX = 0f;
                _rigidbody2D.velocityY = 0f;
                if (!_holdingMove)
                {
                    if (moveVector.x != 0f)
                    {
                        if (_count > _moveHoldTime)
                        {
                            _holdingMove = true;
                            _count = 0f;
                        }
                        _count += Time.deltaTime;
                    }
                    else
                    {
                        _count = 0f;
                    }
                }
                else
                {
                    if (moveVector.x == 0f)
                    {
                        _holdingMove = false;
                        _count = 0f;
                    }
                }
                _rigidbody2D.isKinematic = true;

                if (!_holdingMove)
                {
                    if (moveVector.x != 0f)
                    {
                        if (_moveCoroutine == null)
                        {
                            _moveCoroutine = StartCoroutine(MovePlayer(moveVector.x));
                        }
                        else
                        {
                            Debug.Log("_moveCoroutine HOLD...");
                        }
                    }
                    else
                    {
                        _interpolatedVelocity = 0f;
                    }
                }
                else
                {
                    if (moveVector.x != 0f)
                    {
                        var movePos = _rigidbody2D.position;
                        float multiplier = moveVector.x != 0 ? Mathf.Sign(moveVector.x) : 0f;
                        float gridPos = (movePos.x + (multiplier * _gridSnap * Time.fixedDeltaTime * (1f/_moveSpeed)));
                        _rigidbody2D.MovePosition(new Vector2(gridPos, movePos.y));
                        _interpolatedVelocity = multiplier * _gridSnap * (1f / _moveSpeed);
                    }
                    else
                    {
                        _interpolatedVelocity = 0f;
                    }
                }
            }
            else
            {
                // TODO
                // Add a final raycast to force on grounded = true
                // its possible to be on a floor but not grounded atm

                    ConvertToDynamicRB();
                    float x = Mathf.Clamp(_rigidbody2D.velocityX + moveVector.x *_airSpeed * Time.fixedDeltaTime, -_maxAirVelocity, _maxAirVelocity);
                    _rigidbody2D.velocityX = x;
                    _holdingMove = false;
                    vX = _rigidbody2D.velocityY;
                
            }

            _spriteObject.position = _rigidbody2D.position;
            
        }

        void GroundPlayer()
        {
            _grounded = true;
            _interpolatedVelocity = 0f;
            _jumping = false;
        }
        
        private IEnumerator MovePlayer(float moveX)
        {
            if(_grounded)
            {
                _interpolatedVelocity = 0f;
                var movePos = _rigidbody2D.position;
                float multiplier = moveX != 0 ? Mathf.Sign(moveX) : 0f;
                float gridPos = (movePos.x + (multiplier * _gridSnap));
                _rigidbody2D.MovePosition(new Vector2(gridPos, movePos.y));
                yield return new WaitForSeconds(_moveSpeed);
                
            }
            _moveCoroutine = null;
        }

        private void ConvertToDynamicRB()
        {
            _rigidbody2D.isKinematic = false;
            float interpolatedVelocityTemp = 0f;
            if (_interpolatedVelocity != 0f)
            {
                interpolatedVelocityTemp = _interpolatedVelocity;
                _interpolatedVelocity = 0f;
            }
            float x = Mathf.Clamp(_rigidbody2D.velocityX + interpolatedVelocityTemp, -_maxAirVelocity, _maxAirVelocity);
            //Debug.Log("2: rb.vx: "+_rigidbody2D.velocityX+", i: "+ interpolatedVelocityTemp +", v: "+x);
                
            _rigidbody2D.velocityX = x;
            _rigidbody2D.velocityY += _playerGravity * Time.fixedDeltaTime;
            _holdingMove = false;
            vX = x;
        }
        
        private void OnSpecial(InputAction.CallbackContext context)
        {
            Debug.Log("Special!");
            if (_jumping || !_grounded) return;
            ConvertToDynamicRB();
            _grounded = false;
            _jumping = true;
            _rigidbody2D.velocityY += _jumpSpeed;
        }
        
        private void OnSlide(InputAction.CallbackContext context)
        {
            Debug.Log("Slide!");
            switch (_crouchstate)
            {
                case CrouchState.Off : _crouchstate = CrouchState.Moving;
                    break;
                case CrouchState.Crouching : _crouchstate = CrouchState.Moving;
                    break;
            }
            // TODO:
            // * locate next crouch point, set it
            // * slide is pressed, crouch if long pressed
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

        private bool CheckForFloor()
        {
            if (_jumping) return false;
            bool bHit = false;
            var origin = transform.position + (Vector3.up);
            var direction = Vector2.down;
            var distance = _floorCheckDistance + Vector3.up.magnitude;
            var hits = Physics2D.RaycastAll(origin, direction, distance);
            
            foreach (var hit in hits)
            {
                var normal = hit.normal;
                var nDotUp = Vector2.Dot(normal, Vector2.up);
                if (nDotUp > 0.1f && !hit.transform.CompareTag("Player"))
                {
                    Debug.DrawLine(origin, hit.point, Color.red);
                    Vector2 perpLine = new Vector2(hit.point.x - 0.25f, hit.point.y);
                    Vector2 perpLine2 = new Vector2(hit.point.x + 0.25f, hit.point.y);
                    Debug.DrawLine(origin, hit.point, Color.red);
                    Debug.DrawLine(perpLine, perpLine2, Color.red);
                    bHit = true;
                    transform.position = hit.point;
                    Debug.Log("Grounding from "+hit.collider.name);
                    break;
                }
            }
            if(!bHit)
            {
                Vector2 o = new Vector2(origin.x, origin.y);
                Debug.Log("No floor detected!");
                Vector2 perpLine3 = o + (direction *_floorCheckDistance) + new Vector2( - 0.25f, 0f);
                Vector2 perpLine4 = o + (direction *_floorCheckDistance) + new Vector2( 0.25f, 0f);
                Debug.DrawLine(o + direction *_floorCheckDistance, perpLine3, Color.magenta);
                Debug.DrawLine(o + direction *_floorCheckDistance, perpLine4, Color.magenta);
                Debug.DrawRay(origin, direction *_floorCheckDistance, Color.magenta );

            }

            return bHit;
        }

        private void OnWallHit(Vector3 hitPoint)
        {
            _blockMovementDirection = hitPoint.x - transform.position.x > 0f ? 1 : -1;
        }
        
        private void OnWallRelease()
        {
            _blockMovementDirection = 0;
        }
        
        
        // TODO : Don't use layers, use normal direction of surface to work out wall/floor
        private void OnCollisionEnter2D(Collision2D other)
        {

            var normal = other.contacts[0].normal;
            var nDotUp = Vector2.Dot(normal, Vector2.up);
            var nDotRight = Vector2.Dot(normal, Vector2.right);
            if(other.gameObject.layer == LayerMask.NameToLayer(_wallLayerName))
                Debug.Log("? Hit wall! " + nDotRight);
            else if(other.gameObject.layer == LayerMask.NameToLayer(_groundLayerName))
                Debug.Log("? Hit floor! " + nDotUp);
            Debug.Log("Ah shitballs - hit something? " + nDotRight +", " + nDotUp);
            // TODO : If a wall, prevent movement in that direction until onCollisionExit from that collider
            //
            if (nDotUp > 0.5f)
            {
                // TODO : make this use the top of the sprite instead of the center of the transform!
                if (other.contacts[0].point.y > transform.position.y)
                {
                    return;
                }   
                GroundPlayer();
                if (_rigidbody2D.velocityY > _deathFallSpeed)
                {
                    Debug.Log("You died from a high fall!");
                }
                else if (_rigidbody2D.velocityX > 0f && _rigidbody2D.velocityX > _slideFallSpeed)
                {
                    Debug.Log("You survived a fall and can slide, weeee!");
                }
            } // other.gameObject.layer == LayerMask.NameToLayer(_wallLayerName)
            if (nDotRight > 0.5f || nDotRight < -0.5f)
            {
                OnWallHit(other.contacts[0].point);
                Debug.Log("CONFIRM Hit wall! " + nDotRight);

            }
            else
            {
                Debug.Log("Ah shit - hit something? " + nDotRight +", " + nDotUp);

            }
            
            Debug.Log("(PlayerMovementAbsolute) OnCollisionEnter2D");
        }

        private void OnCollisionExit2D(Collision2D other)
        {
            Debug.Log("Collision OUT: "+other.contacts.Length);
            foreach (var contact in other.contacts)
            {
                Debug.Log("Contact! "+contact.point);
            }
            if (other.gameObject.layer == LayerMask.NameToLayer(_groundLayerName))
            {
                _grounded = false;
            }
            else if (other.gameObject.layer == LayerMask.NameToLayer(_wallLayerName))
            {
                OnWallRelease();
            }
            Debug.Log("(PlayerMovementAbsolute) OnCollisionExit2D");
        }
    }
}
