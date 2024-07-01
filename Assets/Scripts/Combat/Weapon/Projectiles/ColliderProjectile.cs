using Character;
using UnityEngine;
using Utils;

namespace Combat.Weapon.Projectiles
{
    public class ColliderProjectile : ProjectileBase
    {
        private Rigidbody2D _rigidbody2D;
        private Collider2D _collider2D;
        public float speed = 100f;
        [SerializeField] protected float range = 100f;
        [SerializeField] protected float minimumSpeed = 1f;

    
        protected override void Awake()
        {
            base.Awake();
        
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _collider2D = GetComponent<Collider2D>();
        
            if (_rigidbody2D != null)
            {
                _rigidbody2D.SetVelocityX(speed);
                startPosition = _rigidbody2D.position;
            }
            else
            {
                Debug.LogError("Collider Projectile has no Rigidbody2D!");
                enabled = false;
            }
        }

        public void SetStartSpeed(float newXVelocity)
        {
            if (_rigidbody2D != null)
            {
                _rigidbody2D.SetVelocityX(newXVelocity);
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
            Debug.Log(gameObject.name +" detected a collision with "+other.gameObject.name);
            if (hitBox == null) return;
            DoDamage(hitBox);
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log(gameObject.name +" detected a trigger overlap with "+other.gameObject.name);
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            DoDamage(hitBox);
        }
    
        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (_rigidbody2D.velocity.magnitude < minimumSpeed)
            {
                Destroy(gameObject);
                return;
            }
            
            if (!(Vector2.Distance(startPosition, _rigidbody2D.position) > range)) return;
            if (damageCoroutine != null) return;
        
            StopCoroutine(lifetimeTimer);
            Destroy(gameObject);
        }
    }
}
