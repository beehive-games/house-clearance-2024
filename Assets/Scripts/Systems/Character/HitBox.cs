using Character.NPC;
using Character.Player;
using UnityEngine;

public enum HitBoxType { Body, Head }

namespace Character
{
    public class HitBox : MonoBehaviour
    {
        private CharacterBase _parentCharacter;
        public bool sleep = false;
        [SerializeField] private Allegiance _allegiance;
        [SerializeField] private HitBoxType _hitBoxType;
        [SerializeField] private float damageMultiplier = 1f;

        private void Awake()
        {
            _parentCharacter = GetComponentInParent<CharacterBase>();
            if (_parentCharacter != null) return;
            //Debug.LogError("Hit box - can't find parent CharacterBase component");
            enabled = false;
        }

        public void CoverHitCheck(Vector3 sourcePosition)
        {
            var direction = (sourcePosition - _parentCharacter.transform.position);
            
            var forwardDirection = _parentCharacter.transform.right;
            // if npc, we use forward direction, if player, we need to use flipX on the sprite
            var p = (_parentCharacter as PlayerCharacter);
            if (p != null)
            {
                if (p._spriteRenderer.flipX)
                {
                    forwardDirection = -forwardDirection;
                }
            }

            var dotProduct = Vector3.Dot(direction.normalized, forwardDirection);
            if (dotProduct < 0f)
            {
                _parentCharacter.LeaveCover();
            }
        }

        
        public bool Hit(float damage, DamageType damageType, Allegiance allegiance)
        {
            if (sleep) return false;
            
            damage *= damageMultiplier;
            if (damageType == DamageType.Projectile && _hitBoxType == HitBoxType.Head)
            {
                damageType = DamageType.ProjectileHead;
            }

            if (_allegiance != allegiance || allegiance == Allegiance.Neutral || _allegiance == Allegiance.Neutral)
            {
                _parentCharacter.Damage(damage,damageType);
                return true;
            }

            return false;
        }

    }
}
