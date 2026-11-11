using UnityEngine;

public class ExplosionBehavior : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayExplosion();

        Destroy(this.gameObject,3f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
