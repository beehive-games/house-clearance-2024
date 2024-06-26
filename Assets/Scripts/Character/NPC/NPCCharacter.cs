using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCCharacter : CharacterBase
{

    private void XDirection(bool isGrounded)
    {
        // TODO:
        // Initial Movement (patrol):
        // if we are stunned or dead, obviously don't do movement
        // Work out target position if we don't have one
        // Check n-Units in-front of NPC in direction of target for wall & floor at that point
        // if there is no wall and floor, move towards that location
        // if there is a wall or no floor, stop, wait for random(seconds) and find a new valid target
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
        if (_movementState == MovementState.Teleporting)
        {
            return;
        }
            
        bool isGrounded = IsGrounded();
        XDirection(isGrounded);
        
    }
}
