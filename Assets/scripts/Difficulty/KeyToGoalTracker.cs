using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registra el recorrido real del jugador desde que recoge la llave hasta que llega a la meta.
/// Se activa mediante StartTracking() (llamado desde DifficultyMetricsCollector)
/// y termina mediante StopTracking() cuando la puerta se abre.
///
/// No usa Update() de MonoBehaviour para el tracking de celda; en su lugar se actualiza
/// desde DifficultyMetricsCollector.Update() para evitar duplicar lógica de Update.
/// </summary>
public class KeyToGoalTracker : MonoBehaviour
{
    public static KeyToGoalTracker Instance { get; private set; }

    // ── Estado de tracking ────────────────────────────────────────────────────────
    private bool isTracking;
    private float trackingStartTime;

    // ── Datos del recorrido ───────────────────────────────────────────────────────
    private Vector2Int lastCell = new Vector2Int(-1, -1);
    private readonly HashSet<Vector2Int>  visitedCells   = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> cellVisitCount = new Dictionary<Vector2Int, int>();
    private readonly HashSet<int> usedPairIndices = new HashSet<int>();
    private int totalCaveUses;

    // ── Referencias a rutas óptimas (calculadas al recoger la llave) ──────────────
    private PathfindResult optimalWalkingPath;
    private PathfindResult optimalMechanicPath;
    private float normalStepCost = 1f;
    private float teleportCost   = 3f;

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
    /// <param name="walkingPath">Ruta óptima caminando (sin portales).</param>
    /// <param name="mechanicPath">Ruta óptima con portales activos.</param>
    /// <param name="data">Referencia al MazeData para conversión de coordenadas.</param>
    /// <param name="stepCost">Costo por paso normal.</param>
    /// <param name="portalCost">Costo por teletransporte.</param>
    public void StartTracking(
        PathfindResult walkingPath,
        PathfindResult mechanicPath,
        MazeData data,
        float stepCost   = 1f,
        float portalCost = 3f)
    {
        if (isTracking)
        {
            Debug.LogWarning("[KeyToGoalTracker] StartTracking llamado mientras ya estaba activo. Reiniciando.");
        }

        isTracking        = true;
        trackingStartTime = Time.time;
        normalStepCost    = stepCost;
        teleportCost      = portalCost;
        mazeData          = data;

        optimalWalkingPath  = walkingPath  ?? PathfindResult.NoPath();
        optimalMechanicPath = mechanicPath ?? PathfindResult.NoPath();

        visitedCells.Clear();
        cellVisitCount.Clear();
        usedPairIndices.Clear();
        totalCaveUses    = 0;
        lastCell         = new Vector2Int(-1, -1);
        HasCompletedMetrics = false;
        CompletedMetrics    = null;
    }

    /// <summary>
    /// Actualiza la posición actual del jugador. Debe llamarse cada frame desde DifficultyMetricsCollector.
    /// </summary>
    /// <param name="worldPosition">Posición world del jugador.</param>
    public void UpdatePlayerPosition(Vector3 worldPosition)
    {
        if (!isTracking || mazeData == null) return;

        var origin   = mazeData.MapOrigin;
        var cellSize = mazeData.CellSize;

        if (cellSize.x <= 0 || cellSize.y <= 0) return;

        int cx = Mathf.FloorToInt((worldPosition.x - origin.x) / cellSize.x);
        int cy = Mathf.FloorToInt((worldPosition.y - origin.y) / cellSize.y);

        if (cx < 0 || cx >= mazeData.Width || cy < 0 || cy >= mazeData.Height) return;

        var currentCell = new Vector2Int(cx, cy);
        if (currentCell == lastCell) return;

        lastCell = currentCell;

        // Registrar visita
        if (cellVisitCount.ContainsKey(currentCell))
            cellVisitCount[currentCell]++;
        else
            cellVisitCount[currentCell] = 1;

        visitedCells.Add(currentCell);
    }

