using Character;
using UnityEngine;

namespace Combat.Weapon.Projectiles
{
    public class ColliderProjectile : ProjectileBase
    {
        private Rigidbody2D _rigidbody2D;
        private Collider2D _collider2D;
        [SerializeField] protected float speed = 100f;
        [SerializeField] protected float range = 100f;

    
        protected override void Awake()
        {
            base.Awake();
        
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _collider2D = GetComponent<Collider2D>();
        
            if (_rigidbody2D != null)
            {
                _rigidbody2D.velocityX = speed;
                startPosition = _rigidbody2D.position;
            }
            else
            {
                Debug.LogError("Collider Projectile has no Rigidbody2D!");
                enabled = false;
            }
        }

        protected override void DoDamage(HitBox hitBox)
        {
            _collider2D.enabled = false;
            base.DoDamage(hitBox);
        }
    
        private void OnCollisionEnter2D(Collision2D other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            DoDamage(hitBox);
        }
    
        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!(Vector2.Distance(startPosition, _rigidbody2D.position) > range)) return;
            if (damageCoroutine != null) return;
        
            StopCoroutine(lifetimeTimer);
            Destroy(this);
        }
    }
}
