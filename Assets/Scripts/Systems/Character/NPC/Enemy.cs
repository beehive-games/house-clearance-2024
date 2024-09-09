using System;
using System.Collections;
using Character.Player;
using UnityEngine;
using UnityEngine.Serialization;
using BeehiveGames.HouseClearance;
using Random = UnityEngine.Random;

namespace Character.NPC
{
    public class Enemy : CharacterBase
    {
        public float debugDotProduct;
        
        public float patrolWaitTimer = 3f;
        public float patrolDistance = 4f;
        public float distanceArrivalThreshold = 1f;
        [SerializeField] private Transform lineOfSightOrigin;
        [SerializeField] private float maxPlayerVisibilityDistance = 10f;
        [SerializeField] private LayerMask playerVisibilityLayerMask;
        [SerializeField] private LayerMask patrolVisibilityLayerMask;
        [SerializeField] private LayerMask _coverLayerMask;
        [SerializeField, Range(0,1)] private float playerInCoverDetectionDistance = 0.5f;
        [SerializeField, Range(0,1)] private float randomShotChance = 0.5f;
        [SerializeField] private float distanceToPlayerToMaintain = 5f;
        [SerializeField] private float distanceToPlayerToMaintainThreshold = 1f;
        [SerializeField] private float maximumPursueDistance = 20f;
        [SerializeField] private float _coverSlideDistance = 2f;
        
        private Vector3 _startPosV3;
        private float _startPosLs;
        private Coroutine _waitTimer;
        private Coroutine _newPositionCoroutine;
        private Vector3 _targetPos;
        private float _previousSign;
        private Vector3 _targetPositionV3;
        private bool _waiting;
        private Vector3 forwardDirection;
        private LineOfSight _lineOfSight;
        private Transform cameraTransform;
        public bool _queueSlide;
        private bool _damageTaken;
        private Vector3 _coverPosition;


        public enum EnemyState
        {
            Idling,
            Patrolling,
            Combat
        }

        public EnemyState _enemyState;
        private PlayerCharacter _playerCharacter;
        
        
        protected override void Awake()
        {
            base.Awake();
            

        }

        
        public override void Damage(float damage, DamageType damageType)
        {
            base.Damage(damage, damageType);
            _damageTaken = true;
        }
        
        private void Start()
        {
            // Put this in Start() to avoid race conditions, as GameRoot.Player is set on Awake()
            if (_playerCharacter == null)
            {
                _playerCharacter = GameRoot.Player;
            }
            if (movementLine == false)
            {
                var lines = GameObject.FindGameObjectsWithTag("MovementLine");
                var distance = Mathf.Infinity;
                var position = transform.position;
                NPCMovementLine nearestLine = lines[0].GetComponent<NPCMovementLine>();
                foreach (var line in lines)
                {
                    var distanceToLine = Vector3.Distance(position, line.transform.position);
                    if (distanceToLine < distance)
                    {
                        distance = distanceToLine;
                        nearestLine = line.GetComponent<NPCMovementLine>();
                    }
                }

                movementLine = nearestLine;
            }
            var newPos = movementLine.GetClosestPointOnLine(_rigidbody.position);
            _rigidbody.MovePosition(newPos);
            _startPosV3 = _rigidbody.position;
            _startPosLs = movementLine.GetInterpolatedPointFromPosition(newPos);
            
            var projectedPosition = movementLine.GetClosestPointOnLine(_rigidbody.position);
            var normal = movementLine.GetEdgeNormalFromLinePosition(projectedPosition);
            
            movementLine.RotateRigidbodyToMatchNormal(_rigidbody, normal);
            
            GetSetNewPatrolTargetPosition();
            _enemyState = EnemyState.Patrolling;

            _lineOfSight = lineOfSightOrigin.GetComponent<LineOfSight>();
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogError("Camera not found!");
                enabled = false;
            }
        }


        protected override void HitCover(Vector3 coverPosition)
        {
            base.HitCover(coverPosition);
            _coverPosition = coverPosition;
            _targetPositionV3 = _rigidbody.position;
        }
        
        public bool GotValidNewPosition()
        {
            var randomOffset = Random.Range(-patrolDistance / 2, patrolDistance / 2f);
            var newTargetPositionLineSpace = movementLine.TrasformOffsetWorldSpaceToLineSpace(randomOffset);
            var intendedPosition = movementLine.Interpolate(_startPosLs + newTargetPositionLineSpace);
            var direction = (intendedPosition - _rigidbody.position);
            var distance = direction.magnitude;
            direction.Normalize();
            _results = new RaycastHit[1];
            int hits = Physics.RaycastNonAlloc(_rigidbody.position, direction, _results, distance, patrolVisibilityLayerMask);
            
            if (hits <= 0)
            {
                _targetPositionV3 = intendedPosition;
                return true;
            }

            return false;
        }

