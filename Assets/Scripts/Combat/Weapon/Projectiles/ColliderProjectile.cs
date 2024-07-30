using Character;
using UnityEngine;
using Utils;

namespace Combat.Weapon.Projectiles
{
    public class ColliderProjectile : ProjectileBase
    {
        private Rigidbody _rigidbody;
        private Collider _collider;
        public float speed = 100f;
        [SerializeField] protected float range = 100f;
        [SerializeField] protected float minimumSpeed = 1f;

    
        protected override void Awake()
        {
            base.Awake();
        
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            
            // TODO:
            /*
             * Get forward axis, xy or zy, from tower rotation
             * lock out the x/z axis we're not using
             */
        
            if (_rigidbody != null)
            {
                _rigidbody.SetVelocityX(speed);
                startPosition = _rigidbody.position;
            }
            else
            {
                Debug.LogError("Collider Projectile has no Rigidbody!");
                enabled = false;
            }
        }

        public void SetStartSpeed(float newXVelocity)
        {
            if (_rigidbody != null)
            {
                _rigidbody.SetVelocityX(newXVelocity);
            }
        }
        
        protected override void DoDamage(HitBox hitBox)
        {
            _collider.enabled = false;
            base.DoDamage(hitBox);
        }
    
        private void OnCollisionEnter(Collision other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            DoDamage(hitBox);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            DoDamage(hitBox);
        }
        
        private void OnCollisionEnter2D(Collision2D other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            DoDamage(hitBox);
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            var hitBox = other.gameObject.GetComponent<HitBox>();
            if (hitBox == null) return;
            DoDamage(hitBox);
        }
    
        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (_rigidbody.velocity.magnitude < minimumSpeed)
            {
                Destroy(gameObject);
                return;
            }
            
            if (!(Vector3.Distance(startPosition, _rigidbody.position) > range)) return;
            if (damageCoroutine != null) return;
        
            StopCoroutine(lifetimeTimer);
            Destroy(gameObject);
        }
    }
}
