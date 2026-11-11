using UnityEngine;

public class RadialShot : MonoBehaviour
{
    [SerializeField] private Bullet _projectilePrefab;
    [SerializeField, Min(1)] private int _projectileCount = 8;
    [SerializeField, Min(0f)] private float _spawnRadius = 0.3f;

    void Awake()
    {
        if (_projectilePrefab != null)
        {
            int count = Mathf.Max(1, _projectileCount);
            float angleStep = 360f / count;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, 0f, i * angleStep) * Vector3.up;
                Bullet projectile = Instantiate(
                    _projectilePrefab,
                    transform.position + direction * _spawnRadius,
                    Quaternion.identity
                );
                projectile.Launch(direction);
            }
        }
        else
        {
            Debug.LogError("Assign the radial projectile prefab.", this);
        }

        // The bolts live independently and use Bullet's normal lifetime/cleanup.
        Destroy(gameObject);
    }
}