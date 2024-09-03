using System;
using System.Collections;
using System.Collections.Generic;
using Character;
using Character.NPC;
using Character.Player;
using Combat.Weapon;
using Environment;
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
	[FormerlySerializedAs("_rotationZoneLayers")] [SerializeField] private protected LayerMask _rotationZoneLayer;
	[SerializeField] private protected float _groundCheckPivotRadius = 0.5f;

	[Space] 
	[Header("Art")]
	public Transform _spriteObject;
	private protected Rigidbody _rigidbody;
	private protected Collider _collider;
	public SpriteRenderer _spriteRenderer;
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
	protected Vector3 _teleportLocation;

	protected float _currentHealth;
	private protected float _lastDamageCounter;
	private protected Vector3 _previousVelocity;
	private protected RaycastHit2D[] _results2D;
	private protected RaycastHit[] _results;
	private HitBox[] _hitBoxes;
	private Coroutine _shootFromCoverCO;
	private Coroutine _downedTimerCO;
	private GameObject _coverGameObject;
	public Vector3 previousPosition;
	private MovementState _preRotationMovementState;
	protected TowerCorner _activeCorner;
	public Vector3 worldPosition;
	protected NPCMovementLine movementLine;

	
	internal enum MovementState
	{
		Walk,
		Slide,
		Cover,
		Jump,
		Dead,
		Immobile,
		Teleporting,
		Rotating
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
	private protected List<NPCCharacter> _meleeTargets;

	protected internal virtual bool IsInCover()
	{
		return _movementState == MovementState.Cover && (_aliveState is AliveState.Alive or AliveState.Wounded);
	}
	
	public void InTowerCorner(TowerCorner currentCorner)
	{
		_activeCorner = currentCorner;
		//Debug.Log(gameObject.name+" added "+_activeCorner.towerCorner +" to active!");
	}
	
	public void OutTowerCorner()
	{
		//Debug.Log(gameObject.name+" removed "+_activeCorner.towerCorner +" from active!");
		_activeCorner = null;
	}
	
	protected virtual void Awake()
	{
		StartUpChecks();
		_results2D = new RaycastHit2D[1];
	}

	protected void SetRigidbodyX(float xValue)
	{
		var velocity = _rigidbody.velocity;
		velocity.x = xValue;
		_rigidbody.velocity = velocity;
	}
	
	protected void SetRigidbodyY(float yValue)
	{
		var velocity = _rigidbody.velocity;
		velocity.x = yValue;
		_rigidbody.velocity = velocity;
	}

	protected void MeleeAttack()
	{
		// if we're inside an NPC's melee attack trigger volume
		// do damage
		// TODO: play animation
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
		
		_rigidbody = GetComponent<Rigidbody>();
		if (_spriteRenderer == null)
		{
			_spriteRenderer = _spriteObject.GetComponent<SpriteRenderer>();
		}
		_spriteAnimCtrl = _spriteRenderer.GetComponent<Animator>();
		_collider = GetComponent<Collider>();
		

		if (!_rigidbody)
		{
			Debug.LogError("Rigidbody missing from PlayerMovement!");
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
			_animator = _spriteRenderer.GetComponent<Animator>();
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

		if (!_collider)
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

		_meleeTargets = new List<NPCCharacter>();

	}
	
	protected void SetRigidbody2DVelocityX(float x)
	{
		_rigidbody.velocity = new Vector2(x, _rigidbody.VelocityY());
	}
	
	protected void SetRigidbodyVelocityX(float x)
	{
		_rigidbody.velocity = new Vector3(x, _rigidbody.VelocityY(), _rigidbody.VelocityZ());
	}
	protected void SetRigidbodyVelocityY(float y)
	{
		_rigidbody.velocity = new Vector3(_rigidbody.VelocityX(), y, _rigidbody.VelocityZ());
	}
	protected void SetRigidbodyVelocityZ(float z)
	{
		_rigidbody.velocity = new Vector3(_rigidbody.VelocityX(), _rigidbody.VelocityY(), z);
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

	protected virtual void HitTeleporter(Collider other)
	{
		var teleporter = other.GetComponent<Teleporter>();
		if (teleporter != null && teleporter.teleportLocation != null)
		{
			_canTeleport = true;
			_teleportLocation = teleporter.teleportLocation.position;
		}
	}
	
	protected void TryMelee()
	{
		NPCCharacter nearestValidTargetComponent = null;
		GameObject nearestValidTargetGameObject = null;
		
		foreach (var meleeTarget in _meleeTargets)
		{
			if (meleeTarget == null) continue;

			if (meleeTarget._movementState != MovementState.Immobile ||
			    meleeTarget._aliveState is not (AliveState.Alive or AliveState.Wounded)) continue;
			
			if (nearestValidTargetGameObject != null)
			{
				var distanceA = Vector2.Distance(nearestValidTargetGameObject.transform.position,
					_rigidbody.transform.position);
				var distanceB = Vector2.Distance(meleeTarget.gameObject.transform.position,
					_rigidbody.transform.position);
				
				if (!(distanceB < distanceA)) continue;
				
				nearestValidTargetGameObject = meleeTarget.gameObject;
				nearestValidTargetComponent = meleeTarget;
			}
			else
			{
				nearestValidTargetGameObject = meleeTarget.gameObject;
				nearestValidTargetComponent = meleeTarget;
			}
		}

		if (nearestValidTargetComponent != null)
		{
			nearestValidTargetComponent.Damage(10000f, DamageType.Melee);
		}
	}
	
	protected virtual void HitMeleeZone(Collider2D other)
	{
		var meleeZone = other.GetComponent<MeleeZone>();
		if (meleeZone == null || meleeZone.npcCharacter == null)
		{
			return;
		}

		NPCCharacter npcCharacter = meleeZone.npcCharacter;

		if (!_meleeTargets.Contains(npcCharacter))
		{
			_meleeTargets.Add(npcCharacter);
		}
	}
	
	protected virtual void HitMeleeZone(Collider other)
	{
		var meleeZone = other.GetComponent<MeleeZone>();
		if (meleeZone == null || meleeZone.npcCharacter == null)
		{
			return;
		}

		NPCCharacter npcCharacter = meleeZone.npcCharacter;

		if (!_meleeTargets.Contains(npcCharacter))
		{
			_meleeTargets.Add(npcCharacter);
		}
	}
		
	protected bool CheckHitCharacter(Collision other, ref CharacterBase character)
	{
		return CheckHitCharacter(other.gameObject, ref character);
	}
	
	protected bool CheckHitCharacter(Collider other, ref CharacterBase character)
	{
		return CheckHitCharacter(other.gameObject, ref character);
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
	
	protected bool CheckHitCover(Collider other)
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
	
	protected bool CheckLeaveCover(Collider other)
	{
		return other.gameObject.layer == LayerMask.NameToLayer("Cover");
	}
	
	protected bool CheckHitTeleporter(Collider other)
	{
		return other.gameObject.layer == LayerMask.NameToLayer("Teleport");
	}
	
	protected bool CheckHitMeleeZone(Collider other)
	{
		return other.gameObject.layer == LayerMask.NameToLayer("MeleeZone");
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
	
	protected bool CheckHitMeleeZone(Collider2D other)
	{
		return other.gameObject.layer == LayerMask.NameToLayer("MeleeZone");
	}
	
	protected void StartSlide()
	{
		_movementState = MovementState.Slide;
		float direction = _spriteRenderer.flipX ? 1 : -1;
		var velocity = _rigidbody.velocity;
		velocity = transform.right * (_slideBoost * _rigidbody.mass * direction);
		velocity.y = _rigidbody.velocity.y;
		_rigidbody.velocity = velocity;
		
		//SetRigidbody2DVelocityX(_slideBoost * _rigidbody.mass * direction);
	}
	
	protected bool CheckFallDamage()
	{
		if (_aliveState is AliveState.Alive or AliveState.Wounded)
		{
			// Death from fall?
			if (_previousVelocity.y > -_fallDeathVelocity) return false;
		
			_rigidbody.SetVelocityY(0f);
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
	
	protected void LeaveMeleeZone(Collider2D other)
	{
		var meleeZone = other.GetComponent<MeleeZone>();
		NPCCharacter npcCharacter = meleeZone.npcCharacter;

		if (_meleeTargets.Contains(npcCharacter))
		{
			_meleeTargets.Remove(npcCharacter);
		}
	}

	protected void LeaveMeleeZone(Collider other)
	{
		var meleeZone = other.GetComponent<MeleeZone>();
		NPCCharacter npcCharacter = meleeZone.npcCharacter;

		if (_meleeTargets.Contains(npcCharacter))
		{
			_meleeTargets.Remove(npcCharacter);
		}
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

	private void HitNPCMovementLine(Collider other)
	{
		movementLine = other.GetComponent<NPCMovementLine>();
	}

	private bool CheckHitNPCMovementLine(Collider other)
	{
		return other.gameObject.layer == LayerMask.NameToLayer("MovementLine");
	}
	
	private void OnTriggerExit(Collider other)
	{
		if (CheckLeaveCover(other)) LeaveCover();
		if (CheckHitTeleporter(other)) LeaveTeleporter();
		if (CheckHitMeleeZone(other)) LeaveMeleeZone(other);
	}
	
	private void OnTriggerStay(Collider other)
	{
		if (CheckHitTeleporter(other)) HitTeleporter(other);
		if (CheckHitMeleeZone(other)) HitMeleeZone(other);
		if (CheckHitNPCMovementLine(other)) HitNPCMovementLine(other);
	}
	
	private void OnTriggerEnter(Collider other)
	{
		if (CheckHitCover(other)) HitCover();
		if (CheckHitTeleporter(other)) HitTeleporter(other);
		CharacterBase characterHit = null;
		if (CheckHitCharacter(other, ref characterHit)) HitCharacter(characterHit);
	}
	
	private void OnTriggerExit2D(Collider2D other)
	{
		if (CheckLeaveCover(other)) LeaveCover();
		if (CheckHitTeleporter(other)) LeaveTeleporter();
		if (CheckHitMeleeZone(other)) LeaveMeleeZone(other);
	}
	
	private void OnTriggerStay2D(Collider2D other)
	{
		if (CheckHitTeleporter(other)) HitTeleporter(other);
		if (CheckHitMeleeZone(other)) HitMeleeZone(other);
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
	protected virtual void Move()
	{
		worldPosition = _rigidbody.position;
	}
	
	protected virtual bool CanMove()
	{
		return _movementState is not (MovementState.Dead or MovementState.Immobile or MovementState.Rotating);
	}
	
	protected virtual void Update() { }

	
	protected void DrawRightDirection(Transform tf, float lineSize, float length, Color color)
	{
		Debug.DrawLine(tf.position + (tf.right *lineSize) + (tf.forward * lineSize), tf.position + tf.right * length, color);
		Debug.DrawLine(tf.position + (tf.right * lineSize) - (tf.forward * lineSize), tf.position + tf.right * length, color);
		Debug.DrawLine(tf.position + (tf.right * lineSize) + (tf.up * lineSize), tf.position + tf.right *length, color);
		Debug.DrawLine(tf.position + (tf.right * lineSize) - (tf.up * lineSize), tf.position + tf.right * length, color);
		Debug.DrawLine(tf.position, tf.position + tf.right * 2f, color);
	}
	protected virtual void FixedUpdate()
	{
		DrawRightDirection(transform, 0.8f, 3f, Color.white);
		
		if(CanMove())
			Move();
		
		// Add extra gravity
		_rigidbody.AddForce((_gravityMultiplier - 1) * -9.81f * Vector3.up);
		_previousVelocity = _rigidbody.velocity;
		var position = _rigidbody.position;
		
		// I _could_ make it use Rigidbody3D - but that is quite a chunk of work
		// as the z-position is irrelevant for the purposes of movement (as we only truly move on a 2D plane during combat)
		// we'll stick with this for now
		_spriteObject.position = new Vector3(position.x, position.y - _collider.bounds.size.y / 2f, transform.position.z);//_rigidbody2D.position - vectorOffset;
		UpdateSprite();

		if (_weaponInstance != null)
		{
			//_weaponInstance.transform.localScale = _spriteRenderer.flipX ? new Vector2(-1,1) : new Vector2(1,1);
		}

		previousPosition = _rigidbody.position;
	}

	public virtual void BeginRotation()
	{

	}

	public virtual void Rotation()
	{

	}
        
	public virtual void EndRotation()
	{

	}
	
	private bool GroundCheckNonAlloc(Vector2 position, Vector2 direction, float maxDistance, LayerMask layerMask)
	{
		int hits = Physics2D.RaycastNonAlloc(position, direction, _results2D, maxDistance, layerMask);
		return hits > 0;
	}

	protected virtual bool IsGrounded()
	{
		var position3D = _groundCheckPivot.position;
		
		var positionLeft = position3D - _groundCheckPivotRadius * transform.right;
		var positionRight = position3D + _groundCheckPivotRadius * transform.right;

		bool left = GroundCheckNonAlloc(positionLeft, Vector3.down, _groundCheckDistance, _groundCheckLayers);
		bool center = GroundCheckNonAlloc(position3D, Vector3.down, _groundCheckDistance, _groundCheckLayers);
		bool right = GroundCheckNonAlloc(positionRight, Vector3.down, _groundCheckDistance, _groundCheckLayers);
		
		Debug.DrawRay(positionLeft, Vector3.down * _groundCheckDistance, left ? Color.red : Color.magenta);
		Debug.DrawRay(position3D, Vector3.down * _groundCheckDistance, center ? Color.green : Color.yellow);
		Debug.DrawRay(positionRight, Vector3.down * _groundCheckDistance, right? Color.blue : Color.cyan);

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
		
		var velocity = _rigidbody.velocity;
            
		var acceleration = isGrounded ? _maxAcceleration : _maxAirAcceleration;
		var maxSpeedDelta = acceleration * Time.fixedDeltaTime;
		var targetVelocity = transform.right * (input * _moveSpeed);

		float dp = Vector3.Dot(transform.right, velocity);

		if (dp > 0.95f)
		{
			velocity = new Vector3(
				Mathf.MoveTowards(velocity.x, targetVelocity.x, maxSpeedDelta),
				velocity.y,
				Mathf.MoveTowards(velocity.z, targetVelocity.z, maxSpeedDelta)
			);
		}
		else
		{
			velocity = new Vector3(targetVelocity.x, velocity.y, targetVelocity.z);
		}
            
		_rigidbody.velocity = velocity;
	}
	
    
	private void Kill(DamageType damageType)
	{
		_aliveState = SwitchDamageStatToAliveState(damageType);
		_movementState = MovementState.Dead;
		if(_weaponInstance != null)
			_weaponInstance.DisableShooting();
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

	protected void Teleport(Vector3 newLocation)
	{
		if (_teleportIn != null || _teleportOut != null)
		{
			return;
		}

		_teleportOut = StartCoroutine(TeleportOutCo(newLocation, _teleportSpeed / 2f));

	}

	IEnumerator TeleportOutCo(Vector3 newLocation, float time)
	{
		_movementState = MovementState.Teleporting;
		float elapsedTime = 0f;
		while (elapsedTime < time)
		{
			var alpha = 1f / time * elapsedTime;
			elapsedTime += Time.deltaTime;
			_rigidbody.velocity = new Vector3(0f, _rigidbody.VelocityY(), 0f);
			_spriteRenderer.color = Color.Lerp(Color.white, _transitionalColorTint, alpha);
			yield return null;
		}

		_rigidbody.position = newLocation;
		
		if (_teleportIn != null)
		{
			StopCoroutine(_teleportIn);
		}

		_teleportOut = null;
		_teleportIn = StartCoroutine(TeleportInCo(time));
	}
	
	IEnumerator TeleportInCo(float time)
	{
		
		float elapsedTime = 0f;
		// Wait for player movement to update before we try and snap to new line
		yield return new WaitForFixedUpdate();
		_rigidbody.position = movementLine.GetClosestPointOnLine(_rigidbody.position);
		while (elapsedTime < time)
		{
			var alpha = 1f / time * elapsedTime;
			elapsedTime += Time.deltaTime;
			_rigidbody.velocity = new Vector3(0f, _rigidbody.VelocityY(), 0f);
			_spriteRenderer.color = Color.Lerp(_transitionalColorTint, Color.white, alpha);
			yield return null;
		}
		if(_movementState != MovementState.Dead)
			_movementState = MovementState.Walk;
		// snap to line
		
		_teleportIn = null;
	}
	
	
	protected virtual void Jump()
	{
		_rigidbody.AddForce(_jumpForce * Vector3.up);
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
