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

    [SerializeField]
    private int customExtraConnections = 5;

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
        else if (keyboard.f5Key.wasPressedThisFrame)
        {
            LoadConfigFromJSONNow();
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

    [ContextMenu("Aplicar Dificultad por Score (Inmediato)")]
    public void ApplyTargetScore()
    {
        SimulateScoreRequest(targetScore, "Ajuste manual desde menú contextual");
        RegenerateCurrentLevel();
    }

    [ContextMenu("Cargar Perfil 'Easy' por Nombre")]
    public void LoadEasyByName()
    {
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.LoadProfileByName("Easy");
            RegenerateCurrentLevel();
        }
    }

    [ContextMenu("Cargar Perfil 'Normal' por Nombre")]
    public void LoadNormalByName()
    {
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.LoadProfileByName("Normal");
            RegenerateCurrentLevel();
        }
    }

    [ContextMenu("Cargar Perfil 'Hard' por Nombre")]
    public void LoadHardByName()
    {
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.LoadProfileByName("Hard");
            RegenerateCurrentLevel();
        }
    }

    [ContextMenu("📄 Cargar Configuración desde JSON (level_config_request.json)")]
    public void LoadConfigFromJSONNow()
    {
        if (DifficultyManager.Instance != null)
        {
            bool loaded = DifficultyManager.Instance.TryLoadConfigFromJSONFile();
            if (loaded)
            {
                RegenerateCurrentLevel();
            }
        }
    }

    [ContextMenu("🧪 Probar Caso 1: Carga por Perfil 'Normal'")]
    public void TestPayloadCase1()
    {
        string json = "{\"takeDifficultyScore\": false, \"nameLevel\": \"Normal\", \"applyImmediately\": false}";
        LevelLoadConfig config = JsonUtility.FromJson<LevelLoadConfig>(json);
        if (DifficultyManager.Instance != null && config != null)
        {
            DifficultyManager.Instance.ApplyLevelLoadConfig(config);
            RegenerateCurrentLevel();
        }
    }

    [ContextMenu("🧪 Probar Caso 2: Carga por Score '0.3'")]
    public void TestPayloadCase2()
    {
        string json = "{\"takeDifficultyScore\": true, \"difficultyScore\": 0.3, \"applyImmediately\": false}";
        LevelLoadConfig config = JsonUtility.FromJson<LevelLoadConfig>(json);
        if (DifficultyManager.Instance != null && config != null)
        {
            DifficultyManager.Instance.ApplyLevelLoadConfig(config);
            RegenerateCurrentLevel();
        }
    }

    [ContextMenu("🧪 Probar Caso 3: Carga Perfil 'Easy' con Overrides (25x25)")]
    public void TestPayloadCase3()
    {
        string json = "{\"takeDifficultyScore\": false, \"nameLevel\": \"easy\", \"customSettings\": {\"overrideMapWidth\": true, \"mapWidth\": 25, \"overrideMapHeight\": true, \"mapHeight\": 25, \"overrideExtraConnections\": true, \"extraConnections\": 8, \"overridePlayerMoveSpeed\": true, \"playerMoveSpeed\": 8.0}}";
        LevelLoadConfig config = JsonUtility.FromJson<LevelLoadConfig>(json);
        if (DifficultyManager.Instance != null && config != null)
        {
            DifficultyManager.Instance.ApplyLevelLoadConfig(config);
            RegenerateCurrentLevel();
        }
    }

    [ContextMenu("Aumentar Extra Connections (+3) en Configuración Actual")]
    public void RequestExtraConnectionsAdd()
    {
        if (DifficultyManager.Instance == null) return;

        DifficultyAdjustmentRequest request = new DifficultyAdjustmentRequest
        {
            mode = DifficultyAdjustmentMode.Relative,
            overrideExtraConnections = true,
            extraConnections = customExtraConnections,
            reason = "Aumento relativo de extraConnections (+" + customExtraConnections + ")",
            requesterId = "DebugController",
            applyImmediately = true
        };

        DifficultyManager.Instance.ApplyAdjustment(request);
    }

    [ContextMenu("Simular Cambio de Ancho de Mapa (Absoluto)")]
    public void RequestCustomWidth()
    {
        if (DifficultyManager.Instance == null) return;
        
        DifficultyAdjustmentRequest request = new DifficultyAdjustmentRequest
        {
            mode = DifficultyAdjustmentMode.Absolute,
            overrideMapWidth = true,
            mapWidth = customMapWidth,
            reason = "Ajuste manual de ancho de mapa a " + customMapWidth,
            requesterId = "DebugController",
            applyImmediately = true
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
            overrideMissionDestructiblesHealth = true,
            missionDestructiblesHealth = 2,
            reason = "Aumento relativo de vida de destructibles (+2)",
            requesterId = "DebugController",
            applyImmediately = true
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
            requesterId = "DebugController",
            applyImmediately = false
        };

        DifficultyManager.Instance.ApplyAdjustment(request);
    }
}
