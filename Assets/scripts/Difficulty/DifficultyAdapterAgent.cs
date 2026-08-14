using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using System.IO;

/// <summary>
/// Agente de ML-Agents encargado de adaptar la dificultad del laberinto para el siguiente nivel.
/// No controla al personaje; toma una decisión puntual al finalizar cada nivel N para configurar el nivel N+1.
/// 
/// Configuración requerida en Unity Inspector (Behavior Parameters):
/// - Behavior Name: DifficultyAdapter
/// - Vector Observation Size: 12
/// - Actions: Discrete Branches = 2
///     - Branch 0 Size: 3 (Map Size: DECREASE, KEEP, INCREASE)
///     - Branch 1 Size: 3 (Extra Connections: DECREASE, KEEP, INCREASE)
/// </summary>
[AddComponentMenu("Difficulty/Difficulty Adapter Agent")]
public class DifficultyAdapterAgent : Agent
{
    public static DifficultyAdapterAgent Instance { get; private set; }

    protected DifficultyMetrics lastMetrics;

    protected override void Awake()
    {
        base.Awake();
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        BehaviorParameters bp = GetComponent<BehaviorParameters>();
        if (bp != null)
        {
            bp.BehaviorName = "DifficultyAdapter";
            bp.BrainParameters.VectorObservationSize = 12;
            bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(3, 3);
            Debug.Log("[DifficultyAdapterAgent] ⚙️ BehaviorParameters configurado: BehaviorName='DifficultyAdapter', ObsSize=12, Branches=[3, 3]");
        }

        Debug.Log("[DifficultyAdapterAgent] 🚀 Agente inicializado correctamente.");
    }

    /// <summary>
    /// Invoca la toma de decisiones para configurar el siguiente nivel N+1
    /// basándose en las métricas obtenidas del nivel N que acaba de finalizar.
    /// </summary>
    public virtual void RequestDecisionForNextLevel(DifficultyMetrics metrics)
    {
        lastMetrics = metrics;

        if (lastMetrics == null)
        {
            Debug.LogWarning("[DifficultyAdapterAgent] ⚠️ RequestDecisionForNextLevel invocado con métricas nulas.");
        }
        else
        {
            // FASE 7: Calcular e impartir Recompensa (Reward) antes de finalizar el episodio para PPO
            float levelReward = CalculateReward(lastMetrics);
            AddReward(levelReward);

            string rewardLog = $"[DifficultyAdapterAgent] 🏆 Recompensa calculada para Nivel N: {levelReward:F2} (Acumulada: {GetCumulativeReward():F2}) | Éxito: {lastMetrics.levelCompleted}, Razón: {lastMetrics.terminationReason}";
            Debug.Log(rewardLog);
            GameLogger.LogAdapter(rewardLog);
        }

        // Finalizar el episodio anterior para ML-Agents (para que registre la reward acumulada)
        EndEpisode();

        // Solicitar la nueva decisión a ML-Agents para el nivel N+1
        Debug.Log("[DifficultyAdapterAgent] 🧠 Solicitando decisión a ML-Agents para la adaptación del nivel N+1...");
        RequestDecision();
    }

