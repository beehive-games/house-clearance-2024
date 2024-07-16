using Character;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Serialization;
using Utils;

namespace Combat.Weapon.Projectiles
{
    public class InstantProjectile : ProjectileBase
    {
        [SerializeField] protected float range = 100f;
        [SerializeField] protected GameObject impactVFX;
        [SerializeField] protected GameObject trailVFX;
        [SerializeField] protected LayerMask hitBoxlayerMask;
        [SerializeField] protected LayerMask vfxlayerMask;

        public override void StartMethod()
        {
            InstaHit();
        }

        private void InstaHit()
        {
            
            var hit = Physics2D.Raycast(transform.position, transform.right * directionSign, range, vfxlayerMask);
            Vector2 hitpoint = transform.position + transform.right * range;
            
            if (hit.collider != null)
            {
                hitpoint = hit.point;
            }

            if (impactVFX != null)
            {
                Instantiate(impactVFX, hitpoint, Quaternion.identity);
            }
            
            
            hit = Physics2D.Raycast(transform.position, transform.right, range, hitBoxlayerMask);
            if (hit.collider != null)
            {
                hitpoint = hit.point;
                var hitBox = hit.collider.gameObject.GetComponent<HitBox>();
                if (hitBox != null)
                {
                    DoDamage(hitBox);
                }
                Debug.DrawLine(transform.position, hitpoint, Color.cyan, 3f);
            }
            else
            {
                Debug.DrawLine(transform.position, hitpoint, Color.red, 3f);
            }
            
            if (trailVFX != null)
            {
                var instantiated = Instantiate(trailVFX, transform.position, Quaternion.identity);
                TrailRenderer trailRenderer = instantiated.GetComponent<TrailRenderer>();
                if (trailRenderer != null)
                {
                    trailRenderer.AddPosition(transform.position);
                    trailRenderer.AddPosition(hitpoint);
                }

                var selfDestruct = instantiated.AddComponent<SelfDestruct>();
                selfDestruct.lifetime = _lifeTime;
                selfDestruct.StartSelfDestruct();

            }
        }
    }
}
