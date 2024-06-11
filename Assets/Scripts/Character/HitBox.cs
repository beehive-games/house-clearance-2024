using UnityEngine;

namespace Character
{
    public class HitBox : MonoBehaviour
    {
        private CharacterBase _parentCharacter;
        [SerializeField] private Allegiance _allegiance;
        [SerializeField] private float damageMultiplier = 1f;
    
        private void Awake()
        {
            _parentCharacter = GetComponentInParent<CharacterBase>();
            if (_parentCharacter != null) return;
            Debug.LogError("Hit box - can't find parent CharacterBase component");
            enabled = false;
        }

        public void Hit(float damage, DamageType damageType, Allegiance allegiance)
        {
            damage *= damageMultiplier;
            if(_allegiance != allegiance || allegiance == Allegiance.Neutral || _allegiance == Allegiance.Neutral)    
                _parentCharacter.Damage(damage,damageType);
        }

    }
}
