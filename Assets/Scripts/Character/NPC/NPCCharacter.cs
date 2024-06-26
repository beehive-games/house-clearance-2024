using System.Collections;
using UnityEngine;

namespace Character.NPC
{
    public class NPCCharacter : CharacterBase
    {

        [SerializeField] private float _patrolRange = 10f;
        [SerializeField] private float _maxPatrolWaitTime = 4f;
        [SerializeField] private float _forwardCheckIntervalDistance = 2f;
        [SerializeField] private LayerMask _wallLayerMask;
        private RaycastHit2D[] _results = new RaycastHit2D[1];
        private Vector2 _startLocation;
        private Vector2 _targetLocation;
        private Coroutine _patrolWaitCo;
        private bool _waiting;

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
        
        private bool RaycastFloor(Vector2 position, Vector2 moveDirection, float maxMovePerFrameY, float maxMovePerFrameX)
        {
            int hits = Physics2D.RaycastNonAlloc(position + Vector2.right * moveDirection * maxMovePerFrameX, Vector2.down, _results, maxMovePerFrameY, _wallLayerMask);
            Debug.DrawRay(position + Vector2.right * moveDirection * maxMovePerFrameX, Vector2.down * maxMovePerFrameY, Color.cyan);
            return hits > 0;
        }
        
        protected override void Awake()
        {
            base.Awake();
            RevalidateStartPatrolPosition();
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

            _patrolWaitCo = StartCoroutine(PatrolWait());
        }
        
        private void Patrol(bool isGrounded)
        {
            if (_waiting) return;
            
            var position = _groundCheckPivot.position;
            var velocity = _rigidbody2D.velocity;
            var fixedDelta = Time.fixedDeltaTime;
            
            float sign = _targetLocation.x - position.x < 0f ? -1f : 1f;
            Vector2 raycastWallCheckDirection = Vector2.right * sign;
            
            float maxMovePerFrameX = Mathf.Max(Mathf.Abs(velocity.x) * fixedDelta,_forwardCheckIntervalDistance);
            float maxMovePerFrameY = Mathf.Max(Mathf.Abs(velocity.y) * fixedDelta,_groundCheckDistance);
            
            bool hitWall = RaycastWalls(position, raycastWallCheckDirection, maxMovePerFrameX);
            bool hitFloor = RaycastFloor(position, raycastWallCheckDirection, maxMovePerFrameY, maxMovePerFrameX);
            bool reachedDestination = Mathf.Abs(_targetLocation.x - position.x) < maxMovePerFrameX;

            bool getNewPosition = hitWall || !hitFloor || reachedDestination;
            
            if (getNewPosition)
            {
                GetNewTargetLocation(isGrounded);
            }
            else
            {
                MoveMechanics(isGrounded, sign);
            }
        }
        
        private void XDirection(bool isGrounded)
        {
            Patrol(isGrounded);
            
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
