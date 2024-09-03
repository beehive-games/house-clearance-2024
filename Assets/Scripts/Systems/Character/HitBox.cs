using UnityEngine;

public enum HitBoxType { Body, Head }

namespace Character
{
    public class HitBox : MonoBehaviour
    {
        private CharacterBase _parentCharacter;
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

        public bool Hit(float damage, DamageType damageType, Allegiance allegiance)
        {

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
