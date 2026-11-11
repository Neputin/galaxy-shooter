using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.Canvas))]
public class UIManager : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text _scoreText;
    [SerializeField] private Sprite[] _livesSprites;
    [SerializeField] private UnityEngine.UI.Image _LivesImg;
    [SerializeField] private TMPro.TMP_Text _gameOverText;
    [SerializeField] private TMPro.TMP_Text _restartText;

    [Header("Thruster HUD")]
    [SerializeField] private UnityEngine.UI.Image _thrusterChargeFill;
    [SerializeField] private TMPro.TMP_Text _thrusterChargeText;
    [SerializeField] private TMPro.TMP_Text _thrusterStatusText;
    [SerializeField] private Color _thrusterReadyColor = new Color(0.12f, 0.85f, 1f);
    [SerializeField] private Color _thrusterCoolingColor = new Color(1f, 0.48f, 0.2f);

    [Header("Shield HUD")]
    [SerializeField] private TMPro.TMP_Text _shieldStatusText;
    [SerializeField] private UnityEngine.UI.Image[] _shieldHitIndicators;
    [SerializeField] private Color _shieldStrongColor = new Color(0.12f, 0.85f, 1f);
    [SerializeField] private Color _shieldMediumColor = new Color(1f, 0.75f, 0.15f);
    [SerializeField] private Color _shieldWeakColor = new Color(1f, 0.24f, 0.2f);
    [SerializeField] private Color _shieldEmptyColor = new Color(0.1f, 0.17f, 0.22f);

    [Header("Ammo HUD")]
    [SerializeField] private TMPro.TMP_Text _weaponModeText;
    [SerializeField] private TMPro.TMP_Text _ammoCountText;
    [SerializeField] private TMPro.TMP_Text _ammoStatusText;
    [SerializeField] private UnityEngine.UI.Image _ammoFill;
    [SerializeField] private Color _ammoReadyColor = new Color(0.12f, 0.85f, 1f);
    [SerializeField] private Color _ammoLowColor = new Color(1f, 0.75f, 0.15f);
    [SerializeField] private Color _ammoEmptyColor = new Color(1f, 0.24f, 0.2f);

    [Header("Wave and Player Effects HUD")]
    [SerializeField] private TMPro.TMP_Text _waveText;
    [SerializeField] private TMPro.TMP_Text _effectStatusText;
    [Header("Boss HUD")]
    [SerializeField] private GameObject _bossHud;
    [SerializeField] private UnityEngine.UI.Image _bossHealthFill;
    [SerializeField] private TMPro.TMP_Text _bossHealthText;
    [SerializeField] private TMPro.TMP_Text _bossAttackText;
    private bool _isGameOver;
    private bool _isVictory;
    public bool IsRoundOver => _isGameOver || _isVictory;

    void Awake()
    {
        _isGameOver = false;
        _isVictory = false;
        if (_bossHud != null) _bossHud.SetActive(false);
        UpdateScore(0);
        UpdateThrusters(1f, false, 0f);
        UpdateShield(0, 3);
        UpdateRadialShot(0f);
        UpdateWave(0, 0, false);
        UpdatePlayerEffects(0f);

        if (_gameOverText != null)
            _gameOverText.gameObject.SetActive(false);
        else
            Debug.LogError("Assign the Game Over text to UIManager on the Canvas.", this);

        if (_restartText != null)
            _restartText.gameObject.SetActive(false);
        else
            Debug.LogError("Assign the restart text to UIManager on the Canvas.", this);
    }

    void Update()
    {
        // R restarts only after defeat or after beating the final boss.
        if (!IsRoundOver)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            _isGameOver = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Display the player's total; the enemy decides how many points to award.
    public void UpdateScore(int playerScore)
    {
        if (_scoreText == null)
        {
            Debug.LogError("Assign Score_text_TMP to UIManager on the Canvas.", this);
            return;
        }

        _scoreText.text = $"Score: {playerScore}";
    }

    public void UpdateThrusters(float charge, bool isBoosting, float cooldownRemaining)
    {
        if (_isGameOver)
            return;

        charge = Mathf.Clamp01(charge);
        bool isCoolingDown = cooldownRemaining > 0f;
        Color color = isCoolingDown ? _thrusterCoolingColor : _thrusterReadyColor;

        if (_thrusterChargeFill != null)
        {
            _thrusterChargeFill.fillAmount = charge;
            _thrusterChargeFill.color = color;
        }

        if (_thrusterChargeText != null)
            _thrusterChargeText.text = $"{Mathf.RoundToInt(charge * 100f)}%";

        if (_thrusterStatusText != null)
        {
            _thrusterStatusText.color = color;
            if (isCoolingDown)
                _thrusterStatusText.text = $"COOLING {cooldownRemaining:0.0}s";
            else if (isBoosting)
                _thrusterStatusText.text = "BOOSTING";
            else if (charge < 1f)
                _thrusterStatusText.text = "RECHARGING";
            else
                _thrusterStatusText.text = "READY";
        }
    }

    public void UpdateShield(int remainingHits, int maximumHits)
    {
        if (_isGameOver)
            return;

        maximumHits = Mathf.Max(1, maximumHits);
        remainingHits = Mathf.Clamp(remainingHits, 0, maximumHits);
        Color strengthColor = remainingHits == maximumHits
            ? _shieldStrongColor
            : remainingHits > 1 ? _shieldMediumColor : _shieldWeakColor;

        if (_shieldStatusText != null)
        {
            _shieldStatusText.text = remainingHits > 0
                ? $"SHIELD {remainingHits}/{maximumHits}"
                : "SHIELD OFF";
            _shieldStatusText.color = remainingHits > 0
                ? strengthColor
                : new Color(0.55f, 0.67f, 0.73f);
        }

        if (_shieldHitIndicators != null)
        {
            for (int i = 0; i < _shieldHitIndicators.Length; i++)
            {
                if (_shieldHitIndicators[i] != null)
                    _shieldHitIndicators[i].color = i < remainingHits
                        ? strengthColor
                        : _shieldEmptyColor;
            }
        }
    }

    public void UpdateRadialShot(float remainingSeconds)
    {
        UpdateWeaponMode("RADIAL", remainingSeconds);
    }

    public void UpdateWeaponMode(string mode, float remainingSeconds)
    {
        if (_isGameOver || _weaponModeText == null) return;
        bool active = remainingSeconds > 0f;
        _weaponModeText.text = active ? $"{mode} {remainingSeconds:0.0}s" : "LASER AMMO";
        _weaponModeText.color = active ? new Color(0.83f, 0.55f, 1f) : new Color(0.68f, 0.79f, 0.84f);
    }

    public void UpdateWave(int wave, int remaining, bool intermission)
    {
        if (_isGameOver || _waveText == null) return;
        _waveText.text = wave == 0 ? "DESTROY THE ASTEROID TO START" : intermission
            ? $"WAVE {wave} CLEAR - NEXT WAVE INCOMING"
            : $"WAVE {wave}  |  ENEMIES LEFT: {remaining}";
    }

    public void UpdatePlayerEffects(float slowRemaining)
    {
        if (_isGameOver || _effectStatusText == null) return;
        _effectStatusText.text = slowRemaining > 0f ? $"SLOWED {slowRemaining:0.0}s" : "";
    }

    public void ShowBossApproaching(int wave)
    {
        if (IsRoundOver) return;
        if (_waveText != null) _waveText.text = $"FINAL WAVE {wave}  |  BOSS APPROACHING";
    }

    public void UpdateBoss(int health, int maximum, string attack)
    {
        if (IsRoundOver) return;
        if (_waveText != null && attack != "APPROACHING") _waveText.text = "FINAL WAVE  |  BOSS FIGHT";
        if (_bossHud != null) _bossHud.SetActive(true);
        if (_bossHealthFill != null) _bossHealthFill.fillAmount = Mathf.Clamp01(health / (float)Mathf.Max(1, maximum));
        if (_bossHealthText != null) _bossHealthText.text = $"DREADNOUGHT  {health} / {maximum}";
        if (_bossAttackText != null) _bossAttackText.text = attack;
    }

    public void ShowVictory()
    {
        if (IsRoundOver) return;
        _isVictory = true;
        StopAllCoroutines();
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        if (_gameOverText != null)
        {
            _gameOverText.text = "VICTORY!";
            _gameOverText.gameObject.SetActive(true);
        }
        if (_restartText != null) _restartText.gameObject.SetActive(true);
        // Freeze remaining projectiles/pickups; Update still listens for R.
        Time.timeScale = 0f;
    }

    public void UpdateAmmo(int currentAmmo, int maximumAmmo)
    {
        if (_isGameOver)
            return;

        maximumAmmo = Mathf.Max(1, maximumAmmo);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, maximumAmmo);
        bool isEmpty = currentAmmo == 0;
        bool isLow = currentAmmo <= Mathf.CeilToInt(maximumAmmo * 0.2f);
        Color color = isEmpty ? _ammoEmptyColor : isLow ? _ammoLowColor : _ammoReadyColor;

        if (_ammoCountText != null)
        {
            _ammoCountText.text = $"{currentAmmo} / {maximumAmmo}";
            _ammoCountText.color = color;
        }

        if (_ammoStatusText != null)
        {
            _ammoStatusText.text = isEmpty
                ? "OUT OF AMMO\nCOLLECT AMMO"
                : isLow ? "LOW AMMO" : "READY";
            _ammoStatusText.color = color;
        }

        if (_ammoFill != null)
        {
            _ammoFill.fillAmount = currentAmmo / (float)maximumAmmo;
            _ammoFill.color = color;
        }
    }

    public void UpdateLives(int currentLives)
    {
        if (_isGameOver)
            return;

        // Keep the sprite index valid, even if a caller passes an unexpected value.
        if (_LivesImg != null && _livesSprites != null && _livesSprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp(currentLives, 0, _livesSprites.Length - 1);
            _LivesImg.sprite = _livesSprites[spriteIndex];
        }

        if (currentLives <= 0)
            ShowGameOver();
    }

    void ShowGameOver()
    {
        if (_gameOverText == null)
        {
            Debug.LogError("Assign the Game Over text to UIManager on the Canvas.", this);
            return;
        }

        _isGameOver = true;

        // Keep the Canvas running; hide its other UI children.
        foreach (Transform child in transform)
        {
            if (child != _gameOverText.transform)
                child.gameObject.SetActive(false);
        }

        _gameOverText.gameObject.SetActive(true);
        if (_restartText != null)
            _restartText.gameObject.SetActive(true);
        StartCoroutine(GameOverFlicker());
    }

    IEnumerator GameOverFlicker()
    {
        while (true)
        {
            _gameOverText.text = "Game Over";
            yield return new WaitForSeconds(0.5f);
            _gameOverText.text = ""; 
            yield return new WaitForSeconds(0.5f);


        }
    }
}
