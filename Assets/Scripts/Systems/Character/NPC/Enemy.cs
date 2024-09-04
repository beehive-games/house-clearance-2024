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
        [SerializeField, Range(0,1)] private float playerInCoverDetectionDistance = 0.5f;
        [SerializeField, Range(0,1)] private float randomShotChance = 0.5f;
        [SerializeField] private float distanceToPlayerToMaintain = 5f;
        [SerializeField] private float distanceToPlayerToMaintainThreshold = 1f;
        [SerializeField] private float maximumPursueDistance = 20f;

        
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

        private void Start()
        {
            // Put this in Start() to avoid race conditions, as GameRoot.Player is set on Awake()
            if (_playerCharacter == null)
            {
                _playerCharacter = GameRoot.Player;
            }
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
        
        private bool RaycastPlayer()
        {
            Vector3 playerPosition = GameRoot.Player.transform.position;
            Vector3 origin = lineOfSightOrigin.position;
            Vector3 direction = forwardDirection;//_lineOfSight.GetLookDirection(); //(playerPosition - origin).normalized;

            var dbg_offset = Vector3.up * 0.2f;

            Debug.DrawLine(origin  + dbg_offset, playerPosition + dbg_offset, new Color(0.5f,0.75f,0.25f));

            
            var facingDirection = _spriteRenderer.flipX ? -1 : 1;
            float dotProduct = Vector3.Dot(direction, forwardDirection);
            if (dotProduct < 0.9f)
            {
                Debug.DrawRay(origin, direction * maxPlayerVisibilityDistance, Color.magenta);
                Debug.DrawRay(origin - dbg_offset, forwardDirection, Color.magenta * 0.5f);
                return false;
            }

            _results = new RaycastHit[1];
            int hits = Physics.RaycastNonAlloc(origin, direction, _results, maxPlayerVisibilityDistance, playerVisibilityLayerMask);
            
            if (hits <= 0)
            {
                Debug.DrawRay(origin, direction * maxPlayerVisibilityDistance, Color.red);
                return false;
            }

            if (!_results[0].collider.CompareTag("Player"))
            {
                Debug.DrawLine(origin, _results[0].point, Color.yellow);
                return false;
            }
            // tweak - check player direction...
            
            if (_playerCharacter.IsInCover() && !CoverVisibilityCheck() && _enemyState is not EnemyState.Combat && Vector3.Distance(playerPosition, _rigidbody.position) > playerInCoverDetectionDistance )
            {
                Debug.DrawLine(origin, _results[0].point, new Color(1,0.5f,0f));
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

        private void SetMoveDirection()
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
        
        private void SetTargetToLocationFromPlayer(Vector3 playerPosition, Vector3 currentPosition)
        {
            var distance = Vector3.Distance(playerPosition, currentPosition);
            var minDistance = distanceToPlayerToMaintain - distanceToPlayerToMaintainThreshold;
            var maxDistance = distanceToPlayerToMaintain + distanceToPlayerToMaintainThreshold;
            
            if (distance < minDistance || distance > maxDistance)
            {
                var xzPlayerPosition = playerPosition;
                xzPlayerPosition.y = currentPosition.y;
                
                var directionToPlayer = xzPlayerPosition - currentPosition;
                directionToPlayer.Normalize();

                var offset = directionToPlayer * ( distanceToPlayerToMaintain);
                var targetPosition = xzPlayerPosition - offset;
                targetPosition = movementLine.GetClosestPointOnLine(targetPosition);
                _targetPositionV3 = targetPosition;
            }
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

        
        protected override void Move()
        {
            
            base.Move();
            if(movementLine == null) return;
            if (!CanMove()) return;
            
            var isGrounded = IsGrounded();
            var canSeePlayer = RaycastPlayer();
            var playerBeyondDistance = Vector3.Distance(GameRoot.Player.transform.position, _rigidbody.position) >
                                       maximumPursueDistance;
            var walk = false;
            // temp
            var _damageTaken = false;

            if (playerBeyondDistance && _enemyState is EnemyState.Combat)
            {
                _enemyState = EnemyState.Patrolling;
            }
            if(canSeePlayer || _enemyState is EnemyState.Combat || _damageTaken)
            {
                if (_waitTimer != null)
                {
                    StopCoroutine(_waitTimer);
                    _waitTimer = null;
                }
                
                _damageTaken = false;
                
                // face player - set new target location
                SetTargetToLocationFromPlayer(GameRoot.Player.transform.position, _rigidbody.position);
                
                //CombatMovement(isGrounded, canSeePlayer);

                _enemyState = EnemyState.Combat;
                if (CanShoot(canSeePlayer))
                {
                    _weaponInstance.Fire(true);
                }

                if (!ReachedDestination())
                {
                    walk = true;
                }
            }
            else
            {
                var reachedDestination = ReachedDestination();
                if (_enemyState is EnemyState.Patrolling)
                {
                    if (reachedDestination)
                    {
                        _waitTimer = StartCoroutine(PatrolWait());
                    }
                    walk = true;
                }
            }
            
            SetMoveDirection();
            EnemyMoveMechanics(true, walk ? -1 : 0f);
        }
    }
}
