using System;
using System.Collections;
using System.Collections.Generic;
using Character;
using UnityEngine;
using Random = UnityEngine.Random;

public enum Allegiance
{
    Friendly,
    Neutral,
    Enemy
}

public enum SubProjectileSpawn
{
    FirstContact,
    DamageTick,
    OnDestroy
}


public class ProjectileBase : MonoBehaviour
{
    [SerializeField] protected float _damageLow = 1f;
    [SerializeField] protected float _damageHigh = 1f;
    [SerializeField] protected float _damageRepeatTime = 0f;
    [SerializeField] protected int _damageRepeats = 0;
    [SerializeField] protected float _lifeTime = 1f;
    [SerializeField] protected GameObject _subProjectile;
    [SerializeField] protected SubProjectileSpawn _subProjectileSpawn;
    
    public DamageType damageType;
    [HideInInspector] public float damage;
    [HideInInspector] public Allegiance allegiance;

    protected Vector2 startPosition;
    protected Coroutine lifetimeTimer;
    protected Coroutine damageCoroutine;
    
    protected virtual void Awake()
    {
        damage = Random.Range(_damageLow, _damageHigh);
        startPosition = transform.position;
    }

    private void Start()
    {
        lifetimeTimer = StartCoroutine(Lifetime());
    }

    private void SpawnSubProjectile()
    {
        var tf = transform;
        Instantiate(_subProjectile, tf.position, tf.rotation);
    }
    
    protected virtual void FixedUpdate() { }

    protected virtual void DoDamage(HitBox hitBox)
    {
        // Apply damage on a coroutine so we can use it for AOE, bleed effects
        damageCoroutine ??= StartCoroutine(DamageTime(hitBox));
    }

    private IEnumerator DamageTime(HitBox hitBox)
    {
        hitBox.Hit(damage, damageType, allegiance);
        
        if (_subProjectileSpawn is SubProjectileSpawn.FirstContact or SubProjectileSpawn.DamageTick)
        {
            SpawnSubProjectile();
        }
        
        if (!(_damageRepeatTime > 0f) || _damageRepeats <= 0) yield break;
        
        while (_damageRepeats > 0)
        {
            _damageRepeats--;
            yield return new WaitForSeconds(_damageRepeatTime);
            hitBox.Hit(damage, damageType, allegiance);
            if (_subProjectileSpawn is SubProjectileSpawn.DamageTick)
            {
                SpawnSubProjectile();
            }
        }
        
        // Damage application complete, kill the projectile
        if (_subProjectileSpawn is SubProjectileSpawn.OnDestroy)
        {
            SpawnSubProjectile();
        }
        
        Destroy(this);
    }
    
    private IEnumerator Lifetime()
    {
        if (_lifeTime > 0f)
        {
            yield return new WaitForSeconds(_lifeTime);
            Destroy(this);
        }
        else
        {
            Destroy(this);
        }
    }

    
    // TODO:
    // 1. on fixed update, check range if collider
    // 2. if instant, do raycastnonalloc to distance, if a hitbox, do damage
    // 3. Add support to spawn VFX
    // 4. add repeat damage - if this is 0, destroy this projectile
    //      and destroy this projectile once lifetime expired
}