    /// <summary>
    /// FASE 7: Función de Recompensa (Reward) equilibrada para DDA.
    /// Premia mantener al jugador en una zona de balance ideal (~40-60% del tiempo límite).
    /// </summary>
    private float CalculateReward(DifficultyMetrics m)
    {
        if (m == null) return 0f;

        float reward = 0f;

        // 1. Resultado Base (Éxito vs Fallo con gradiente de progreso)
        if (!m.levelCompleted && m.terminationReason != "GOAL")
        {
            // Falló el nivel: penalización progresiva según la fase alcanzada
            float progressStage = 0f;
            if (m.axeCollected && m.keyCollected) progressStage = 0.66f;
            else if (m.axeCollected) progressStage = 0.33f;

            // Penalización entre -1.0 (no encontró hacha) y -0.4 (llegó cerca de la meta)
            reward = -1.0f + (progressStage * 0.9f);
        }
        else
        {
            // Completó el nivel exitosamente
            reward = 1.0f;

            // 2. Evaluación de Balance de Tiempo
            float timeRatio = (m.maxTimeLimitInSeconds > 0f) 
                ? Mathf.Clamp01(m.totalLevelTime / m.maxTimeLimitInSeconds) 
                : 0.5f;

            if (timeRatio < 0.25f)
            {
                // Demasiado Fácil: penalización por terminar excesivamente rápido (< 25% del tiempo)
                float easyPenalty = -0.5f * (1.0f - (timeRatio / 0.25f));
                reward += easyPenalty;
            }
            else if (timeRatio >= 0.25f && timeRatio <= 0.75f)
            {
                // Zona Ideal / Balanceada: bonificación tipo campana con pico en 50% del tiempo
                float balanceBonus = 0.5f * (1.0f - Mathf.Abs(timeRatio - 0.5f) * 2f);
                reward += balanceBonus;
            }
            else if (timeRatio > 0.85f)
            {
                // Demasiado Difícil: rozó el límite de tiempo
                reward -= 0.2f;
            }
        }

        return reward;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // ── 0. Determinar progreso del nivel ──
        float progressStage = 0.0f;
        if (lastMetrics != null)
        {
            if (lastMetrics.levelCompleted || lastMetrics.terminationReason == "GOAL")
            {
                progressStage = 1.0f;
            }
            else if (lastMetrics.axeCollected && lastMetrics.keyCollected)
            {
                progressStage = 0.66f;
            }
            else if (lastMetrics.axeCollected)
            {
                progressStage = 0.33f;
            }
            else
            {
                progressStage = 0.0f;
            }
        }

        // ── 1. Proporción del tiempo usado ──
        float timeUsageRatio = (lastMetrics != null && lastMetrics.maxTimeLimitInSeconds > 0f)
            ? Mathf.Clamp01(lastMetrics.totalLevelTime / lastMetrics.maxTimeLimitInSeconds)
            : 1.0f;

        // ── 2. Proporción tiempo Spawn -> Hacha ──
        float axeTimeRatio = (lastMetrics != null && lastMetrics.axeCollected && lastMetrics.totalLevelTime > 0f)
            ? Mathf.Clamp01(lastMetrics.timeToFindAxe / lastMetrics.totalLevelTime)
            : 0.0f;

        // ── 3. Proporción tiempo Hacha -> Llave ──
        float axeToKeyTimeRatio = (lastMetrics != null && lastMetrics.axeCollected && lastMetrics.keyCollected && lastMetrics.totalLevelTime > 0f)
            ? Mathf.Clamp01((lastMetrics.timeToFindKey - lastMetrics.timeToFindAxe) / lastMetrics.totalLevelTime)
            : 0.0f;

        // ── 4. Porcentaje de exploración ──
        float explorationPercentage = (lastMetrics != null)
            ? Mathf.Clamp01(lastMetrics.explorationPercentage)
            : 0.0f;

        // ── 5. Eficiencia de navegación Llave -> Meta ──
        bool isPathValid = (lastMetrics != null && lastMetrics.keyToGoal != null && lastMetrics.keyToGoal.keyToGoalPathDataValid);
        float keyToGoalEfficiency = isPathValid
            ? Mathf.Clamp01(lastMetrics.keyToGoal.keyToGoalEfficiency)
            : 0.0f;

        // ── 6. Ratio de celdas repetidas Llave -> Meta ──
        float keyToGoalRepeatedCellRatio = isPathValid
            ? Mathf.Clamp01(lastMetrics.keyToGoal.keyToGoalRepeatedCellRatio)
            : 0.0f;

        // ── 7. Validez de la ruta Llave -> Meta ──
        float keyToGoalPathDataValid = isPathValid ? 1.0f : 0.0f;

        // ── 8. Terminado por Timeout ──
        float endedByTimeout = (lastMetrics != null && lastMetrics.terminationReason == "TIMEOUT") ? 1.0f : 0.0f;

        // ── 9. Proporción de pasos usados ──
        float stepUsageRatio = (lastMetrics != null && lastMetrics.maxEpisodeSteps > 0)
            ? Mathf.Clamp01((float)lastMetrics.episodeStepCount / lastMetrics.maxEpisodeSteps)
            : 0.0f;

        // ── 10. Configuración actual: Tamaño de Mapa Normalizado (9 a 35) ──
        DifficultySettings settings = DifficultyManager.Instance != null 
            ? DifficultyManager.Instance.CurrentSettings 
            : new DifficultySettings();
        float mapSizeNormalized = Mathf.InverseLerp(9f, 35f, settings.mapWidth);

        // ── 11. Configuración actual: Conexiones Extra Normalizadas (0 a 20) ──
        float extraConnectionsNormalized = Mathf.InverseLerp(0f, 20f, settings.extraConnections);

        // Añadir las 12 observaciones vectoriales al sensor
        sensor.AddObservation(progressStage);               // 0
        sensor.AddObservation(timeUsageRatio);               // 1
        sensor.AddObservation(axeTimeRatio);                 // 2
        sensor.AddObservation(axeToKeyTimeRatio);            // 3
        sensor.AddObservation(explorationPercentage);        // 4
        sensor.AddObservation(keyToGoalEfficiency);          // 5
        sensor.AddObservation(keyToGoalRepeatedCellRatio);    // 6
        sensor.AddObservation(keyToGoalPathDataValid);       // 7
        sensor.AddObservation(endedByTimeout);               // 8
        sensor.AddObservation(stepUsageRatio);               // 9
        sensor.AddObservation(mapSizeNormalized);            // 10
        sensor.AddObservation(extraConnectionsNormalized);   // 11

        // Imprimir resumen detallado de observaciones en Consola y GameLogger
        string obsLog = $"[DifficultyAdapterAgent] 👁️ Observaciones V1 recolectadas (Total: 12):\n" +
            $"  0. progressStage: {progressStage:F2}\n" +
            $"  1. timeUsageRatio: {timeUsageRatio:F2} ({lastMetrics?.totalLevelTime:F1}s / {lastMetrics?.maxTimeLimitInSeconds:F1}s)\n" +
            $"  2. axeTimeRatio: {axeTimeRatio:F2} (Encontrada: {lastMetrics?.axeCollected})\n" +
            $"  3. axeToKeyTimeRatio: {axeToKeyTimeRatio:F2} (Encontrada: {lastMetrics?.keyCollected})\n" +
            $"  4. explorationPercentage: {explorationPercentage:P0}\n" +
            $"  5. keyToGoalEfficiency: {keyToGoalEfficiency:P0} (Válido: {isPathValid})\n" +
            $"  6. keyToGoalRepeatedCellRatio: {keyToGoalRepeatedCellRatio:P0}\n" +
            $"  7. keyToGoalPathDataValid: {keyToGoalPathDataValid}\n" +
            $"  8. endedByTimeout: {endedByTimeout} (Reason: {lastMetrics?.terminationReason})\n" +
            $"  9. stepUsageRatio: {stepUsageRatio:F2} ({lastMetrics?.episodeStepCount} / {lastMetrics?.maxEpisodeSteps})\n" +
            $"  10. mapSizeNormalized: {mapSizeNormalized:F2} (Ancho: {settings.mapWidth})\n" +
            $"  11. extraConnectionsNormalized: {extraConnectionsNormalized:F2} (Conexiones: {settings.extraConnections})";

        Debug.Log(obsLog);
        GameLogger.LogAdapter(obsLog);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Obtener las acciones discretas de las 2 ramas
        int actionMapSize = actions.DiscreteActions[0];       // 0 = DECREASE, 1 = KEEP, 2 = INCREASE
        int actionExtraConn = actions.DiscreteActions[1];     // 0 = DECREASE, 1 = KEEP, 2 = INCREASE

        string strMapSize = ActionToString(actionMapSize);
        string strExtraConn = ActionToString(actionExtraConn);

        string actionLog = $"[DifficultyAdapterAgent] 🎯 Acciones Recibidas -> Map Size: {strMapSize} | Extra Connections: {strExtraConn}";
        Debug.Log(actionLog);
        GameLogger.LogAdapter(actionLog);

        // ── Evaluar Dirección de Adaptación (Feedback para PPO) ──
        if (lastMetrics != null)
        {
            float adaptationReward = 0f;

            if (!lastMetrics.levelCompleted && lastMetrics.terminationReason != "GOAL")
            {
                // CASO FALLO: El nivel fue demasiado difícil o no se completó.
                // 1. Map Size: Reducir (DECREASE) es la decisión correcta (+0.3). Aumentar (INCREASE) es castigado (-0.5).
                if (actionMapSize == 0) adaptationReward += 0.3f;
                else if (actionMapSize == 2) adaptationReward -= 0.5f;

                // 2. Extra Connections: Aumentar (INCREASE) abre atajos (+0.2). Reducir (DECREASE) es castigado (-0.3).
                if (actionExtraConn == 2) adaptationReward += 0.2f;
                else if (actionExtraConn == 0) adaptationReward -= 0.3f;
            }
            else
            {
                // CASO ÉXITO: El nivel se completó.
                float timeRatio = (lastMetrics.maxTimeLimitInSeconds > 0f)
                    ? Mathf.Clamp01(lastMetrics.totalLevelTime / lastMetrics.maxTimeLimitInSeconds)
                    : 0.5f;

                if (timeRatio < 0.25f)
                {
                    // Ganó demasiado fácil: Aumentar MapSize (+0.3) es correcto. Reducir MapSize (-0.5) es castigado.
                    if (actionMapSize == 2) adaptationReward += 0.3f;
                    else if (actionMapSize == 0) adaptationReward -= 0.5f;
                }
                else if (timeRatio >= 0.25f && timeRatio <= 0.75f)
                {
                    // Zona Ideal: Mantener (KEEP) premia (+0.3).
                    if (actionMapSize == 1) adaptationReward += 0.2f;
                    if (actionExtraConn == 1) adaptationReward += 0.1f;
                }
            }

            if (adaptationReward != 0f)
            {
                AddReward(adaptationReward);
                string adaptLog = $"[DifficultyAdapterAgent] ⚖️ Recompensa por Dirección de Adaptación: {adaptationReward:+0.00;-0.00}";
                Debug.Log(adaptLog);
                GameLogger.LogAdapter(adaptLog);
            }
        }

        if (DifficultyManager.Instance == null)
        {
            Debug.LogWarning("[DifficultyAdapterAgent] ⚠️ DifficultyManager.Instance no está presente en la escena.");
            return;
        }

        DifficultySettings current = DifficultyManager.Instance.CurrentSettings;
        if (current == null) current = new DifficultySettings();

        int targetWidth = current.mapWidth;
        int targetHeight = current.mapHeight;
        int targetExtraConn = current.extraConnections;

        // ── Aplicar cambios de la Rama 0 (Map Size: ±2 celdas para mantener paridad impar) ──
        if (actionMapSize == 0)
        {
            targetWidth -= 2;
            targetHeight -= 2;
        }
        else if (actionMapSize == 2)
        {
            targetWidth += 2;
            targetHeight += 2;
        }

        // ── Aplicar cambios de la Rama 1 (Extra Connections: ±1) ──
        if (actionExtraConn == 0)
        {
            targetExtraConn -= 1;
        }
        else if (actionExtraConn == 2)
        {
            targetExtraConn += 1;
        }

        // Clampar entre límites seguros de DifficultyConstraints
        targetWidth = Mathf.Clamp(targetWidth, 9, 35);
        targetHeight = Mathf.Clamp(targetHeight, 9, 35);
        
        // Garantizar paridad impar obligatoria
        if (targetWidth % 2 == 0) targetWidth++;
        if (targetHeight % 2 == 0) targetHeight++;

        targetExtraConn = Mathf.Clamp(targetExtraConn, 0, 20);

        // Construir la solicitud de ajuste para la configuración del nivel N+1
        DifficultyAdjustmentRequest request = new DifficultyAdjustmentRequest
        {
            mode = DifficultyAdjustmentMode.Absolute,
            requesterId = "DifficultyAdapterAgent",
            reason = $"IA DDA Adaptacion Nivel N+1: MapSize={strMapSize} ({targetWidth}x{targetHeight}), ExtraConn={strExtraConn} ({targetExtraConn})",
            applyImmediately = false,

            overrideMapWidth = true,
            mapWidth = targetWidth,

            overrideMapHeight = true,
            mapHeight = targetHeight,

            overrideExtraConnections = true,
            extraConnections = targetExtraConn
        };

        // FASE 5: Validar y aplicar el cambio mediante el pipeline de DifficultyManager
        bool applied = DifficultyManager.Instance.ApplyAdjustment(request, enforceRateLimit: false);

        if (applied)
        {
            // Sincronizar y escribir la nueva dificultad en el archivo JSON level_config_request.json en disco
            LevelLoadConfig jsonConfig = new LevelLoadConfig
            {
                takeDifficultyScore = false,
                difficultyScore = DifficultyManager.Instance.DifficultyScore,
                nameLevel = "Adapt",
                applyImmediately = false,
                customSettings = request
            };

            SaveConfigToJSONFile(jsonConfig);
        }
        else
        {
            GameLogger.LogAdapter("[DifficultyAdapterAgent] ⚠️ La acción propuesta fue rechazada por la validación de DifficultyManager. Se mantienen las configuraciones anteriores.");
        }
    }

