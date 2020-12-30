﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class EnemyController : EnemyStateMachineMonoBehaviour, IDamageable // TODO combine visual navigation w pathfinding / rework nav; attack as class with interface / get attack component
{
    public static Action<Vector3> OnEnemyDeath;
    public static Pathfinding<PathfindingNode> Pathfinder { get; set; } // TODO do differently?

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
    private float _secondaryAttackRangeMultiplier;
    [SerializeField]
    private float _secondaryAttackAngleInDegrees, _delayBeforeAttack, _delayAfterAttack, _attackAngleInDegrees;

    [Header("Pathfinding")]
    [SerializeField]
    private float _moveDistanceTolerance;
    [SerializeField]
    Vector2Int _randomMovementCycles, _randomMovementLength;

    [Header("Animaton")]
    [SerializeField]
    private Animator animator;

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

    private float _currentHealth;
    private bool _grounded;
    private float _timeSinceGrounded, _currentGravity;

    #endregion

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
        ChangeState(WaitForNextAction());
    }

    void Update()
    {
        if (_healthBarCanvas.enabled)
        {
            LookAtCamera();
        }
    }

    void FixedUpdate()
    {
        _grounded = IsGrounded();
        CalculateYSpeed();
        ResolveCollisions();
    }

    #region State Methods

    private IEnumerator FollowPathToTarget()
    {
        animator.SetBool("Walk", true);

        _path = Pathfinder.GetPath(transform.position, _target.position);

        if (_path == null)
        {
            ChangeState(FollowTarget());
        } else if(_path.Count < 1)
        {
            ChangeState(FollowTarget());
        }

        int index = 0;
        float distance;
        Vector3 positionDelta;

        while (true)
        {
            // TODO check if close to player
            if (CanAttack())
            {
                break;
            }

            positionDelta = GamePhysics.MoveTowardsPositionNonYClamped(transform.position, _path[index], _movementSpeed, out distance);
            transform.rotation = GamePhysics.RotateTowardsMovementDirection(transform.rotation, positionDelta, _rotationSpeed);

            if (distance <= _moveDistanceTolerance)
            {
                index++;

                if (index >= _path.Count || _path[index] == null) // TODO how can it be null
                {
                    break;
                }
            }

            transform.position += positionDelta;
            yield return new WaitForFixedUpdate();
        }

        animator.SetBool("Walk", false);

        ChangeState(FollowTarget());
    }

    private IEnumerator WaitForNextAction()
    {
        float timer = 0;
        float timeToWait = UnityEngine.Random.Range(_waitTime.x, _waitTime.y);

        while(timer < timeToWait)
        {
            CheckForTarget();

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        int randomNumber = UnityEngine.Random.Range(0, 2);
        if(randomNumber == 0)
        {
            ChangeState(WalkToRandomPlace());
        } else
        {
            ChangeState(FaceRandomDirection());
        }
    }

    private IEnumerator AttackTarget()
    {
        animator.SetTrigger("Attack");
        yield return new WaitForSeconds(_delayBeforeAttack);
        Attack();
        yield return new WaitForSeconds(_delayAfterAttack);

        ChangeState(FollowPathToTarget());
    }

    private IEnumerator FollowTarget()
    {
        animator.SetBool("Walk", true);

        float distance;
        Vector3 positionDelta;
        bool attack = false;

        while (IsTargetVisible())
        {
            positionDelta = GamePhysics.MoveTowardsPositionNonYClamped(transform.position, _target.position, _movementSpeed, out distance);
            transform.rotation = GamePhysics.RotateTowardsTarget(transform.rotation, transform.position, _target.position, _rotationSpeed);

            if (distance <= _attackRange * 0.75f) // TODO hardcoded
            {
                attack = true;
                break;
            }

            transform.position += positionDelta;
            yield return new WaitForFixedUpdate();
        }

        animator.SetBool("Walk", false);

        if (attack)
        {
            ChangeState(AttackTarget());
        } else
        {
            ChangeState(FollowPathToTarget());
        }
    }

    private IEnumerator WalkToRandomPlace()
    {
        animator.SetBool("Walk", true);

        _path = Pathfinder.GetRandomPath(transform.position, UnityEngine.Random.Range(_randomMovementCycles.x, _randomMovementCycles.y), UnityEngine.Random.Range(_randomMovementLength.x, _randomMovementLength.y));

        int index = 0;
        float distance;
        Vector3 positionDelta;

        while (true)
        {
            CheckForTarget();

            positionDelta = GamePhysics.MoveTowardsPositionNonYClamped(transform.position, _path[index], _movementSpeed, out distance);
            transform.rotation = GamePhysics.RotateTowardsMovementDirection(transform.rotation, positionDelta, _rotationSpeed);

            if (distance <= _moveDistanceTolerance)
            {
                index++;

                if (index >= _path.Count)
                {
                    break;
                }
            }

            transform.position += positionDelta;
            yield return new WaitForFixedUpdate();
        }

        animator.SetBool("Walk", false);

        ChangeState(WaitForNextAction());
    }

    private IEnumerator FaceRandomDirection()
    {
        float degreesToRotate = UnityEngine.Random.Range(90f, 180f);
        float alreadyRotatedDegrees = 0;
        int sign = 1;
        if (UnityEngine.Random.Range(0, 2) == 0)
        {
            sign = -1;
        }

        do
        {
            CheckForTarget();

            if (alreadyRotatedDegrees + _angularSpeed >= degreesToRotate)
            {
                transform.Rotate(0, (degreesToRotate - alreadyRotatedDegrees) * sign, 0);
                break;
            }

            transform.Rotate(0, _angularSpeed * sign, 0);
            alreadyRotatedDegrees += _angularSpeed;
            yield return new WaitForFixedUpdate();
        } while (true);

        ChangeState(WaitForNextAction());
    }

    #endregion

    #region Attack Methods

    private void Attack()
    {
        float distance = Vector3.Distance(transform.position, _target.transform.position);

        if (distance <= _attackRange)
        {
            Vector3 attackDirection3D = _target.transform.position - transform.position;

            Vector2 attackDirection = new Vector2(attackDirection3D.x, attackDirection3D.z);
            Vector2 forward = new Vector2(transform.forward.x, transform.forward.z);
            float angle = Vector2.Angle(forward, attackDirection);

            if (angle < _attackAngleInDegrees)
            {
                _target.GetComponent<IDamageable>().TakeDamage(_stats.damage, _stats.armourPenetration);
            } else if (distance <= _attackRange * _secondaryAttackRangeMultiplier)
            {
                if(angle < _secondaryAttackAngleInDegrees)
                {
                    _target.GetComponent<IDamageable>().TakeDamage(_stats.damage, _stats.armourPenetration);
                }
            }
        }
    }

    private bool InRangeToAttack()
    {
        return Vector3.Distance(transform.position, _target.transform.position) < _attackRange;
    }

    private bool IsTargetVisible()
    {
        return Physics.Raycast(transform.position, _target.transform.position - transform.position, _attackRange, ~transform.gameObject.layer);
    }

    public bool CanAttack()
    {
        if (InRangeToAttack()) // Is enemy in range to attack?
        {
            if (IsTargetVisible())
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Physics Methods

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

    private bool IsGrounded()
    {
        bool grounded = GamePhysics.IsGroundedRayCast(transform.position, _groundOffset, _rayOverhead, _groundLayer, out float yCorrection);
        transform.position = new Vector3(transform.position.x, transform.position.y + yCorrection, transform.position.z);
        return grounded;
    }

    private void ResolveCollisions()
    {
        _raycastDirections = new Vector3[] { transform.right, -transform.right, transform.forward };
        transform.position += GamePhysics.RaycastCollisionDetection(transform.position, _raycastDirections, _raycastLength, _collisionLayer);
    }

    #endregion

    #region Update Methods

    private void LookAtCamera()
    {
        _healthBarGO.transform.LookAt(_mainCamera.transform);
        _healthBarGO.transform.Rotate(0, 180, 0);
    }

    private void CheckForTarget()
    {
        if (!_seenTarget)
        {
            if (Vector3.Distance(transform.position, _target.transform.position) < _detectionRange)
            {
                if (Vector3.Angle(transform.forward, _target.transform.position - transform.position) < _detectionAngle)
                {
                    ChangeState(FollowPathToTarget());
                }
            }
        }
    }

    #endregion
 
    private void GetDestroyed()
    {
        OnEnemyDeath?.Invoke(transform.position);
        Destroy(gameObject);
    }

    public void TakeDamage(float damage, float armourPenetration)
    {
        _currentHealth -= DamageCalculator.CalculateDamage(damage, armourPenetration, _stats.armour);
        _healthBar.fillAmount = _currentHealth / _stats.health;

        if (!_seenTarget)
        {
            ChangeState(FollowPathToTarget());
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
