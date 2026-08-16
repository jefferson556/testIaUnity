using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Header("Perfiles y Carga Inicial")]
    [SerializeField]
    private DifficultyProfile defaultProfile;

    [Tooltip("Lista de perfiles de dificultad disponibles para selección (Fácil, Normal, Difícil, etc.)")]
    [SerializeField]
    private List<DifficultyProfile> availableProfiles = new List<DifficultyProfile>();

    [SerializeField]
    private DifficultyConstraints constraints = new DifficultyConstraints();

    [Header("Configuración Activa")]
    [SerializeField]
    private DifficultySettings currentSettings;

    [SerializeField]
    [Range(0f, 1f)]
    private float difficultyScore = 0.5f;

    [Header("Historial de Cambios")]
    [SerializeField]
    private List<DifficultyHistoryEntry> history = new List<DifficultyHistoryEntry>();

    [Header("Control de Sesión")]
    [Tooltip("Activa o desactiva el límite de niveles por sesión antes de recargar la escena principal.")]
    public bool enableSessionLimit = true;

    [Tooltip("Cantidad máxima de niveles por sesión antes de volver a la escena principal.")]
    public int maxLevelsPerSession = 4;

    [Tooltip("Nombre de la escena a cargar al terminar la sesión.")]
    public string endSessionSceneName = "SampleScene";

    private int currentLevelNumber = 1;
    private DifficultyMetrics lastLevelMetrics;

    public string CurrentDifficultyName { get; private set; } = "Custom";
    public DifficultySettings CurrentSettings => currentSettings != null ? currentSettings.Clone() : null;
    public float DifficultyScore => difficultyScore;
    public IReadOnlyList<DifficultyHistoryEntry> History => history;
    public IReadOnlyList<DifficultyProfile> AvailableProfiles => availableProfiles;

    // Eventos
    public event Action<DifficultySettings> OnDifficultyChanged;
    public event Action<DifficultyHistoryEntry> OnAdjustmentApplied;
    public event Action<string> OnAdjustmentRejected;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDifficulty();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDifficulty()
    {
        if (defaultProfile == null && availableProfiles != null && availableProfiles.Count > 0)
        {
            defaultProfile = availableProfiles[0];
        }

        if (defaultProfile != null)
        {
            currentSettings = defaultProfile.Settings.Clone();
            CurrentDifficultyName = defaultProfile.ProfileName;
            CalculateScoreFromSettings();
            Debug.Log($"[DifficultyManager] Inicializado con el perfil: {defaultProfile.ProfileName}. Score: {difficultyScore:F2}");
        }
        else
        {
            currentSettings = GetSettingsFromScore(0.5f);
            difficultyScore = 0.5f;
            Debug.LogWarning("[DifficultyManager] No se especificó perfil por defecto. Inicializado con dificultad Normal (0.5).");
        }
    }

    /// <summary>
    /// Intenta leer la configuración enviada externamente desde el archivo JSON level_config_request.json.
    /// </summary>
    public bool TryLoadConfigFromJSONFile()
    {
        try
        {
            string folderPath = Path.Combine(Application.dataPath, "MetricsLogs");
            string filePath = Path.Combine(folderPath, "level_config_request.json");

            if (!File.Exists(filePath))
            {
                // Fallback a persistentDataPath para builds ejecutables fuera del editor
                filePath = Path.Combine(Application.persistentDataPath, "level_config_request.json");
            }

            if (!File.Exists(filePath))
            {
                return false;
            }

            string jsonContent = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return false;
            }

            LevelLoadConfig config = JsonUtility.FromJson<LevelLoadConfig>(jsonContent);
            if (config != null)
            {
                Debug.Log($"[DifficultyManager] 📄 Leyendo y aplicando configuración desde archivo JSON: {filePath}");
                bool result = ApplyLevelLoadConfig(config);

                if (result)
                {
                    Debug.Log($"[DifficultyManager] 🎯 CONFIGURACIÓN APLICADA DESDE JSON:\n" +
                              $"  ➜ Perfil Base: '{config.nameLevel}' (takeDifficultyScore={config.takeDifficultyScore})\n" +
                              $"  ➜ mapWidth: {currentSettings.mapWidth}\n" +
                              $"  ➜ mapHeight: {currentSettings.mapHeight}\n" +
                              $"  ➜ extraConnections: {currentSettings.extraConnections}\n" +
                              $"  ➜ playerMoveSpeed: {currentSettings.playerMoveSpeed:F1}\n" +
                              $"  ➜ destructibleWallsPercentage: {currentSettings.destructibleWallsPercentage:P0}");
                }

                return result;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DifficultyManager] Error al procesar el archivo JSON de configuración de nivel: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Aplica la configuración dual de nivel (Por Score vs Por Nombre de Perfil) y luego sus overrides personalizados.
    /// </summary>
    public bool ApplyLevelLoadConfig(LevelLoadConfig config)
    {
        if (config == null) return false;

        if (config.customSettings != null && config.customSettings.overrideSessionSettings)
        {
            this.enableSessionLimit = config.customSettings.enableSessionLimit;
            this.maxLevelsPerSession = config.customSettings.maxLevelsPerSession;
            Debug.Log($"[DifficultyManager] Control de Sesión JSON APLICADO: Habilitado={this.enableSessionLimit}, Máx Niveles={this.maxLevelsPerSession}");
        }

        bool baseApplied = false;

        // 1. Establecer la Dificultad Base (Por Score o Por Nombre)
        if (config.takeDifficultyScore)
        {
            SetDifficultyScore(config.difficultyScore, $"Carga Dual JSON por Score ({config.difficultyScore:F2})", enforceRateLimit: false);
            baseApplied = true;
        }
        else
        {
            baseApplied = LoadProfileByName(config.nameLevel);
            if (!baseApplied)
            {
                Debug.LogWarning($"[DifficultyManager] No se pudo cargar la base por nombre '{config.nameLevel}'. Manteniendo configuración actual.");
            }
        }

        // 2. Aplicar Ajustes Personalizados (si vienen en customSettings)
        if (config.customSettings != null)
        {
            config.customSettings.reason = $"Ajustes personalizados desde JSON sobre base (Score={config.takeDifficultyScore}, Name='{config.nameLevel}')";
            config.customSettings.requesterId = "JSON_Config";
            ApplyAdjustment(config.customSettings, enforceRateLimit: false);
        }

        if (config.applyImmediately)
        {
            DynamicLevelManager levelManager = UnityEngine.Object.FindAnyObjectByType<DynamicLevelManager>();
            if (levelManager != null)
            {
                levelManager.StartGeneration();
            }
        }

        return true;
    }

    public void SetDifficultyScore(float targetScore, string reason = "Score change request", bool enforceRateLimit = true)
    {
        DifficultyAdjustmentRequest request = new DifficultyAdjustmentRequest
        {
            adjustByScore = true,
            targetScore = Mathf.Clamp01(targetScore),
            reason = reason,
            requesterId = "ScoreManager"
        };
        ApplyAdjustment(request, enforceRateLimit);
    }

    public bool LoadProfileByName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return false;

        DifficultyProfile profile = availableProfiles.Find(p => p != null && string.Equals(p.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
        
        if (profile == null)
        {
            profile = availableProfiles.Find(p => p != null && string.Equals(p.name, profileName, StringComparison.OrdinalIgnoreCase));
        }

        if (profile == null && defaultProfile != null && (string.Equals(defaultProfile.ProfileName, profileName, StringComparison.OrdinalIgnoreCase) || string.Equals(defaultProfile.name, profileName, StringComparison.OrdinalIgnoreCase)))
        {
            profile = defaultProfile;
        }

#if UNITY_EDITOR
        if (profile == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DifficultyProfile");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                DifficultyProfile p = UnityEditor.AssetDatabase.LoadAssetAtPath<DifficultyProfile>(path);
                if (p != null && (string.Equals(p.ProfileName, profileName, StringComparison.OrdinalIgnoreCase) || string.Equals(p.name, profileName, StringComparison.OrdinalIgnoreCase)))
                {
                    profile = p;
                    if (!availableProfiles.Contains(p)) availableProfiles.Add(p);
                    break;
                }
            }
        }
#endif

        if (profile != null)
        {
            LoadProfile(profile);
            return true;
        }

        Debug.LogWarning($"[DifficultyManager] No se encontró el perfil de dificultad con el nombre: '{profileName}'");
        return false;
    }

    public void LoadProfile(DifficultyProfile profile)
    {
        if (profile == null) return;

        DifficultySettings previous = CurrentSettings;
        currentSettings = profile.Settings.Clone();
        CurrentDifficultyName = profile.ProfileName;
        CalculateScoreFromSettings();

        RegisterHistoryEntry(
            previous,
            currentSettings,
            currentSettings,
            $"Perfil cargado: {profile.ProfileName}",
            "System",
            false
        );

        OnDifficultyChanged?.Invoke(CurrentSettings);
        Debug.Log($"[DifficultyManager] Perfil '{profile.ProfileName}' cargado con éxito.");
    }

    public bool ModifyCurrentSettings(Action<DifficultySettings> modifyAction, string reason = "Custom modification")
    {
        if (modifyAction == null) return false;

        DifficultySettings previous = CurrentSettings;
        DifficultySettings target = previous.Clone();
        modifyAction(target);

        List<string> errors;
        if (!ValidateSettings(target, out errors))
        {
            string errorMessage = $"Modificación rechazada. Configuración inválida: {string.Join(", ", errors)}";
            Debug.LogError($"[DifficultyManager] {errorMessage}");
            OnAdjustmentRejected?.Invoke(errorMessage);
            return false;
        }

        DifficultySettings clampedSettings = LimitChanges(previous, target, enforceRateLimit: false);
        bool wasClamped = CheckIfClamped(target, clampedSettings);

        currentSettings = clampedSettings;
        CalculateScoreFromSettings();

        var entry = RegisterHistoryEntry(previous, target, currentSettings, reason, "CustomModify", wasClamped);
        OnDifficultyChanged?.Invoke(CurrentSettings);
        OnAdjustmentApplied?.Invoke(entry);

        Debug.Log($"[DifficultyManager] Modificación directa aplicada con éxito. Score actual: {difficultyScore:F2}. Razón: {reason}");
        return true;
    }

    public bool ApplyAdjustment(DifficultyAdjustmentRequest request, bool enforceRateLimit = false)
    {
        if (request == null)
        {
            OnAdjustmentRejected?.Invoke("La solicitud es nula.");
            return false;
        }

        DifficultySettings previous = CurrentSettings;
        DifficultySettings targetSettings = null;

        if (request.adjustByScore)
        {
            float targetScore = Mathf.Clamp01(request.targetScore);
            targetSettings = GetSettingsFromScore(targetScore);
        }
        else
        {
            targetSettings = previous.Clone();
            if (request.mode == DifficultyAdjustmentMode.Absolute)
            {
                if (request.overrideMapWidth || request.mapWidth > 0) targetSettings.mapWidth = request.mapWidth;
                if (request.overrideMapHeight || request.mapHeight > 0) targetSettings.mapHeight = request.mapHeight;
                if (request.overrideExtraConnections || request.extraConnections != 0) targetSettings.extraConnections = request.extraConnections;
                if (request.overridePlayerMoveSpeed || request.playerMoveSpeed > 0) targetSettings.playerMoveSpeed = request.playerMoveSpeed;
                if (request.overrideDestructibleWallsPercentage || request.destructibleWallsPercentage > 0) targetSettings.destructibleWallsPercentage = request.destructibleWallsPercentage;
                if (request.overrideMissionDestructiblesHealth || request.missionDestructiblesHealth > 0) targetSettings.missionDestructiblesHealth = request.missionDestructiblesHealth;

                if (request.overrideMinPlayerToCaveADistance || request.minPlayerToCaveADistance > 0) targetSettings.minPlayerToCaveADistance = request.minPlayerToCaveADistance;
                if (request.overrideMinAxeToStartAndMetaDistance || request.minAxeToStartAndMetaDistance > 0) targetSettings.minAxeToStartAndMetaDistance = request.minAxeToStartAndMetaDistance;
                if (request.overrideMinKeyToAxeDistance || request.minKeyToAxeDistance > 0) targetSettings.minKeyToAxeDistance = request.minKeyToAxeDistance;
                if (request.overrideMinKeyToMetaDistance || request.minKeyToMetaDistance > 0) targetSettings.minKeyToMetaDistance = request.minKeyToMetaDistance;
                if (request.overrideMinPlayerToMetaDistance || request.minPlayerToMetaDistance > 0) targetSettings.minPlayerToMetaDistance = request.minPlayerToMetaDistance;

                if (request.overrideEnableTravelCaves) targetSettings.enableTravelCaves = request.enableTravelCaves;
                if (request.overrideMaximumTravelCavePairs || request.maximumTravelCavePairs > 0) targetSettings.maximumTravelCavePairs = request.maximumTravelCavePairs;
                if (request.overrideAxeZoneSize && request.axeZoneSize != Vector2Int.zero) targetSettings.axeZoneSize = request.axeZoneSize;

                // [FUTURO / HINTS]
                if (request.overrideHintsAvailable || request.hintsAvailable > 0) targetSettings.hintsAvailable = request.hintsAvailable;
                if (request.overrideHintDelaySeconds || request.hintDelaySeconds > 0) targetSettings.hintDelaySeconds = request.hintDelaySeconds;
            }
            else // Relative
            {
                if (request.overrideMapWidth || request.mapWidth != 0) targetSettings.mapWidth += request.mapWidth;
                if (request.overrideMapHeight || request.mapHeight != 0) targetSettings.mapHeight += request.mapHeight;
                if (request.overrideExtraConnections || request.extraConnections != 0) targetSettings.extraConnections += request.extraConnections;
                if (request.overridePlayerMoveSpeed || request.playerMoveSpeed != 0) targetSettings.playerMoveSpeed += request.playerMoveSpeed;
                if (request.overrideDestructibleWallsPercentage || request.destructibleWallsPercentage != 0) targetSettings.destructibleWallsPercentage += request.destructibleWallsPercentage;
                if (request.overrideMissionDestructiblesHealth || request.missionDestructiblesHealth != 0) targetSettings.missionDestructiblesHealth += request.missionDestructiblesHealth;

                if (request.overrideMinPlayerToCaveADistance || request.minPlayerToCaveADistance != 0) targetSettings.minPlayerToCaveADistance += request.minPlayerToCaveADistance;
                if (request.overrideMinAxeToStartAndMetaDistance || request.minAxeToStartAndMetaDistance != 0) targetSettings.minAxeToStartAndMetaDistance += request.minAxeToStartAndMetaDistance;
                if (request.overrideMinKeyToAxeDistance || request.minKeyToAxeDistance != 0) targetSettings.minKeyToAxeDistance += request.minKeyToAxeDistance;
                if (request.overrideMinKeyToMetaDistance || request.minKeyToMetaDistance != 0) targetSettings.minKeyToMetaDistance += request.minKeyToMetaDistance;
                if (request.overrideMinPlayerToMetaDistance || request.minPlayerToMetaDistance != 0) targetSettings.minPlayerToMetaDistance += request.minPlayerToMetaDistance;

                if (request.overrideMaximumTravelCavePairs || request.maximumTravelCavePairs != 0) targetSettings.maximumTravelCavePairs += request.maximumTravelCavePairs;

                // [FUTURO / HINTS]
                if (request.overrideHintsAvailable || request.hintsAvailable != 0) targetSettings.hintsAvailable += request.hintsAvailable;
                if (request.overrideHintDelaySeconds || request.hintDelaySeconds != 0) targetSettings.hintDelaySeconds += request.hintDelaySeconds;
            }
        }

        List<string> errors;
        if (!ValidateSettings(targetSettings, out errors))
        {
            string errorMessage = $"Solicitud rechazada. Configuración inválida: {string.Join(", ", errors)}";
            Debug.LogError($"[DifficultyManager] {errorMessage}");
            OnAdjustmentRejected?.Invoke(errorMessage);
            return false;
        }

        DifficultySettings clampedSettings = LimitChanges(previous, targetSettings, enforceRateLimit);
        bool wasClamped = CheckIfClamped(targetSettings, clampedSettings);

        currentSettings = clampedSettings;
        CalculateScoreFromSettings();

        var entry = RegisterHistoryEntry(
            previous,
            targetSettings,
            currentSettings,
            request.reason,
            request.requesterId,
            wasClamped
        );

        OnDifficultyChanged?.Invoke(CurrentSettings);
        OnAdjustmentApplied?.Invoke(entry);

        Debug.Log($"[DifficultyManager] Ajuste aplicado con éxito. Score actual: {difficultyScore:F2}. Razón: {request.reason}");

        if (request.applyImmediately)
        {
            DynamicLevelManager levelManager = UnityEngine.Object.FindAnyObjectByType<DynamicLevelManager>();
            if (levelManager != null)
            {
                levelManager.StartGeneration();
            }
        }

        return true;
    }

    public bool ValidateSettings(DifficultySettings settings, out List<string> errors)
    {
        errors = new List<string>();

        if (settings == null)
        {
            errors.Add("La configuración a validar es nula.");
            return false;
        }

        if (settings.mapWidth < 5 || settings.mapHeight < 5)
        {
            errors.Add("El mapa es demasiado pequeño (debe ser al menos 5x5).");
        }

        int maxGridDistance = (settings.mapWidth - 2) + (settings.mapHeight - 2);

        if (settings.minPlayerToCaveADistance > maxGridDistance)
        {
            errors.Add($"La distancia jugador-Cueva A ({settings.minPlayerToCaveADistance}) excede el tamaño del mapa ({maxGridDistance}).");
        }

        if (settings.minAxeToStartAndMetaDistance > maxGridDistance)
        {
            errors.Add($"La distancia del Hacha ({settings.minAxeToStartAndMetaDistance}) excede el tamaño del mapa ({maxGridDistance}).");
        }

        if (settings.minPlayerToMetaDistance > maxGridDistance)
        {
            errors.Add($"La distancia del Jugador a la Meta ({settings.minPlayerToMetaDistance}) excede el tamaño del mapa ({maxGridDistance}).");
        }

        int totalArea = settings.mapWidth * settings.mapHeight;
        int estimatedWalkable = totalArea / 2;
        int requiredArea = 1 + (settings.axeZoneSize.x * settings.axeZoneSize.y) + 4;
        
        if (requiredArea >= estimatedWalkable)
        {
            errors.Add($"El mapa de {settings.mapWidth}x{settings.mapHeight} es demasiado pequeño para acomodar las zonas y objetivos obligatorios.");
        }

        if (settings.destructibleWallsPercentage > 0.8f)
        {
            errors.Add("Advertencia: El porcentaje de destructibles decorativos es mayor al 80%. Esto podría abrir o colapsar demasiado el mapa.");
        }

        return errors.Count == 0;
    }

    private DifficultySettings LimitChanges(DifficultySettings prev, DifficultySettings req, bool enforceRateLimit = true)
    {
        DifficultySettings result = req.Clone();

        result.mapWidth = constraints.mapWidth.Clamp(prev.mapWidth, req.mapWidth, enforceRateLimit);
        if (result.mapWidth % 2 == 0) result.mapWidth++;

        result.mapHeight = constraints.mapHeight.Clamp(prev.mapHeight, req.mapHeight, enforceRateLimit);
        if (result.mapHeight % 2 == 0) result.mapHeight++;

        result.extraConnections = constraints.extraConnections.Clamp(prev.extraConnections, req.extraConnections, enforceRateLimit);
        result.maxTimeLimitInSeconds = constraints.maxTimeLimitInSeconds.Clamp(prev.maxTimeLimitInSeconds, req.maxTimeLimitInSeconds, enforceRateLimit);
        result.minPlayerToCaveADistance = constraints.minPlayerToCaveADistance.Clamp(prev.minPlayerToCaveADistance, req.minPlayerToCaveADistance, enforceRateLimit);
        result.minAxeToStartAndMetaDistance = constraints.minAxeToStartAndMetaDistance.Clamp(prev.minAxeToStartAndMetaDistance, req.minAxeToStartAndMetaDistance, enforceRateLimit);
        result.minKeyToAxeDistance = constraints.minKeyToAxeDistance.Clamp(prev.minKeyToAxeDistance, req.minKeyToAxeDistance, enforceRateLimit);
        result.minKeyToMetaDistance = constraints.minKeyToMetaDistance.Clamp(prev.minKeyToMetaDistance, req.minKeyToMetaDistance, enforceRateLimit);
        result.minPlayerToMetaDistance = constraints.minPlayerToMetaDistance.Clamp(prev.minPlayerToMetaDistance, req.minPlayerToMetaDistance, enforceRateLimit);
        
        result.minimumPathDistanceBetweenTravelCaves = constraints.minimumPathDistanceBetweenTravelCaves.Clamp(prev.minimumPathDistanceBetweenTravelCaves, req.minimumPathDistanceBetweenTravelCaves, enforceRateLimit);
        result.minimumShortcutSaving = constraints.minimumShortcutSaving.Clamp(prev.minimumShortcutSaving, req.minimumShortcutSaving, enforceRateLimit);
        result.maximumTravelCavePairs = constraints.maximumTravelCavePairs.Clamp(prev.maximumTravelCavePairs, req.maximumTravelCavePairs, enforceRateLimit);
        
        result.axeZoneSize = new Vector2Int(
            constraints.axeZoneSizeX.Clamp(prev.axeZoneSize.x, req.axeZoneSize.x, enforceRateLimit),
            constraints.axeZoneSizeY.Clamp(prev.axeZoneSize.y, req.axeZoneSize.y, enforceRateLimit)
        );

        result.destructibleWallsPercentage = constraints.destructibleWallsPercentage.Clamp(prev.destructibleWallsPercentage, req.destructibleWallsPercentage, enforceRateLimit);
        result.missionDestructiblesHealth = constraints.missionDestructiblesHealth.Clamp(prev.missionDestructiblesHealth, req.missionDestructiblesHealth, enforceRateLimit);
        
        result.playerMoveSpeed = constraints.playerMoveSpeed.Clamp(prev.playerMoveSpeed, req.playerMoveSpeed, enforceRateLimit);

        // [FUTURO / HINTS]
        result.hintsAvailable = constraints.hintsAvailable.Clamp(prev.hintsAvailable, req.hintsAvailable, enforceRateLimit);
        result.hintDelaySeconds = constraints.hintDelaySeconds.Clamp(prev.hintDelaySeconds, req.hintDelaySeconds, enforceRateLimit);
        result.hintIntensity = constraints.hintIntensity.Clamp(prev.hintIntensity, req.hintIntensity, enforceRateLimit);

        result.zoomOutMaxDuration = constraints.zoomOutMaxDuration.Clamp(prev.zoomOutMaxDuration, req.zoomOutMaxDuration, enforceRateLimit);
        result.zoomOutCooldown = constraints.zoomOutCooldown.Clamp(prev.zoomOutCooldown, req.zoomOutCooldown, enforceRateLimit);
        result.zoomOutSize = constraints.zoomOutSize.Clamp(prev.zoomOutSize, req.zoomOutSize, enforceRateLimit);
        result.normalZoomSize = constraints.normalZoomSize.Clamp(prev.normalZoomSize, req.normalZoomSize, enforceRateLimit);

        return result;
    }

    private bool CheckIfClamped(DifficultySettings target, DifficultySettings clamped)
    {
        return target.mapWidth != clamped.mapWidth ||
               target.mapHeight != clamped.mapHeight ||
               target.extraConnections != clamped.extraConnections ||
               Mathf.Abs(target.maxTimeLimitInSeconds - clamped.maxTimeLimitInSeconds) > 0.01f ||
               Mathf.Abs(target.minPlayerToCaveADistance - clamped.minPlayerToCaveADistance) > 0.01f ||
               Mathf.Abs(target.minAxeToStartAndMetaDistance - clamped.minAxeToStartAndMetaDistance) > 0.01f ||
               Mathf.Abs(target.playerMoveSpeed - clamped.playerMoveSpeed) > 0.01f ||
               target.missionDestructiblesHealth != clamped.missionDestructiblesHealth ||
               Mathf.Abs(target.zoomOutMaxDuration - clamped.zoomOutMaxDuration) > 0.01f ||
               Mathf.Abs(target.zoomOutCooldown - clamped.zoomOutCooldown) > 0.01f;
    }

    public DifficultySettings GetSettingsFromScore(float score)
    {
        DifficultySettings settings = new DifficultySettings();
        
        settings.mapWidth = Mathf.RoundToInt(Mathf.Lerp(constraints.mapWidth.minimum, constraints.mapWidth.maximum, score));
        if (settings.mapWidth % 2 == 0) settings.mapWidth++;
        
        settings.mapHeight = Mathf.RoundToInt(Mathf.Lerp(constraints.mapHeight.minimum, constraints.mapHeight.maximum, score));
        if (settings.mapHeight % 2 == 0) settings.mapHeight++;

        settings.extraConnections = Mathf.RoundToInt(Mathf.Lerp(constraints.extraConnections.maximum, constraints.extraConnections.minimum, score));

        settings.minPlayerToCaveADistance = Mathf.Lerp(constraints.minPlayerToCaveADistance.minimum, constraints.minPlayerToCaveADistance.maximum, score);
        settings.minAxeToStartAndMetaDistance = Mathf.Lerp(constraints.minAxeToStartAndMetaDistance.minimum, constraints.minAxeToStartAndMetaDistance.maximum, score);
        settings.minKeyToAxeDistance = Mathf.Lerp(constraints.minKeyToAxeDistance.minimum, constraints.minKeyToAxeDistance.maximum, score);
        settings.minKeyToMetaDistance = Mathf.Lerp(constraints.minKeyToMetaDistance.minimum, constraints.minKeyToMetaDistance.maximum, score);
        settings.minPlayerToMetaDistance = Mathf.Lerp(constraints.minPlayerToMetaDistance.minimum, constraints.minPlayerToMetaDistance.maximum, score);

        settings.enableTravelCaves = true;
        settings.minimumPathDistanceBetweenTravelCaves = Mathf.RoundToInt(Mathf.Lerp(constraints.minimumPathDistanceBetweenTravelCaves.minimum, constraints.minimumPathDistanceBetweenTravelCaves.maximum, score));
        settings.minimumShortcutSaving = Mathf.RoundToInt(Mathf.Lerp(constraints.minimumShortcutSaving.minimum, constraints.minimumShortcutSaving.maximum, score));
        settings.maximumTravelCavePairs = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(constraints.maximumTravelCavePairs.minimum, constraints.maximumTravelCavePairs.maximum, score)));
        
        settings.axeZoneSize = new Vector2Int(
            Mathf.RoundToInt(Mathf.Lerp(constraints.axeZoneSizeX.minimum, constraints.axeZoneSizeX.maximum, score)),
            Mathf.RoundToInt(Mathf.Lerp(constraints.axeZoneSizeY.minimum, constraints.axeZoneSizeY.maximum, score))
        );

        settings.destructibleWallsPercentage = Mathf.Lerp(constraints.destructibleWallsPercentage.minimum, constraints.destructibleWallsPercentage.maximum, score);
        settings.missionDestructiblesHealth = Mathf.RoundToInt(Mathf.Lerp(constraints.missionDestructiblesHealth.minimum, constraints.missionDestructiblesHealth.maximum, score));
        settings.spawnDestructibles = true;

        settings.maxTimeLimitInSeconds = Mathf.Lerp(constraints.maxTimeLimitInSeconds.maximum, constraints.maxTimeLimitInSeconds.minimum, score);
        settings.playerMoveSpeed = Mathf.Lerp(constraints.playerMoveSpeed.minimum, constraints.playerMoveSpeed.maximum, score);

        // [FUTURO / HINTS]
        settings.hintsAvailable = Mathf.RoundToInt(Mathf.Lerp(constraints.hintsAvailable.maximum, constraints.hintsAvailable.minimum, score));
        settings.hintDelaySeconds = Mathf.Lerp(constraints.hintDelaySeconds.minimum, constraints.hintDelaySeconds.maximum, score);
        settings.hintIntensity = Mathf.Lerp(constraints.hintIntensity.maximum, constraints.hintIntensity.minimum, score);

        settings.highlightObjectives = score < 0.6f;
        settings.showDirectionIndicator = score < 0.5f;

        settings.zoomOutMaxDuration = Mathf.Lerp(constraints.zoomOutMaxDuration.maximum, constraints.zoomOutMaxDuration.minimum, score);
        settings.zoomOutCooldown = Mathf.Lerp(constraints.zoomOutCooldown.minimum, constraints.zoomOutCooldown.maximum, score);
        settings.zoomOutSize = Mathf.Lerp(constraints.zoomOutSize.minimum, constraints.zoomOutSize.maximum, score);
        settings.normalZoomSize = Mathf.Lerp(constraints.normalZoomSize.minimum, constraints.normalZoomSize.maximum, score);

        return settings;
    }

    private void CalculateScoreFromSettings()
    {
        if (currentSettings == null)
        {
            difficultyScore = 0.5f;
            return;
        }

        float widthNorm = Mathf.InverseLerp(constraints.mapWidth.minimum, constraints.mapWidth.maximum, currentSettings.mapWidth);
        float heightNorm = Mathf.InverseLerp(constraints.mapHeight.minimum, constraints.mapHeight.maximum, currentSettings.mapHeight);
        float speedNorm = Mathf.InverseLerp(constraints.playerMoveSpeed.minimum, constraints.playerMoveSpeed.maximum, currentSettings.playerMoveSpeed);
        float timeNorm = Mathf.InverseLerp(constraints.maxTimeLimitInSeconds.maximum, constraints.maxTimeLimitInSeconds.minimum, currentSettings.maxTimeLimitInSeconds);
        
        difficultyScore = Mathf.Clamp01((widthNorm + heightNorm + speedNorm + timeNorm) / 4f);
    }

    public void RegisterLevelEnd(DifficultyMetrics metrics)
    {
        lastLevelMetrics = metrics;

        DifficultySettings current = CurrentSettings != null ? CurrentSettings : new DifficultySettings();

        RegisterHistoryEntry(
            current,
            current,
            current,
            $"Nivel {currentLevelNumber} finalizado. Éxito: {metrics.levelCompleted}",
            "Player",
            false
        );

        SaveMetricsToCsvFile(metrics, current);

        Debug.Log($"[DifficultyManager] Métricas del nivel {currentLevelNumber} registradas y guardadas en CSV.");
        
        // Notificar al adaptador de dificultad para procesar métricas y solicitar la siguiente decisión DDA
        if (DifficultyAdapterAgent.Instance != null && DifficultyAdapterAgent.Instance.enabled)
        {
            DifficultyAdapterAgent.Instance.RequestDecisionForNextLevel(metrics);
        }

        currentLevelNumber++;
        
        if (enableSessionLimit && currentLevelNumber > maxLevelsPerSession)
        {
            Debug.Log($"[DifficultyManager] Límite de sesión alcanzado ({maxLevelsPerSession} niveles). Regresando a la escena: {endSessionSceneName}");
            currentLevelNumber = 1; // Reiniciar contador
            SceneManager.LoadScene(endSessionSceneName);
        }
    }

    private void SaveMetricsToCsvFile(DifficultyMetrics metrics, DifficultySettings settings)
    {
        try
        {
            string folderPath = Path.Combine(Application.dataPath, "MetricsLogs");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, "metrics_history_all.csv");
            bool fileExists = File.Exists(filePath);

            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                if (!fileExists || new FileInfo(filePath).Length == 0)
                {
                    string header = "episodeId,agentVersion,difficultyScore,mapWidth,mapHeight,extraConnections,maxTimeLimitInSeconds,maxEpisodeSteps,episodeStepCount,missionDestructiblesHealth,destructibleWallsPercentage,minAxeToStartAndMetaDistance,minKeyToAxeDistance,minKeyToMetaDistance,playerMoveSpeed,hintsAvailable,totalLevelTime,axeCollected,timeToFindAxe,keyCollected,timeToFindKey,movementCount,idleCount,destructibleHits,failedHitsWithoutAxe,cavesUsed,explorationPercentage,hintsUsed,restartCount,errorCount,keyToGoalPathDataValid,keyToGoalTime,keyToGoalActualDistance,keyToGoalOptimalDistance,keyToGoalExtraDistance,keyToGoalRepeatedCells,keyToGoalRepeatedCellRatio,keyToGoalEfficiency,keyToGoalNavigationState,keyToGoalUsefulCaveUses,keyToGoalNeutralCaveUses,keyToGoalUnproductiveCaveUses,levelCompleted,terminationReason";
                    writer.WriteLine(header);
                }

                var culture = System.Globalization.CultureInfo.InvariantCulture;
                
                string s_timeToFindAxe = metrics.axeCollected ? metrics.timeToFindAxe.ToString(culture) : "";
                string s_timeToFindKey = metrics.keyCollected ? metrics.timeToFindKey.ToString(culture) : "";
                
                bool pValid = metrics.keyToGoal.keyToGoalPathDataValid;
                string s_kgTime = pValid ? metrics.keyToGoal.keyToGoalTime.ToString(culture) : "";
                string s_kgOptDist = pValid ? metrics.keyToGoal.keyToGoalOptimalDistance.ToString(culture) : "";
                string s_kgExtDist = pValid ? metrics.keyToGoal.keyToGoalExtraDistance.ToString(culture) : "";
                string s_kgEff = pValid ? metrics.keyToGoal.keyToGoalEfficiency.ToString(culture) : "";
                string s_kgNav = pValid ? metrics.keyToGoal.keyToGoalNavigationState.ToString() : "";
                
                string s_episodeStepCount = metrics.agentVersion == "Human" ? "" : metrics.episodeStepCount.ToString(culture);
                string s_maxEpisodeSteps = metrics.agentVersion == "Human" ? "" : metrics.maxEpisodeSteps.ToString(culture);

                string[] fields = new string[]
                {
                    metrics.episodeId,
                    metrics.agentVersion,
                    difficultyScore.ToString(culture),
                    settings.mapWidth.ToString(culture),
                    settings.mapHeight.ToString(culture),
                    settings.extraConnections.ToString(culture),
                    metrics.maxTimeLimitInSeconds.ToString(culture),
                    s_maxEpisodeSteps,
                    s_episodeStepCount,
                    settings.missionDestructiblesHealth.ToString(culture),
                    settings.destructibleWallsPercentage.ToString(culture),
                    settings.minAxeToStartAndMetaDistance.ToString(culture),
                    settings.minKeyToAxeDistance.ToString(culture),
                    settings.minKeyToMetaDistance.ToString(culture),
                    settings.playerMoveSpeed.ToString(culture),
                    settings.hintsAvailable.ToString(culture),
                    
                    metrics.totalLevelTime.ToString(culture),
                    
                    metrics.axeCollected.ToString(),
                    s_timeToFindAxe,
                    
                    metrics.keyCollected.ToString(),
                    s_timeToFindKey,
                    
                    metrics.movementCount.ToString(culture),
                    metrics.idleCount.ToString(culture),
                    metrics.destructibleHits.ToString(culture),
                    metrics.failedHitsWithoutAxe.ToString(culture),
                    metrics.cavesUsed.ToString(culture),
                    metrics.explorationPercentage.ToString(culture),
                    metrics.hintsUsed.ToString(culture),
                    metrics.restartCount.ToString(culture),
                    metrics.errorCount.ToString(culture),
                    
                    metrics.keyToGoal.keyToGoalPathDataValid.ToString(),
                    s_kgTime,
                    metrics.keyToGoal.keyToGoalActualDistance.ToString(culture),
                    s_kgOptDist,
                    s_kgExtDist,
                    metrics.keyToGoal.keyToGoalRepeatedCells.ToString(culture),
                    metrics.keyToGoal.keyToGoalRepeatedCellRatio.ToString(culture),
                    s_kgEff,
                    s_kgNav,
                    metrics.keyToGoal.keyToGoalUsefulCaveUses.ToString(culture),
                    metrics.keyToGoal.keyToGoalNeutralCaveUses.ToString(culture),
                    metrics.keyToGoal.keyToGoalUnproductiveCaveUses.ToString(culture),
                    
                    metrics.levelCompleted.ToString(),
                    metrics.terminationReason ?? "OTHER"
                };

                writer.WriteLine(string.Join(",", fields));
            }

            Debug.Log($"[DifficultyManager] 📄 CSV de métricas guardado exitosamente en: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DifficultyManager] Error al guardar el archivo CSV de métricas: {ex.Message}");
        }
    }

    private DifficultyHistoryEntry RegisterHistoryEntry(
        DifficultySettings prev,
        DifficultySettings requested,
        DifficultySettings applied,
        string reason,
        string origin,
        bool wasClamped)
    {
        DifficultyHistoryEntry entry = new DifficultyHistoryEntry
        {
            levelNumber = currentLevelNumber,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            previousSettingsJson = JsonUtility.ToJson(prev),
            requestedSettingsJson = JsonUtility.ToJson(requested),
            appliedSettingsJson = JsonUtility.ToJson(applied),
            reason = reason,
            origin = origin,
            metricsJson = lastLevelMetrics != null ? JsonUtility.ToJson(lastLevelMetrics) : "{}",
            valuesWereClamped = wasClamped
        };

        history.Add(entry);
        return entry;
    }

    public string ExportHistoryToJson()
    {
        return JsonUtility.ToJson(new HistoryWrapper { entries = history }, true);
    }

    public string ExportSettingsToJson()
    {
        return JsonUtility.ToJson(currentSettings, true);
    }

    [Serializable]
    private class HistoryWrapper
    {
        public List<DifficultyHistoryEntry> entries;
    }
}

[Serializable]
public class LevelMetricsDataFile
{
    public string levelName;
    public string difficultyName;
    public int levelNumber;
    public string timestamp;
    public float difficultyScore;
    public DifficultySettings appliedSettings;
    public DifficultyMetrics metrics;
}
