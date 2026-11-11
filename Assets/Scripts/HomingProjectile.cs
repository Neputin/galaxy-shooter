using UnityEngine;

[RequireComponent(typeof(Bullet))]
public class HomingProjectile : MonoBehaviour
{
    [SerializeField, Min(1f)] private float _turnSpeed = 360f;
    [SerializeField, Min(0.02f)] private float _searchInterval = 0.15f;
    private Bullet _bullet;
    private Enemy _target;
    private BossEnemy _bossTarget;
    private float _nextSearch;
    public Enemy Target => _target;
    public BossEnemy BossTarget => _bossTarget;

    void Awake() { _bullet = GetComponent<Bullet>(); }
    void FixedUpdate()
    {
        if (_bullet.IsConsumed) return;
        bool validTarget = (_target != null && _target.IsTargetable) || (_bossTarget != null && _bossTarget.IsTargetable);
        if (!validTarget || Time.time >= _nextSearch)
        {
            _target = null;
            _bossTarget = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Enemy enemy in FindObjectsByType<Enemy>())
            {
                if (!enemy.IsTargetable) continue;
                float distance = (enemy.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                _target = enemy;
            }
            foreach (BossEnemy boss in FindObjectsByType<BossEnemy>())
            {
                if (!boss.IsTargetable) continue;
                float distance = (boss.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                _target = null;
                _bossTarget = boss;
            }
            _nextSearch = Time.time + _searchInterval;
        }
        Vector3 direction = transform.up;
        Transform target = _bossTarget != null ? _bossTarget.transform : _target != null ? _target.transform : null;
        if (target != null)
            direction = Vector3.RotateTowards(direction, (target.position - transform.position).normalized,
                _turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        _bullet.Launch(direction);
    }
}
