using System;
using System.Collections.Generic;
using UnityEngine;

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Header("Perfiles y Carga Inicial")]
    [SerializeField]
    private DifficultyProfile defaultProfile;

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

    private int currentLevelNumber = 1;
    private DifficultyMetrics lastLevelMetrics;

    public DifficultySettings CurrentSettings => currentSettings != null ? currentSettings.Clone() : null;
    public float DifficultyScore => difficultyScore;
    public IReadOnlyList<DifficultyHistoryEntry> History => history;

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
        if (defaultProfile != null)
        {
            currentSettings = defaultProfile.Settings.Clone();
            CalculateScoreFromSettings();
            Debug.Log($"[DifficultyManager] Inicializado con el perfil: {defaultProfile.name}. Score: {difficultyScore:F2}");
        }
        else
        {
            // Usar valores predeterminados de las restricciones
            currentSettings = GetSettingsFromScore(0.5f);
            difficultyScore = 0.5f;
            Debug.LogWarning("[DifficultyManager] No se especificó perfil por defecto. Inicializado con dificultad Normal (0.5).");
        }
    }

    public void LoadProfile(DifficultyProfile profile)
    {
        if (profile == null) return;

        DifficultySettings previous = CurrentSettings;
        currentSettings = profile.Settings.Clone();
        CalculateScoreFromSettings();

        // Registrar en el historial
        RegisterHistoryEntry(
            previous,
            currentSettings,
            currentSettings,
            $"Perfil cargado: {profile.name}",
            "System",
            false
        );

        OnDifficultyChanged?.Invoke(CurrentSettings);
        Debug.Log($"[DifficultyManager] Perfil '{profile.name}' cargado con éxito.");
    }

    public bool ApplyAdjustment(DifficultyAdjustmentRequest request)
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
            // Ajustar usando el score general
            float targetScore = Mathf.Clamp01(request.targetScore);
            targetSettings = GetSettingsFromScore(targetScore);
        }
        else
        {
            // Ajustar individualmente
            targetSettings = previous.Clone();
            if (request.mode == DifficultyAdjustmentMode.Absolute)
            {
                if (request.mapWidth > 0) targetSettings.mapWidth = request.mapWidth;
                if (request.mapHeight > 0) targetSettings.mapHeight = request.mapHeight;
                if (request.extraConnections >= 0) targetSettings.extraConnections = request.extraConnections;
                if (request.minPlayerToCaveADistance > 0) targetSettings.minPlayerToCaveADistance = request.minPlayerToCaveADistance;
                if (request.minAxeToStartAndMetaDistance > 0) targetSettings.minAxeToStartAndMetaDistance = request.minAxeToStartAndMetaDistance;
                if (request.destructibleWallsPercentage >= 0) targetSettings.destructibleWallsPercentage = request.destructibleWallsPercentage;
                if (request.missionDestructiblesHealth > 0) targetSettings.missionDestructiblesHealth = request.missionDestructiblesHealth;
                if (request.playerMoveSpeed > 0) targetSettings.playerMoveSpeed = request.playerMoveSpeed;
                if (request.hintDelaySeconds > 0) targetSettings.hintDelaySeconds = request.hintDelaySeconds;
            }
            else // Relative
            {
                targetSettings.mapWidth += request.mapWidth;
                targetSettings.mapHeight += request.mapHeight;
                targetSettings.extraConnections += request.extraConnections;
                targetSettings.minPlayerToCaveADistance += request.minPlayerToCaveADistance;
                targetSettings.minAxeToStartAndMetaDistance += request.minAxeToStartAndMetaDistance;
                targetSettings.destructibleWallsPercentage += request.destructibleWallsPercentage;
                targetSettings.missionDestructiblesHealth += request.missionDestructiblesHealth;
                targetSettings.playerMoveSpeed += request.playerMoveSpeed;
                targetSettings.hintDelaySeconds += request.hintDelaySeconds;
            }
        }

        // 1. Validar la configuración solicitada
        List<string> errors;
        if (!ValidateSettings(targetSettings, out errors))
        {
            string errorMessage = $"Solicitud rechazada. Configuración inválida: {string.Join(", ", errors)}";
            Debug.LogError($"[DifficultyManager] {errorMessage}");
            OnAdjustmentRejected?.Invoke(errorMessage);
            return false;
        }

        // 2. Limitar cambios bruscos y aplicar restricciones (Clamp)
        DifficultySettings clampedSettings = LimitChanges(previous, targetSettings);

        // Comprobar si hubo valores limitados
        bool wasClamped = CheckIfClamped(targetSettings, clampedSettings);

        // 3. Aplicar configuración final
        currentSettings = clampedSettings;
        CalculateScoreFromSettings();

        // 4. Registrar en historial
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

        // 1. Dimensiones del mapa
        if (settings.mapWidth < 5 || settings.mapHeight < 5)
        {
            errors.Add("El mapa es demasiado pequeño (debe ser al menos 5x5).");
        }

        // 2. Distancia máxima física del mapa
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

        // 3. Área libre requerida vs tamaño del mapa
        int totalArea = settings.mapWidth * settings.mapHeight;
        int estimatedWalkable = totalArea / 2;
        int requiredArea = 1 + (settings.axeZoneSize.x * settings.axeZoneSize.y) + 4; // spawn + axeZone + cave entrances + key + goal
        
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

    private DifficultySettings LimitChanges(DifficultySettings prev, DifficultySettings req)
    {
        DifficultySettings result = req.Clone();

        result.mapWidth = constraints.mapWidth.Clamp(prev.mapWidth, req.mapWidth);
        if (result.mapWidth % 2 == 0) result.mapWidth++; // Mantener impar para el algoritmo de laberinto

        result.mapHeight = constraints.mapHeight.Clamp(prev.mapHeight, req.mapHeight);
        if (result.mapHeight % 2 == 0) result.mapHeight++; // Mantener impar

        result.extraConnections = constraints.extraConnections.Clamp(prev.extraConnections, req.extraConnections);
        result.minPlayerToCaveADistance = constraints.minPlayerToCaveADistance.Clamp(prev.minPlayerToCaveADistance, req.minPlayerToCaveADistance);
        result.minAxeToStartAndMetaDistance = constraints.minAxeToStartAndMetaDistance.Clamp(prev.minAxeToStartAndMetaDistance, req.minAxeToStartAndMetaDistance);
        result.minKeyToAxeDistance = constraints.minKeyToAxeDistance.Clamp(prev.minKeyToAxeDistance, req.minKeyToAxeDistance);
        result.minKeyToMetaDistance = constraints.minKeyToMetaDistance.Clamp(prev.minKeyToMetaDistance, req.minKeyToMetaDistance);
        result.minPlayerToMetaDistance = constraints.minPlayerToMetaDistance.Clamp(prev.minPlayerToMetaDistance, req.minPlayerToMetaDistance);
        
        result.minimumPathDistanceBetweenTravelCaves = constraints.minimumPathDistanceBetweenTravelCaves.Clamp(prev.minimumPathDistanceBetweenTravelCaves, req.minimumPathDistanceBetweenTravelCaves);
        result.minimumShortcutSaving = constraints.minimumShortcutSaving.Clamp(prev.minimumShortcutSaving, req.minimumShortcutSaving);
        result.travelCavePairs = constraints.travelCavePairs.Clamp(prev.travelCavePairs, req.travelCavePairs);
        
        result.axeZoneSize = new Vector2Int(
            constraints.axeZoneSizeX.Clamp(prev.axeZoneSize.x, req.axeZoneSize.x),
            constraints.axeZoneSizeY.Clamp(prev.axeZoneSize.y, req.axeZoneSize.y)
        );

        result.destructibleWallsPercentage = constraints.destructibleWallsPercentage.Clamp(prev.destructibleWallsPercentage, req.destructibleWallsPercentage);
        result.missionDestructiblesHealth = constraints.missionDestructiblesHealth.Clamp(prev.missionDestructiblesHealth, req.missionDestructiblesHealth);
        
        result.playerMoveSpeed = constraints.playerMoveSpeed.Clamp(prev.playerMoveSpeed, req.playerMoveSpeed);
        result.hintsAvailable = constraints.hintsAvailable.Clamp(prev.hintsAvailable, req.hintsAvailable);
        result.hintDelaySeconds = constraints.hintDelaySeconds.Clamp(prev.hintDelaySeconds, req.hintDelaySeconds);
        result.hintIntensity = constraints.hintIntensity.Clamp(prev.hintIntensity, req.hintIntensity);

        result.zoomOutMaxDuration = constraints.zoomOutMaxDuration.Clamp(prev.zoomOutMaxDuration, req.zoomOutMaxDuration);
        result.zoomOutCooldown = constraints.zoomOutCooldown.Clamp(prev.zoomOutCooldown, req.zoomOutCooldown);
        result.zoomOutSize = constraints.zoomOutSize.Clamp(prev.zoomOutSize, req.zoomOutSize);
        result.normalZoomSize = constraints.normalZoomSize.Clamp(prev.normalZoomSize, req.normalZoomSize);

        return result;
    }

    private bool CheckIfClamped(DifficultySettings target, DifficultySettings clamped)
    {
        return target.mapWidth != clamped.mapWidth ||
               target.mapHeight != clamped.mapHeight ||
               target.extraConnections != clamped.extraConnections ||
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

        // extraConnections: más conexiones = laberinto más abierto y fácil. Invertimos dirección.
        settings.extraConnections = Mathf.RoundToInt(Mathf.Lerp(constraints.extraConnections.maximum, constraints.extraConnections.minimum, score));

        settings.minPlayerToCaveADistance = Mathf.Lerp(constraints.minPlayerToCaveADistance.minimum, constraints.minPlayerToCaveADistance.maximum, score);
        settings.minAxeToStartAndMetaDistance = Mathf.Lerp(constraints.minAxeToStartAndMetaDistance.minimum, constraints.minAxeToStartAndMetaDistance.maximum, score);
        settings.minKeyToAxeDistance = Mathf.Lerp(constraints.minKeyToAxeDistance.minimum, constraints.minKeyToAxeDistance.maximum, score);
        settings.minKeyToMetaDistance = Mathf.Lerp(constraints.minKeyToMetaDistance.minimum, constraints.minKeyToMetaDistance.maximum, score);
        settings.minPlayerToMetaDistance = Mathf.Lerp(constraints.minPlayerToMetaDistance.minimum, constraints.minPlayerToMetaDistance.maximum, score);

        settings.enableTravelCaves = score < 0.8f;
        settings.minimumPathDistanceBetweenTravelCaves = Mathf.RoundToInt(Mathf.Lerp(constraints.minimumPathDistanceBetweenTravelCaves.minimum, constraints.minimumPathDistanceBetweenTravelCaves.maximum, score));
        settings.minimumShortcutSaving = Mathf.RoundToInt(Mathf.Lerp(constraints.minimumShortcutSaving.minimum, constraints.minimumShortcutSaving.maximum, score));
        settings.travelCavePairs = Mathf.RoundToInt(Mathf.Lerp(constraints.travelCavePairs.minimum, constraints.travelCavePairs.maximum, score));
        
        settings.axeZoneSize = new Vector2Int(
            Mathf.RoundToInt(Mathf.Lerp(constraints.axeZoneSizeX.minimum, constraints.axeZoneSizeX.maximum, score)),
            Mathf.RoundToInt(Mathf.Lerp(constraints.axeZoneSizeY.minimum, constraints.axeZoneSizeY.maximum, score))
        );

        settings.destructibleWallsPercentage = Mathf.Lerp(constraints.destructibleWallsPercentage.minimum, constraints.destructibleWallsPercentage.maximum, score);
        settings.missionDestructiblesHealth = Mathf.RoundToInt(Mathf.Lerp(constraints.missionDestructiblesHealth.minimum, constraints.missionDestructiblesHealth.maximum, score));
        settings.spawnDestructibles = true;

        // playerMoveSpeed: más rápido = más fácil. Invertimos dirección.
        settings.playerMoveSpeed = Mathf.Lerp(constraints.playerMoveSpeed.maximum, constraints.playerMoveSpeed.minimum, score);

        // hintsAvailable: más pistas = más fácil. Invertimos dirección.
        settings.hintsAvailable = Mathf.RoundToInt(Mathf.Lerp(constraints.hintsAvailable.maximum, constraints.hintsAvailable.minimum, score));
        
        settings.hintDelaySeconds = Mathf.Lerp(constraints.hintDelaySeconds.minimum, constraints.hintDelaySeconds.maximum, score);
        
        // hintIntensity: menos intenso = más difícil. Invertimos dirección.
        settings.hintIntensity = Mathf.Lerp(constraints.hintIntensity.maximum, constraints.hintIntensity.minimum, score);

        settings.highlightObjectives = score < 0.6f;
        settings.showDirectionIndicator = score < 0.5f;

        // zoomOutMaxDuration: más tiempo = más fácil. Invertimos dirección.
        settings.zoomOutMaxDuration = Mathf.Lerp(constraints.zoomOutMaxDuration.maximum, constraints.zoomOutMaxDuration.minimum, score);
        
        // zoomOutCooldown: más cooldown = más difícil.
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

        // Promedio ponderado o interpolación simple del tamaño de mapa y velocidad para estimar el score de vuelta
        float widthNorm = Mathf.InverseLerp(constraints.mapWidth.minimum, constraints.mapWidth.maximum, currentSettings.mapWidth);
        float heightNorm = Mathf.InverseLerp(constraints.mapHeight.minimum, constraints.mapHeight.maximum, currentSettings.mapHeight);
        float speedNorm = Mathf.InverseLerp(constraints.playerMoveSpeed.maximum, constraints.playerMoveSpeed.minimum, currentSettings.playerMoveSpeed); // invertido
        
        difficultyScore = (widthNorm + heightNorm + speedNorm) / 3f;
    }

    public void RegisterLevelCompletion(DifficultyMetrics metrics)
    {
        lastLevelMetrics = metrics;
        Debug.Log($"[DifficultyManager] Métricas del nivel {currentLevelNumber} registradas.");
        
        // Aumentar el número de nivel para la siguiente generación
        currentLevelNumber++;
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

    // Métodos para convertir datos a JSON para persistencia futura
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
