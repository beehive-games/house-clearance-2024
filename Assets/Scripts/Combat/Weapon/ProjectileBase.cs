using System;
using System.Collections;
using System.Collections.Generic;
using Character;
using Unity.Collections;
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
    [HideInInspector] public float directionSign;
    [HideInInspector] public float damage;
    [ReadOnly] public Allegiance allegiance;

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
        if (_subProjectile == null) return;
        var tf = transform;
        Instantiate(_subProjectile, tf.position, tf.rotation);
    }
    
    protected virtual void FixedUpdate() { }

    protected virtual void DoDamage(HitBox hitBox)
    {
        // Apply damage on a coroutine so we can use it for AOE, bleed effects
        damageCoroutine ??= StartCoroutine(DamageTime(hitBox));
    }

    private void ComputeHit(HitBox hitBox)
    {
        hitBox.Hit(damage, damageType, allegiance);
        if (_subProjectileSpawn is SubProjectileSpawn.DamageTick)
        {
            SpawnSubProjectile();
        }
    }

    public virtual void StartMethod()
    {
        
    }
    
    private IEnumerator DamageTime(HitBox hitBox)
    {
        // Hit() returns true if the hitbox can be damaged by projectile
        // and will do damage if it does. We exit early as if nothing happened
        // if Hit() returns false
        var hitSuccessful = hitBox.Hit(damage, damageType, allegiance);
        if (!hitSuccessful)
        {
            damageCoroutine = null;
            yield break;
        }
        
        if (_subProjectileSpawn is SubProjectileSpawn.FirstContact or SubProjectileSpawn.DamageTick)
        {
            SpawnSubProjectile();
        }

        if (!(_damageRepeatTime > 0f) || _damageRepeats <= 0)
        {
            ComputeHit(hitBox);
            Destroy(gameObject);
            damageCoroutine = null;
            yield break;
        }
        
        while (_damageRepeats > 0)
        {
            _damageRepeats--;
            yield return new WaitForSeconds(_damageRepeatTime);
            ComputeHit(hitBox);

        }
        
        // Damage application complete, kill the projectile
        if (_subProjectileSpawn is SubProjectileSpawn.OnDestroy)
        {
            SpawnSubProjectile();
        }

        damageCoroutine = null;
        Destroy(gameObject);
    }
    
    private IEnumerator Lifetime()
    {
        if (_lifeTime > 0f)
        {
            yield return new WaitForSeconds(_lifeTime);
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    
    // TODO:
    // 1. on fixed update, check range if collider
    // 2. if instant, do raycastnonalloc to distance, if a hitbox, do damage
    // 3. Add support to spawn VFX
    // 4. add repeat damage - if this is 0, destroy this projectile
    //      and destroy this projectile once lifetime expired
}
