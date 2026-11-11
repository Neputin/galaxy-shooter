using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class EnemyLaser : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _speed = 6f;
    [SerializeField, Min(0.1f)] private float _lifetime = 5f;
    [SerializeField] private bool _seeking;
    [SerializeField, Min(1f)] private float _turnSpeed = 90f;
    private Rigidbody2D _body;
    private BoxCollider2D _hitbox;
    private Transform _target;
    private Vector2 _direction = Vector2.down;
    private bool _consumed;

    void Awake() { _body = GetComponent<Rigidbody2D>(); _hitbox = GetComponent<BoxCollider2D>(); }
    void Start() { Destroy(gameObject, _lifetime); }

    public void Launch(Vector2 direction, Transform target = null)
    {
        _target = target;
        _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.down;
        transform.up = _direction;
        _body.linearVelocity = _direction * _speed;
    }

    void FixedUpdate()
    {
        if (_consumed) return;
        if (_seeking && _target != null)
        {
            Vector2 desired = (_target.position - transform.position).normalized;
            _direction = Vector3.RotateTowards(_direction, desired, _turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            transform.up = _direction;
        }
        _body.linearVelocity = _direction * _speed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_consumed || !other.enabled) return;
        Player player = other.GetComponentInParent<Player>();
        TripleShotPowerup pickup = other.GetComponentInParent<TripleShotPowerup>();
        if (player == null && (pickup == null || !pickup.IsAvailable)) return;
        _consumed = true;
        _hitbox.enabled = false;
        _body.linearVelocity = Vector2.zero;
        _body.simulated = false;
        Destroy(gameObject);
        if (player != null) player.Damage();
        else pickup.DestroyByEnemy();
    }
}
