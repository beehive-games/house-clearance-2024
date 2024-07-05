using System;
using System.Collections;
using System.Numerics;
using Character.Player;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;

namespace Character.NPC
{
    public class NPCCharacter : CharacterBase
    {
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
        private RaycastHit2D[] _results = new RaycastHit2D[1];
        private Vector2 _startLocation;
        private Vector2 _targetLocation;
        private Coroutine _patrolWaitCo;
        private bool _waiting;
        private bool _couldSeePlayer;
        private Transform _playerColliderTransform;
        private PlayerCharacter _playerCharacter;
        private bool _queueSlide;
        private bool _pursusing;
        private bool _damageTaken;

        enum DebugNPCState
        {
            Patrol,
            Combat
        }
        [FormerlySerializedAs("NPCState")]
        [Header("Debugging")]
        [SerializeField, ReadOnly] private DebugNPCState dbg_NPCState;
        [SerializeField, ReadOnly] private bool dbg_canSeePlayer;

        private void GrabPlayerCollider2D()
        {
            GameObject playerGameObject = GameObject.FindGameObjectWithTag("Player");
            Collider2D playerCollider = null;
            
            if (playerGameObject != null)
            {
                playerCollider = playerGameObject.GetComponent<Collider2D>();
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
        
        private void RevalidateStartPatrolPosition()
        {
            _startLocation = _rigidbody2D.position;
            CreateTargetLocation();
        }

        private bool RaycastWalls(Vector2 position, Vector2 moveDirection, float maxMovePerFrame)
        {
            int hits = Physics2D.RaycastNonAlloc(position, moveDirection, _results, maxMovePerFrame, _wallLayerMask);
            Debug.DrawRay(position, moveDirection * maxMovePerFrame, Color.red);
            return hits > 0;
        }
        
        private bool RaycastCover(Vector2 position, Vector2 moveDirection, float maxCheckDistance)
        {
            int hits = Physics2D.RaycastNonAlloc(position, moveDirection, _results, maxCheckDistance, _coverLayerMask);
            Debug.DrawRay(position, moveDirection * maxCheckDistance, Color.magenta);
            if (hits > 0)
            {
                var playerDistance= Vector2.Distance(position, _playerColliderTransform.position);
                var hitDistance= Vector2.Distance(position, _results[0].point);
                return playerDistance > hitDistance;
            }
            return false;
        }
        
        private bool RaycastFloor(Vector2 position, Vector2 moveDirection, float maxMovePerFrameY, float maxMovePerFrameX)
        {
            int hits = Physics2D.RaycastNonAlloc(position + Vector2.right * moveDirection * maxMovePerFrameX, Vector2.down, _results, maxMovePerFrameY, _wallLayerMask);
            Debug.DrawRay(position + Vector2.right * moveDirection * maxMovePerFrameX, Vector2.down * maxMovePerFrameY, Color.cyan);
            return hits > 0;
        }
        
        private bool RaycastPlayer()
        {
            Vector2 playerPosition = _playerColliderTransform.position;
            Vector2 origin = _lineOfSightOrigin.position;

            float facingDirection = _spriteRenderer.flipX ? -1 : 1;
            
            Vector2 direction = facingDirection * Vector2.right;
            
            int hits = Physics2D.RaycastNonAlloc(origin, direction, _results, _maxPlayerVisibilityDistance, _playerVisibilityLayerMask);
            
            if (hits <= 0 || !_results[0])
            {
                Debug.DrawRay(origin, direction * _maxPlayerVisibilityDistance, Color.red);
                return false;
            }

            if (!_results[0].collider.CompareTag("Player"))
            {
                Debug.DrawRay(origin, direction * _maxPlayerVisibilityDistance, Color.red);
                return false;
            }
            
            if (_playerCharacter.IsInCover() && !_pursusing && Vector2.Distance(playerPosition, _rigidbody2D.position) > _PlayerInCoverDetectionDistance )
            {
                Debug.DrawLine(origin, _results[0].point, new Color(1f,1.5f,0f));
                return false;
            }

            Debug.DrawLine(origin, _results[0].point, Color.green);
            return _results[0].collider.CompareTag("Player");
        }
        
        protected override void Awake()
        {
            base.Awake();
            GrabPlayerCollider2D();
            RevalidateStartPatrolPosition();
            _playerCharacter = _playerColliderTransform.GetComponent<PlayerCharacter>();
            if (_playerCharacter == null)
            {
                Debug.LogError("Player character not found! (on "+gameObject.name+")");
            }
        }

        IEnumerator PatrolWait()
        {
            _waiting = true;
            float waitTime = Random.Range(0f, _maxPatrolWaitTime);
            
            yield return new WaitForSeconds(waitTime);
            
            CreateTargetLocation();
            _waiting = false;
        }
        
        private void CreateTargetLocation()
        {
            float sign = Random.value > 0.5f ? 1f : -1f;
            _targetLocation = _startLocation + (Vector2.right * (sign * _patrolRange));
        }

        private void GetNewTargetLocation(bool isGrounded)
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
            
            _patrolWaitCo = StartCoroutine(PatrolWait());
        }

        private float GetSignOfDirection(float a, float b)
        {
            return a - b < 0f ? -1f : 1f;
        }
        
        private void Patrol(bool isGrounded)
        {
            if (_waiting) return;
            
            var position = _groundCheckPivot.position;
            var velocity = _rigidbody2D.velocity;
            var fixedDelta = Time.fixedDeltaTime;

            float sign = GetSignOfDirection(_targetLocation.x, position.x);
            Vector2 raycastWallCheckDirection = Vector2.right * sign;
            
            float maxMovePerFrameX = Mathf.Max(Mathf.Abs(velocity.x) * fixedDelta,_forwardCheckIntervalDistance);
            float maxMovePerFrameY = Mathf.Max(Mathf.Abs(velocity.y) * fixedDelta,_groundCheckDistance);
            
            bool hitWall = RaycastWalls(position, raycastWallCheckDirection, maxMovePerFrameX);
            bool hitFloor = RaycastFloor(position, raycastWallCheckDirection, maxMovePerFrameY, maxMovePerFrameX);
            bool reachedDestination = Mathf.Abs(_targetLocation.x - position.x) < maxMovePerFrameX;

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
            if (!isGrounded || Mathf.Abs(_rigidbody2D.velocity.x) < 1f)
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

        private bool CanShoot(bool canSeePlayer)
        {
            if (!canSeePlayer) return false;
            
            if (weaponPrefab == null) return false;
            
            var playerPosition = _playerColliderTransform.position;
            var rbPosition = _rigidbody2D.position;

            var distance = Vector2.Distance(playerPosition, rbPosition);
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
                _targetLocation = targetPosition;
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

            if (!canSeePlayer && Vector2.Distance(_playerColliderTransform.position, _rigidbody2D.position) > _maximumPursueDistance)
            {
                _pursusing = false;
            }
            
            var position = _groundCheckPivot.position;
            var velocity = _rigidbody2D.velocity;
            var fixedDelta = Time.fixedDeltaTime;
            
            float maxMovePerFrameX = Mathf.Max(Mathf.Abs(velocity.x) * fixedDelta,_forwardCheckIntervalDistance);
            float maxMovePerFrameY = Mathf.Max(Mathf.Abs(velocity.y) * fixedDelta,_groundCheckDistance);
            bool reachedDestination = Mathf.Abs(_targetLocation.x - position.x) < maxMovePerFrameX;

            float signedDirection = GetSignOfDirection(_targetLocation.x, position.x);
            var direction = reachedDestination ? 0f : signedDirection ;
            
            Vector2 raycastWallCheckDirection = Vector2.right * direction;
            Vector2 coverCheckDirection = (_spriteRenderer.flipX ? -1f : 1f) * Vector2.right;

            var playerPosition = _playerColliderTransform.position;
            
            bool hitWall = RaycastWalls(position, raycastWallCheckDirection, maxMovePerFrameX);
            bool hitFloor = RaycastFloor(position, raycastWallCheckDirection, maxMovePerFrameY, maxMovePerFrameX);
            bool canSeeCover = RaycastCover(_rigidbody2D.position, coverCheckDirection, _coverSlideDistance);
            bool turnedAroundFromCover = RaycastCover(_rigidbody2D.position, coverCheckDirection, 1f);
            bool sliding = _movementState == MovementState.Slide;
            bool inCover = _movementState == MovementState.Cover;
            bool stopped = Mathf.Abs(_rigidbody2D.velocity.x) < 0.1f; 
            float signedDirPlayer =  GetSignOfDirection(playerPosition.x, _rigidbody2D.position.x);
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
        }
        
        private void XDirection(bool isGrounded)
        {
            bool canSeePlayer = RaycastPlayer();
            dbg_canSeePlayer = canSeePlayer;
            
            if(canSeePlayer || _pursusing || _damageTaken)
            {
                _damageTaken = false;
                CombatMovement(isGrounded, canSeePlayer);
                // turn to face player after moving
                _spriteRenderer.flipX = _playerColliderTransform.position.x - _rigidbody2D.position.x < 0f; 
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

            // TODO:
            // Initial Movement (patrol):
            // DONE
            //
            // Combat-based movement:
            // Check line-of-sight to player
            //    also have trigger for sounds/events to attract this NPC to that position
            // If can see the player or have been triggered, set to "pursue" state
            //    if pursuing
            //      look ahead to cover, if we find cover, set that as target location
            //      when distance to cover is less than n-Units, start a slide
            //      Stay in cover, unless direction to player in the x-axis becomes flipped (i.e. the player walks past)
            //      Stay in cover, unless grenade/bomb/AOE is dropped in range, then flee
            //          in opposite direction to AOE to next nearest cover
            //      if we lose light-of-sight to player, exit pursue mode, attempt to
            //      return to starting positing and being patrolling again
            //      if we cant reach starting position, take current position as new patrol position
            //
            // If there isn't valid cover, just stop at "max shooting distance"
            // try to maintain this distance to player - if the players distance is
            // less than a threshold, we move back a bit to maintain distance
            // this simulates the NPCs having a bit of intelligence...
            //
            // To Consider:
            // Do we allow them to jump?? I say "No", but its an open-question
            // Do we allow them to teleport?? I say yes, _if_ they are pursuing player, but this makes returning to initial
            // location tricky. Though this could allow for some emergent gameplay if you can pull NPCs through portals
            // and they get stuck there
        }
    
        protected override void Move()
        {
            base.Move();
        
            bool isGrounded = IsGrounded();
            XDirection(isGrounded);
        
        }
    }
}
