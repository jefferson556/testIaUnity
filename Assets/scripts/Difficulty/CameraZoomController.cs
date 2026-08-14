using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraZoomController : MonoBehaviour
{
    public static CameraZoomController Instance { get; private set; }

    [Header("Cinemachine Camera (Dejar vacío para buscar en la escena)")]
    [SerializeField]
    private CinemachineCamera virtualCamera;

    [Header("Cámara Tradicional (Como fallback)")]
    [SerializeField]
    private Camera fallbackCamera;

    [Header("Velocidad de Transición de Zoom")]
    [SerializeField]
    private float zoomSpeed = 5f;

    // Estados públicos leídos por la interfaz
    public bool IsZoomedOut { get; private set; }
    public float RemainingTime { get; private set; }
    public float CooldownTime { get; private set; }
    public bool IsCoolingDown => CooldownTime > 0f;

    private float currentZoomOutMaxDuration = 4f;
    private float currentZoomOutCooldown = 3f;
    private float currentZoomOutSize = 9f;
    private float currentNormalZoomSize = 4f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (virtualCamera == null && fallbackCamera == null)
        {
            fallbackCamera = Camera.main;
        }

        UpdateSettingsFromDifficulty();

        // Aplicar el zoom inicial
        ApplyZoomSize(currentNormalZoomSize);
    }

    public void UpdateSettingsFromDifficulty()
    {
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.CurrentSettings != null)
        {
            var settings = DifficultyManager.Instance.CurrentSettings;
            currentZoomOutMaxDuration = settings.zoomOutMaxDuration;
            currentZoomOutCooldown = settings.zoomOutCooldown;
            currentZoomOutSize = settings.zoomOutSize;
            currentNormalZoomSize = settings.normalZoomSize;
        }
        else
        {
            currentZoomOutMaxDuration = 4f;
            currentZoomOutCooldown = 3f;
            currentZoomOutSize = 9f;
            currentNormalZoomSize = 4f;
        }

        RemainingTime = currentZoomOutMaxDuration;
        CooldownTime = 0f;
        IsZoomedOut = false;
    }

    private void Update()
    {
        if (virtualCamera == null && fallbackCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
            if (virtualCamera == null) fallbackCamera = Camera.main;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 1. Manejar Cooldown (Enfriamiento)
        if (CooldownTime > 0f)
        {
            CooldownTime -= Time.deltaTime;
            if (CooldownTime < 0f) CooldownTime = 0f;
        }

        // 2. Leer Entrada y Determinar Estado Objetivo (Soporta Shift Izquierdo o Derecho)
        bool wantZoomOut = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        if (wantZoomOut && !IsCoolingDown && RemainingTime > 0f)
        {
            IsZoomedOut = true;
            RemainingTime -= Time.deltaTime;

            if (RemainingTime <= 0f)
            {
                RemainingTime = 0f;
                IsZoomedOut = false;
                CooldownTime = currentZoomOutCooldown; // Forzar Cooldown
            }
        }
        else
        {
            IsZoomedOut = false;
            
            // Recargar el tiempo de zoom poco a poco si no se está usando y no está en cooldown forzado
            if (!IsCoolingDown && RemainingTime < currentZoomOutMaxDuration)
            {
                RemainingTime += Time.deltaTime;
                if (RemainingTime > currentZoomOutMaxDuration)
                {
                    RemainingTime = currentZoomOutMaxDuration;
                }
            }
        }

        // 3. Suavizar tamaño ortográfico de la cámara activa
        float targetSize = IsZoomedOut ? currentZoomOutSize : currentNormalZoomSize;
        float currentSize = GetCurrentZoomSize();
        float newSize = Mathf.Lerp(currentSize, targetSize, Time.deltaTime * zoomSpeed);
        
        ApplyZoomSize(newSize);
    }

    private float GetCurrentZoomSize()
    {
        if (virtualCamera != null)
        {
            return virtualCamera.Lens.OrthographicSize;
        }
        else if (fallbackCamera != null)
        {
            return fallbackCamera.orthographicSize;
        }
        return 4f;
    }

    private void ApplyZoomSize(float size)
    {
        if (virtualCamera != null)
        {
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = size;
            virtualCamera.Lens = lens;
        }
        else if (fallbackCamera != null)
        {
            fallbackCamera.orthographicSize = size;
        }
    }
}