    /// <summary>
    /// Registra que el jugador usó un portal con el índice de pareja dado.
    /// Debe llamarse desde el evento CaveTraveler.OnTeleportWithPairId.
    /// </summary>
    public void RegisterCaveUse(int pairIndex)
    {
        if (!isTracking) return;
        totalCaveUses++;
        usedPairIndices.Add(pairIndex);
    }

    /// <summary>
    /// Finaliza el tracking y calcula las métricas completas.
    /// Debe llamarse cuando el jugador llega a la meta (puerta abierta).
    /// </summary>
    public void StopTracking()
    {
        if (!isTracking) return;
        isTracking = false;

        CompletedMetrics    = CalculateMetrics();
        HasCompletedMetrics = true;

        LogMetrics(CompletedMetrics);
    }

    /// <summary>
    /// Cancela el tracking sin calcular métricas (ej. al regenerar el nivel).
    /// </summary>
    public void CancelTracking()
    {
        isTracking          = false;
        HasCompletedMetrics = false;
        CompletedMetrics    = null;
        visitedCells.Clear();
        cellVisitCount.Clear();
        usedPairIndices.Clear();
        totalCaveUses = 0;
    }

    public bool IsTracking => isTracking;

    // ── Cálculo de métricas ────────────────────────────────────────────────────────

    private KeyToGoalMetrics CalculateMetrics()
    {
        var m = new KeyToGoalMetrics();

        // ── Datos de rutas óptimas ─────────────────────────────────────────────────
        bool walkingExists  = optimalWalkingPath  != null && optimalWalkingPath.PathExists;
        bool mechanicExists = optimalMechanicPath != null && optimalMechanicPath.PathExists;

        m.keyToGoalPathDataValid = walkingExists && mechanicExists;

        m.keyToGoalOptimalWalkingCost  = walkingExists  ? optimalWalkingPath.TotalCost  : -1f;
        m.keyToGoalOptimalMechanicCost = mechanicExists ? optimalMechanicPath.TotalCost : -1f;

        m.keyToGoalOptimalWalkingDistance          = walkingExists  ? optimalWalkingPath.WalkingSteps  : 0;
        m.keyToGoalOptimalMechanicWalkingDistance  = mechanicExists ? optimalMechanicPath.WalkingSteps : 0;
        m.keyToGoalOptimalPortalUses               = mechanicExists ? optimalMechanicPath.TeleportCount : 0;
        m.keyToGoalOptimalUsesCaves                = mechanicExists && optimalMechanicPath.UsesCaves;

        if (m.keyToGoalPathDataValid)
        {
            float potSaving = m.keyToGoalOptimalWalkingCost - m.keyToGoalOptimalMechanicCost;
            m.keyToGoalPotentialSaving = Mathf.Max(0f, potSaving);
        }
        else
        {
            m.keyToGoalPotentialSaving = 0f;
        }

        // ── Datos del recorrido real ───────────────────────────────────────────────
        m.keyToGoalTime = Time.time - trackingStartTime;

        // Sumar todos los pasos reales (incluyendo repetidos)
        int totalSteps = 0;
        int repeatedCells = 0;
        foreach (var kvp in cellVisitCount)
        {
            totalSteps += kvp.Value;
            if (kvp.Value > 1) repeatedCells += kvp.Value - 1;
        }

        m.keyToGoalActualWalkingDistance = totalSteps;
        m.keyToGoalRepeatedCells         = repeatedCells;
        m.keyToGoalCaveUses              = totalCaveUses;
        m.keyToGoalUniqueCavePairsUsed   = usedPairIndices.Count;
        m.keyToGoalCavePairIndicesUsed   = new System.Collections.Generic.List<int>(usedPairIndices);

        // Costo real estimado
        float actualWalkCost = totalSteps * normalStepCost;
        float actualPortalCost = totalCaveUses * teleportCost;
        m.keyToGoalActualCost = actualWalkCost + actualPortalCost;

        // ── Eficiencias y Clasificación (solo si el pathfinder encontró ruta válida) ───────────────────
        if (m.keyToGoalPathDataValid)
        {
            if (m.keyToGoalActualWalkingDistance > 0)
            {
                m.keyToGoalWalkingEfficiency = Mathf.Clamp01(
                    (float)m.keyToGoalOptimalWalkingDistance / m.keyToGoalActualWalkingDistance);
            }

            if (m.keyToGoalActualCost > 0f)
            {
                m.keyToGoalMechanicEfficiency = Mathf.Clamp01(
                    m.keyToGoalOptimalMechanicCost / m.keyToGoalActualCost);
            }

            m.keyToGoalActualSaving = m.keyToGoalOptimalWalkingCost - m.keyToGoalActualCost;

            if (mechanicExists)
            {
                foreach (var usedIdx in usedPairIndices)
                {
                    if (optimalMechanicPath.PortalPairIndicesUsed.Contains(usedIdx))
                    {
                        m.keyToGoalUsedOptimalCave = true;
                        break;
                    }
                }
            }

            m.keyToGoalIgnoredUsefulCave = m.keyToGoalPotentialSaving > 0f && totalCaveUses == 0;
            m.keyToGoalNavigationStyle = ClassifyNavigation(m);
        }
        else
        {
            m.keyToGoalWalkingEfficiency = 0f;
            m.keyToGoalMechanicEfficiency = 0f;
            m.keyToGoalActualSaving = 0f;
            m.keyToGoalNavigationStyle = NavigationStyle.Efficient; // Estilo neutro cuando no hay evaluación
        }

        return m;
    }

