using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class TripleShotPowerup : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _speed = 2.5f;

    private Rigidbody2D _body;
    private BoxCollider2D _hitbox;
    private SpriteRenderer _spriteRenderer;
    private Camera _mainCamera;
    private bool _collected;
    [SerializeField, Min(0.1f)] private float _magnetSpeed = 14f;
    private Player _magnetTarget;
    public bool IsAvailable => !_collected && isActiveAndEnabled;
    public int PowerupID => _powerupID;

    public void AttractTo(Player target)
    {
        if (IsAvailable) _magnetTarget = target;
    }

    public void DestroyByEnemy()
    {
        if (!_collected) RemovePickup();
    }
    // IDs: 0 = triple shot, 1 = speed, 2 = shield, 3 = ammo, 4 = health, 5 = radial shot, 6 = slow hazard, 7 = homing.
    [SerializeField] private int _powerupID = 0; 
   
    void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _hitbox = GetComponent<BoxCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _mainCamera = Camera.main != null
            ? Camera.main
            : FindAnyObjectByType<Camera>();
    }

    void FixedUpdate()
    {
        if (_collected)
            return;

        _body.linearVelocity = _magnetTarget != null
            ? ((Vector2)(_magnetTarget.transform.position - transform.position)).normalized * _magnetSpeed
            : Vector2.down * _speed;

        if (_mainCamera == null)
            return;

        float cameraDistance = Vector3.Dot(
            transform.position - _mainCamera.transform.position,
            _mainCamera.transform.forward
        );
        float bottomEdge = _mainCamera.ViewportToWorldPoint(
            new Vector3(0.5f, 0f, cameraDistance)
        ).y;

        // Missed pickups disappear once the whole sprite is below the camera.
        if (_spriteRenderer.bounds.max.y < bottomEdge)
            RemovePickup();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected)
            return;

        // Only the player can collect this; lasers and enemies pass through.
        Player player = other.GetComponentInParent<Player>();
        if (player == null)
            return;
        switch (_powerupID)
        {
            case 0:
                // Activate triple shot.
                player.EnableTripleShot();
                RemovePickup();
                break;

            case 1:
                // Activate speed boost.
                player.SpeedIncreasePowerup();
                RemovePickup();
                break;

            case 2:
                // Restore the shield to its full three-hit strength.
                player.ShieldsActive();
                RemovePickup();
                break;

            case 3:
                // Restore all shots and allow an empty weapon to fire again.
                player.RefillAmmo();
                RemovePickup();
                break;

            case 4:
                // Repair one life; Player caps health and updates the visuals/UI.
                player.Heal();
                RemovePickup();
                break;

            case 5:
                player.EnableRadialShot();
                RemovePickup();
                break;

            case 6:
                player.ApplySlow();
                RemovePickup();
                break;

            case 7:
                player.EnableHomingShot();
                RemovePickup();
                break;

            default:
                // Unknown powerup IDs have no effect.
                break;
        }

        // Only successful collection plays a sound; missed pickups stay silent.
        if (_collected && AudioManager.Instance != null)
            AudioManager.Instance.PlayPowerUp();
    }

    void RemovePickup()
    {
        _collected = true;
        _hitbox.enabled = false;
        _body.simulated = false;
        Destroy(gameObject);
    }
}
