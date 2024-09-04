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
            Debug.LogError("NPCCharacter.cs is DEPRECATED! Disabling!");
            this.enabled = false;
            return;
            
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
            _targetLocation = newLocation;
        }
        
        private void CreateTargetLocation()
        {
            float sign = Random.value > 0.5f ? 1f : -1f;
            Vector3 direction = Vector3.right;
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

            float targetLocation = 1;
            float positionLocation = 1;
            
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
                var isFrontal = true;
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
            
            var directionalAdjusted = 
                _targetLocation.x - position.x;
            bool reachedDestination = Mathf.Abs(directionalAdjusted) < maxMovePerFrameX;

            
            var signedDirectionalAdjusted =
                new Vector2(_targetLocation.x, position.x);
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
            //gameObjectTransform.RotateAround(_towerRotationService.ROTATION_ORIGIN, Vector3.up, _towerRotationService.ROTATION_AMOUNT);
            gameObjectTransform.position = new Vector3(gameObjectTransform.position.x, transform.position.y,
                gameObjectTransform.position.z);
            _startLocation = gameObjectTransform.position;
            Destroy(tempGameObject);
            
            // TODO:
            // we can keep prusuing after a rotation, but we should aim to get to the rotation point, then revert to
            // normal behaviour.
            
            
            
            var myPosition = _rigidbody.position;
            RaycastHit[] raycastHits = new RaycastHit[1];
            var targetPos = (Vector3.up * 0.1f);
            var direction = (targetPos - myPosition).normalized;
            var hits = Physics.RaycastNonAlloc(myPosition, direction, raycastHits, Mathf.Infinity, _groundCheckLayers + _rotationZoneLayer);
            
            _waiting = false;
            if (_patrolWaitCo != null)
            {
                StopCoroutine(_patrolWaitCo);
                _patrolWaitCo = null;  
            }
            
            var dbgtargetPos = (Vector3.up * 0.1f);

            var dbgdirection = (dbgtargetPos - (myPosition  + (Vector3.up * 0.5f))).normalized;
            //Debug.DrawRay(dbgtargetPos,dbgdirection,Color.magenta, 5f);
            
            var canReachRotationPoint = hits > 0 && raycastHits[0].transform.CompareTag("RotationZone");
            // if we hit something, see if its the right thing
            if (canReachRotationPoint && _pursusing && _couldSeePlayer)
            {
                UpdateTargetLocation(transform.position);
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

            if (!CanMove()) return;
            
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
