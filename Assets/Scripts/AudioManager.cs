using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [Header("Sound Effects")]
    [SerializeField] private AudioClip _explosionSound;
    [SerializeField] private AudioClip _laserShotSound;
    [SerializeField] private AudioClip _powerUpSound;

    public static AudioManager Instance { get; private set; }
    private AudioSource _audioSource;

    void Awake()
    {
        Instance = this;
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayExplosion()
    {
        PlaySound(_explosionSound);
    }

    public void PlayLaserShot()
    {
        PlaySound(_laserShotSound);
    }

    public void PlayPowerUp()
    {
        PlaySound(_powerUpSound);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            // One-shot sounds can overlap and finish after their owner disappears.
            _audioSource.PlayOneShot(clip);
        }
    }
}