    private void SaveConfigToJSONFile(LevelLoadConfig config)
    {
        try
        {
            string folderPath = Path.Combine(Application.dataPath, "MetricsLogs");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, "level_config_request.json");
            string jsonContent = JsonUtility.ToJson(config, true);
            File.WriteAllText(filePath, jsonContent);

            GameLogger.LogAdapter($"[DifficultyAdapterAgent] 📄 Archivo level_config_request.json actualizado en disco:\n" +
                                  $"  ➜ MapSize: {config.customSettings.mapWidth}x{config.customSettings.mapHeight}\n" +
                                  $"  ➜ ExtraConnections: {config.customSettings.extraConnections}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DifficultyAdapterAgent] Error al guardar level_config_request.json: {ex.Message}");
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        
        // Por defecto: KEEP (1) para ambas ramas
        discreteActions[0] = 1; // Map Size: KEEP
        discreteActions[1] = 1; // Extra Connections: KEEP

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            // Rama 0 (Map Size): Tecla 1 (Dec), Tecla 2 (Keep), Tecla 3 (Inc)
            if (kb.digit1Key.isPressed) discreteActions[0] = 0;
            else if (kb.digit3Key.isPressed) discreteActions[0] = 2;

            // Rama 1 (Extra Connections): Tecla 4 (Dec), Tecla 5 (Keep), Tecla 6 (Inc)
            if (kb.digit4Key.isPressed) discreteActions[1] = 0;
            else if (kb.digit6Key.isPressed) discreteActions[1] = 2;
        }
    }

    private string ActionToString(int action)
    {
        switch (action)
        {
            case 0: return "DECREASE";
            case 1: return "KEEP";
            case 2: return "INCREASE";
            default: return "UNKNOWN";
        }
    }
}
