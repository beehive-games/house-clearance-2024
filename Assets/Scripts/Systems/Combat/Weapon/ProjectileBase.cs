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

enum TriggerFalloff
{
    Binary,
    LinearInterpolate,
    InverseSquared
};


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
    private protected float _falloffAdjustedDamage;
    [ReadOnly] public Allegiance allegiance;

    protected Vector3 startPosition;
    protected Coroutine lifetimeTimer;
    protected Coroutine damageCoroutine;
    
    protected virtual void Awake()
    {
        damage = Random.Range(_damageLow, _damageHigh);
        _falloffAdjustedDamage = damage;
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
    
    private protected float FalloffAdjusted(float incomingDamage, Vector2 hitBoxPosition, TriggerFalloff triggerFalloff)
    {
        float distance = Vector2.Distance(hitBoxPosition, transform.position);
        float inverseDistance = Mathf.Clamp01(1f / distance);
        float inverseSquareDistance = Mathf.Clamp01(1f / (distance * distance));
        switch (triggerFalloff)
        {
            case TriggerFalloff.Binary: return incomingDamage;
            case TriggerFalloff.InverseSquared: return Mathf.Lerp(0f,incomingDamage, inverseSquareDistance);
            case TriggerFalloff.LinearInterpolate: return Mathf.Lerp(0f,incomingDamage, inverseDistance);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    protected virtual void FixedUpdate() { }

    protected virtual void DoDamage(HitBox hitBox)
    {
        // Apply damage on a coroutine so we can use it for AOE, bleed effects
        damageCoroutine ??= StartCoroutine(DamageTime(hitBox));
    }

    protected void StopDamage()
    {
        Debug.Log("Stop damaging!");
        if (damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
            damageCoroutine = null;
        }
    }

    private void ComputeHit(HitBox hitBox)
    {
        hitBox.Hit(_falloffAdjustedDamage, damageType, allegiance);
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
        hitBox.CoverHitCheck(transform.position);
        // Hit() returns true if the hitbox can be damaged by projectile
        // and will do damage if it does. We exit early as if nothing happened
        // if Hit() returns false
        var hitSuccessful = hitBox.Hit(_falloffAdjustedDamage, damageType, allegiance);
        if (!hitSuccessful)
        {
            damageCoroutine = null;
            yield break;
        }
        
        if (_subProjectileSpawn is SubProjectileSpawn.FirstContact or SubProjectileSpawn.DamageTick)
        {
            SpawnSubProjectile();
        }


        bool dontRepeat = _damageRepeats <= 0f ? _damageRepeatTime <= 0f : _damageRepeats <= 0f;
        if (dontRepeat)
        {
            ComputeHit(hitBox);
            Destroy(gameObject);
            damageCoroutine = null;
            yield break;
        }

        // If damage repeats is 0, we assume we mean to use it indefinitely
        if (_damageRepeats <= 0f)
        {
            _damageRepeats = 1000000;
        }
        
        while (_damageRepeats > 0)
        {
            _damageRepeats--;
            yield return new WaitForSeconds(_damageRepeatTime);
            ComputeHit(hitBox);
        }
        
        // Damage application complete, kill the projectile
        damageCoroutine = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_subProjectileSpawn is SubProjectileSpawn.OnDestroy)
        {
            SpawnSubProjectile();
        }
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
