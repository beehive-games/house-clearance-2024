using System.Collections;
using Combat.Weapon.Projectiles;
using UnityEngine;
using UnityEngine.Serialization;
using Utils;
using Random = UnityEngine.Random;

namespace Combat.Weapon
{
    public struct Ammo
    {
        public int currentAmmo;
        public int magazineCapacity;

        public Ammo(int ammo, int magazine)
        {
            currentAmmo = ammo;
            magazineCapacity = magazine;
        }
    }
    
    public class WeaponBase : MonoBehaviour
    {
        [Header("Base Properties")]
        [SerializeField] private GameObject _projectile;
        [SerializeField] private float _shotsPerSecond = 1f;
        [SerializeField] private int _magazineCapacity = 8;
        [SerializeField] private float _reloadTime = 1f;
        [SerializeField] private float _spreadAngle = 5f;
        [SerializeField] private bool _fireOnEveryPress = false;
        [Space]
        [Header("Muzzle")]
        [SerializeField] private Transform _muzzlePosition;
        [SerializeField] private GameObject _muzzleVFX;
        [Space]
        [Header("Ejection")]
        [SerializeField] private Transform _ejectionPosition;
        [SerializeField] private GameObject _ejectionVFX;

    
        private bool _canShootInternal = true;
        private bool _canShootExternal = true;
        private int _currentMagazineCapacity;
        private Coroutine _shootTimer;
        private Coroutine _reloadTimer;
        private CharacterBase _parentCharacter;
        private Rigidbody _parentCharacterRB;
        private TowerRotationService _service;
        [HideInInspector] public Allegiance allegiance;
      
        
        public Ammo GetAmmo()
        {
            return new Ammo(_currentMagazineCapacity, _magazineCapacity);
        }
        
        public void Setup(Transform newParent)
        {
            _service = ServiceLocator.GetService<TowerRotationService>();
            
            _parentCharacter = gameObject.transform.parent.parent.GetComponent<CharacterBase>();
            _parentCharacterRB = _parentCharacter.GetComponent<Rigidbody>();
            if (_parentCharacter == null)
            {
                _parentCharacter = gameObject.transform.parent.parent.GetComponentInChildren<CharacterBase>();
            }

            if (_parentCharacter == null)
            {
                Debug.LogError("_playerCharacter missing!");
            }
            var tf = transform;
            tf.parent = newParent;
            var resetXPositionHack = new Vector2(0f, tf.localPosition.y);
            tf.localPosition = resetXPositionHack;
        }
        
        private void Awake()
        {
            _currentMagazineCapacity = _magazineCapacity;
        }
    
        IEnumerator ShotIntervalTimer()
        {
            _canShootInternal = false;
            yield return new WaitForSeconds(1f / _shotsPerSecond);
            _canShootInternal = true;
            _shootTimer = null;
        }

        public void EnableShooting()
        {
            _canShootExternal = true;
        }

        public void DisableShooting()
        {
            _canShootExternal = false;
        }
        
        public void SetShooting(bool canShoot)
        {
            _canShootExternal = canShoot;
        }
        
        private bool CanShoot()
        {
            return _canShootInternal && _canShootExternal;
        }
    
        IEnumerator ReloadIntervalTimer()
        {
            _canShootInternal = false;
            _currentMagazineCapacity = 0;
            yield return new WaitForSeconds(_reloadTime);
            _currentMagazineCapacity = _magazineCapacity;
            _canShootInternal = true;
            _reloadTimer = null;
        }

        private void SpawnThing(GameObject gameObj, Vector2 position, Quaternion rotation, Vector3 spawnVelocity)
        {
            if (gameObj == null) return;

            var adjustedRotation = Quaternion.Euler(rotation.eulerAngles + Vector3.forward * 180f);
            var adjustedTransform = new GameObject
            {
                transform =
                {
                    position = position,
                    rotation = transform.localScale.x > 0f? rotation : adjustedRotation
                }
            };

            Transform tf = adjustedTransform.transform;
            
            var projectile = Instantiate(gameObj, tf.position, tf.rotation);
            
            var projectileRb = projectile.GetComponent<Rigidbody>();
            
            Vector3 additionalVelocity = _parentCharacterRB.velocity;
            
            float directionalAdditionalVelocity = _service.TOWER_DIRECTION is TowerDirection.East or TowerDirection.West
                ? additionalVelocity.z
                : additionalVelocity.x;
            
            if (projectileRb != null)
            {
                projectileRb.velocity += spawnVelocity * transform.localScale.x ;
                
            }
            
            var projectileBase = projectile.GetComponent<ProjectileBase>();
            if (projectileBase != null)
            {
                projectileBase.allegiance = allegiance;
                var colliderBase = projectileBase as ColliderProjectile;
                if(colliderBase != null)
                {
                    colliderBase.SetStartSpeed(colliderBase.speed * transform.localScale.x + directionalAdditionalVelocity);
                }

                projectileBase.StartMethod();
            }
            Destroy(adjustedTransform);
        }
    
        private void SpawnProjectile()
        {
            var angle = Random.Range(0f, _spreadAngle) - 0.5f * _spreadAngle;
            var rotation = Quaternion.Euler(0, 0, angle); // Rotate around the Y-axis
            SpawnThing(_projectile, _muzzlePosition.position, rotation, Vector3.zero);
        }

        private void SpawnMuzzleVFX()
        {
            SpawnThing(_muzzleVFX, _muzzlePosition.position, Quaternion.identity, Vector3.zero);
        }
    
        private void SpawnEjectionVFX()
        {
            SpawnThing(_ejectionVFX, _ejectionPosition.position,  Quaternion.identity, Vector3.zero);
        }
    
        private void DoShot()
        {
            if (_currentMagazineCapacity - 1 <= 0)
            {
                _reloadTimer ??= StartCoroutine(ReloadIntervalTimer());
            }
            else
            {
                _currentMagazineCapacity--;
                SpawnProjectile();
                SpawnEjectionVFX();
                SpawnMuzzleVFX();
                if (_parentCharacter != null)
                {
                    _parentCharacter.ShootFromCover();
                }
            }
        }
    
        public void Fire(bool hold = true)
        {
            if (!hold && _fireOnEveryPress && CanShoot())
            {
                DoShot();
            }
            else
            {
                if (_shootTimer == null && CanShoot())
                {
                    DoShot();
                    _shootTimer = StartCoroutine(ShotIntervalTimer());
                }
                else if(CanShoot())
                {
                    DoShot();
                }
            }
        }
    }
}
