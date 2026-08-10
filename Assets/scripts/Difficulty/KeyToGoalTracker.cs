using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registra el recorrido real del jugador desde que recoge la llave hasta que llega a la meta.
/// Se activa mediante StartTracking() (llamado desde DifficultyMetricsCollector)
/// y termina mediante StopTracking() cuando la puerta se abre.
/// </summary>
public class KeyToGoalTracker : MonoBehaviour
{
    public static KeyToGoalTracker Instance { get; private set; }

    // ── CONFIGURACIÓN DE UMBRALES (Ajustables en Inspector) ───────────────────────
    [Header("Umbrales de Clasificación")]
    [SerializeField] public float strugglingEfficiencyThreshold = 0.50f;
    [SerializeField] public float strugglingRepeatedRatioThreshold = 0.35f;
    [SerializeField] public int strugglingExtraDistanceThreshold = 8;
    [SerializeField] public float strugglingTimeThreshold = 60f;
    [SerializeField] public int strugglingUnproductiveCavesThreshold = 2;

    [Header("Umbrales de Teletransporte")]
    [SerializeField] public float minimumUsefulCaveSaving = 3f;
    [SerializeField] public float maximumNeutralCaveDifference = 2f;

    // ── Estado de tracking ────────────────────────────────────────────────────────
    private bool isTracking;
    private float trackingStartTime;
    private bool justTeleported;

    // ── Datos del recorrido en tiempo real ────────────────────────────────────────
    private Vector2Int lastCell = new Vector2Int(-1, -1);
    private readonly HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();
    private readonly HashSet<int> usedPairIndices = new HashSet<int>();

    // Contadores de recorrido real
    private int actualWalkingDistance;
    private int repeatedCellsCount;
    private int totalCaveUses;

    // Contadores de categorías de teletransporte
    private int usefulCaveUses;
    private int neutralCaveUses;
    private int unproductiveCaveUses;
    private int unevaluatedCaveUses;
    private int mandatoryCaveUses;

    // ── Referencias a rutas óptimas (calculadas al recoger la llave) ──────────────
    private PathfindResult optimalWalkingPath;
    private PathfindResult optimalMechanicPath;
    private float normalStepCost = 1f;
    private float teleportCost = 3f;

    // ── Referencia al MazeData (para convertir posición → celda) ─────────────────
    private MazeData mazeData;

