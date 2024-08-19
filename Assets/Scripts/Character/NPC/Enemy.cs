using System.Collections;
using Character.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Character.NPC
{
    public class Enemy : CharacterBase
    {

        public NPCMovementLine movementLine;
        public float patrolWaitTimer = 3f;
        public float patrolDistance = 4f;
        public float patrolDistanceArrivalThreshold = 0.1f;
        [SerializeField] private Transform lineOfSightOrigin;
        [SerializeField] private float maxPlayerVisibilityDistance = 10f;
        [SerializeField] private LayerMask playerVisibilityLayerMask;
        [SerializeField, Range(0,1)] private float playerInCoverDetectionDistance = 0.5f;
        [SerializeField, Range(0,1)] private float randomShotChance = 0.5f;

        private Vector3 _startPosV3;
        private float _startPosLs;
        private Coroutine _waitTimer;
        private Vector3 _targetPos;
        private float _previousSign;
        private Vector3 _targetPositionV3;
        private bool _waiting;

        private enum EnemyState
        {
            Idling,
            Patrolling,
            Combat
        }

        private EnemyState _enemyState;
        private Transform _playerColliderTransform;
        private PlayerCharacter _playerCharacter;

        public Enemy(Vector3 targetPos)
        {
            _targetPos = targetPos;
        }

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
        protected override void Awake()
        {
            base.Awake();

            var newPos =movementLine.GetClosestPointOnLine(_rigidbody.position);
            _rigidbody.MovePosition(newPos);
            _startPosV3 = _rigidbody.position;
            _startPosLs = movementLine.GetInterpolatedPointFromPosition(newPos);
            
            var projectedPosition = movementLine.GetClosestPointOnLine(_rigidbody.position);
            var normal = movementLine.GetEdgeNormalFromLinePosition(projectedPosition);
            
            movementLine.RotateRigidbodyToMatchNormal(_rigidbody, normal);
            
            GetSetNewPatrolTargetPosition();
            _enemyState = EnemyState.Patrolling;
            
            GrabPlayerCollider();
            _playerCharacter = _playerColliderTransform.GetComponent<PlayerCharacter>();
            if (_playerCharacter == null)
            {
                Debug.LogError("Player character not found! (on "+gameObject.name+")");
            }

        }
        
        private void GetSetNewPatrolTargetPosition()
        {
            var randomOffset = Random.Range(-patrolDistance / 2, patrolDistance / 2f);
            var newTargetPositionLineSpace = movementLine.TrasformOffsetWorldSpaceToLineSpace(randomOffset);
            _targetPositionV3 = movementLine.Interpolate(_startPosLs + newTargetPositionLineSpace);
            var current = movementLine.GetInterpolatedPointFromAnyPosition(_rigidbody.position);
            //Debug.Log($"targetPositionV3 {_targetPositionV3}, targetInterpolated {_startPosLs + newTargetPositionLineSpace}, currentIntPos {current}, rb {_rigidbody.position}, randomOffset {randomOffset}");
            Debug.DrawLine(_rigidbody.position, _targetPositionV3, Color.black, 3f);
        }
        
        private bool RaycastPlayer()
        {

            Vector3 playerPosition = _playerColliderTransform.position;
            Vector3 origin = lineOfSightOrigin.position;
            Vector3 direction = (playerPosition - origin).normalized;

            float dotProduct = Vector3.Dot(direction, transform.right);
            if (dotProduct < 0.9f)
            {
                return false;
            }
            
            int hits = Physics.RaycastNonAlloc(origin, direction, _results, maxPlayerVisibilityDistance, playerVisibilityLayerMask);
            
            if (hits <= 0)
            {
                return false;
            }

            if (!_results[0].collider.CompareTag("Player"))
            {
                return false;
            }
            
            if (_playerCharacter.IsInCover() && _enemyState is not EnemyState.Combat && Vector3.Distance(playerPosition, _rigidbody.position) > playerInCoverDetectionDistance )
            {
                return false;
            }

            return _results[0].collider.CompareTag("Player");
        }
        
        private IEnumerator PatrolWait()
        {
            _enemyState = EnemyState.Idling;
            
            yield return new WaitForSeconds(Random.Range(patrolWaitTimer, 0f));
            
            GetSetNewPatrolTargetPosition();
            _enemyState = EnemyState.Patrolling;
            _waitTimer = null;
        }

        private bool PatrolReachedDestination()
        {
            var targetPositionV2 = new Vector2(_targetPositionV3.x, _targetPositionV3.z);
            var rigidbodyPosition = _rigidbody.position;
            var rigidbodyPositionV2 = new Vector2(rigidbodyPosition.x, rigidbodyPosition.z);
            var distance = Vector3.Distance(targetPositionV2, rigidbodyPositionV2);
            return distance < patrolDistanceArrivalThreshold;
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
            
            var playerPosition = _playerColliderTransform.position;
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

            Debug.DrawLine(_rigidbody.position, targetPos, Color.magenta);
            movementLine.RotateRigidbodyToMatchNormal(_rigidbody, normal);
        }
        
        protected override void Move()
        {
            
            base.Move();

            var isGrounded = IsGrounded();
            var canSeePlayer = RaycastPlayer();
            var walk = false;
            // temp
            var _damageTaken = false;
            
            if(canSeePlayer || _enemyState is EnemyState.Combat || _damageTaken)
            {
                _damageTaken = false;
                
                // face player - set new target location
                
                //CombatMovement(isGrounded, canSeePlayer);

                _enemyState = EnemyState.Combat;
                if (CanShoot(canSeePlayer))
                {
                    _weaponInstance.Fire(true);
                }
                walk = true;
            }
            else
            {
                var reachedDestination = PatrolReachedDestination();
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
