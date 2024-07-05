using System;
using System.Collections;
using System.Collections.Generic;
using Character;
using Character.Player;
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
	ProjectileHead,
	Explosion,
	Fire,
	Acid
}

public class CharacterBase : MonoBehaviour
{
	[Header("Movement")]
	[SerializeField] private protected float _moveSpeed = 10f;
	[SerializeField] private protected float _movementSmoothing = 0.05f;
	[SerializeField] private protected float _maxAcceleration = 35f;
	[SerializeField] private protected float _maxAirAcceleration = 20f;
	[SerializeField, Range(0,1)] private protected float _woundedSpeed = 0.66f;
	[SerializeField] private protected float _teleportSpeed = 1f;
	[SerializeField] private protected float _stunnedTime = 3f;
	
	
	[Space]
	[SerializeField] private protected float _jumpForce = 1250f;
	[SerializeField] private protected float _gravityMultiplier = 1f;
	
	[Space]
	[SerializeField] private protected float _slideFriction = 15f;
	[SerializeField] private protected float _slideBoost = 300f;
	//[SerializeField] private protected float _slideCooldown = 1f;
	
	[Space]
	[SerializeField] private protected float _fallAutoSlideVelocity = 20f;
	[SerializeField] private protected float _fallDeathVelocity = 30f;
	
	[Space]
	[Header("Ground")]
	[SerializeField] private protected float _groundCheckDistance = 0.25f;
	[SerializeField] private protected Transform _groundCheckPivot;
	[SerializeField] private protected LayerMask _groundCheckLayers;
	[SerializeField] private protected float _groundCheckPivotRadius = 0.5f;

	[Space] 
	[Header("Art")]
	[SerializeField] private protected Transform _spriteObject;
	private protected Rigidbody2D _rigidbody2D;
	private protected Collider2D _collider2D;
	private protected SpriteRenderer _spriteRenderer;
	private protected Animator _spriteAnimCtrl;
	[SerializeField] private protected Color _transitionalColorTint;
	private readonly int _baseColor = Shader.PropertyToID("_BaseColor");
	
	[Space] 
	[Header("Combat")]
	[SerializeField] private protected float _startingHealth = 100f;
	[SerializeField, Range(0,1)] private protected float _woundedHealthLevel = 0.33f;
	[SerializeField] private protected float _rehealCooldown = 3f;
	[SerializeField] private protected float _rehealSpeed = 20f;
	[SerializeField] private protected float _shootFromCoverExposedTime = 1f;
	public GameObject weaponPrefab;
	[SerializeField] private protected Transform _weaponPosition;
	[SerializeField] private protected Transform _weaponSpritePosition;
	[SerializeField] private Allegiance _allegiance;
	protected WeaponBase _weaponInstance;
	[Space]

	[Header("Events")]
	public UnityEvent onLandEvent;

	protected bool _canTeleport;
	protected Vector2 _teleportLocation;

	protected float _currentHealth;
	private protected float _lastDamageCounter;
	private protected Vector2 _previousVelocity;
	RaycastHit2D[] _results;
	private HitBox[] _hitBoxes;
	private Coroutine _shootFromCoverCO;
	private Coroutine _downedTimerCO;
	private GameObject _coverGameObject;
	
	internal enum MovementState
	{
		Walk,
		Slide,
		Cover,
		Jump,
		Dead,
		Immobile,
		Teleporting
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
		DeadAcid,
		DeadHeadShot
	}
	
	private protected MovementState _movementState;
	private protected AliveState _aliveState;
	private protected RuntimeAnimatorController _animationController;
	private protected Animator _animator;

	protected internal virtual bool IsInCover()
	{
		return _movementState == MovementState.Cover && (_aliveState is AliveState.Alive or AliveState.Wounded);
	}
	
	protected virtual void Awake()
	{
		StartUpChecks();
		_results = new RaycastHit2D[1];
	}

	protected void SetRigidbodyX(float xValue)
	{
		var velocity = _rigidbody2D.velocity;
		velocity.x = xValue;
		_rigidbody2D.velocity = velocity;
	}
	