    // ── Resultado final ───────────────────────────────────────────────────────────
    public KeyToGoalMetrics CompletedMetrics { get; private set; }
    public bool HasCompletedMetrics { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── API pública ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia el tracking del segmento llave→meta.
    /// Debe llamarse inmediatamente después de que el jugador recoge la llave.
    /// </summary>
    public void StartTracking(
        PathfindResult walkingPath,
        PathfindResult mechanicPath,
        MazeData data,
        float stepCost = 1f,
        float portalCost = 3f)
    {
        if (isTracking)
        {
            Debug.LogWarning("[KeyToGoalTracker] StartTracking llamado mientras ya estaba activo. Reiniciando.");
        }

        isTracking = true;
        trackingStartTime = Time.time;
        normalStepCost = stepCost;
        teleportCost = portalCost;
        mazeData = data;
        justTeleported = false;

        optimalWalkingPath = walkingPath ?? PathfindResult.NoPath();
        optimalMechanicPath = mechanicPath ?? PathfindResult.NoPath();

        // Limpieza de datos (Section 13)
        visitedCells.Clear();
        usedPairIndices.Clear();
        actualWalkingDistance = 0;
        repeatedCellsCount = 0;
        totalCaveUses = 0;

        usefulCaveUses = 0;
        neutralCaveUses = 0;
        unproductiveCaveUses = 0;
        unevaluatedCaveUses = 0;
        mandatoryCaveUses = 0;

        lastCell = new Vector2Int(-1, -1);
        HasCompletedMetrics = false;
        CompletedMetrics = null;
    }

    /// <summary>
    /// Actualiza la posición actual del jugador. Debe llamarse cada frame desde DifficultyMetricsCollector.
    /// </summary>
    public void UpdatePlayerPosition(Vector3 worldPosition)
    {
        if (!isTracking || mazeData == null) return;

        var origin = mazeData.MapOrigin;
        var cellSize = mazeData.CellSize;

        if (cellSize.x <= 0 || cellSize.y <= 0) return;

        int cx = Mathf.FloorToInt((worldPosition.x - origin.x) / cellSize.x);
        int cy = Mathf.FloorToInt((worldPosition.y - origin.y) / cellSize.y);

        if (cx < 0 || cx >= mazeData.Width || cy < 0 || cy >= mazeData.Height) return;

        var currentCell = new Vector2Int(cx, cy);

        // Si es el primer paso registrado
        if (lastCell == new Vector2Int(-1, -1))
        {
            lastCell = currentCell;
            visitedCells.Add(currentCell);
            return;
        }

        if (currentCell == lastCell) return;

        if (justTeleported)
        {
            justTeleported = false;
            lastCell = currentCell;
            RecordCellVisit(currentCell);
            return;
        }

        // Cambio de celda normal caminando (Section 2)
        actualWalkingDistance++;
        lastCell = currentCell;
        RecordCellVisit(currentCell);
    }

    private void RecordCellVisit(Vector2Int cell)
    {
        if (visitedCells.Contains(cell))
        {
            repeatedCellsCount++;
        }
        else
        {
            visitedCells.Add(cell);
        }
    }

    /// <summary>
    /// Registra y evalúa un teletransporte en tiempo real cuando ocurre (Section 5 y 6).
    /// </summary>
    public void RegisterCaveUse(int pairIndex)
    {
        if (!isTracking) return;

        totalCaveUses++;
        justTeleported = true; // Activar flag para que no sume distancia física caminada

        if (pairIndex == -1)
        {
            // Cueva obligatoria de misión (Section 7): clasificar como Unevaluated
            unevaluatedCaveUses++;
            mandatoryCaveUses++;

            Debug.Log(
                "[KeyToGoal Cave Evaluation]\n" +
                "Pair ID: -1 (Mandatory)\n" +
                "Classification: Unevaluated\n" +
                "Reason: Mandatory mission cave"
            );
            return;
        }

        usedPairIndices.Add(pairIndex);

        // Encontrar referencias en TravelCavePairManager
        var pairManager = Object.FindAnyObjectByType<TravelCavePairManager>();
        TravelCavePair pair = null;
        if (pairManager != null)
        {
            foreach (var p in pairManager.GeneratedPairs)
            {
                if (p.PairIndex == pairIndex)
                {
                    pair = p;
                    break;
                }
            }
        }

        // Determinar celdas de entrada y salida seguras basadas en proximidad lúdica
        Vector2Int entryCell = Vector2Int.zero;
        Vector2Int exitCell = Vector2Int.zero;
        if (pair != null)
        {
            float distToA = Vector2Int.Distance(lastCell, pair.CellA);
            float distToB = Vector2Int.Distance(lastCell, pair.CellB);
            if (distToA < distToB)
            {
                entryCell = pair.CellA;
                exitCell = pair.CellB;
            }
            else
            {
                entryCell = pair.CellB;
                exitCell = pair.CellA;
            }
        }

        // Obtener metaCell del singleton de nivel
        Vector2Int metaCell = Vector2Int.zero;
        if (DynamicLevelManager.Instance != null)
        {
            metaCell = DynamicLevelManager.Instance.MetaCell;
        }
        else if (optimalWalkingPath != null && optimalWalkingPath.Cells != null && optimalWalkingPath.Cells.Count > 0)
        {
            metaCell = optimalWalkingPath.Cells[optimalWalkingPath.Cells.Count - 1];
        }

        // CASOS NO EVALUABLES (Section 6)
        if (pair == null || metaCell == Vector2Int.zero || mazeData == null)
        {
            unevaluatedCaveUses++;
            Debug.Log(
                $"[KeyToGoal Cave Evaluation]\n" +
                $"Pair ID: {pairIndex}\n" +
                $"Classification: Unevaluated\n" +
                $"Reason: missing level references"
            );
            return;
        }

        // Obtener conexiones y barreras actuales
        var activePortals = DynamicLevelManager.Instance != null ? DynamicLevelManager.Instance.ActivePortalConnections : null;
        var barriers = DynamicLevelManager.Instance != null ? DynamicLevelManager.Instance.BarrierCells : null;

        // Comprobar si existe ruta válida antes (con todas las conexiones activas)
        PathfindResult routeBefore = MazePathfinder.FindPathWithPortals(entryCell, metaCell, mazeData, true, activePortals, barriers, 1f);
        if (routeBefore == null || !routeBefore.PathExists)
        {
            unevaluatedCaveUses++;
            Debug.Log(
                $"[KeyToGoal Cave Evaluation]\n" +
                $"Pair ID: {pairIndex}\n" +
                $"Classification: Unevaluated\n" +
                $"Reason: no valid path from entry cell to goal"
            );
            return;
        }

        // Comprobar si existe ruta válida desde la salida
        PathfindResult routeFromExit = MazePathfinder.FindPathWithPortals(exitCell, metaCell, mazeData, true, activePortals, barriers, 1f);
        if (routeFromExit == null || !routeFromExit.PathExists)
        {
            unevaluatedCaveUses++;
            Debug.Log(
                $"[KeyToGoal Cave Evaluation]\n" +
                $"Pair ID: {pairIndex}\n" +
                $"Classification: Unevaluated\n" +
                $"Reason: no valid path from portal exit to goal"
            );
            return;
        }

        // Excluir ÚNICAMENTE el portal actual para calcular costWithoutTeleport (evita recursión, Section 6)
        var portalsWithoutCurrent = new List<PortalConnection>();
        if (activePortals != null)
        {
            foreach (var pc in activePortals)
            {
                if (pc.PairIndex != pairIndex)
                {
                    portalsWithoutCurrent.Add(pc);
                }
            }
        }

        PathfindResult routeWithoutCurrent = MazePathfinder.FindPathWithPortals(entryCell, metaCell, mazeData, true, portalsWithoutCurrent, barriers, 1f);
        
        // Si no existe camino alternativo sin este portal, se asume que el portal es la única vía (ahorro máximo)
        float costWithoutTeleport = routeWithoutCurrent.PathExists ? routeWithoutCurrent.TotalCost : 9999f;
        float costWithTeleport = pair.TeleportCost + routeFromExit.TotalCost;
        float caveSaving = costWithoutTeleport - costWithTeleport;

        // Clasificar utilidad según umbrales centralizados
        string classification;
        if (caveSaving >= minimumUsefulCaveSaving)
        {
            usefulCaveUses++;
            classification = "Useful";
        }
        else if (caveSaving <= -minimumUsefulCaveSaving)
        {
            unproductiveCaveUses++;
            classification = "Unproductive";
        }
        else
        {
            neutralCaveUses++;
            classification = "Neutral";
        }

        // Log exacto solicitado (Section 14)
        Debug.Log(
            $"[KeyToGoal Cave Evaluation]\n" +
            $"Pair ID: {pairIndex}\n" +
            $"Entry cell: {entryCell}\n" +
            $"Exit cell: {exitCell}\n" +
            $"Cost without current portal: {(costWithoutTeleport >= 999f ? "Infinity" : costWithoutTeleport.ToString("F1"))}\n" +
            $"Cost with portal: {costWithTeleport:F1}\n" +
            $"Teleport cost: {pair.TeleportCost:F1}\n" +
            $"Saving: {(costWithoutTeleport >= 999f ? "Infinity" : caveSaving.ToString("F1"))}\n" +
            $"Classification: {classification}"
        );
    }

    /// <summary>
    /// Finaliza el tracking y calcula las métricas completas (Section 2 y 3).
    /// </summary>
    public void StopTracking()
    {
        if (!isTracking) return;
        isTracking = false;

        CompletedMetrics = CalculateMetrics();
        HasCompletedMetrics = true;

        LogSummary(CompletedMetrics);
    }

    /// <summary>
    /// Cancela el tracking sin calcular métricas (ej. al regenerar el nivel).
    /// </summary>
    public void CancelTracking()
    {
        isTracking = false;
        HasCompletedMetrics = false;
        CompletedMetrics = null;
        visitedCells.Clear();
        usedPairIndices.Clear();
        actualWalkingDistance = 0;
        repeatedCellsCount = 0;
        totalCaveUses = 0;
        usefulCaveUses = 0;
        neutralCaveUses = 0;
        unproductiveCaveUses = 0;
        unevaluatedCaveUses = 0;
        mandatoryCaveUses = 0;
        lastCell = new Vector2Int(-1, -1);
    }

    public bool IsTracking => isTracking;

    // ── Cálculo de métricas ────────────────────────────────────────────────────────

    private KeyToGoalMetrics CalculateMetrics()
    {
        var m = new KeyToGoalMetrics();

        // ── Datos de rutas óptimas ─────────────────────────────────────────────────
        bool walkingExists = optimalWalkingPath != null && optimalWalkingPath.PathExists;
        m.keyToGoalPathDataValid = walkingExists;

        m.keyToGoalTime = Time.time - trackingStartTime;

        if (m.keyToGoalPathDataValid)
        {
            m.keyToGoalOptimalDistance = optimalWalkingPath.WalkingSteps;
            m.keyToGoalActualDistance = actualWalkingDistance;
            m.keyToGoalExtraDistance = Mathf.Max(0, m.keyToGoalActualDistance - m.keyToGoalOptimalDistance);
            m.keyToGoalRepeatedCells = repeatedCellsCount;

            m.keyToGoalRepeatedCellRatio = m.keyToGoalActualDistance > 0 
                ? (float)m.keyToGoalRepeatedCells / m.keyToGoalActualDistance 
                : 0f;

            m.keyToGoalEfficiency = m.keyToGoalActualDistance > 0 
                ? (float)m.keyToGoalOptimalDistance / m.keyToGoalActualDistance 
                : 0f;
        }
        else
        {
            m.keyToGoalOptimalDistance = -1;
            m.keyToGoalActualDistance = actualWalkingDistance;
            m.keyToGoalExtraDistance = -1;
            m.keyToGoalRepeatedCells = repeatedCellsCount;
            m.keyToGoalRepeatedCellRatio = m.keyToGoalActualDistance > 0 
                ? (float)m.keyToGoalRepeatedCells / m.keyToGoalActualDistance 
                : 0f;
            m.keyToGoalEfficiency = -1f;
        }

        // Llenar variables de teletransporte
        m.keyToGoalCaveUses = totalCaveUses;
        m.keyToGoalUniqueCavePairsUsed = usedPairIndices.Count;
        m.keyToGoalUsefulCaveUses = usefulCaveUses;
        m.keyToGoalNeutralCaveUses = neutralCaveUses;
        m.keyToGoalUnproductiveCaveUses = unproductiveCaveUses;
        m.keyToGoalUnevaluatedCaveUses = unevaluatedCaveUses;
        m.keyToGoalMandatoryCaveUses = mandatoryCaveUses;

        // Clasificar estilo de navegación
        m.keyToGoalNavigationState = ClassifyNavigation(m);

        return m;
    }

    private NavigationStyle ClassifyNavigation(KeyToGoalMetrics m)
    {
        if (!m.keyToGoalPathDataValid)
        {
            return NavigationStyle.NotEvaluated;
        }

        // Evaluar señales para Struggling (Section 3)
        int strugglingSignals = 0;
        if (m.keyToGoalEfficiency < strugglingEfficiencyThreshold) strugglingSignals++;
        if (m.keyToGoalRepeatedCellRatio >= strugglingRepeatedRatioThreshold) strugglingSignals++;
        if (m.keyToGoalExtraDistance >= strugglingExtraDistanceThreshold) strugglingSignals++;
        if (m.keyToGoalTime >= strugglingTimeThreshold) strugglingSignals++;
        if (m.keyToGoalUnproductiveCaveUses >= strugglingUnproductiveCavesThreshold) strugglingSignals++;

        if (strugglingSignals >= 2)
        {
            return NavigationStyle.Struggling;
        }

        // Evaluar Efficient
        // - extraDistance <= 2 OR efficiency >= 0.80
        // - repeated ratio < 0.20
        bool satisfiesDistance = (m.keyToGoalExtraDistance <= 2) || (m.keyToGoalEfficiency >= 0.80f);
        bool satisfiesRepeated = m.keyToGoalRepeatedCellRatio < 0.20f;
        if (satisfiesDistance && satisfiesRepeated)
        {
            return NavigationStyle.Efficient;
        }

        // De lo contrario, Exploratory
        return NavigationStyle.Exploratory;
    }

    // ── Log de Depuración Detallado (Section 14) ───────────────────────────────────

    private void LogSummary(KeyToGoalMetrics m)
    {
        Debug.Log(
            $"[KeyToGoal Tracker Summary]\n" +
            $"  Distancia óptima: {m.keyToGoalOptimalDistance}\n" +
            $"  Distancia real: {m.keyToGoalActualDistance}\n" +
            $"  Distancia extra: {m.keyToGoalExtraDistance}\n" +
            $"  Celdas repetidas: {m.keyToGoalRepeatedCells}\n" +
            $"  Proporción repetida: {m.keyToGoalRepeatedCellRatio:P0}\n" +
            $"  Eficiencia: {m.keyToGoalEfficiency:P0}\n" +
            $"  Estado de navegación: {m.keyToGoalNavigationState}\n" +
            $"  Cuevas usadas totales: {m.keyToGoalCaveUses} (Únicas: {m.keyToGoalUniqueCavePairsUsed})\n" +
            $"  - Útiles: {m.keyToGoalUsefulCaveUses}\n" +
            $"  - Neutras: {m.keyToGoalNeutralCaveUses}\n" +
            $"  - Improductivas: {m.keyToGoalUnproductiveCaveUses}\n" +
            $"  - No evaluadas: {m.keyToGoalUnevaluatedCaveUses} (Misión: {m.keyToGoalMandatoryCaveUses})"
        );
    }
}