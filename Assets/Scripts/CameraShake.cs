using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _duration = 0.18f;
    [SerializeField, Min(0f)] private float _strength = 0.045f;
    [SerializeField, Min(1f)] private float _frequency = 40f;

    private Camera _camera;
    private float _remainingSeconds;
    private Vector3 _offset;
    private Vector3 _positionBeforeRender;
    private bool _offsetApplied;

    public bool IsShaking => _remainingSeconds > 0f;

    void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    public void Shake()
    {
        if (!isActiveAndEnabled)
            return;

        // Repeated damage refreshes the effect instead of stacking the strength.
        _remainingSeconds = Mathf.Max(0.01f, _duration);
    }

    void LateUpdate()
    {
        _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.unscaledDeltaTime);
        if (!IsShaking)
        {
            _offset = Vector3.zero;
            return;
        }

        float phase = Time.unscaledTime * _frequency;
        Vector2 noise = new Vector2(
            Mathf.PerlinNoise(phase, 0.17f) * 2f - 1f,
            Mathf.PerlinNoise(0.73f, phase) * 2f - 1f
        );
        noise = Vector2.ClampMagnitude(noise, 1f);
        float fade = _remainingSeconds / Mathf.Max(0.01f, _duration);
        _offset = new Vector3(noise.x, noise.y, 0f) * (_strength * fade);
    }

    void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera || _offsetApplied || _offset.sqrMagnitude == 0f)
            return;

        // Shake only the rendered view. Gameplay camera bounds stay unchanged.
        _positionBeforeRender = transform.localPosition;
        transform.localPosition = _positionBeforeRender + _offset;
        _offsetApplied = true;
    }

    void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _camera)
            RestorePosition();
    }

    void RestorePosition()
    {
        if (!_offsetApplied)
            return;

        transform.localPosition = _positionBeforeRender;
        _offsetApplied = false;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestorePosition();
        _remainingSeconds = 0f;
        _offset = Vector3.zero;
    }
}