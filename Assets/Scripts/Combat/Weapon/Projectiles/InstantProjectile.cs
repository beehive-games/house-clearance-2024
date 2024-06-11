using UnityEngine;

namespace Combat.Weapon.Projectiles
{
    public class InstantProjectile : ProjectileBase
    {
        [SerializeField] protected float range = 100f;

        protected override void Awake()
        {
            base.Awake();
            InstaHit();

        }

        private void InstaHit()
        {
            //TODO:
            // Raycast forward direction by range, check against player tag and npc tag
            // separately - based on allegiance. this is to avoid enemies blocking
            // their own shots
        
            // if we hit a hitbox, call DoDamage()
        }
    }
}
