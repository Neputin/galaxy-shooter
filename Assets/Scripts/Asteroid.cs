using System;
using UnityEngine;

public class Asteroid : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Rotation speed in degrees per second. Negative values reverse the direction.")]
    private float _rotationSpeed = 30f;

    [SerializeField] GameObject _explosionPrefab;
    private SpawnManager _spawnManager;
    private bool _isDestroyed;

   private  void Start()
   {
       _spawnManager = GameObject.Find("Spawn_manager").GetComponent<SpawnManager>();
       
   }
    void Update()
    {
        transform.Rotate(0f, 0f, _rotationSpeed * Time.deltaTime);
    }
    // check for laser collision (trigger)
    // instantiate explosion at the asteroid location (the location of the object)
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore extra hits during the asteroid's destruction delay.
        if (_isDestroyed || !other.enabled)
            return;

        if (other.CompareTag("Laser"))
        {
            _isDestroyed = true;
            foreach (Collider2D hitbox in GetComponents<Collider2D>())
                hitbox.enabled = false;
            other.enabled = false;
            Instantiate(_explosionPrefab, transform.position, Quaternion.identity); 
            Destroy(other.gameObject);
            _spawnManager.StartSpawning();
            Destroy(this.gameObject, 0.25f);
            
        }
    }
}