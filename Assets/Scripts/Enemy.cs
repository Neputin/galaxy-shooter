using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    public enum EnemyRole { Raider, Interceptor, Hunter, RearGunner }
    public enum MovementPattern { Wave, ZigZag, Diagonal }

    [Header("Movement")]
    [SerializeField] private EnemyRole _role;
    [SerializeField] private MovementPattern _movement;
    [SerializeField, Min(0.1f)] private float _speed = 1.8f;
    [SerializeField, Min(0f)] private float _sidewaysSpeed = 1.1f;
    [SerializeField, Min(0.1f)] private float _movementFrequency = 2f;
    [SerializeField, Min(0f)] private float _respawnPadding = 0.25f;
    [Header("Explosion and Score")]
    [SerializeField] private AnimationClip _explosionClip;
    [SerializeField, Min(0)] private int _pointsOnKill = 10;
    [Header("Shooting")]
    [SerializeField] private EnemyLaser _laserPrefab;
    [SerializeField, Min(0.1f)] private float _minimumFireDelay = 3f;
    [SerializeField, Min(0.1f)] private float _maximumFireDelay = 7f;
    [SerializeField, Min(0f)] private float _pickupAttackRange = 6f;
    [Header("Shield")]
    [SerializeField] private GameObject _shieldVisual;
    [SerializeField] private bool _startShielded;
    [Header("Ramming")]
    [SerializeField] private bool _canRam = true;
    [SerializeField, Min(0f)] private float _ramRange = 2.2f;
    [SerializeField, Min(0.1f)] private float _ramSpeed = 5f;
    [SerializeField, Min(0.1f)] private float _ramDuration = 0.65f;
    [SerializeField, Min(0.1f)] private float _ramCooldown = 2f;
    [Header("Evasion")]
    [SerializeField, Min(0.1f)] private float _dodgeRange = 2.5f;
    [SerializeField, Min(0.1f)] private float _dodgeSpeed = 3.5f;

    private Camera _mainCamera;
    private Rigidbody2D _body;
    private BoxCollider2D _hitbox;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Player _player;
    private SpawnManager _owner;
    private bool _isDestroyed, _shieldActive, _wasBehindPlayer;
    private float _nextFireTime, _age, _diagonalSign;
    private float _ramUntil, _nextRamTime, _dodgeUntil, _nextDodgeTime;
    private Vector2 _ramDirection, _dodgeDirection;

    public bool IsTargetable => !_isDestroyed && isActiveAndEnabled;
    public bool HasShield => _shieldActive;
    public bool IsRamming => Time.time < _ramUntil;
    public bool IsDodging => Time.time < _dodgeUntil;
    public EnemyRole Role => _role;

    void Awake()
    {
        _player = FindAnyObjectByType<Player>();
        _body = GetComponent<Rigidbody2D>();
        _hitbox = GetComponent<BoxCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        _mainCamera = Camera.main;
        _diagonalSign = _mainCamera != null && transform.position.x > _mainCamera.transform.position.x ? -1f : 1f;
        SetShield(_startShielded);
    }

    void OnEnable() { ScheduleNextShot(); }

    public void Configure(SpawnManager owner, bool shielded, float speedMultiplier)
    {
        _owner = owner;
        _speed *= speedMultiplier;
        // SpawnManager positions the instance after Awake, so choose its entry angle here.
        _diagonalSign = _mainCamera != null && transform.position.x > _mainCamera.transform.position.x ? -1f : 1f;
        SetShield(shielded);
    }

    void SetShield(bool active)
    {
        _shieldActive = active;
        if (_shieldVisual != null) _shieldVisual.SetActive(active);
    }

    void Update()
    {
        if (!IsTargetable || _player == null || _laserPrefab == null) return;
        bool behind = _role == EnemyRole.RearGunner && transform.position.y < _player.transform.position.y - 0.2f;
        // A rear gunner reacts as soon as it passes the player.
        if (Time.time >= _nextFireTime || (behind && !_wasBehindPlayer))
        {
            FireWeapon(behind);
            ScheduleNextShot();
        }
        _wasBehindPlayer = behind;
    }

    void ScheduleNextShot()
    {
        _nextFireTime = Time.time + Random.Range(_minimumFireDelay, Mathf.Max(_minimumFireDelay, _maximumFireDelay));
    }

    TripleShotPowerup FindPickupAhead()
    {
        TripleShotPowerup nearest = null;
        float bestDistance = _pickupAttackRange;
        foreach (TripleShotPowerup pickup in FindObjectsByType<TripleShotPowerup>())
        {
            if (!pickup.IsAvailable) continue;
            Vector2 offset = pickup.transform.position - transform.position;
            if (offset.y >= -0.2f || Mathf.Abs(offset.x) > 1.2f || offset.magnitude >= bestDistance) continue;
            nearest = pickup;
            bestDistance = offset.magnitude;
        }
        return nearest;
    }

    void FireWeapon(bool behind)
    {
        TripleShotPowerup pickup = FindPickupAhead();
        Vector3 target;
        if (pickup != null) target = pickup.transform.position;
        else
        {
            if (_player.transform.position.y > transform.position.y && !behind) return;
            target = _player.transform.position;
        }
        Vector2 direction = (target - transform.position).normalized;
        Vector3 origin = transform.position + (Vector3)direction * 0.55f;
        EnemyLaser laser = Instantiate(_laserPrefab, origin, Quaternion.identity);
        // Seeking bolts aimed at pickups keep that target instead of turning toward the player.
        laser.Launch(direction, pickup != null ? pickup.transform : _player.transform);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayLaserShot();
    }

    void FixedUpdate()
    {
        if (!IsTargetable) return;
        _age += Time.fixedDeltaTime;
        if (_canRam && _player != null && !IsRamming && Time.time >= _nextRamTime &&
            Vector2.Distance(transform.position, _player.transform.position) <= _ramRange)
        {
            // Commit to the direction at the start so the player can dodge the charge.
            _ramDirection = (_player.transform.position - transform.position).normalized;
            _ramUntil = Time.time + _ramDuration;
            _nextRamTime = Time.time + _ramCooldown;
        }
        if (_role == EnemyRole.Interceptor && !IsRamming && !IsDodging && Time.time >= _nextDodgeTime)
            DetectIncomingShot();

        float lateral = _movement == MovementPattern.Wave ? Mathf.Sin(_age * _movementFrequency) :
            _movement == MovementPattern.ZigZag ? Mathf.Sign(Mathf.Sin(_age * _movementFrequency)) : _diagonalSign;
        Vector2 velocity = new Vector2(lateral * _sidewaysSpeed, -_speed);
        if (IsDodging) velocity = new Vector2(_dodgeDirection.x * _dodgeSpeed, -_speed);
        if (IsRamming) velocity = _ramDirection * _ramSpeed;

        if (_mainCamera != null)
        {
            float distance = Vector3.Dot(transform.position - _mainCamera.transform.position, _mainCamera.transform.forward);
            Vector3 lower = _mainCamera.ViewportToWorldPoint(new Vector3(0, 0, distance));
            Vector3 upper = _mainCamera.ViewportToWorldPoint(new Vector3(1, 1, distance));
            float halfWidth = _spriteRenderer.bounds.extents.x;
            if (_body.position.x <= lower.x + halfWidth && velocity.x < 0) velocity.x = Mathf.Abs(velocity.x);
            if (_body.position.x >= upper.x - halfWidth && velocity.x > 0) velocity.x = -Mathf.Abs(velocity.x);
            // Keep moving inward after a diagonal enemy turns at an edge.
            if (_movement == MovementPattern.Diagonal && !IsRamming && !IsDodging)
                _diagonalSign = Mathf.Sign(velocity.x);
            if (_spriteRenderer.bounds.max.y < lower.y - _respawnPadding)
            {
                // Escaped enemies count as leaving the wave; they do not award points.
                Destroy(gameObject);
                return;
            }
        }
        _body.linearVelocity = velocity;
    }

    void DetectIncomingShot()
    {
        foreach (Bullet bullet in FindObjectsByType<Bullet>())
        {
            if (bullet.IsConsumed || bullet.Velocity.sqrMagnitude < 0.01f) continue;
            Vector2 offset = transform.position - bullet.transform.position;
            if (offset.magnitude > _dodgeRange || Vector2.Dot(bullet.Velocity.normalized, offset.normalized) < 0.6f) continue;
            float side = Mathf.Abs(offset.x) > 0.1f ? Mathf.Sign(offset.x) :
                (_mainCamera != null && transform.position.x > _mainCamera.transform.position.x ? -1f : 1f);
            _dodgeDirection = new Vector2(side, 0f);
            _dodgeUntil = Time.time + 0.45f;
            _nextDodgeTime = Time.time + 0.8f;
            return;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_isDestroyed || !other.enabled) return;
        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            DestroyEnemy();
            player.Damage();
            return;
        }
        Bullet bullet = other.GetComponentInParent<Bullet>();
        if (bullet != null)
        {
            if (!bullet.TryConsume()) return;
        }
        else if (other.CompareTag("Laser"))
        {
            other.enabled = false;
            Destroy(other.gameObject);
        }
        else return;

        if (_shieldActive)
        {
            SetShield(false);
            return;
        }
        DestroyEnemy();
        if (_player != null) _player.AddScore(_pointsOnKill);
    }

    void DestroyEnemy()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;
        SetShield(false);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
        _hitbox.enabled = false;
        _body.linearVelocity = Vector2.zero;
        _body.simulated = false;
        if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null && _explosionClip != null)
        {
            _animator.SetTrigger("OnEnemyDeath");
            _animator.Update(0f);
            Destroy(gameObject, _explosionClip.length);
        }
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_owner != null) _owner.EnemyRemoved(this);
    }
}
