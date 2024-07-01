using UnityEngine;

namespace Combat.Weapon.Projectiles
{
    public class InstantProjectile : ProjectileBase
    {
        [SerializeField] protected float range = 100f;
        [SerializeField] protected GameObject impactVFX;

        
        
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
            // if we dont, still spawn the VFX
            
            // TODO - make spawn position impact position
            // TODO - make impactVFX destroy after timer - but make this internal
            if (impactVFX != null)
            {
                Instantiate(impactVFX, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
}
