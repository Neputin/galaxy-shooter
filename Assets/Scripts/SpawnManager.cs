using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class PickupEntry
    {
        public GameObject Prefab;
        [Min(0f)] public float Weight = 1f;
    }
    [Header("Enemy Waves")]
    [SerializeField] private GameObject[] _enemyPrefabs;
    [SerializeField] private Transform _enemyContainer;
    [SerializeField, Min(1)] private int _firstWaveCount = 3;
    [SerializeField, Min(1)] private int _extraEnemiesPerWave = 2;
    [SerializeField, Min(0.1f)] private float _spawnInterval = 1.2f;
    [SerializeField, Min(0f)] private float _intermission = 4f;
    [SerializeField, Range(0f, 1f)] private float _enemyShieldChance = 0.25f;
    [Header("Final Wave")]
    [SerializeField, Min(1)] private int _finalWave = 5;
    [SerializeField] private BossEnemy _bossPrefab;
    [Header("Weighted Pickups")]
    [SerializeField] private PickupEntry[] _pickupTable;
    [SerializeField] private GameObject _ammoPowerupPrefab;
    [SerializeField] private Transform _powerupContainer;
    [SerializeField, Min(0f)] private float _firstPowerupDelay = 1.5f;
    [SerializeField, Min(0.1f)] private float _minimumPickupInterval = 2.5f;
    [SerializeField, Min(0.1f)] private float _maximumPickupInterval = 4f;
    [SerializeField, Min(1)] private int _maximumActivePickups = 4;
    [Header("Camera Placement")]
    [SerializeField] private Camera _spawnCamera;
    [SerializeField, Min(0f)] private float _spawnPadding = 0.25f;

    private Coroutine _spawnRoutine, _powerupRoutine;
    private readonly HashSet<Enemy> _alive = new HashSet<Enemy>();
    private Player _player;
    private UIManager _ui;
    private int _pendingSpawns;
    private bool _running;
    private BossEnemy _boss;
    public bool IsLevelComplete { get; private set; }
    public BossEnemy CurrentBoss => _boss;
    public int CurrentWave { get; private set; }
    public int EnemiesRemaining => _alive.Count + _pendingSpawns;
    public bool IsRunning => _running;

    void Awake()
    {
        if (_spawnCamera == null) _spawnCamera = Camera.main;
        _player = FindAnyObjectByType<Player>();
        _ui = FindAnyObjectByType<UIManager>();
    }

    void Update()
    {
        if (_running && _player == null) StopSpawning();
    }

    public void StartSpawning()
    {
        // The asteroid can only start one pair of spawn loops.
        if (_running || IsLevelComplete || _player == null || _spawnCamera == null || _enemyPrefabs == null || _enemyPrefabs.Length == 0) return;
        _running = true;
        _spawnRoutine = StartCoroutine(WaveRoutine());
        _powerupRoutine = StartCoroutine(PowerupRoutine());
    }

    IEnumerator WaveRoutine()
    {
        yield return new WaitForSeconds(2f);
        while (_running && _player != null)
        {
            CurrentWave++;
            _pendingSpawns = _firstWaveCount + (CurrentWave - 1) * _extraEnemiesPerWave;
            UpdateWaveUI();
            while (_pendingSpawns > 0 && _running && _player != null)
            {
                int unlockedTypes = Mathf.Min(CurrentWave, _enemyPrefabs.Length);
                GameObject prefab = _enemyPrefabs[Random.Range(0, unlockedTypes)];
                if (prefab != null)
                {
                    GameObject instance = SpawnAtTop(prefab, _enemyContainer);
                    Enemy enemy = instance.GetComponent<Enemy>();
                    if (enemy != null)
                    {
                        _alive.Add(enemy);
                        enemy.Configure(this, CurrentWave >= 2 && Random.value < _enemyShieldChance,
                            Mathf.Min(1.35f, 1f + (CurrentWave - 1) * 0.04f));
                    }
                }
                _pendingSpawns--;
                UpdateWaveUI();
                yield return new WaitForSeconds(Mathf.Max(0.5f, _spawnInterval - (CurrentWave - 1) * 0.06f));
            }
            yield return new WaitUntil(() => _alive.Count == 0 || !_running || _player == null);
            if (!_running || _player == null) yield break;
            if (CurrentWave >= _finalWave)
            {
                if (_ui != null) _ui.ShowBossApproaching(CurrentWave);
                yield return new WaitForSeconds(_intermission);
                if (_running && _player != null) SpawnBoss();
                // This is the final wave: never advance to another normal wave.
                yield break;
            }
            if (_ui != null) _ui.UpdateWave(CurrentWave, 0, true);
            yield return new WaitForSeconds(_intermission);
        }
    }

    void SpawnBoss()
    {
        if (_boss != null) return;
        if (_bossPrefab == null)
        {
            Debug.LogError("Assign the Boss prefab on Spawn_manager.", this);
            StopSpawning();
            return;
        }
        float distance = Vector3.Dot(transform.position - _spawnCamera.transform.position, _spawnCamera.transform.forward);
        Vector3 center = _spawnCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
        GameObject instance = SpawnAtTop(_bossPrefab.gameObject, _enemyContainer);
        // Enter vertically above the center, independent of screen aspect ratio.
        instance.transform.position = new Vector3(center.x, instance.transform.position.y, center.z);
        instance.GetComponent<Rigidbody2D>().position = instance.transform.position;
        _boss = instance.GetComponent<BossEnemy>();
        _boss.Configure(this, center);
    }

    public void BossDefeated(BossEnemy boss)
    {
        // A stale callback or simultaneous player death cannot award victory twice.
        if (boss != _boss || !_running || IsLevelComplete || _player == null || (_ui != null && _ui.IsRoundOver)) return;
        IsLevelComplete = true;
        StopSpawning();
        if (_ui != null) _ui.ShowVictory();
    }

    public void EnemyRemoved(Enemy enemy)
    {
        if (_alive.Remove(enemy) && _running) UpdateWaveUI();
    }

    void UpdateWaveUI()
    {
        if (_ui != null) _ui.UpdateWave(CurrentWave, EnemiesRemaining, false);
    }

    IEnumerator PowerupRoutine()
    {
        yield return new WaitForSeconds(_firstPowerupDelay);
        while (_running && _player != null)
        {
            if (FindObjectsByType<TripleShotPowerup>().Length < _maximumActivePickups) SpawnRandomPowerup();
            yield return new WaitForSeconds(Random.Range(_minimumPickupInterval, Mathf.Max(_minimumPickupInterval, _maximumPickupInterval)));
        }
    }

    void SpawnRandomPowerup()
    {
        // Empty weapons get the next available pickup slot as a recovery route.
        if (_player != null && _player.CurrentAmmo == 0 && _ammoPowerupPrefab != null)
        {
            SpawnAtTop(_ammoPowerupPrefab, _powerupContainer);
            return;
        }
        if (_pickupTable == null) return;
        float total = 0f;
        foreach (PickupEntry entry in _pickupTable)
            if (entry != null && entry.Prefab != null) total += Mathf.Max(0f, entry.Weight);
        if (total <= 0f) return;
        float roll = Random.value * total;
        GameObject lastValid = null;
        foreach (PickupEntry entry in _pickupTable)
        {
            if (entry == null || entry.Prefab == null || entry.Weight <= 0f) continue;
            lastValid = entry.Prefab;
            roll -= entry.Weight;
            if (roll < 0f) { SpawnAtTop(entry.Prefab, _powerupContainer); return; }
        }
        // Random.value includes 1, so cover that exact endpoint too.
        if (lastValid != null) SpawnAtTop(lastValid, _powerupContainer);
    }

    GameObject SpawnAtTop(GameObject prefab, Transform container)
    {
        float distance = Vector3.Dot(transform.position - _spawnCamera.transform.position, _spawnCamera.transform.forward);
        Vector3 lower = _spawnCamera.ViewportToWorldPoint(new Vector3(0, 0, distance));
        Vector3 upper = _spawnCamera.ViewportToWorldPoint(new Vector3(1, 1, distance));
        GameObject instance = Instantiate(prefab, transform.position, Quaternion.identity, container);
        SpriteRenderer sprite = instance.GetComponentInChildren<SpriteRenderer>();
        Bounds bounds = sprite != null ? sprite.bounds : new Bounds(instance.transform.position, Vector3.zero);
        Vector3 offset = bounds.center - instance.transform.position;
        float left = lower.x + bounds.extents.x, right = upper.x - bounds.extents.x;
        Vector3 position = new Vector3(left <= right ? Random.Range(left, right) : (lower.x + upper.x) * 0.5f,
            upper.y + bounds.extents.y + _spawnPadding, transform.position.z) - offset;
        instance.transform.position = position;
        Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
        if (body != null) body.position = position;
        return instance;
    }

    public void StopSpawning()
    {
        _running = false;
        if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
        if (_powerupRoutine != null) StopCoroutine(_powerupRoutine);
        _spawnRoutine = null;
        _powerupRoutine = null;
        _pendingSpawns = 0;
    }
    void OnDisable() { StopSpawning(); }
}
