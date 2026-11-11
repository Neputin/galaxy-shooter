using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Bullet : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _speed = 10f;
    [SerializeField, Min(0.01f)] private float _lifetime = 3f;

    private Rigidbody2D _body;
    private BoxCollider2D _hitbox;
    private bool _consumed;
    private Vector2 _direction = Vector2.up;
    public bool IsConsumed => _consumed;
    public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

    void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _hitbox = GetComponent<BoxCollider2D>();
    }

    void Start()
    {
        Destroy(gameObject, _lifetime);
    }

    public void Launch(Vector2 direction)
    {
        // Standard lasers keep Vector2.up; radial bolts receive their own direction.
        _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up;
        transform.up = _direction;
        _body.linearVelocity = _direction * _speed;
    }

    void FixedUpdate()
    {
        if (!_consumed)
            _body.linearVelocity = _direction * _speed;
    }

    // Only one enemy can consume a laser, even if several overlap in one frame.
    public bool TryConsume()
    {
        if (_consumed)
            return false;

        _consumed = true;
        _hitbox.enabled = false;
        _body.simulated = false;
        Destroy(gameObject);
        return true;
    }
}