        IEnumerator GetNewPatrolPositionCO()
        {
            while (!GotValidNewPosition())
            {
                yield return 0;
            }
            Debug.DrawLine(_rigidbody.position, _targetPositionV3, Color.black, 3f);
        }
        
        private void GetSetNewPatrolTargetPosition()
        {
            if (_newPositionCoroutine != null)
            {
               StopCoroutine(_newPositionCoroutine);
            }
            _newPositionCoroutine = StartCoroutine(GetNewPatrolPositionCO());
        }
        
        public bool CoverVisibilityCheck()
        {
            var direction = (transform.position - _playerCharacter.transform.position);
            
            var playerFwdDir = _playerCharacter.transform.forward;
            var myFwdDir = transform.forward;
            
            // if npc, we use forward direction, if player, we need to use flipX on the sprite

            if (_playerCharacter._spriteRenderer.flipX)
            {
                playerFwdDir = -playerFwdDir;
            }

            var dotProduct = Vector3.Dot(playerFwdDir, myFwdDir);
            if (dotProduct > 0f)
            {
                return true;
            }

            return false;

        }
        
        private bool RaycastCover()
        {
            //_coverSlideDistance
            Vector3 position = _rigidbody.position;
            var playerPos = _playerCharacter.transform.position;
            var direction = (playerPos - position).normalized;
            var dotProduct = Vector3.Dot(direction, -transform.right);
            
            if (dotProduct < 0.5f)
            {
                return false;
            }
            
            
            
            
            int hits = Physics.RaycastNonAlloc(position, -transform.right, _results, _coverSlideDistance, _coverLayerMask);
            
            if (hits > 0)
            {
                var playerDistance= Vector3.Distance(position, GameRoot.Player.transform.position);
                var hitDistance= Vector3.Distance(position, _results[0].point);
                Debug.DrawRay(position + Vector3.up * 2, -transform.right * _coverSlideDistance, new Color(1,0.25f,0.5f));

                var playerFurtherAway = playerDistance > hitDistance;
                var tooCloseThreshold = distanceToPlayerToMaintain;
                var tooClose = hitDistance < tooCloseThreshold;
                
                return playerFurtherAway && !tooClose ;
            }

            return false;
        }
        
        private bool RaycastPlayer()
        {
            Vector3 playerPosition = _playerCharacter.transform.position;
            Vector3 origin = lineOfSightOrigin.position;
            Vector3 direction = -transform.right;

            var dbg_offset = Vector3.up * 0.2f;

            Debug.DrawLine(origin  + dbg_offset, playerPosition + dbg_offset, new Color(0.5f,0.75f,0.25f));

            
            var facingDirection = _spriteRenderer.flipX ? -1 : 1;
            float dotProduct = Vector3.Dot(direction, direction);
            if (dotProduct < 0.9f)
            {
                Debug.DrawRay(origin, direction * maxPlayerVisibilityDistance, Color.magenta);
                Debug.DrawRay(origin - dbg_offset, direction, Color.magenta * 0.5f);
                return false;
            }

            _results = new RaycastHit[10];
            int hits = Physics.RaycastNonAlloc(origin, direction, _results, maxPlayerVisibilityDistance, playerVisibilityLayerMask);
            if (hits <= 0)
            {
                Debug.Log("No hits to looking for player");
                Debug.DrawRay(origin, direction * maxPlayerVisibilityDistance, Color.red);
                return false;
            }
            var hitPlayerCollider = false;

            for (int i = 0; i < hits; i++)
            {
                if (_results[0].collider.CompareTag("Player"))
                {
                    hitPlayerCollider = true;
                    break;
                }
            }
            
            if (!hitPlayerCollider)
            {
                Debug.DrawLine(origin, _results[0].point, Color.yellow);
                Debug.Log("hits but no player");

                return false;
            }
            // tweak - check player direction...
            
            if (_playerCharacter.IsInCover() && !CoverVisibilityCheck() && _enemyState is not EnemyState.Combat && Vector3.Distance(playerPosition, _rigidbody.position) > playerInCoverDetectionDistance )
            {
                Debug.DrawLine(origin, _results[0].point, new Color(1,0.5f,0f));
                Debug.Log($"{_playerCharacter.IsInCover()} && {CoverVisibilityCheck()} && {_enemyState} && {Vector3.Distance(playerPosition, _rigidbody.position)} > {playerInCoverDetectionDistance}");
                return false;
            }
            var hitPlayer = _results[0].collider.CompareTag("Player");
            
            Debug.DrawLine(origin, _results[0].point, hitPlayer ? Color.green : Color.blue);

            return hitPlayer;
        }
        
