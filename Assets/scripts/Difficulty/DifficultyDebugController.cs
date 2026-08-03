using UnityEngine;
using UnityEngine.InputSystem;

public class DifficultyDebugController : MonoBehaviour
{
    [Header("Perfiles para Cargar")]
    [SerializeField]
    private DifficultyProfile easyProfile;

    [SerializeField]
    private DifficultyProfile normalProfile;

    [SerializeField]
    private DifficultyProfile hardProfile;

    [Header("Ajuste Manual Rápido (Inspector)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float targetScore = 0.5f;

    [Header("Simulación de Parámetro Específico")]
    [SerializeField]
    private int customMapWidth = 19;

#pragma warning disable 0414
    [SerializeField]
    private int customDestructiblesHealth = 3;
#pragma warning restore 0414

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Teclas rápidas de simulación usando el nuevo Input System
        if (keyboard.f1Key.wasPressedThisFrame)
        {
            ApplyProfileOrScore(easyProfile, 0.0f, "Dificultad Fácil (F1)");
        }
        else if (keyboard.f2Key.wasPressedThisFrame)
        {
            ApplyProfileOrScore(normalProfile, 0.5f, "Dificultad Normal (F2)");
        }
        else if (keyboard.f3Key.wasPressedThisFrame)
        {
            ApplyProfileOrScore(hardProfile, 1.0f, "Dificultad Difícil (F3)");
        }
        else if (keyboard.f4Key.wasPressedThisFrame)
        {
            PrintCurrentState();
        }
    }

    private void ApplyProfileOrScore(DifficultyProfile profile, float fallbackScore, string reason)
    {
        if (DifficultyManager.Instance == null) return;

        if (profile != null)
        {
            DifficultyManager.Instance.LoadProfile(profile);
        }
        else
        {
            SimulateScoreRequest(fallbackScore, reason);
        }

        RegenerateCurrentLevel();
    }

    private void RegenerateCurrentLevel()
    {
        DynamicLevelManager levelManager = Object.FindAnyObjectByType<DynamicLevelManager>();
        if (levelManager != null)
        {
            levelManager.StartGeneration();
        }
    }

    [ContextMenu("Aplicar Dificultad por Score")]
    public void ApplyTargetScore()
    {
        SimulateScoreRequest(targetScore, "Ajuste manual desde menú contextual");
        RegenerateCurrentLevel();
    }

    [ContextMenu("Cargar Perfil Fácil")]
    public void LoadEasy()
    {
        ApplyProfileOrScore(easyProfile, 0.0f, "Menú Contextual: Fácil");
    }

    [ContextMenu("Cargar Perfil Normal")]
    public void LoadNormal()
    {
        ApplyProfileOrScore(normalProfile, 0.5f, "Menú Contextual: Normal");
    }

    [ContextMenu("Cargar Perfil Difícil")]
    public void LoadHard()
    {
        ApplyProfileOrScore(hardProfile, 1.0f, "Menú Contextual: Difícil");
    }

    [ContextMenu("Simular Cambio de Ancho de Mapa (Absoluto)")]
    public void RequestCustomWidth()
    {
        if (DifficultyManager.Instance == null) return;
        
        DifficultyAdjustmentRequest request = new DifficultyAdjustmentRequest
        {
            mode = DifficultyAdjustmentMode.Absolute,
            mapWidth = customMapWidth,
            reason = "Ajuste manual de ancho de mapa a " + customMapWidth,
            requesterId = "DebugController"
        };
        
        DifficultyManager.Instance.ApplyAdjustment(request);
    }

    [ContextMenu("Simular Aumento de Destructibles en +2 (Relativo)")]
    public void RequestRelativeDestructibles()
    {
        if (DifficultyManager.Instance == null) return;

        DifficultyAdjustmentRequest request = new DifficultyAdjustmentRequest
        {
            mode = DifficultyAdjustmentMode.Relative,
            missionDestructiblesHealth = 2,
            reason = "Aumento relativo de vida de destructibles (+2)",
            requesterId = "DebugController"
        };

        DifficultyManager.Instance.ApplyAdjustment(request);
    }

    [ContextMenu("Imprimir Historial y Estado en Consola")]
    public void PrintCurrentState()
    {
        if (DifficultyManager.Instance == null) return;

        Debug.Log("=== DIFICULTAD ACTUAL ===");
        Debug.Log(DifficultyManager.Instance.ExportSettingsToJson());

        Debug.Log("=== HISTORIAL DE CAMBIOS ===");
        Debug.Log(DifficultyManager.Instance.ExportHistoryToJson());
    }

    private void SimulateScoreRequest(float score, string reason)
    {
        if (DifficultyManager.Instance == null) return;

        DifficultyAdjustmentRequest request = new DifficultyAdjustmentRequest
        {
            adjustByScore = true,
            targetScore = score,
            reason = reason,
            requesterId = "DebugController"
        };

        DifficultyManager.Instance.ApplyAdjustment(request);
    }
}
