using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Combat.Weapon
{
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

        private void Awake()
        {
            _currentMagazineCapacity = _magazineCapacity;
        }
    
        IEnumerator ShotIntervalTimer()
        {
            _canShootInternal = false;
            yield return new WaitForSeconds(1f / _shotsPerSecond);
            _canShootInternal = true;
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
            Debug.Log("Reloading!");
            _canShootInternal = false;
            _currentMagazineCapacity = 0;
            yield return new WaitForSeconds(_reloadTime);
            _currentMagazineCapacity = _magazineCapacity;
            _canShootInternal = true;
        }

        private void SpawnThing(GameObject gameObj, Vector2 position, Quaternion rotation)
        {
            var projectile = Instantiate(gameObj, position, Quaternion.identity);
            var tf = transform;
            projectile.transform.LookAt(tf.position + tf.right);
        }
    
        private void SpawnProjectile()
        {
            var angle = Random.Range(0f, _spreadAngle) - 0.5f * _spreadAngle;
            var rotation = Quaternion.Euler(0, angle, 0); // Rotate around the Y-axis
            SpawnThing(_projectile, _muzzlePosition.position, rotation);
        }

        private void SpawnMuzzleVFX()
        {
            SpawnThing(_muzzleVFX, _muzzlePosition.position, Quaternion.identity);
        }
    
        private void SpawnEjectionVFX()
        {
            SpawnThing(_ejectionVFX, _ejectionPosition.position,  Quaternion.identity);
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
                Debug.Log("Shoot!");
                SpawnProjectile();
                SpawnEjectionVFX();
                SpawnMuzzleVFX();
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