        private IEnumerator PatrolWait()
        {
            _enemyState = EnemyState.Idling;
            
            yield return new WaitForSeconds(Random.Range(patrolWaitTimer, 0f));
            
            GetSetNewPatrolTargetPosition();
            _enemyState = EnemyState.Patrolling;
            _waitTimer = null;
        }

        private bool ReachedDestination()
        {
            var targetPositionV2 = new Vector2(_targetPositionV3.x, _targetPositionV3.z);
            var rigidbodyPosition = _rigidbody.position;
            var rigidbodyPositionV2 = new Vector2(rigidbodyPosition.x, rigidbodyPosition.z);
            var distance = Vector3.Distance(targetPositionV2, rigidbodyPositionV2);
            return distance < distanceArrivalThreshold;
        }

        private void EnemyMoveMechanics(bool isGrounded, float input)
        {
            input = input * (_aliveState == AliveState.Wounded ? _woundedSpeed : 1f);
		
            var velocity = _rigidbody.velocity;
            
            var acceleration = isGrounded ? _maxAcceleration : _maxAirAcceleration;
            var maxSpeedDelta = acceleration * Time.fixedDeltaTime;
            var targetVelocity = transform.right * (input * _moveSpeed);

            float dp = Vector3.Dot(transform.right, velocity);

            if (dp > 0.95f)
            {
                velocity = new Vector3(
                    Mathf.MoveTowards(velocity.x, targetVelocity.x, maxSpeedDelta),
                    velocity.y,
                    Mathf.MoveTowards(velocity.z, targetVelocity.z, maxSpeedDelta)
                );
            }
            else
            {
                velocity = targetVelocity;
            }
            
            _rigidbody.velocity = velocity;
        }
        
        private bool CanShoot(bool canSeePlayer)
        {
            if (!canSeePlayer) return false;
            
            if (weaponPrefab == null) return false;

            var playerPosition = GameRoot.Player.transform.position;
            var rbPosition = _rigidbody.position;
            var distance = Vector3.Distance(playerPosition, rbPosition);

            if (distance < maxPlayerVisibilityDistance)
            {
                var randomChance = Random.Range(0f, 1f) < randomShotChance;
                return randomChance;
            }
            
            return false;
        }


        private float GetShortestDirection(float currentPosition, float targetPosition)
        {
            var forwardDistance = (targetPosition - currentPosition + 1) % 1;
            var backwardDistance = (currentPosition - targetPosition + 1) % 1;
            
            if (forwardDistance < backwardDistance)
            {
                return 1;
            }
            if (backwardDistance < forwardDistance)
            {
                return -1;
            }
            
            return 1;

        }

        private void SetMoveDirection(bool forceFacePlayer = false)
        {
            if (!forceFacePlayer)
            {
                
                var targetPos = movementLine.GetClosestPointOnLine(_targetPositionV3);
                var interpolatedTargetPos = movementLine.GetInterpolatedPointFromPosition(targetPos);
            
                var transformPos = movementLine.GetClosestPointOnLine(_rigidbody.position);
                var interpolatedTransformPos = movementLine.GetInterpolatedPointFromPosition(transformPos);
            
                var dp = GetShortestDirection(interpolatedTransformPos, interpolatedTargetPos);

                var forwardDirectionForNormalInterpolated = interpolatedTransformPos + 0.01f * dp;
                var forwardPosition = movementLine.Interpolate(forwardDirectionForNormalInterpolated);
                var normal = (forwardPosition - transformPos).normalized;


                var dotProduct = Mathf.Abs(Vector3.Dot(transform.right, normal));
                if (dotProduct < 0.95f)
                {
                    var realignPos = movementLine.GetClosestPointOnLine(_rigidbody.position);
                    var newPos = new Vector3(realignPos.x, _rigidbody.position.y, realignPos.z);
                    _rigidbody.MovePosition(newPos);
                }
            
                Debug.DrawLine(_rigidbody.position, targetPos, Color.magenta);
                movementLine.RotateRigidbodyToMatchNormal(_rigidbody, normal);
                forwardDirection = normal;
            }
            else
            {
                var rb2D = new Vector3(_rigidbody.position.x,0, _rigidbody.position.z);
                var player2D = new Vector3(_playerCharacter.transform.position.x,0, _playerCharacter.transform.position.z);
                forwardDirection = (player2D - rb2D).normalized;
                movementLine.RotateRigidbodyToMatchNormal(_rigidbody, forwardDirection);
            }

        }
        
