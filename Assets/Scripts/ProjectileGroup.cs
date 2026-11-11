using UnityEngine;

// Removes the volley container after its lasers have hit enemies or expired.
public class ProjectileGroup : MonoBehaviour
{
    void LateUpdate()
    {
        if (transform.childCount == 0)
            Destroy(gameObject);
    }
}
