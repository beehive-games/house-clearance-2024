using System;
using System.Collections;
using Character.Player;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace Character.NPC
{
    public class NPCCharacter : CharacterBase
    {
        public Vector3 _targetLocation;
        public bool _waiting;
        public bool _couldSeePlayer;
        private Transform _playerColliderTransform;
        private PlayerCharacter _playerCharacter;
        public bool _queueSlide;
        public bool _pursusing;
        public bool _damageTaken;
        public bool _followingRotation;
        
        
        [Header("NPC controls")]
        [SerializeField] private float _patrolRange = 10f;
        [SerializeField] private float _maxPatrolWaitTime = 4f;
        [SerializeField] private float _forwardCheckIntervalDistance = 2f;
        [SerializeField] private float _maxPlayerVisibilityDistance = 10f;
        [SerializeField] private float _distanceToPlayerToMaintain = 5f;
        [SerializeField] private Transform _lineOfSightOrigin;
        [SerializeField] private float _distanceToPlayerToMaintainThreshold = 1f;
        [SerializeField] private LayerMask _wallLayerMask;
        [SerializeField] private LayerMask _playerVisibilityLayerMask;
        [SerializeField] private LayerMask _coverLayerMask;
        [SerializeField] private float _coverSlideDistance = 2f;
        [SerializeField] private float _maximumPursueDistance = 20f;
        //[SerializeField] private float _minimumDistanceToPlayerForSliding = 2f;
        [SerializeField, Range(0,1)] private float _randomShotChance = 0.5f;
        [SerializeField, Range(0,1)] private float _PlayerInCoverDetectionDistance = 0.5f;
        private RaycastHit[] _results = new RaycastHit[1];
        private Vector3 _startLocation;
        
        private Coroutine _patrolWaitCo;
        

        enum DebugNPCState
        {
            Patrol,
            Combat
        }
        [FormerlySerializedAs("NPCState")]
        [Header("Debugging")]
        [SerializeField, ReadOnly] private DebugNPCState dbg_NPCState;
        [SerializeField, ReadOnly] private bool dbg_canSeePlayer;

        private void GrabPlayerCollider()
        {
            GameObject playerGameObject = GameObject.FindGameObjectWithTag("Player");
            Collider playerCollider = null;
            
            if (playerGameObject != null)
            {
                playerCollider = playerGameObject.GetComponent<Collider>();
            }
            else
            {
                Debug.LogError("Can't locate player GameObject on "+name);

            }

            if (playerCollider == null)
            {
                Debug.LogError("Can't locate player Collider on "+name);
            }
            else
            {
                _playerColliderTransform = playerCollider.transform;
            }
        }

        enum DebugState
        {
            Patrol,
            Waiting,
            Combat,
            Rotating
        }

        private Color gizmoColor = Color.magenta;
        void OnDrawGizmos()
        {
            // Draw a yellow sphere at the transform's position
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position + (Vector3.up * 1.2f), 0.5f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawCube(_targetLocation, Vector3.one * 1f);
            
            Gizmos.color = Color.magenta;
            Gizmos.DrawCube(_startLocation, Vector3.one * 1f);

            if (_activeCorner != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawCube(_activeCorner.transform.position + (Vector3.up), Vector3.one * 0.3f); 
            }

            bool snorth = currentTowerSide is TowerDirection.North or TowerDirection.South;
            
            Gizmos.color = snorth ? Color.black : Color.white;
            Gizmos.DrawCube(_startLocation, new Vector3( snorth? 4: 1, 1, snorth ? 1: 4));

        }
        
        private void DrawStateGizmo(DebugState dbgState)
        {
            Color color = Color.black;
            switch (dbgState)
            {
                case DebugState.Patrol : color = Color.green;
                    break;
                case DebugState.Waiting : color = Color.yellow;
                    break;
                case DebugState.Combat : color = Color.red;
                    break;
                case DebugState.Rotating : color = Color.blue;
                    break;
            }
            gizmoColor = color;
        }
        
        private void RevalidateStartPatrolPosition()
        {
            _startLocation = _rigidbody.position;
            CreateTargetLocation();
        }

        
        private bool RaycastWalls(Vector3 position, Vector3 moveDirection, float maxMovePerFrame)
        {
            int hits = Physics.RaycastNonAlloc(position, moveDirection, _results, maxMovePerFrame, _wallLayerMask);
            //Debug.DrawRay(position, moveDirection * maxMovePerFrame, Color.red);
            return hits > 0;
        }
        
        private bool RaycastCover(Vector3 position, Vector3 moveDirection, float maxCheckDistance)
        {
            int hits = Physics.RaycastNonAlloc(position, moveDirection, _results, maxCheckDistance, _coverLayerMask);
            //Debug.DrawRay(position, moveDirection * maxCheckDistance, Color.magenta);
            if (hits > 0)
            {
                var playerDistance= Vector3.Distance(position, _playerColliderTransform.position);
                var hitDistance= Vector3.Distance(position, _results[0].point);
                return playerDistance > hitDistance;
            }
            return false;
        }
        
        private bool RaycastFloor(Vector3 position, Vector3 moveDirection, float maxMovePerFrameY, float maxMovePerFrameX)
        {
            int hits = Physics.RaycastNonAlloc(position + moveDirection * Mathf.Abs(maxMovePerFrameX), Vector3.down, _results, maxMovePerFrameY, _wallLayerMask);
            Debug.DrawRay(position + moveDirection * maxMovePerFrameX, Vector3.down * maxMovePerFrameY, Color.cyan);
            return hits > 0;
        }
        
        private bool DotProductDirectionMatch(Vector3 a, Vector3 b, bool normalize = false)
        {
            if (normalize)
            {
                a.Normalize();
                b.Normalize();
            }
            float dotProduct = Vector3.Dot(a, b);
            
            //Debug.Log("dotProduct: " +dotProduct);
            return dotProduct > 0.5f;
        }
        
        private bool RaycastPlayer()
        {

            var dotProduct = DotProductDirectionMatch(transform.right, _playerColliderTransform.right, true);
            if (!dotProduct)
            {
                return false;
            }

            Vector3 playerPosition = _playerColliderTransform.position;
            Vector3 origin = _lineOfSightOrigin.position;

            float facingDirection = _spriteRenderer.flipX ? -1 : 1;
            
            //Vector3 direction = facingDirection * transform.right;
            Vector3 direction = (playerPosition - origin).normalized;
            
            int hits = Physics.RaycastNonAlloc(origin, direction, _results, _maxPlayerVisibilityDistance, _playerVisibilityLayerMask);
            
            if (hits <= 0)
            {
                //Debug.DrawRay(origin, direction * _maxPlayerVisibilityDistance, Color.red);
                return false;
            }

            if (!_results[0].collider.CompareTag("Player"))
            {
                //Debug.DrawRay(origin, direction * _maxPlayerVisibilityDistance, Color.red);
                return false;
            }
            
            if (_playerCharacter.IsInCover() && !_pursusing && Vector3.Distance(playerPosition, _rigidbody.position) > _PlayerInCoverDetectionDistance )
            {
                //Debug.DrawLine(origin, _results[0].point, new Color(1f,1.5f,0f));
                return false;
            }

            //Debug.DrawLine(origin, _results[0].point, Color.green);
            return _results[0].collider.CompareTag("Player");
        }
        
        protected override void Awake()
        {
            base.Awake();
            GrabPlayerCollider();
            RevalidateStartPatrolPosition();
            _playerCharacter = _playerColliderTransform.GetComponent<PlayerCharacter>();
            if (_playerCharacter == null)
            {
                Debug.LogError("Player character not found! (on "+gameObject.name+")");
            }
        }

        IEnumerator PatrolWait(float overrideTime = -1f)
        {
            _waiting = true;
            float waitTime = overrideTime >= 0f ? overrideTime : Random.Range(0f, _maxPatrolWaitTime);
            SetRigidbodyVelocityX(0);
            SetRigidbodyVelocityZ(0);
            
            var count = waitTime;
            while (count > 0f)
            {
                DrawStateGizmo(DebugState.Waiting);
                count -= Time.fixedDeltaTime;
                yield return 0;
            }
            //yield return new WaitForSeconds(waitTime);
            
            CreateTargetLocation();
            _waiting = false;
        }

        private void UpdateTargetLocation(Vector3 newLocation)
        {
            Debug.Log("<color=#4444BB> "+currentTowerSide +" and " +_towerRotationService.TOWER_DIRECTION +"</color>");
            _targetLocation = newLocation;
        }
        
        private void CreateTargetLocation()
        {
            float sign = Random.value > 0.5f ? 1f : -1f;
            Vector3 direction = currentTowerSide is TowerDirection.North or TowerDirection.South
                ? _towerRotationService.TOWER_DIRECTION is TowerDirection.North or TowerDirection.South
                    ? Vector3.right
                    : Vector3.forward
                : _towerRotationService.TOWER_DIRECTION is TowerDirection.North or TowerDirection.South
                    ? Vector3.forward
                    : Vector3.right;
            Debug.Log("<color=#44BB44> "+currentTowerSide +" and " +_towerRotationService.TOWER_DIRECTION +", " +direction+"</color>");
            UpdateTargetLocation(_startLocation + (direction * (sign * _patrolRange)));
        }

        private void GetNewTargetLocation(bool isGrounded, float timerOverride = -1f)
        {
            if (isGrounded)
            {
                SetRigidbodyX(0f);
            }
                
            if (_patrolWaitCo != null)
            {
                StopCoroutine(_patrolWaitCo);
            }

            _couldSeePlayer = false;
            
            _patrolWaitCo = StartCoroutine(PatrolWait(timerOverride));
        }

        private float GetSignOfDirection(float a, float b)
        {
            return a - b < 0f ? -1f : 1f;
        }

        private float LocalAxisValue(Vector3 vector, TowerDirection sideAxis)
        {
            var returnValue = 1f;
            
            /*returnValue = sideAxis switch
            {
                TowerDirection.North => vector.x,
                TowerDirection.East => vector.z,
                TowerDirection.South => vector.x,
                TowerDirection.West => vector.z,
                _ => 0f
            };

            return returnValue;*/
            switch (sideAxis)
            {
                case TowerDirection.North :
                    if (currentTowerSide is TowerDirection.North or TowerDirection.South)
                    {
                        returnValue = vector.x;
                    }
                    else
                    {
                        returnValue =  vector.z;
                    }

                    break;
                case TowerDirection.East :
                    if (currentTowerSide is TowerDirection.North or TowerDirection.South)
                    {
                        returnValue =  vector.z;
                    }
                    else
                    {
                        returnValue =  vector.x;
                    }
                    break;

                case TowerDirection.South :
                    if (currentTowerSide is TowerDirection.North or TowerDirection.South)
                    {
                        returnValue =  vector.x;
                    }
                    else
                    {
                        returnValue =  vector.z;
                    }
                    break;

                case TowerDirection.West :
                    if (currentTowerSide is TowerDirection.North or TowerDirection.South)
                    {
                        returnValue =  vector.z;
                    }
                    else
                    {
                        returnValue = vector.x;
                    }
                    break;
            }
            /*
            var returnValue = sideAxis switch
            {
                TowerDirection.North => vector.x,
                TowerDirection.East => vector.z,
                TowerDirection.South => vector.x,
                TowerDirection.West => vector.z,
                _ => 0f
            };
*/
            return returnValue;
        }
        
        private float RotateNPC(TowerDirection characterAxis, TowerCorners cornerHit)
        {
            var returnValue = 0f;
            switch (cornerHit)
            {
                case TowerCorners.NorthEast :
                    if (characterAxis is TowerDirection.North)
                    {
                        returnValue = -1f;
                    }
                    else
                    {
                        returnValue =  1f;
                    }

                    break;
                case TowerCorners.SouthEast :
                    if (characterAxis is TowerDirection.East)
                    {
                        returnValue =  -1f;
                    }
                    else
                    {
                        returnValue =  1f;
                    }
                    break;

                case TowerCorners.SouthWest :
                    if (characterAxis is TowerDirection.South)
                    {
                        returnValue =  -1f;
                    }
                    else
                    {
                        returnValue =  1f;
                    }
                    break;

                case TowerCorners.NorthWest :
                    if (characterAxis is TowerDirection.West)
                    {
                        returnValue =  -1f;
                    }
                    else
                    {
                        returnValue =  1f;
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(cornerHit), cornerHit, null);
            }

            return returnValue;
        }

        float GetSignOfFacingDirection()
        {
            // for now, we are going to assume that NPCs can only ever be in cardinal directions of right/forward, never a diagonal
            if (transform.right.x > 0.5f)
            {
                return 1f;
            }
            if (transform.right.x < 0.5f)
            {
                return -1f;
            }
            if (transform.right.z > 0.5f)
            {
                return 1f;
            }
            if (transform.right.z > 0.5f)
            {
                return -1f;
            }

            return 1f;
        }
        
        private void Patrol(bool isGrounded)
        {
            if (_waiting) return;
            DrawStateGizmo(DebugState.Patrol);
            Vector3 position = _groundCheckPivot.position;
            Vector3 velocity = _rigidbody.velocity;
            var fixedDelta = Time.fixedDeltaTime;

            float targetLocation = LocalAxisValue(_targetLocation, _towerRotationService.TOWER_DIRECTION);
            float positionLocation = LocalAxisValue(position, _towerRotationService.TOWER_DIRECTION);
            
            float sign = GetSignOfDirection(targetLocation, positionLocation);

            Vector3 raycastWallCheckDirection = transform.right * (sign * GetSignOfFacingDirection());
            
            Debug.DrawRay(transform.position, raycastWallCheckDirection * 10, Color.magenta);
            
            float maxMovePerFrameX = Mathf.Max(Mathf.Abs(velocity.x) * fixedDelta,_forwardCheckIntervalDistance);
            float maxMovePerFrameY = Mathf.Max(Mathf.Abs(velocity.y) * fixedDelta,_groundCheckDistance);
            
            bool hitWall = RaycastWalls(position, raycastWallCheckDirection, maxMovePerFrameX);
            bool hitFloor = RaycastFloor(position, raycastWallCheckDirection, maxMovePerFrameY, maxMovePerFrameX);
            
            float posToCheck = targetLocation - positionLocation;
            
            bool reachedDestination = Mathf.Abs(posToCheck) < maxMovePerFrameX;

            if (_followingRotation)
            {
                DrawStateGizmo(DebugState.Rotating);
            }
            if (_followingRotation && _activeCorner != null)
            {
                

                _followingRotation = false;
                var towerCornerPos = _activeCorner.transform.position;
                var newPos = new Vector3(towerCornerPos.x, _rigidbody.position.y, towerCornerPos.z);
                _startLocation = newPos;

                var rotationAmount = 
                    RotateNPC(currentTowerSide, _activeCorner.towerCorner);
                var rotationValue = Quaternion.Euler((transform.rotation.eulerAngles ) + (transform.up * (90f * rotationAmount)));
                //Debug.Log("Rotate : " +_targetLocation +" by " +rotationValue.eulerAngles);
                var positionValue = newPos;//new Vector3(_targetLocation.x, _rigidbody.position.y, _targetLocation.z);
                _rigidbody.Move(positionValue,rotationValue);

                var player = RaycastPlayer();
                if (player)
                {
                    SetTargetToLocationFromPlayer(_playerColliderTransform.position, _rigidbody.position);
                }
                else
                {
                    GetNewTargetLocation(isGrounded, Time.fixedDeltaTime + (1f/30f)); // hack to wait a short moment to allow rotation to happen
                }
                
                switch (currentTowerSide)
                {
                    case TowerDirection.North:
                        currentTowerSide = rotationAmount < 0f ? TowerDirection.East : TowerDirection.West;
                        break;
                    case TowerDirection.East: 
                        currentTowerSide = rotationAmount < 0f ? TowerDirection.South : TowerDirection.North;
                        break;;
                    case TowerDirection.South: 
                        currentTowerSide = rotationAmount < 0f ? TowerDirection.West : TowerDirection.East;
                        break;;
                    case TowerDirection.West: 
                        currentTowerSide = rotationAmount < 0f ? TowerDirection.North : TowerDirection.South;
                        break;;
                }
                
                Debug.Log("NPC is now on " +currentTowerSide);
                
                return;
            }

            bool getNewPosition = hitWall || !hitFloor || reachedDestination || _couldSeePlayer;
            
            if (getNewPosition)
            {
                GetNewTargetLocation(isGrounded);
            }
            else
            {
                
                MoveMechanics(isGrounded, sign);
            }
        }

        private void Slide()
        {
            if(_movementState != MovementState.Slide) _queueSlide = true;
        }

        private void StopSlide()
        {
            _movementState = MovementState.Walk;
            SetRigidbodyX(0f);
        }

        private void DuringSlide(bool isGrounded)
        {
            _queueSlide = false;
            var rigidbodyVelocity = LocalAxisValue(_rigidbody.velocity, currentTowerSide);
            if (!isGrounded || Mathf.Abs(rigidbodyVelocity) < 1f)
            {
                StopSlide();
            }
            else
            {
                float x = rigidbodyVelocity;
                bool sign = Mathf.Sign(x) < 0f;
                float deltaV = _slideFriction * Time.fixedDeltaTime;
                var clamped1 = Mathf.Clamp(x + deltaV, x, 0f);
                var clamped2 = Mathf.Clamp(x - deltaV, 0f, x);
                if (DirectionIsX())
                {
                    SetRigidbodyVelocityX(sign ? clamped1 : clamped2);
                }
                else
                {
                    SetRigidbodyVelocityZ(sign ? clamped1 : clamped2);
                }
            }
        }

        private bool CanShoot(bool canSeePlayer)
        {
            if (!canSeePlayer) return false;
            
            if (weaponPrefab == null) return false;
            
            var playerPosition = _playerColliderTransform.position;
            var rbPosition = _rigidbody.position;

            var distance = Vector3.Distance(playerPosition, rbPosition);
            var signedDirection = GetSignOfDirection(playerPosition.x, rbPosition.x);
            var facingSignedDirection = _spriteRenderer.flipX ? -1f : 1f;
            var directionMatchesFacingDirection = Mathf.Approximately(facingSignedDirection, signedDirection);

            if (directionMatchesFacingDirection && distance < _maxPlayerVisibilityDistance)
            {
                var randomChance = Random.Range(0f, 1f) < _randomShotChance;
                return randomChance;
            }
            
            return false;
        }
        
        private void SetTargetToLocationFromPlayer(Vector3 playerPosition, Vector3 currentPosition)
        {
            var distance = Vector3.Distance(playerPosition, currentPosition);
            var minDistance = _distanceToPlayerToMaintain - _distanceToPlayerToMaintainThreshold;
            var maxDistance = _distanceToPlayerToMaintain + _distanceToPlayerToMaintainThreshold;

            if (distance < minDistance)
            {
                Debug.Log("distance < minDistance");

            }
            else if (distance > minDistance)
            {
                Debug.Log("distance > maxDistance");
            }
            else
            {
                Debug.Log("neither");
            }
            
            if (distance < minDistance || distance > maxDistance)
            {
                var isFrontal = currentTowerSide is TowerDirection.North or TowerDirection.South;
                var directionalAdjusted = isFrontal
                    ? playerPosition.x - currentPosition.x
                    : playerPosition.z - currentPosition.z;
                var direction = (directionalAdjusted) > 0f ? 1f : -1f;
                var directionV3 = new Vector3(
                    isFrontal ? direction * _distanceToPlayerToMaintain : currentPosition.x, 
                    currentPosition.y, 
                    isFrontal ? currentPosition.z : direction * _distanceToPlayerToMaintain//isFrontal ? currentPosition.z : direction * _distanceToPlayerToMaintain
                    );
                var targetPosition = playerPosition - directionV3;
                UpdateTargetLocation(targetPosition);
                Debug.Log("targetPosition = " +targetPosition);
            }

            
        }
        
        private void SetTargetToLocationFromPlayer(Vector2 playerPosition, Vector2 currentPosition)
        {
            var distance = Vector2.Distance(playerPosition, currentPosition);
            var minDistance = _distanceToPlayerToMaintain - _distanceToPlayerToMaintainThreshold;
            var maxDistance = _distanceToPlayerToMaintain + _distanceToPlayerToMaintainThreshold;

            if (distance < minDistance || distance > maxDistance)
            {
                var direction = (playerPosition.x - currentPosition.x) > 0f ? 1f : -1f;
                var directionV2 = new Vector2(direction * _distanceToPlayerToMaintain, 0f);
                var targetPosition = playerPosition - directionV2;
                UpdateTargetLocation(targetPosition);
            }
        }

        public override void Damage(float damage, DamageType damageType)
        {
            base.Damage(damage, damageType);
            _damageTaken = true;
        }
        
        private void CombatMovement(bool isGrounded, bool canSeePlayer)
        {
            if (_patrolWaitCo != null)
            {
                StopCoroutine(_patrolWaitCo);
                _patrolWaitCo = null;
                _waiting = false;
            }

            if (_movementState == MovementState.Immobile)
            {
                StopSlide();
                _pursusing = false;
                SetRigidbodyX(0f);
                return;
            }
            
            if (canSeePlayer && !_pursusing)
            {
                _pursusing = true;
            }

            if (!canSeePlayer && Vector3.Distance(_playerColliderTransform.position, transform.position) > _maximumPursueDistance)
            {
                _pursusing = false;
            }
            
            Vector3 position = _groundCheckPivot.position;
            Vector3 velocity = _rigidbody.velocity;
            var fixedDelta = Time.fixedDeltaTime;
            
            float maxMovePerFrameX = Mathf.Max(Mathf.Abs(velocity.x) * fixedDelta,_forwardCheckIntervalDistance);
            float maxMovePerFrameY = Mathf.Max(Mathf.Abs(velocity.y) * fixedDelta,_groundCheckDistance);
            
            var directionalAdjusted = currentTowerSide is TowerDirection.North or TowerDirection.South
                ? _targetLocation.x - position.x
                : _targetLocation.x - position.x;
            bool reachedDestination = Mathf.Abs(directionalAdjusted) < maxMovePerFrameX;

            
            var signedDirectionalAdjusted = currentTowerSide is TowerDirection.North or TowerDirection.South
                ? new Vector2(_targetLocation.x, position.x)
                : new Vector2(_targetLocation.x, position.x);
            float signedDirection = GetSignOfDirection(signedDirectionalAdjusted.x, signedDirectionalAdjusted.y);
            var direction = reachedDestination ? 0f : signedDirection ;
            
            Vector3 raycastWallCheckDirection = transform.right * (direction * GetSignOfFacingDirection());
            Vector3 coverCheckDirection = (_spriteRenderer.flipX ? -1f : 1f) * Vector3.right;

            var playerPosition = _playerColliderTransform.position;
            
            bool hitWall = RaycastWalls(position, raycastWallCheckDirection, maxMovePerFrameX);
            bool hitFloor = RaycastFloor(position, raycastWallCheckDirection, maxMovePerFrameY, maxMovePerFrameX);
            bool canSeeCover = RaycastCover(_rigidbody.position, coverCheckDirection, _coverSlideDistance);
            bool turnedAroundFromCover = RaycastCover(_rigidbody.position, coverCheckDirection, 1f);
            bool sliding = _movementState == MovementState.Slide;
            bool inCover = _movementState == MovementState.Cover;

            /*
            var rbVelocityAdjusted = LocalAxisValue(_rigidbody.velocity, currentTowerSide);
    
            var signedSpriteAdjusted = DirectionIsX()
                ? new Vector2(playerPosition.x, _rigidbody.position.x)
                : new Vector2(playerPosition.z, _rigidbody.position.z);
            */
            
            bool stopped = Mathf.Abs(_rigidbody.velocity.x) < 0.1f; 
            // TODO: make this use z axis where appropriate
            float signedDirPlayer =  GetSignOfDirection(playerPosition.x, _rigidbody.position.x);
            bool directionsMatch = Mathf.Approximately(signedDirPlayer,_spriteRenderer.flipX ? -1 : 1);

            
            if (!hitFloor && sliding)
            {
                StopSlide();
                sliding = false;
            }

            if (!inCover && sliding && stopped)
            {
                StopSlide();
                sliding = false;
            }
            
            // checking to see if we've turned around from cover, if we're not facing cover, we should come out of it
            if (inCover && !turnedAroundFromCover)
            {
                LeaveCover();
            }
            else if (inCover || sliding)
            {
                return;
            }

            if (hitFloor && !hitWall && !sliding)
            {
                if (!canSeeCover || !directionsMatch)
                {
                    SetTargetToLocationFromPlayer(playerPosition, position);
                }
                else
                {
                    Slide();
                }
            }
            else if (hitFloor && hitWall && !directionsMatch)
            {
                if (sliding)
                {
                    StopSlide();
                }
                SetTargetToLocationFromPlayer(playerPosition, position);
            }

            if (!hitFloor)
            {
                direction = 0f;
                SetRigidbodyX(0f);
                _movementState = MovementState.Walk;
            }
            
            if (isGrounded && _queueSlide)
            {
                StartSlide();
                Debug.Log("Sliding now....");
                _queueSlide = false;
            }
            else if (_movementState == MovementState.Slide)
            {
                DuringSlide(isGrounded);
            }
            else
            {
                MoveMechanics(isGrounded, direction);
            }
            DrawStateGizmo(DebugState.Combat);
        }
        
        private void XDirection(bool isGrounded)
        {
            bool canSeePlayer = RaycastPlayer();
            dbg_canSeePlayer = canSeePlayer;
            
            if(canSeePlayer || _pursusing || _damageTaken)
            {
                //Debug.Log(canSeePlayer+" || "+_pursusing +" || "+_damageTaken);
                _damageTaken = false;
                CombatMovement(isGrounded, canSeePlayer);
                // turn to face player after moving
                _spriteRenderer.flipX = _playerColliderTransform.position.x - _rigidbody.position.x < 0f; 
                dbg_NPCState = DebugNPCState.Combat;
                if (CanShoot(canSeePlayer))
                {
                    _weaponInstance.Fire(true);
                }
            }
            else
            {
                bool inCover = _movementState == MovementState.Cover;

                if (inCover)
                {
                    LeaveCover();
                }
                
                Patrol(isGrounded);
                dbg_NPCState = DebugNPCState.Patrol;
            }

            _couldSeePlayer = canSeePlayer;

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
            
            var tempGameObject = new GameObject();
            var gameObjectTransform = tempGameObject.transform;
            gameObjectTransform.position = _startLocation;
            gameObjectTransform.RotateAround(_towerRotationService.ROTATION_ORIGIN, Vector3.up, _towerRotationService.ROTATION_AMOUNT);
            gameObjectTransform.position = new Vector3(gameObjectTransform.position.x, transform.position.y,
                gameObjectTransform.position.z);
            _startLocation = gameObjectTransform.position;
            Destroy(tempGameObject);
            
            // TODO:
            // we can keep prusuing after a rotation, but we should aim to get to the rotation point, then revert to
            // normal behaviour.
            
            
            
            var myPosition = _rigidbody.position;
            RaycastHit[] raycastHits = new RaycastHit[1];
            var targetPos = _towerRotationService.ROTATION_ORIGIN + (Vector3.up * 0.1f);
            var direction = (targetPos - myPosition).normalized;
            var hits = Physics.RaycastNonAlloc(myPosition, direction, raycastHits, Mathf.Infinity, _groundCheckLayers + _rotationZoneLayer);
            
            _waiting = false;
            if (_patrolWaitCo != null)
            {
                StopCoroutine(_patrolWaitCo);
                _patrolWaitCo = null;  
            }
            
            var dbgtargetPos = _towerRotationService.ROTATION_ORIGIN + (Vector3.up * 0.1f);

            var dbgdirection = (dbgtargetPos - (myPosition  + (Vector3.up * 0.5f))).normalized;
            //Debug.DrawRay(dbgtargetPos,dbgdirection,Color.magenta, 5f);
            
            var canReachRotationPoint = hits > 0 && raycastHits[0].transform.CompareTag("RotationZone");
            // if we hit something, see if its the right thing
            if (canReachRotationPoint && _pursusing && _couldSeePlayer)
            {
                UpdateTargetLocation(_towerRotationService.ROTATION_ORIGIN);
                _followingRotation = true;
                // TODO: set a new state to rotate and face player once we get there
                Debug.DrawLine(myPosition, raycastHits[0].point,Color.green, 5f);
            }
            else
            {
                GetNewTargetLocation(IsGrounded(), 0f);
                //Debug.DrawRay(myPosition,direction,Color.red, 5f);
            }
            _pursusing = false;
            _couldSeePlayer = false;
            

        }
        
        protected override void Move()
        {
            
            base.Move();
        
            if (_movementState == MovementState.Rotating)
            {
                return;
            }
            
            bool isGrounded = IsGrounded();
            XDirection(isGrounded);
            
        }
    }
}
