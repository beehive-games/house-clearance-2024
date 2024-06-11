using System;
using Combat.Weapon;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Utils;

public enum DamageType
{
	Fall,
	Melee,
	Projectile,
	Explosion,
	Fire,
	Acid
}

public class CharacterBase : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private protected float _moveSpeed = 10f;
	[SerializeField] private protected float _movementSmoothing = 0.05f;
	[SerializeField, Range(0,1)] private protected float _woundedSpeed = 0.66f;

	[Space]
	[SerializeField] private protected float _jumpForce = 400f;
	[SerializeField] private protected float _gravityMultiplier = 1f;
	
	[Space]
	[SerializeField] private protected float _slideFriction = 15f;
	[SerializeField] private protected float _slideBoost = 300f;
	[SerializeField] private protected float _slideCooldown = 1f;
	
	[Space]
	[SerializeField] private protected float _fallAutoSlideVelocity = 750f;
	[SerializeField] private protected float _fallDeathVelocity = 1f;

	[Space] 
	[Header("Art")]
	[SerializeField] private protected bool _facingRight;
	[SerializeField] private protected Transform _spriteObject;
	private protected Rigidbody2D _rigidbody2D;
	private protected Collider2D _collider2D;
	private protected SpriteRenderer _spriteRenderer;
	private protected Animator _spriteAnimCtrl;
	private readonly int _baseColor = Shader.PropertyToID("_BaseColor");
	
	[Space] 
	[Header("Combat")]
	[SerializeField] private protected float _startingHealth = 100f;
	[SerializeField, Range(0,1)] private protected float _woundedHealthLevel = 0.33f;
	[SerializeField] private protected float _rehealCooldown = 3f;
	[SerializeField] private protected float _rehealSpeed = 20f;
	public WeaponBase weapon;
	[Space]

	[Header("Events")]
	public UnityEvent onLandEvent;

	private float _currentHealth;
	private float _previousVY;
	private protected float _lastDamageCounter;
	
	internal enum MovementState
	{
		Idle,
		Walk,
		Slide,
		Cover,
		Jump,
		Dead,
		Immobile
	}

	internal enum AliveState
	{
		Alive,
		Wounded,
		DeadFall,
		DeadMelee,
		DeadShot,
		DeadGibs,
		DeadBurnt,
		DeadAcid
	}
	
	
	
	private protected MovementState _movementState;
	private protected AliveState _aliveState;
	
	protected virtual void Awake()
	{
		StartUpChecks();
	}

	private void StartUpChecks()
	{
		
		if (_spriteObject == null)
		{
			Debug.LogError("SpriteObject not assigned!");
			enabled = false;
			return;
		}
		
		onLandEvent ??= new UnityEvent();
		_currentHealth = _startingHealth;
		
		_rigidbody2D = GetComponent<Rigidbody2D>();
		_spriteRenderer = _spriteObject.GetComponent<SpriteRenderer>();
		_spriteAnimCtrl = _spriteObject.GetComponent<Animator>();
		_collider2D = GetComponent<Collider2D>();

		if (!_rigidbody2D)
		{
			Debug.LogError("Rigidbody2D missing from PlayerMovement!");
			enabled = false;
			return;
		}

		if (!_spriteObject)
		{
			enabled = false;
			Debug.LogError("_spriteObject missing from PlayerMovement!");
			return;
		}
        
		if (!_spriteRenderer)
		{
			Debug.LogError("SpriteRenderer missing from PlayerMovement!");
			enabled = false;
			return; 
		}

		if (!_spriteAnimCtrl)
		{
			Debug.LogError("Animator missing from PlayerMovement");
			enabled = false;
			return;
		}

		if (!_collider2D)
		{
			Debug.LogError("Collider2D missing from PlayerMovement");
			enabled = false;
			return;
		}

		if (_fallAutoSlideVelocity > _fallDeathVelocity)
		{
			Debug.LogWarning("Fall Velocity to Slide is greater than Fall Velocity to Die. Please check this is the behaviour you intended");
		}
	}

	// Todo: move to hitbox code? ALl damage is from hitboxes only?
	private void OnCollisionEnter2D(Collision2D other)
	{
		// Death from fall?
		if (_previousVY > -_fallDeathVelocity) return;
		
		_rigidbody2D.SetVelocityY(0f);
		Kill(DamageType.Fall);
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		// TODO: Support cover trigger volume collisions
	}

	// This will contain the final movement logic based on 
	// inherited class behaviour
	// i.e. for Player, we will take controller input
	// for NPC we will use some AI logic
	protected virtual void Move() { }
	
	protected virtual bool CanMove()
	{
		return _movementState is not (MovementState.Dead or MovementState.Immobile);
	}
	
	protected virtual void Update() { }
	
	protected virtual void FixedUpdate()
	{
		if(CanMove())
			Move();
		_previousVY = _rigidbody2D.VelocityY();
	}
	
	private static AliveState SwitchDamageStatToAliveState(DamageType damageType)
	{
		return damageType switch
		{
			DamageType.Fall => AliveState.DeadFall,
			DamageType.Melee => AliveState.DeadMelee,
			DamageType.Projectile => AliveState.DeadShot,
			DamageType.Explosion => AliveState.DeadGibs,
			DamageType.Acid => AliveState.DeadAcid,
			DamageType.Fire => AliveState.DeadBurnt,
			_ => AliveState.Alive
		};
	}

	private void Kill(DamageType damageType)
	{
		_aliveState = SwitchDamageStatToAliveState(damageType);
		_movementState = MovementState.Dead;
		weapon.DisableShooting();
	}
	
	// This should only really be called from HitBox components or AOE objects
	public virtual void Damage(float damage, DamageType damageType)
	{
		if (_aliveState is not (AliveState.Alive or AliveState.Wounded)) return;
		
		_currentHealth -= damage;
		if (_currentHealth <= 0f)
		{
			Kill(damageType);
		}
		else if(_currentHealth < (1f/_startingHealth) * _woundedHealthLevel)
		{
			_aliveState = AliveState.Wounded;
		}
	}
	
	public virtual void Heal(float health)
	{
		if (_aliveState is AliveState.Alive or AliveState.Wounded)
		{
			_currentHealth += health;
		}
	}

	public bool InCover()
	{
		return _movementState == MovementState.Cover;
	}
	
	private void Flip()
	{
		// Switch the way the player is labelled as facing.
		_facingRight = !_facingRight;

		// Multiply the player's x local scale by -1.
		var currentTransform = transform;
		Vector2 theScale = currentTransform.localScale;
		theScale.x *= -1;
		currentTransform.localScale = theScale;
	}

	protected bool Flipped()
	{
		return !_facingRight;
	}

}
