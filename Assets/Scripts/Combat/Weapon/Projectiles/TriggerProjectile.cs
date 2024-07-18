using System;
using System.Collections;
using System.Collections.Generic;
using Character;
using UnityEngine;
using UnityEngine.Rendering;

namespace Combat.Weapon.Projectiles
{
    public class TriggerProjectile : ProjectileBase
    {

        [SerializeField] private TriggerFalloff _triggerFalloff = TriggerFalloff.Binary;
        [SerializeField] private float continueDamageTimeAfterExit = 0f;
        private Coroutine _exitDamage;
        
        
        private void OnTriggerStay2D(Collider2D other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;

            StopContinueDamageCO();
            
            _falloffAdjustedDamage = FalloffAdjusted(damage, hitBox.transform.position, _triggerFalloff);
            DoDamage(hitBox);
            
        }

        private void StopContinueDamageCO()
        {
            if (_exitDamage != null)
            {
                StopCoroutine(_exitDamage);
                _exitDamage = null;
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            if (continueDamageTimeAfterExit > 0f)
            {
                StopContinueDamageCO();
                _exitDamage = StartCoroutine(ContinueDamage(hitBox));
            }
        }
        
        private IEnumerator ContinueDamage(HitBox hitBox)
        {
            float timer = continueDamageTimeAfterExit;
            
            while (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (hitBox == null) yield break;
                _falloffAdjustedDamage = FalloffAdjusted(damage, hitBox.transform.position, _triggerFalloff);
                DoDamage(hitBox);
                yield return 0;
            }
            StopDamage();
            _exitDamage = null;
        }
    }
}