        private void SetTargetToLocationFromPlayer(Vector3 playerPosition, Vector3 currentPosition)
        {
            var distance = Vector3.Distance(playerPosition, currentPosition);
            var minDistance = distanceToPlayerToMaintain - distanceToPlayerToMaintainThreshold;
            var maxDistance = distanceToPlayerToMaintain + distanceToPlayerToMaintainThreshold;

            bool canReachDestination = true;
            var dirToCheck = _targetPositionV3 - currentPosition;
            var distanceToCheck = dirToCheck.magnitude - 0.1f;
            dirToCheck.Normalize();
            _results = new RaycastHit[1];
            int hits = Physics.RaycastNonAlloc(currentPosition, dirToCheck, _results, distanceToCheck, _groundCheckLayers);
            // cant reach!
            if (hits > 0)
            {
                canReachDestination = false;
            }

            if ((ReachedDestination() && (distance < minDistance || distance > maxDistance)) || !canReachDestination)
            {
                var xzPlayerPosition = playerPosition;
                xzPlayerPosition.y = currentPosition.y;
                
                var directionToPlayer = xzPlayerPosition - currentPosition;
                directionToPlayer.Normalize();

                var offset = directionToPlayer * ( distanceToPlayerToMaintain);
                var targetPosition = xzPlayerPosition - offset;
                
                dirToCheck = targetPosition - currentPosition;
                distanceToCheck = dirToCheck.magnitude - 0.1f;
                dirToCheck.Normalize();

                hits = Physics.RaycastNonAlloc(currentPosition, dirToCheck, _results, distanceToCheck, _groundCheckLayers);
                
                if (hits > 0)
                {
                    targetPosition = xzPlayerPosition + offset;
                }
                
                
                targetPosition = movementLine.GetClosestPointOnLine(targetPosition);
                _targetPositionV3 = targetPosition;
                Debug.DrawLine(Vector3.zero, _targetPositionV3, new Color(0.75f, 0.25f, 0.5f), 10f);
            }
        }
        
        private void Slide()
        {
            if(_movementState != MovementState.Slide) _queueSlide = true;
        }

        private void StopSlide()
        {
            //_movementState = MovementState.Walk;
            var velocity = _rigidbody.velocity;
            velocity.x = 0f;
            velocity.z = 0;
            _rigidbody.velocity = velocity;
        }

