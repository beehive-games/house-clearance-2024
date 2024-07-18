using System.Collections;
using System.Collections.Generic;
using Character;
using UnityEngine;

namespace Combat.Weapon.Projectiles
{
    public class TriggerProjectile : ProjectileBase
    {

        [SerializeField] private float continueDamageTimeAfterExit = 0f;
        
        private void OnTriggerStay2D(Collider2D other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            DoDamage(hitBox);
        }

        
        private void OnTriggerExit2D(Collider2D other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            if (continueDamageTimeAfterExit > 0f)
            {
                StartCoroutine(ContinueDamage(hitBox));
            }
        }
        
        private IEnumerator ContinueDamage(HitBox hitBox)
        {
            float timer = continueDamageTimeAfterExit;
            
            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (hitBox == null) yield break;
                DoDamage(hitBox);
                yield return 0;
            }
            StopDamage();
        }
    }
}