    private static NavigationStyle ClassifyNavigation(KeyToGoalMetrics m)
    {
        const float EFFICIENCY_GOOD = 0.8f;
        const float EFFICIENCY_POOR = 0.4f;

        bool poorOverall = m.keyToGoalWalkingEfficiency < EFFICIENCY_POOR
                        && m.keyToGoalMechanicEfficiency < EFFICIENCY_POOR;
        if (poorOverall)
            return NavigationStyle.Lost;

        if (m.keyToGoalCaveUses > 0)
        {
            if (m.keyToGoalUsedOptimalCave && m.keyToGoalMechanicEfficiency >= EFFICIENCY_GOOD)
                return NavigationStyle.EfficientWithCave;
            return NavigationStyle.SuboptimalCave;
        }

        if (m.keyToGoalIgnoredUsefulCave)
            return NavigationStyle.MissedShortcut;

        if (m.keyToGoalWalkingEfficiency >= EFFICIENCY_GOOD)
            return NavigationStyle.Efficient;

        return NavigationStyle.Lost;
    }

    // ── Log de resumen ─────────────────────────────────────────────────────────────

    private static void LogMetrics(KeyToGoalMetrics m)
    {
        Debug.Log(
            $"[KeyToGoalTracker] Segmento Llave→Meta completado.\n" +
            $"  Tiempo: {m.keyToGoalTime:F2}s\n" +
            $"  Pasos reales: {m.keyToGoalActualWalkingDistance} | Repetidos: {m.keyToGoalRepeatedCells}\n" +
            $"  Costo real: {m.keyToGoalActualCost:F1}\n" +
            $"  Cuevas usadas: {m.keyToGoalCaveUses} (parejas únicas: {m.keyToGoalUniqueCavePairsUsed})\n" +
            $"  Ruta óptima caminando: costo={m.keyToGoalOptimalWalkingCost:F1} pasos={m.keyToGoalOptimalWalkingDistance}\n" +
            $"  Ruta óptima con mecánicas: costo={m.keyToGoalOptimalMechanicCost:F1} portales={m.keyToGoalOptimalPortalUses}\n" +
            $"  Ahorro potencial: {m.keyToGoalPotentialSaving:F1} | Ahorro real: {m.keyToGoalActualSaving:F1}\n" +
            $"  Eficiencia caminando: {m.keyToGoalWalkingEfficiency:P0} | Con mecánicas: {m.keyToGoalMechanicEfficiency:P0}\n" +
            $"  Usó cueva óptima: {m.keyToGoalUsedOptimalCave} | Ignoró atajo: {m.keyToGoalIgnoredUsefulCave}\n" +
            $"  Estilo: {m.keyToGoalNavigationStyle}"
        );
    }
}