        private void DuringSlide(bool isGrounded)
        {
            _queueSlide = false;
            SetMoveDirection(true);
            var velocity = _rigidbody.velocity;
            var maxV = Mathf.Max(Mathf.Abs(velocity.x), Mathf.Abs(velocity.z));
            if (!isGrounded || maxV < 0.1f)
            {
                StopSlide();
                _movementState = MovementState.Jump;
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
        
        protected override void StartSlide()
        {
            base.StartSlide();
            SetMoveDirection(true);
            
            var velocity = _rigidbody.velocity;
            velocity = -transform.right * (_slideBoost * _rigidbody.mass);
            velocity.y = _rigidbody.velocity.y;
            _rigidbody.velocity = velocity;

        }
        
        protected override void UpdateSprite()
        {
            base.UpdateSprite();
            
            var camPos = cameraTransform.forward;
            var camXZ = new Vector2(camPos.x, camPos.z);
        
            var npsPos = transform.forward;
            var npcPosXZ = new Vector2(npsPos.x, npsPos.z);
        
            var npcDotCam = Vector3.Dot(npcPosXZ, camXZ);
            debugDotProduct = npcDotCam;
            
            var playerRight = npcDotCam > 0f ? -_playerCharacter.transform.forward : _playerCharacter.transform.forward;

            _spriteRenderer.transform.LookAt(_spriteRenderer.transform.position + playerRight);
        }


        private bool KeepCoverStatus()
        {
            var distance = Vector3.Distance(_rigidbody.position, _playerCharacter.transform.position);
            var distanceCheck = distance > maxPlayerVisibilityDistance;
            if (distanceCheck)
            {
                Debug.Log($"{distance} > {maxPlayerVisibilityDistance} = {distanceCheck}");
                return false;
            }
            // TODO: Directional Check
            return true;
        }

        private void OnDrawGizmos()
        {
            Color newColor = Color.black;
            if (_movementState == MovementState.Slide)
                newColor = Color.red;
            else if (_movementState == MovementState.Cover)
                newColor = Color.green;

            Gizmos.color = newColor;
            //Gizmos.DrawCube(_rigidbody.position + Vector3.up * 2, Vector3.one);
            
        }

        private bool IsStationary()
        {
            var vX = Mathf.Abs(_rigidbody.velocity.x);
            var vZ = Mathf.Abs(_rigidbody.velocity.z);
            return Mathf.Max(vX, vZ) < 0.1f;
        }

        protected override void Move()
        {
            
            // Initial checks and code
            base.Move();
            if(movementLine == null) return;
            if (!CanMove()) return;
            
            // useful states to know about
            var sliding = _movementState == MovementState.Slide;
            var inCover = _movementState == MovementState.Cover;
           
            var isGrounded = IsGrounded();
            var canSeePlayer = RaycastPlayer();
            var canSeeCover = RaycastCover();

            var playerPosition = _playerCharacter.transform.position;

            // check if player is in a different direction to current cover to us - we dont want to be in cover if that cover is _behind us_
            if (inCover)
            {
                var pDirection = playerPosition - _rigidbody.position;
                pDirection.Normalize();
                
                var cDirection = _coverPosition - _rigidbody.position;
                cDirection.Normalize();

                var cDotP = Vector3.Dot(cDirection, pDirection);
                
                if (canSeeCover)
                {
                    canSeeCover = false; 
                }
                else if(cDotP < 0.5f)
                {
                    LeaveCover();
                    inCover = false;
                    _movementState = MovementState.Walk;
                }
            }
            
            Debug.Log($"{_movementState} and {(canSeePlayer ? "can" : "can't")} see player, and {(canSeeCover ? "can" : "can't")} see cover");

            // check if player is too far away
            var playerBeyondDistance = Vector3.Distance(playerPosition, _rigidbody.position) > maximumPursueDistance;
            var walk = false;

            if (playerBeyondDistance && _enemyState is EnemyState.Combat)
            {
                _enemyState = EnemyState.Patrolling;
                Debug.Log("player too far, patrolling");
                canSeePlayer = false;
                canSeeCover = false;
                _movementState = MovementState.Walk;
            }
            
            
            // COMBAT MODE
            if((canSeePlayer || _enemyState is EnemyState.Combat || _damageTaken) && !sliding)
            {
                if (_waitTimer != null)
                {
                    StopCoroutine(_waitTimer);
                    _waitTimer = null;
                }
                
                SetTargetToLocationFromPlayer(_playerCharacter.transform.position, _rigidbody.position);
 
                _enemyState = EnemyState.Combat;
                if (CanShoot(canSeePlayer))
                {
                    _weaponInstance.Fire(true);
                }

                if (!inCover)
                {
                    if (!ReachedDestination())
                    {
                        walk = true;
                        SetMoveDirection();
                    }
                    else
                    {
                        SetMoveDirection(true);
                    }
                }
            }
            
            // PATROL MODE
            else if(!sliding)
            {
                if (inCover)
                {
                    LeaveCover();
                    inCover = false;
                }
                var reachedDestination = ReachedDestination();
                if (_enemyState is EnemyState.Patrolling)
                {
                    if (reachedDestination)
                    {
                        _waitTimer = StartCoroutine(PatrolWait());
                    }
                    walk = true;
                }
                SetMoveDirection();
            }


            //Debug.Log($"{_enemyState}, {canSeePlayer} && {canSeeCover} && {sliding} && {inCover} && {ReachedDestination()}");

            // Have we stopped sliding from interia of have entered cover?
            if ((sliding && IsStationary()) || inCover)
            {
                StopSlide();
                if(!inCover)
                    _movementState = MovementState.Walk;
            }
            // No? Make us slide
            else if ((_enemyState is EnemyState.Combat || canSeePlayer) && canSeeCover && !sliding)
            {
                Slide();
            }
            
            // Final movement mechanics based on whether we're starting a slide, continuing a slide, walking or in cover
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
                if (_movementState is MovementState.Cover)
                {
                    //Debug.Log("not sliding, in cover.. setting move direction?");
                    SetMoveDirection(true);
                    return;
                }
                EnemyMoveMechanics(true, walk ? -1 : 0f);
            }
        }
    }
}
