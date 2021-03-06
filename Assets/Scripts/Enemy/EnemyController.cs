﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class EnemyController : EnemyStateMachineMonoBehaviour, IDamageable
{
    public static Action<Vector3> OnEnemyDeath;
    public static Pathfinding<PathfindingNode> Pathfinder { get; set; }

    #region Variables

    [Header("Enemy")]
    [SerializeField]
    private CharacterStatsSO _stats;
    [SerializeField]
    private float _movementSpeed, _rotationSpeed, _angularSpeed;
    [SerializeField]
    private GameObject _healthBarGO;

    [Header("Target detection")]
    [SerializeField]
    private float _detectionRange;
    [SerializeField]
    private float _detectionAngle;
    [SerializeField]
    private Vector3 _firstRaycastOffset, _secondRaycastOffset;
    [SerializeField]
    private LayerMask _environmentLayer;

    [Header("RayCast")]
    [SerializeField]
    private LayerMask _groundLayer;
    [SerializeField]
    private float _sphereRadius, _groundOffset, _rayOverhead;

    [Header("Collisions"), SerializeField]
    private LayerMask _collisionLayer;
    [SerializeField]
    private float _raycastLength;

    [Header("Combat")]
    [SerializeField]
    private float _attackRange;
    [SerializeField, Range(0f, 1f)]
    private float _secondaryAttackRangeMultiplier, _attackInitiationPercentage;
    [SerializeField]
    private float _secondaryAttackAngleInDegrees, _delayBeforeAttack, _delayAfterAttack, _attackAngleInDegrees;

    [Header("Pathfinding")]
    [SerializeField]
    private float _moveDistanceTolerance;
    [SerializeField]
    private float _pathfindingRefreshInterval, _yPathfindingRayOffset;
    [SerializeField]
    Vector2Int _randomMovementCycles, _randomMovementLength;

    [Header("Animaton")]
    [SerializeField]
    private Animator _animator;

    [Header("Other"), SerializeField]
    Vector2 _waitTime;

    // Objects
    private Image _healthBar;
    private Canvas _healthBarCanvas;
    private Camera _mainCamera;
    private Transform _target;
    private List<Vector3> _path;
    private bool _seenTarget;
    private Vector3[] _raycastDirections;
    private RaycastHit _hitInfo;

    private float _currentHealth;
    private bool _grounded;
    private float _timeSinceGrounded, _currentGravity;

    #endregion

    // Inicializace proměnných
    void Start()
    {
        _mainCamera = Camera.main;
        _healthBarCanvas = _healthBarGO.GetComponent<Canvas>();
        _healthBar = _healthBarCanvas.transform.GetChild(0).transform.GetChild(0).GetComponent<Image>();

        _currentHealth = _stats.health;
        _healthBarCanvas.enabled = false;

        _grounded = true;

        _seenTarget = false;
        _target = GameManager.Instance.Player.transform;
    }

    #region Attack Methods

    // Spočítá vzdálenost mezi sebou a hráčem a taky úhel a podle toho mu udělí nebo neudělí poškození
    public void Attack()
    {
        float distance = Vector3.Distance(transform.position, _target.transform.position);

        if (distance <= _attackRange && IsTargetVisible(_attackRange))
        {
            Vector3 attackDirection3D = _target.transform.position - transform.position;

            Vector2 attackDirection = new Vector2(attackDirection3D.x, attackDirection3D.z);
            Vector2 forward = new Vector2(transform.forward.x, transform.forward.z);
            float angle = Vector2.Angle(forward, attackDirection);

            if (angle < _attackAngleInDegrees)
            {
                _target.GetComponent<IDamageable>().TakeDamage(_stats.damage, _stats.armourPenetration);
            }
            else if ((distance <= _attackRange * _secondaryAttackRangeMultiplier) && (angle < _secondaryAttackAngleInDegrees))
            {
                _target.GetComponent<IDamageable>().TakeDamage(_stats.damage, _stats.armourPenetration);
            }
        }
    }

    // Vrací true, když je ráč viditelný a v dosahu
    public bool CanAttack()
    {
        if (IsTargetInRange(_attackRange)) // Is enemy in range to attack?
        {
            if (IsTargetVisible(_attackRange))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Physics Methods

    // Zavolá metody, které by se měly provádět pokaždé řešící např. kolize a gravitaci
    public void PassivePhysics()
    {
        _grounded = IsGrounded();
        CalculateYSpeed();
        ResolveCollisions();
    }

    // Počítá sílu gravitace
    private void CalculateYSpeed()
    {
        if (_grounded)
        {
            _timeSinceGrounded = 0;
        }
        else
        {
            _timeSinceGrounded += Time.fixedDeltaTime;
        }

        transform.position = new Vector3(transform.position.x, transform.position.y + GamePhysics.GetGravitationalForce(_timeSinceGrounded), transform.position.z);
    }

    // Určuje jestli je nepřítel na zemi
    private bool IsGrounded()
    {
        bool grounded = GamePhysics.IsGroundedRayCast(transform.position, _groundOffset, _rayOverhead, _groundLayer, out float yCorrection);
        transform.position = new Vector3(transform.position.x, transform.position.y + yCorrection, transform.position.z);
        return grounded;
    }

    // Řeší kolize
    private void ResolveCollisions()
    {
        _raycastDirections = new Vector3[] { transform.right, -transform.right, transform.forward };
        transform.position += GamePhysics.RaycastCollisionDetection(transform.position, _raycastDirections, _raycastLength, _collisionLayer);
    }

    // Posune nepřátele blíž k cílové pozici a otočí podle směru pohybu
    public float MoveToPositionAndRotate(Vector3 targetPosition)
    {
        float distance;
        Vector3 positionDelta = GamePhysics.MoveTowardsPositionNonYClamped(transform.position, targetPosition, _movementSpeed, out distance);
        transform.position += positionDelta;
        transform.rotation = GamePhysics.RotateTowardsMovementDirection(transform.rotation, positionDelta, _rotationSpeed);
        return distance;
    }

    // Posune nepřátele blíž k cílové pozici a otočí podle směru pohybu
    public float MoveToTargetAndRotate()
    {
        float distance;
        Vector3 positionDelta = GamePhysics.MoveTowardsPositionNonYClamped(transform.position, _target.position, _movementSpeed, out distance);
        transform.position += positionDelta;
        transform.rotation = GamePhysics.RotateTowardsMovementDirection(transform.rotation, positionDelta, _rotationSpeed);
        return distance;
    }

    // Otočí nepřítele o daný počet úhlů
    public void RotateYDegrees(float _degrees)
    {
        transform.Rotate(0, _degrees, 0);
    }

    public void FacePlayer()
    {
        Vector3 lookVector = _target.transform.position - transform.position;
        lookVector.y = 0;
        Quaternion newRotation = Quaternion.LookRotation(lookVector);
        transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, 1);
    }

    #endregion

    #region Detection
    
    // Vrací pravda, když je hráč viditelný a v dosahu útoku
    public bool IsEnemyVisibleAndInAttackRange()
    {
        return (IsTargetVisible(_attackRange) && IsTargetInRange(_attackRange));
    }

    // Vrací pravda, když je hráč viditelný a v dosahu útoku
    public bool IsEnemyVisibleAndInDetectionRange()
    {
        return IsTargetVisible(_detectionRange) && IsTargetInRange(_detectionRange);
    }

    // Kontroluje, jestli je hráč v zorném poli protivníka
    public bool TryToDetectTarget()
    {
        if (IsTargetInRange(_detectionRange))
        {
            if (IsTargetVisible(_detectionRange))
            {
                Vector3 forward = transform.forward;
                Vector3 targetDirection = _target.transform.position - transform.position;
                if (Vector2.Angle(new Vector2(forward.x, forward.z), new Vector2(targetDirection.x, targetDirection.z)) < _detectionAngle)
                {
                    _seenTarget = true;
                    return true;
                }
            }
        }

        return false;
    }

    // Vrací hodnotu, jestli je hráč viditelný
    private bool IsTargetVisible(float range)
    {
        bool result = false;
        if (Physics.Raycast(transform.position, _target.transform.position - transform.position, out _hitInfo, range, _collisionLayer))
        {
            result = _hitInfo.transform.CompareTag("Player");
        }
        return result;
    }

    // Vrátí boolean, jestli je hráč v dosahu
    private bool IsTargetInRange(float range)
    {
        return Vector3.Distance(transform.position, _target.transform.position) < range;
    }

    // Je pathfinding node viditelný?
    public bool IsPathToNextWaypointClear(Vector3 position)
    {
        Vector3 correctedPosition = new Vector3(position.x, position.y + _yPathfindingRayOffset, position.z);
        return !Physics.Raycast(transform.position, correctedPosition - transform.position, Vector3.Distance(transform.position, correctedPosition), _environmentLayer);
    }

    #endregion

    #region State initialization

    // Vrací potřebné hodnoty pro inicializaci stavu Follow Path To Target
    public void GetFPTTInitValues(out float pathfingindRefreshInterval, out float distanceTolerance)
    {
        pathfingindRefreshInterval = _pathfindingRefreshInterval;
        distanceTolerance = _moveDistanceTolerance;
    }

    // Vrací potřebné hodnoty pro inicializaci stavu Wait For Next Action
    public Vector3 GetWFNAInitValues()
    {
        return _waitTime;
    }

    // Vrací potřebné hodnoty pro inicializaci stavu AttackTarget
    public void GetATInitValues(out float delayBeforeAttack, out float delayAfterAttack)
    {
        delayBeforeAttack = _delayBeforeAttack;
        delayAfterAttack = _delayAfterAttack;
    }

    // Vrací potřebné hodnoty pro inicializaci stavu Follow Target
    public float GetFTInitValues()
    {
        return _attackRange * _attackInitiationPercentage;
    }

    // Vrací potřebné hodnoty pro inicializaci stavu Walk To Random Place
    public float GetWTRPInitValues()
    {
        return _moveDistanceTolerance;
    }

    // Vrací potřebné hodnoty pro inicializaci stavu Face Random Direction
    public float GetFRDInitValues()
    {
        return _angularSpeed;
    }

    #endregion

    // Metoda je zavolána, když je nepřítel poražen; Vyvolá akci On Enemy Death
    private void GetDestroyed()
    {
        OnEnemyDeath?.Invoke(transform.position);
        Destroy(gameObject);
    }

    // Otočí Canvas směrem ke kameře
    public void LookAtCameraIfCanvasEnabled()
    {
        if (_healthBarCanvas.enabled)
        {
            LookAtCamera();
        }
    }

    // Otáčí Canvas s životy směrem ke kameře
    private void LookAtCamera()
    {
        _healthBarGO.transform.LookAt(_mainCamera.transform);
        _healthBarGO.transform.Rotate(0, 180, 0);
    }

    // Vrátí Animator
    public Animator GetAnimator()
    {
        return _animator;
    }

    // Vrací cestu k hráči
    public List<Vector3> GetPathToTarget()
    {
        return Pathfinder.GetPath(transform.position, _target.position);
    }

    // Získá a vrátí náhodnou cestu
    public List<Vector3> GetRandomPath()
    {
        return Pathfinder.GetRandomPath(transform.position, UnityEngine.Random.Range(_randomMovementCycles.x, _randomMovementCycles.y), UnityEngine.Random.Range(_randomMovementLength.x, _randomMovementLength.y));
    }

    // Metoda implementovaná interfacem Damageable, dovoluje nepřáteli obdržet poškození
    public void TakeDamage(float damage, float armourPenetration)
    {
        _currentHealth -= DamageCalculator.CalculateDamage(damage, armourPenetration, _stats.armour);
        _healthBar.fillAmount = _currentHealth / _stats.health;

        if (!_seenTarget)
        {
            _seenTarget = true;
            GetComponent<EnemyFSM>().ChangeState(EnemyStateType.FollowTarget);
        }

        if (_currentHealth <= 0)
        {
            _healthBarCanvas.enabled = false;
            GetDestroyed();
        }
        else
        {
            _healthBarCanvas.enabled = true;
        }
    }
}