	protected void SetRigidbodyY(float yValue)
	{
		var velocity = _rigidbody2D.velocity;
		velocity.x = yValue;
		_rigidbody2D.velocity = velocity;
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
		else
		{
			_animator = _spriteObject.GetComponent<Animator>();
			if (_animator != null)
			{
				_animationController = _animator.runtimeAnimatorController;
				if (_animationController == null)
				{
					Debug.LogError("Animation Controller missing from _spriteObject's Animator!");
					enabled = false;
					return;
				}
			}
			else
			{
				Debug.LogError("Animator missing from _spriteObject!");
				enabled = false;
				return;
			}
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

		_hitBoxes = GetComponentsInChildren<HitBox>();

		if (weaponPrefab != null)
		{
			var weaponGameObject = Instantiate(weaponPrefab.gameObject, _weaponPosition.position, _weaponPosition.rotation);
			weaponGameObject.transform.parent = _weaponPosition;
			_weaponInstance = weaponGameObject.GetComponent<WeaponBase>();
			_weaponInstance.allegiance = _allegiance;
			_weaponInstance.Setup(_weaponSpritePosition);
		}

	}
	
	protected void SetRigidbody2DVelocityX(float x)
	{
		_rigidbody2D.velocity = new Vector2(x, _rigidbody2D.VelocityY());
	}

	protected virtual void HitCover()
	{
		Debug.Log(gameObject.name + " hit cover");
		_movementState = MovementState.Cover;
		SetRigidbody2DVelocityX(0f);
		_spriteRenderer.color = _transitionalColorTint;
		foreach (var hitBox in _hitBoxes)
		{
			hitBox.gameObject.SetActive(false);
		}
		
	}
	
	protected virtual void HitTeleporter(Collider2D other)
	{
		var teleporter = other.GetComponent<Teleporter>();
		if (teleporter != null && teleporter.teleportLocation != null)
		{
			_canTeleport = true;
			_teleportLocation = teleporter.teleportLocation.position;
		}
	}
	
		
	
	protected bool CheckHitCharacter(Collision2D other, ref CharacterBase character)
	{
		return CheckHitCharacter(other.gameObject, ref character);
	}
	
	protected bool CheckHitCharacter(Collider2D other, ref CharacterBase character)
	{
		return CheckHitCharacter(other.gameObject, ref character);
	}
	
	protected bool CheckHitCharacter(GameObject other, ref CharacterBase character)
	{
		var player = LayerMask.NameToLayer("PlayerTrigger");
		var comparisonLayer = other.gameObject.layer;
		
		if (comparisonLayer != player) return false;
		
		character = other.transform.parent.GetComponent<CharacterBase>();
		return true;

	}

	IEnumerator DownedTimerCO()
	{
		_weaponInstance.SetShooting(false);
		_spriteRenderer.color = Color.white;
		yield return new WaitForSeconds(_stunnedTime);
		_downedTimerCO = null;
		if (_movementState is MovementState.Immobile && _aliveState is AliveState.Alive or AliveState.Wounded)
		{
			_movementState = MovementState.Walk;
			_weaponInstance.SetShooting(true);
		}
	}
	
	protected virtual void HitCharacter(CharacterBase character)
	{
		// stun!
		Debug.Log("Character = "+character);
		if(character == null || character._movementState != MovementState.Slide)
			return;
		_movementState = MovementState.Immobile;
		SetRigidbody2DVelocityX(0f);
		_downedTimerCO = StartCoroutine(DownedTimerCO());
	}

	protected bool CheckHitCover(Collider2D other)
	{
		var objectCheck = other.gameObject.layer == LayerMask.NameToLayer("Cover") &&
		                  _movementState == MovementState.Slide;
		if (objectCheck && _coverGameObject != other.gameObject)
		{
			_coverGameObject = other.gameObject;
			return true;
		}

		return false;
	}
	
	protected bool CheckLeaveCover(Collider2D other)
	{
		return other.gameObject.layer == LayerMask.NameToLayer("Cover");
	}
	
	protected bool CheckHitTeleporter(Collider2D other)
	{
		return other.gameObject.layer == LayerMask.NameToLayer("Teleport");
	}
	
	protected void StartSlide()
	{
		_movementState = MovementState.Slide;
		float direction = _spriteRenderer.flipX ? -1 : 1;
		SetRigidbody2DVelocityX(_slideBoost * _rigidbody2D.mass * direction);
	}
	
	protected bool CheckFallDamage()
	{
		if (_aliveState is AliveState.Alive or AliveState.Wounded)
		{
			// Death from fall?
			if (_previousVelocity.y > -_fallDeathVelocity) return false;
		
			_rigidbody2D.SetVelocityY(0f);
			Kill(DamageType.Fall);
		}
		return true;
	}

	// Todo: move to hitbox code? ALl damage is from hitboxes only?
	private void OnCollisionEnter2D(Collision2D other)
	{
		if (CheckFallDamage()) return;

		if (_previousVelocity.y < -_fallAutoSlideVelocity)
		{
			StartSlide();
		}
		
		CharacterBase characterHit = null;
		if (CheckHitCharacter(other, ref characterHit)) HitCharacter(characterHit);
	}

	private void OnCollisionExit2D(Collision2D other)
	{

	}
	protected void LeaveTeleporter()
	{
		_canTeleport = false;
	}

	public void ShootFromCover()
	{
		if (_movementState != MovementState.Cover)
		{
			return;
		}

		if (_shootFromCoverCO != null)
		{
			StopCoroutine(_shootFromCoverCO);
		}
		
		_shootFromCoverCO = StartCoroutine(ShootFromCoverCO());
	}

	IEnumerator ShootFromCoverCO()
	{
		foreach (var hitbox in _hitBoxes)
		{
			hitbox.gameObject.SetActive(true);
		}

		_spriteRenderer.color = Color.white;
		yield return new WaitForSeconds(_shootFromCoverExposedTime);
		foreach (var hitbox in _hitBoxes)
		{
			if (_movementState == MovementState.Cover)
			{
				hitbox.gameObject.SetActive(false);
			}
		}
		_spriteRenderer.color = _transitionalColorTint;
		_shootFromCoverCO = null;
	}

	protected void LeaveCover()
	{
		if (_movementState == MovementState.Cover)
		{
			foreach (var hitbox in _hitBoxes)
			{
				hitbox.gameObject.SetActive(true);
			}
			_movementState = MovementState.Walk;
			if (_shootFromCoverCO != null)
			{
				StopCoroutine(_shootFromCoverCO);
			}
		}
		_coverGameObject = null;
		_spriteRenderer.color = Color.white;
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (CheckLeaveCover(other))
		{
			LeaveCover();
			Debug.Log(gameObject.name + " left cover");
			return;
		}
		if (CheckHitTeleporter(other)) LeaveTeleporter();
	}
	
	private void OnTriggerStay2D(Collider2D other)
	{
		if (CheckHitTeleporter(other)) HitTeleporter(other);
	}
	
	private void OnTriggerEnter2D(Collider2D other)
	{
		if (CheckHitCover(other)) HitCover();
		if (CheckHitTeleporter(other)) HitTeleporter(other);
		CharacterBase characterHit = null;
		if (CheckHitCharacter(other, ref characterHit)) HitCharacter(characterHit);
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
		
		// Add extra gravity
		_rigidbody2D.AddForce((_gravityMultiplier - 1) * -9.81f * Vector2.up);
		_previousVelocity = _rigidbody2D.velocity;
		var position = _rigidbody2D.position;
		
		_spriteObject.position = new Vector2(position.x, position.y - _collider2D.bounds.size.y / 2f);//_rigidbody2D.position - vectorOffset;
		UpdateSprite();

		if (_weaponInstance != null)
		{
			_weaponInstance.transform.localScale = _spriteRenderer.flipX ? new Vector2(-1,1) : new Vector2(1,1);
		}
		
		//_spriteRenderer.color = _movementState is MovementState.Cover or MovementState.Teleporting ? _transitionalColorTint : Color.white;
	}

	private bool GroundCheckNonAlloc(Vector2 position, Vector2 direction, float maxDistance, LayerMask layerMask)
	{
		int hits = Physics2D.RaycastNonAlloc(position, direction, _results, maxDistance, layerMask);
		return hits > 0;
	}

	protected virtual bool IsGrounded()
	{
		var position3D = _groundCheckPivot.position;
		var position2D = new Vector2(position3D.x, position3D.y);
		
		var positionLeft = position2D - _groundCheckPivotRadius * Vector2.right;
		var positionRight = position2D + _groundCheckPivotRadius * Vector2.right;

		bool left = GroundCheckNonAlloc(positionLeft, Vector2.down, _groundCheckDistance, _groundCheckLayers);
		bool center = GroundCheckNonAlloc(position2D, Vector2.down, _groundCheckDistance, _groundCheckLayers);
		bool right = GroundCheckNonAlloc(positionRight, Vector2.down, _groundCheckDistance, _groundCheckLayers);
		
		Debug.DrawRay(positionLeft, Vector2.down * _groundCheckDistance, left ? Color.red : Color.magenta);
		Debug.DrawRay(position2D, Vector2.down * _groundCheckDistance, center ? Color.green : Color.yellow);
		Debug.DrawRay(positionRight, Vector2.down * _groundCheckDistance, right? Color.blue : Color.cyan);

		return left || right || center;
	}
	
	private static AliveState SwitchDamageStatToAliveState(DamageType damageType)
	{
		return damageType switch
		{
			DamageType.Fall => AliveState.DeadFall,
			DamageType.Melee => AliveState.DeadMelee,
			DamageType.Projectile => AliveState.DeadShot,
			DamageType.ProjectileHead => AliveState.DeadHeadShot,
			DamageType.Explosion => AliveState.DeadGibs,
			DamageType.Acid => AliveState.DeadAcid,
			DamageType.Fire => AliveState.DeadBurnt,
			_ => AliveState.Alive
		};
	}

	protected void MoveMechanics(bool isGrounded, float input, float slideToJumpMaxVx = -1f)
	{
		input = input * (_aliveState == AliveState.Wounded ? _woundedSpeed : 1f);
		
		var velocity = _rigidbody2D.velocity;
		var acceleration = isGrounded ? _maxAcceleration : _maxAirAcceleration;
		var maxSpeedDelta = acceleration * Time.fixedDeltaTime;
		velocity.x = Mathf.MoveTowards(velocity.x, _moveSpeed * input, maxSpeedDelta);

		if (slideToJumpMaxVx >= 0f)
		{
			var min = Mathf.Min(slideToJumpMaxVx, -slideToJumpMaxVx);
			var max = Mathf.Max(slideToJumpMaxVx, -slideToJumpMaxVx);
			velocity.x = Mathf.Clamp(velocity.x, min, max);
		}
		_rigidbody2D.velocity = velocity;
		
	}
	
    
	private void Kill(DamageType damageType)
	{
		_aliveState = SwitchDamageStatToAliveState(damageType);
		_movementState = MovementState.Dead;
		if(_weaponInstance != null)
			_weaponInstance.DisableShooting();
		Debug.Log(name + " died, due to "+damageType);
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

	private Coroutine _teleportOut, _teleportIn;

	protected void Teleport(Vector2 newLocation)
	{
		if (_teleportIn != null || _teleportOut != null)
		{
			return;
		}

		_teleportOut = StartCoroutine(TeleportOutCo(newLocation, _teleportSpeed / 2f));

	}

	IEnumerator TeleportOutCo(Vector2 newLocation, float time)
	{
		_movementState = MovementState.Teleporting;
		float elapsedTime = 0f;
		while (elapsedTime < time)
		{
			var alpha = 1f / time * elapsedTime;
			elapsedTime += Time.deltaTime;
			_rigidbody2D.velocity = new Vector2(0f, _rigidbody2D.VelocityY());
			_spriteRenderer.color = Color.Lerp(Color.white, _transitionalColorTint, alpha);
			yield return null;
		}

		_rigidbody2D.position = newLocation;
		if (_teleportIn != null)
		{
			StopCoroutine(_teleportIn);
		}

		_teleportOut = null;
		_teleportIn = StartCoroutine(TeleportInCo(newLocation, time));
	}
	
	IEnumerator TeleportInCo(Vector2 newLocation, float time)
	{
		float elapsedTime = 0f;
		while (elapsedTime < time)
		{
			var alpha = 1f / time * elapsedTime;
			elapsedTime += Time.deltaTime;
			_rigidbody2D.velocity = new Vector2(0f, _rigidbody2D.VelocityY());
			_spriteRenderer.color = Color.Lerp(_transitionalColorTint, Color.white, alpha);
			yield return null;
		}
		if(_movementState != MovementState.Dead)
			_movementState = MovementState.Walk;
		_teleportIn = null;
	}
	
	
	protected virtual void Jump()
	{
		_rigidbody2D.AddForce(_jumpForce * Vector2.up);
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
		foreach (var hitbox in _hitBoxes)
		{
			hitbox.gameObject.SetActive(false);
		}
		return _movementState == MovementState.Cover;
	}

	protected void UpdateSpriteState(string animationName, bool tint = false)
	{
		_animator.Play(animationName);
	}
	
	protected void UpdateDeadSprite()
	{
		switch (_aliveState)
		{
			case AliveState.Alive : return;
			case AliveState.DeadFall    : UpdateSpriteState("Dead_Fall");
				break;
			case AliveState.DeadShot    : UpdateSpriteState("Dead_Shot");
				break;
			case AliveState.DeadAcid    : UpdateSpriteState("Dead_Shot");
				break;
			case AliveState.DeadBurnt   : UpdateSpriteState("Dead_Shot");
				break;
			case AliveState.DeadMelee   : UpdateSpriteState("Dead_Shot");
				break;
			case AliveState.DeadGibs    : UpdateSpriteState("Dead_Shot");
				break;
			case AliveState.DeadHeadShot: UpdateSpriteState("Dead_Headshot");
				break;
		}
	}
	
	protected virtual void UpdateSprite()
	{
		var vX = _rigidbody2D.velocity.x;
		_spriteRenderer.flipX = vX switch
		{
			< 0f => true,
			> 0f => false,
			_ => _spriteRenderer.flipX
		};
		
		switch (_movementState)
		{
			case MovementState.Walk            : UpdateSpriteState("Idle");
				break;
			case MovementState.Cover           : UpdateSpriteState("Idle");
				break;
			case MovementState.Immobile        : UpdateSpriteState("Stunned_Floor");
				break;
			case MovementState.Slide           : UpdateSpriteState("Slide");
				break;
			case MovementState.Jump            : UpdateSpriteState("Jump");
				break;
			case MovementState.Dead            : UpdateDeadSprite(); 
				break;
			case MovementState.Teleporting     : UpdateSpriteState("Idle");
				break;
		}
	}

}
