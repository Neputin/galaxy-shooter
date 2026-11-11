
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _speed = 3.5f;
    [SerializeField, Min(1f)]
    [Tooltip("Movement speed multiplier while Left Shift is held.")]
    private float _sprintMultiplier = 2f;
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField, Min(0.1f)] private float _speedBoostDuration = 5f;


    [Header("Thruster Charge")]
    [SerializeField, Min(0.1f)]
    [Tooltip("Seconds of continuous sprint available from a full charge.")]
    private float _thrusterBoostDuration = 4f;
    [SerializeField, Min(0.1f)]
    [Tooltip("Seconds needed to recharge from empty to full while not boosting.")]
    private float _thrusterRechargeDuration = 6f;
    [SerializeField, Min(0.1f)]
    [Tooltip("Seconds sprint is locked after the thrusters run out. Charge recovers during this time.")]
    private float _thrusterCooldownDuration = 3f;

    private float _thrusterCharge = 1f;
    private float _thrusterCooldownRemaining;

    private bool _isSpeedBoostActive;
    private Coroutine _speedBoostRoutine;

    [Header("Shooting")]
    [SerializeField] private InputActionReference _shootAction;
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private GameObject _tripleShotPrefab;
    [SerializeField, Min(0.1f)] private float _tripleShotDuration = 5f;
    [SerializeField] private GameObject _radialShotPrefab;
    [SerializeField, Min(0.1f)] private float _radialShotDuration = 5f;
    private float _radialShotExpiresAt;
    public bool IsRadialShotActive => Time.time < _radialShotExpiresAt;
    [SerializeField] private Transform _firePoint;
    [SerializeField, Min(0.1f)] private float _fireRate = 5f;
    [Header("Health")]
    private const int MaxLives = 3;
    [SerializeField, Range(1, MaxLives)] private int _lives = MaxLives;
    private float _nextFireTime = 0f;
    [Header("Ammo")]
    [SerializeField, Min(1)]
    [Tooltip("Shots available at the start and after collecting an ammo pickup. A triple-shot volley costs one ammo.")]
    private int _maxAmmo = 15;
    private int _currentAmmo;

    [Header("Shield")]
    [SerializeField] public bool _isShieldActive = false;
    [SerializeField] private GameObject _shieldVisual;
    [SerializeField] private SpriteRenderer _shieldRenderer;
    private const int MaxShieldHits = 3;
    private int _shieldHitsRemaining;

    [Header("Damage Feedback")]
    [SerializeField] private CameraShake _cameraShake;

    [Header("Engine Damage")]
    [SerializeField] private GameObject _engineBurn1;
    [SerializeField] private GameObject _engineBurn2;
    // -1 means no damaged engine has been chosen; 0 and 1 identify the engines.
    private int _firstDamagedEngine = -1;
    private Camera _mainCamera;
    private float _forwardLimit;
    // is trippleshot enabled
    private Coroutine _tripleShotRoutine;
    private bool _IsTrippleShotOn = false; 

    private int _score;
    private UIManager _uiManager;

    public int Score => _score;
    public int CurrentAmmo => _currentAmmo;
    [Header("Homing Weapon")]
    [SerializeField] private GameObject _homingShotPrefab;
    [SerializeField, Min(0.1f)] private float _homingShotDuration = 5f;
    private float _homingShotExpiresAt;
    public bool IsHomingShotActive => Time.time < _homingShotExpiresAt;
    [Header("Slow Hazard and Pickup Magnet")]
    [SerializeField, Range(0.1f, 1f)] private float _slowMultiplier = 0.5f;
    [SerializeField, Min(0.1f)] private float _slowDuration = 5f;
    [SerializeField, Min(0f)] private float _pickupMagnetRange = 12f;
    private float _slowExpiresAt;
    public bool IsSlowed => Time.time < _slowExpiresAt;

    void Awake()
    {
        _score = 0;
        _radialShotExpiresAt = 0f;
        if (_cameraShake == null)
            _cameraShake = FindAnyObjectByType<CameraShake>();
        _maxAmmo = Mathf.Max(1, _maxAmmo);
        _currentAmmo = _maxAmmo;
        _thrusterCharge = 1f;
        _thrusterCooldownRemaining = 0f;
        _lives = Mathf.Clamp(_lives, 1, MaxLives);
        _firstDamagedEngine = -1;
        UpdateEngineDamageVisuals();
        _uiManager = FindFirstObjectByType<UIManager>();
        if (_uiManager == null)
            Debug.LogError("UIManager is missing from the Canvas.", this);

        // Find the child even when it starts inactive in the Hierarchy.
        if (_shieldVisual == null)
        {
            Transform shield = transform.Find("PlayerShield");
            if (shield != null)
                _shieldVisual = shield.gameObject;
        }

        if (_shieldRenderer == null && _shieldVisual != null)
            _shieldRenderer = _shieldVisual.GetComponentInChildren<SpriteRenderer>(true);

        ShieldsDEActivete();
    }

    void OnEnable()
    {
        _moveAction.action.Enable();
        _shootAction.action.Enable();
    }

    void OnDisable()
    {
        DisableHomingShot();
        _slowExpiresAt = 0f;
        if (_uiManager != null) _uiManager.UpdatePlayerEffects(0f);
        DisableRadialShot();
        DisableTripleShot();
        DisableSpeedBoost();
        ShieldsDEActivete();
        _moveAction.action.Disable();
        _shootAction.action.Disable();
    }

    void Start()
    {
        _mainCamera = Camera.main != null
            ? Camera.main
            : FindAnyObjectByType<Camera>();

        transform.position = Vector3.zero;

        _forwardLimit = transform.position.y;

        if (_uiManager != null)
        {
            _uiManager.UpdateScore(_score);
            _uiManager.UpdateLives(_lives);
            _uiManager.UpdateThrusters(_thrusterCharge, false, 0f);
            _uiManager.UpdateAmmo(_currentAmmo, _maxAmmo);
            _uiManager.UpdateShield(_shieldHitsRemaining, MaxShieldHits);
        }

        Debug.Log($"Player lives: {_lives}", this);
    }

    void Update()
    {
        if (_uiManager != null && _uiManager.IsRoundOver) return;
        if (_uiManager != null)
        {
            _uiManager.UpdateWeaponMode(IsHomingShotActive ? "HOMING" : "RADIAL",
                Mathf.Max(0f, (IsHomingShotActive ? _homingShotExpiresAt : _radialShotExpiresAt) - Time.time));
            _uiManager.UpdatePlayerEffects(Mathf.Max(0f, _slowExpiresAt - Time.time));
        }
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            AttractPickups();
        // MOVEMENT
        Vector2 input = _moveAction.action.ReadValue<Vector2>();

        Vector3 movement = new Vector3(input.x, input.y, 0f);
        movement = Vector3.ClampMagnitude(movement, 1f);

        // Keep the base speed unchanged; the bonus only applies while active.
        float currentSpeed = _speed + (_isSpeedBoostActive ? 1f : 0f);

        bool sprintHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        // Holding Shift while stationary does not spend charge.
        bool isSprinting = UpdateThrusters(sprintHeld && movement.sqrMagnitude > 0.001f);
        if (isSprinting)
            currentSpeed *= _sprintMultiplier;

        if (IsSlowed)
            currentSpeed *= _slowMultiplier;

        Vector3 position = transform.position
            + movement * currentSpeed * Time.deltaTime;

        // Limit forward movement
        position.y = Mathf.Min(position.y, _forwardLimit);

        // SCREEN WRAPPING
        if (_mainCamera != null)
        {
            float cameraDistance = Vector3.Dot(
                position - _mainCamera.transform.position,
                _mainCamera.transform.forward
            );

            float leftEdge = _mainCamera.ViewportToWorldPoint(
                new Vector3(0f, 0.5f, cameraDistance)
            ).x;

            float rightEdge = _mainCamera.ViewportToWorldPoint(
                new Vector3(1f, 0.5f, cameraDistance)
            ).x;

            float visibleWidth = rightEdge - leftEdge;

            if (visibleWidth > 0f &&
                (position.x < leftEdge || position.x > rightEdge))
            {
                position.x = leftEdge +
                    Mathf.Repeat(position.x - leftEdge, visibleWidth);
            }
        }

        transform.position = position;

        // AUTOMATIC SHOOTING
        if (_shootAction.action.IsPressed() &&
            Time.time >= _nextFireTime)
        {
            Shoot();
            _nextFireTime = Time.time + 1f / _fireRate;
        }
    }

    bool UpdateThrusters(bool boostRequested)
    {
        float deltaTime = Time.deltaTime;
        float rechargeRate = 1f / Mathf.Max(0.1f, _thrusterRechargeDuration);
        bool isBoosting = false;

        if (_thrusterCooldownRemaining > 0f)
        {
            // Overheated thrusters cannot boost, but they can recover charge.
            _thrusterCooldownRemaining = Mathf.Max(0f, _thrusterCooldownRemaining - deltaTime);
            _thrusterCharge = Mathf.Clamp01(_thrusterCharge + rechargeRate * deltaTime);
        }
        else if (boostRequested && _thrusterCharge > 0f)
        {
            float drainRate = 1f / Mathf.Max(0.1f, _thrusterBoostDuration);
            _thrusterCharge = Mathf.Clamp01(_thrusterCharge - drainRate * deltaTime);
            isBoosting = _thrusterCharge > 0f;

            if (!isBoosting)
                _thrusterCooldownRemaining = Mathf.Max(0.1f, _thrusterCooldownDuration);
        }
        else
        {
            // Releasing Shift or stopping movement lets the charge refill.
            _thrusterCharge = Mathf.Clamp01(_thrusterCharge + rechargeRate * deltaTime);
        }

        if (_uiManager != null)
            _uiManager.UpdateThrusters(_thrusterCharge, isBoosting, _thrusterCooldownRemaining);

        return isBoosting;
    }

    void Shoot()
    {
        // Empty weapons cannot spawn a laser or play the firing sound.
        if (_lives <= 0 || _currentAmmo <= 0 || (_uiManager != null && _uiManager.IsRoundOver))
            return;

        // Weapon pickups replace one another; only one shot pattern is created.
        GameObject shotPrefab = IsHomingShotActive
            ? _homingShotPrefab
            : IsRadialShotActive
            ? _radialShotPrefab
            : _IsTrippleShotOn && _tripleShotPrefab != null
                ? _tripleShotPrefab
                : _bulletPrefab;

        // Do not spend ammo when the weapon has not been configured.
        if (shotPrefab == null || _firePoint == null)
            return;

        Instantiate(shotPrefab, _firePoint.position, Quaternion.identity);

        // Every volley costs one ammo, including triple shot and radial shot.
        _currentAmmo--;
        if (_uiManager != null)
            _uiManager.UpdateAmmo(_currentAmmo, _maxAmmo);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLaserShot();
    }

    public void EnableHomingShot()
    {
        if (_lives <= 0 || _homingShotPrefab == null) return;
        DisableTripleShot();
        DisableRadialShot();
        _homingShotExpiresAt = Time.time + _homingShotDuration;
        if (_uiManager != null) _uiManager.UpdateWeaponMode("HOMING", _homingShotDuration);
    }

    public void DisableHomingShot()
    {
        _homingShotExpiresAt = 0f;
        if (_uiManager != null) _uiManager.UpdateWeaponMode("HOMING", 0f);
    }

    public void ApplySlow()
    {
        if (_lives <= 0) return;
        _slowExpiresAt = Time.time + _slowDuration;
        if (_uiManager != null) _uiManager.UpdatePlayerEffects(_slowDuration);
    }

    public void AttractPickups()
    {
        foreach (TripleShotPowerup pickup in FindObjectsByType<TripleShotPowerup>())
            if (pickup.IsAvailable && Vector2.Distance(transform.position, pickup.transform.position) <= _pickupMagnetRange)
                pickup.AttractTo(this);
    }

    public void RefillAmmo()
    {
        if (_lives <= 0)
            return;

        // Refill to capacity; pickups do not stack beyond the maximum.
        _currentAmmo = _maxAmmo;
        if (_uiManager != null)
            _uiManager.UpdateAmmo(_currentAmmo, _maxAmmo);
    }

    public void EnableRadialShot()
    {
        DisableHomingShot();
        if (_lives <= 0 || _radialShotPrefab == null)
            return;

        DisableTripleShot();
        // Another radial pickup refreshes the five seconds without stacking timers.
        _radialShotExpiresAt = Time.time + _radialShotDuration;
        if (_uiManager != null)
            _uiManager.UpdateRadialShot(_radialShotDuration);
    }

    public void DisableRadialShot()
    {
        _radialShotExpiresAt = 0f;
        if (_uiManager != null)
            _uiManager.UpdateRadialShot(0f);
    }

    // The upgrade pickup calls this when the player collects it.
    public void EnableTripleShot()
    {
        DisableHomingShot();
        DisableRadialShot();
        // A new pickup restarts the timer instead of leaving an old timer running.
        if (_tripleShotRoutine != null)
            StopCoroutine(_tripleShotRoutine);

        _IsTrippleShotOn = true;
        _tripleShotRoutine = StartCoroutine(TripleShotPowerDownRoutine());
    }

    public void SpeedIncreasePowerup()
    {
        // Collecting another speed pickup refreshes the timer without stacking.
        if (_speedBoostRoutine != null)
            StopCoroutine(_speedBoostRoutine);

        _isSpeedBoostActive = true;
        _speedBoostRoutine = StartCoroutine(SpeedBoostPowerDownRoutine());
    }

    public void DisableSpeedBoost()
    {
        if (_speedBoostRoutine != null)
        {
            StopCoroutine(_speedBoostRoutine);
            _speedBoostRoutine = null;
        }

        _isSpeedBoostActive = false;
    }

    IEnumerator SpeedBoostPowerDownRoutine()
    {
        yield return new WaitForSeconds(_speedBoostDuration);

        _isSpeedBoostActive = false;
        _speedBoostRoutine = null;
    }

    public void DisableTripleShot()
    {
        if (_tripleShotRoutine != null)
        {
            StopCoroutine(_tripleShotRoutine);
            _tripleShotRoutine = null;
        }

        _IsTrippleShotOn = false;
    }

    IEnumerator TripleShotPowerDownRoutine()
    {
        // Wait without blocking movement, shooting, or the rest of the game.
        yield return new WaitForSeconds(_tripleShotDuration);

        _IsTrippleShotOn = false;
        _tripleShotRoutine = null;
    }

    // Called by an enemy with that enemy's own point value.
    public void AddScore(int points)
    {
        if (points <= 0)
            return;

        _score += points;
        if (_uiManager != null)
            _uiManager.UpdateScore(_score);
    }

    public void Damage()
    {
        // Ignore extra collisions after a fatal hit; Destroy finishes at frame end.
        if (_lives <= 0 || (_uiManager != null && _uiManager.IsRoundOver))
            return;

        if (_shieldHitsRemaining > 0)
        {
            _shieldHitsRemaining--;
            UpdateShieldVisuals();
            // The hit that breaks the shield is still fully absorbed.
            return;
        }

        _lives = Mathf.Max(0, _lives - 1);
        // Shield-absorbed hits returned above, so only actual health loss shakes.
        if (_cameraShake != null)
            _cameraShake.Shake();

        UpdateEngineDamageVisuals();
        Debug.Log($"Player lives: {_lives}", this);
        if (_uiManager != null)
            _uiManager.UpdateLives(_lives);

        if (_lives == 0)
            Destroy(gameObject);
    }

    public void Heal()
    {
        // Health pickups cannot revive a dead player or exceed three lives.
        if (_lives <= 0 || _lives >= MaxLives)
            return;

        _lives = Mathf.Min(MaxLives, _lives + 1);
        UpdateEngineDamageVisuals();

        Debug.Log($"Player lives: {_lives}", this);
        if (_uiManager != null)
            _uiManager.UpdateLives(_lives);
    }

    void UpdateEngineDamageVisuals()
    {
        int missingLives = MaxLives - _lives;

        if (missingLives == 0)
        {
            // Fully repairing the ship lets the next hit choose an engine again.
            _firstDamagedEngine = -1;
        }
        else if (_firstDamagedEngine < 0)
        {
            // Choose once, so partial healing does not swap the damaged side.
            _firstDamagedEngine = Random.Range(0, 2);
        }

        if (_engineBurn1 != null)
            _engineBurn1.SetActive(missingLives >= 2 ||
                (missingLives == 1 && _firstDamagedEngine == 0));

        if (_engineBurn2 != null)
            _engineBurn2.SetActive(missingLives >= 2 ||
                (missingLives == 1 && _firstDamagedEngine == 1));
    }

    public void ShieldsActive()
    {
        if (_lives <= 0)
            return;

        // A pickup refills the shield instead of stacking extra hits.
        _shieldHitsRemaining = MaxShieldHits;
        UpdateShieldVisuals();
    }

    public void ShieldsDEActivete()
    {
        _shieldHitsRemaining = 0;
        UpdateShieldVisuals();
    }

    void UpdateShieldVisuals()
    {
        _isShieldActive = _shieldHitsRemaining > 0;
        if (_shieldVisual != null)
            _shieldVisual.SetActive(_isShieldActive);

        if (_shieldRenderer != null)
        {
            // Preserve the blue animation's tint; fade its opacity with strength.
            Color color = _shieldRenderer.color;
            float strength = _shieldHitsRemaining / (float)MaxShieldHits;
            color.a = Mathf.Lerp(0.25f, 1f, strength);
            _shieldRenderer.color = color;
        }

        if (_uiManager != null)
            _uiManager.UpdateShield(_shieldHitsRemaining, MaxShieldHits);
    }
}
