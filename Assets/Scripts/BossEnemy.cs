using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class BossEnemy : MonoBehaviour
{
    [Header("Entrance")]
    [SerializeField, Min(0.1f)] private float _entranceSpeed = 2f;
    [Header("Health and Reward")]
    [SerializeField, Min(1)] private int _maximumHealth = 30;
    [SerializeField, Min(0)] private int _pointsOnDefeat = 250;
    [SerializeField] private AnimationClip _explosionClip;
    [Header("Aimed Fan Attack")]
    [SerializeField] private EnemyLaser _fanProjectile;
    [SerializeField, Min(1)] private int _fanProjectileCount = 5;
    [SerializeField, Range(1f, 45f)] private float _fanAngleStep = 12f;
    [SerializeField, Min(0.1f)] private float _fanWarningDuration = 0.65f;
    [SerializeField, Min(0.1f)] private float _burstSpacing = 0.4f;
    [SerializeField, Min(0.1f)] private float _fanRecovery = 2.2f;
    [Header("Twin Seekers Attack")]
    [SerializeField] private EnemyLaser _seekerProjectile;
    [SerializeField, Min(0.1f)] private float _seekerWarningDuration = 0.9f;
    [SerializeField, Min(0.1f)] private float _seekerRecovery = 3f;
    [SerializeField] private SpriteRenderer _chargeVisual;

    private Rigidbody2D _body;
    private BoxCollider2D _hitbox;
    private Animator _animator;
    private Player _player;
    private UIManager _ui;
    private SpawnManager _owner;
    private Vector2 _holdPosition;
    private Coroutine _attackRoutine;
    private int _health;
    private bool _configured, _arrived, _defeated;

    public int CurrentHealth => _health;
    public int MaximumHealth => _maximumHealth;
    public bool HasArrived => _arrived;
    public bool IsTargetable => !_defeated && _health > 0 && isActiveAndEnabled;
    public string CurrentAttack { get; private set; } = "APPROACHING";

    void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _hitbox = GetComponent<BoxCollider2D>();
        _animator = GetComponent<Animator>();
        _player = FindAnyObjectByType<Player>();
        _ui = FindAnyObjectByType<UIManager>();
        _health = Mathf.Max(1, _maximumHealth);
    }

    public void Configure(SpawnManager owner, Vector2 holdPosition)
    {
        _owner = owner;
        _holdPosition = holdPosition;
        _configured = true;
        SetAttackStatus("APPROACHING", Color.clear);
    }

    void FixedUpdate()
    {
        if (!_configured || !IsTargetable) return;
        // MoveTowards cannot overshoot. Once centered, the body stays at this point.
        _body.linearVelocity = Vector2.zero;
        Vector2 next = Vector2.MoveTowards(_body.position, _holdPosition, _entranceSpeed * Time.fixedDeltaTime);
        _body.MovePosition(next);
        // Check the completed physics position, not the position scheduled for next step.
        if (!_arrived && (_body.position - _holdPosition).sqrMagnitude < 0.000001f)
        {
            _arrived = true;
            _attackRoutine = StartCoroutine(AttackRoutine());
        }
    }

    IEnumerator AttackRoutine()
    {
        while (CanAttack())
        {
            // Give the player a readable warning before each distinct attack.
            SetAttackStatus("FAN INCOMING", new Color(1f, 0.35f, 0.08f, 0.9f));
            yield return new WaitForSeconds(_fanWarningDuration);
            if (!CanAttack()) yield break;
            FireFan();
            yield return new WaitForSeconds(_burstSpacing);
            if (!CanAttack()) yield break;
            FireFan();
            SetAttackStatus("RECOVERING", Color.clear);
            yield return new WaitForSeconds(_fanRecovery);
            if (!CanAttack()) yield break;

            SetAttackStatus("SEEKERS INCOMING", new Color(0.85f, 0.25f, 1f, 0.9f));
            yield return new WaitForSeconds(_seekerWarningDuration);
            if (!CanAttack()) yield break;
            FireSeekers();
            SetAttackStatus("RECOVERING", Color.clear);
            yield return new WaitForSeconds(_seekerRecovery);
        }
    }

    bool CanAttack() => IsTargetable && _player != null && (_ui == null || !_ui.IsRoundOver);

    void FireFan()
    {
        if (_fanProjectile == null || !CanAttack()) return;
        Vector2 aim = (_player.transform.position - transform.position).normalized;
        Vector3 origin = transform.position + (Vector3)aim * 0.9f;
        for (int i = 0; i < _fanProjectileCount; i++)
        {
            float angle = (i - (_fanProjectileCount - 1) * 0.5f) * _fanAngleStep;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
            Instantiate(_fanProjectile, origin, Quaternion.identity).Launch(direction);
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayLaserShot();
    }

    void FireSeekers()
    {
        if (_seekerProjectile == null || !CanAttack()) return;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 origin = transform.position + new Vector3(side * 0.75f, -0.55f, 0f);
            Vector2 aim = (_player.transform.position - origin).normalized;
            Vector2 direction = Quaternion.Euler(0f, 0f, side * 35f) * aim;
            Instantiate(_seekerProjectile, origin, Quaternion.identity).Launch(direction, _player.transform);
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayLaserShot();
    }

    void SetAttackStatus(string status, Color chargeColor)
    {
        CurrentAttack = status;
        if (_chargeVisual != null) _chargeVisual.color = chargeColor;
        if (_ui != null) _ui.UpdateBoss(_health, MaximumHealth, status);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsTargetable || !other.enabled) return;
        Bullet bullet = other.GetComponentInParent<Bullet>();
        if (bullet != null && bullet.TryConsume())
        {
            _health = Mathf.Max(0, _health - 1);
            if (_ui != null) _ui.UpdateBoss(_health, MaximumHealth, CurrentAttack);
            if (_health == 0) Defeat();
            return;
        }
        Player player = other.GetComponentInParent<Player>();
        if (player != null) player.Damage();
    }

    void Defeat()
    {
        if (_defeated) return;
        _defeated = true;
        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _hitbox.enabled = false;
        _body.linearVelocity = Vector2.zero;
        _body.simulated = false;
        SetAttackStatus("DEFEATED", Color.clear);
        // Remove hostile shots immediately so the explosion is a safe victory moment.
        foreach (EnemyLaser projectile in FindObjectsByType<EnemyLaser>())
        {
            projectile.gameObject.SetActive(false);
            Destroy(projectile.gameObject);
        }
        if (_player != null) _player.AddScore(_pointsOnDefeat);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
        float delay = 0.6f;
        if (_animator != null && _animator.runtimeAnimatorController != null && _explosionClip != null)
        {
            _animator.SetTrigger("OnEnemyDeath");
            _animator.Update(0f);
            delay = _explosionClip.length;
        }
        StartCoroutine(FinishDefeat(delay));
    }

    IEnumerator FinishDefeat(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_owner != null) _owner.BossDefeated(this);
        Destroy(gameObject);
    }
}
