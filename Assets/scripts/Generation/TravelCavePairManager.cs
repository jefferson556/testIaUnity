using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera, gestiona y limpia múltiples parejas de cuevas de viaje rápido opcionales.
///
/// Responsabilidades:
///  - Determinar cuántas parejas generar según el tamaño del mapa.
///  - Encontrar ubicaciones válidas para cada pareja.
///  - Instanciar los GameObjects y configurar los portales.
///  - Registrar las celdas reservadas para evitar conflictos entre parejas.
///  - Destruir todas las parejas al regenerar el nivel.
///  - Exponer las conexiones de portal para el pathfinder.
///
/// No modifica la lógica de las cuevas obligatorias de misión.
/// </summary>
public class TravelCavePairManager : MonoBehaviour
{
    // ── Configuración en el Inspector ─────────────────────────────────────────────
    [Header("Configuración de Área y Intentos")]
    [Tooltip("Radio de celdas libres alrededor de cada entrada. 0 = solo la celda exacta.")]
    [SerializeField] private int safeAreaRadius = 0;

    [Tooltip("Intentos máximos por pareja antes de pasar a la siguiente ronda con parámetros relajados.")]
    [SerializeField] private int maximumAttemptsPerPair = 50;

    [Tooltip("Activa logs detallados y Gizmos de depuración.")]
    [SerializeField] public bool enableDebug = true;

    // ── Lista de parejas generadas ────────────────────────────────────────────────
    private readonly List<TravelCavePair> generatedPairs = new List<TravelCavePair>();

    /// <summary>Acceso de solo lectura a las parejas generadas.</summary>
    public IReadOnlyList<TravelCavePair> GeneratedPairs => generatedPairs;

    // ── API pública ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Genera todas las parejas de viaje rápido posibles según la configuración.
    /// Debe llamarse tras colocar la misión (cuevas obligatorias, hacha, llave, meta).
    /// </summary>
    /// <param name="maze">Matriz lógica del laberinto.</param>
    /// <param name="mazeData">Fuente de verdad del estado del laberinto.</param>
    /// <param name="cavePrefab">Prefab compartido de cueva.</param>
    /// <param name="itemsContainer">Transform padre para los GameObjects instanciados.</param>
    /// <param name="mazeRenderer">Renderer para obtener posiciones world.</param>
    /// <param name="startCell">Celda de inicio del jugador.</param>
    /// <param name="missionCellsToExclude">Celdas de misión que no pueden usarse.</param>
    /// <param name="barrierCells">Celdas de barreras destructibles.</param>
    /// <param name="axeZoneCells">Celdas de la zona del hacha.</param>
    /// <param name="settings">Configuración de dificultad.</param>
    /// <param name="seed">Semilla aleatoria.</param>
    public void GeneratePairs(
        MazeCellType[,] maze,
        MazeData mazeData,
        GameObject cavePrefab,
        Transform itemsContainer,
        MazeTilemapRenderer mazeRenderer,
        Vector2Int startCell,
        HashSet<Vector2Int> missionCellsToExclude,
        HashSet<Vector2Int> barrierCells,
        List<Vector2Int> axeZoneCells,
        DifficultySettings settings,
        int seed)
    {
        ClearAllPairs();

        if (!settings.enableTravelCaves)
        {
            if (enableDebug) Debug.Log("[TravelCavePairManager] enableTravelCaves está deshabilitado en DifficultySettings.");
            return;
        }

        if (cavePrefab == null)
        {
            Debug.LogError("[TravelCavePairManager] cavePrefab es NULL en DynamicLevelManager. Asigna el Prefab de la Cueva en el Inspector.");
            return;
        }

        int mazeWidth  = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        // ── 1. Determinar cuántas parejas intentar ─────────────────────────────────
        int requestedPairs = CalculateRequestedPairs(mazeData, mazeWidth, mazeHeight, settings);

        if (requestedPairs == 0)
        {
            Debug.Log($"[TravelCavePairManager] El mapa ({mazeWidth}x{mazeHeight}) no cumple los requisitos " +
                      $"mínimos para cuevas opcionales.");
            return;
        }

        // ── 2. Construir el conjunto global de celdas excluidas ────────────────────
        HashSet<Vector2Int> globalExcluded = BuildExcludedSet(
            missionCellsToExclude, barrierCells, axeZoneCells);

        System.Random random = new System.Random(seed + 999);

        int placedCount = 0;

        for (int pairIdx = 0; pairIdx < requestedPairs; pairIdx++)
        {
            // Actualizar el conjunto excluido con las celdas ya reservadas por parejas previas
            HashSet<Vector2Int> currentExcluded = new HashSet<Vector2Int>(globalExcluded);
            foreach (var existingPair in generatedPairs)
            {
                foreach (var cell in existingPair.GetAllReservedCells())
                    currentExcluded.Add(cell);
            }

            TravelCavePair pair = TryPlaceOnePair(
                pairIdx, maze, mazeData, cavePrefab, itemsContainer, mazeRenderer,
                startCell, currentExcluded, barrierCells, settings, random);

            if (pair != null)
            {
                generatedPairs.Add(pair);
                placedCount++;
            }
            else
            {
                if (enableDebug)
                    Debug.Log($"[TravelCavePairManager] Pareja {(pairIdx + 1):D2}: RECHAZADA — " +
                              "no se encontraron candidatos válidos tras relajación.");
            }
        }

        // ── 3. Log de resumen ──────────────────────────────────────────────────────
        if (enableDebug)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[TravelCavePairManager] Parejas solicitadas: {requestedPairs}, generadas: {placedCount}.");
            foreach (var p in generatedPairs)
            {
                sb.AppendLine($"  Pareja {(p.PairIndex + 1):D2}: A={p.CellA} B={p.CellB} " +
                              $"distancia={p.NormalPathDistance} ahorro={p.EstimatedSaving:F1}");
            }
            Debug.Log(sb.ToString());
        }
    }

    /// <summary>
    /// Destruye todos los GameObjects de parejas y limpia el estado interno.
    /// Debe llamarse en ClearPreviousAttemptRoutine de DynamicLevelManager.
    /// </summary>
    public void ClearAllPairs()
    {
        foreach (var pair in generatedPairs)
        {
            if (pair.GameObjectA != null) Object.Destroy(pair.GameObjectA);
            if (pair.GameObjectB != null) Object.Destroy(pair.GameObjectB);
        }
        generatedPairs.Clear();
    }

    /// <summary>
    /// Construye la lista de PortalConnection para usar con MazePathfinder.
    /// Solo incluye portales cuyo estado IsActive == true.
    /// </summary>
    public List<PortalConnection> BuildPortalConnections(MazeData mazeData)
    {
        var connections = new List<PortalConnection>();
        foreach (var pair in generatedPairs)
        {
            if (!pair.IsActive) continue;

            // La celda de salida es la celda adyacente libre al prefab en el lado opuesto.
            // Dado que la celda del prefab está marcada como Occupied, el jugador
            // emerge en una celda vecina. Para el pathfinder, la salida es la misma
            // celda del portal del otro lado (el trigger mueve al jugador a ExitPoint).
            // Modelamos: EntryA → ExitB = CellB, ExitA = CellA
            // porque CavePortal teletransporta al jugador a ExitPoint, que está en el prefab del otro lado.
            connections.Add(new PortalConnection(
                entryA:       pair.CellA,
                exitB:        pair.CellB,   // jugador aparece en CellB (lado B)
                entryB:       pair.CellB,
                exitA:        pair.CellA,   // jugador aparece en CellA (lado A)
                teleportCost: pair.TeleportCost,
                pairIndex:    pair.PairIndex,
                isActive:     pair.IsActive
            ));
        }
        return connections;
    }

    // ── Gizmos ─────────────────────────────────────────────────────────────────────

    public void DrawGizmos(MazeTilemapRenderer mazeRenderer, List<int> optimalPairIndices)
    {
        if (!enableDebug || mazeRenderer == null) return;

        // Colores distintos por índice para distinguir parejas sin depender solo del color
        Color[] pairColors =
        {
            new Color(1f, 0.2f, 0.8f),   // Magenta
            new Color(0.2f, 1f, 0.8f),   // Cyan
            new Color(1f, 0.8f, 0.2f),   // Amarillo
            new Color(0.8f, 0.2f, 1f),   // Violeta
            new Color(0.2f, 0.8f, 1f),   // Azul claro
        };

        foreach (var pair in generatedPairs)
        {
            Color baseColor = pairColors[pair.PairIndex % pairColors.Length];
            bool isOptimal  = optimalPairIndices != null && optimalPairIndices.Contains(pair.PairIndex);

            // Radio del marcador: mayor si la pareja es la óptima
            float radius = isOptimal ? 0.35f : 0.2f;

            // Posiciones world
            Vector3 posA = mazeRenderer.GetWorldPosition(pair.CellA);
            Vector3 posB = mazeRenderer.GetWorldPosition(pair.CellB);

            // Esferas en las entradas
            Gizmos.color = baseColor;
            Gizmos.DrawWireSphere(posA, radius);
            Gizmos.DrawWireSphere(posB, radius);

            // Línea de conexión
            Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
            Gizmos.DrawLine(posA, posB);

            // Ruta normal (camino sin portal que reemplaza el teletransporte)
            if (pair.NormalPath != null && pair.NormalPath.Count >= 2)
            {
                Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.3f);
                for (int i = 0; i < pair.NormalPath.Count - 1; i++)
                {
                    Vector3 p1 = mazeRenderer.GetWorldPosition(pair.NormalPath[i]);
                    Vector3 p2 = mazeRenderer.GetWorldPosition(pair.NormalPath[i + 1]);
                    Gizmos.DrawLine(p1, p2);
                }
            }

#if UNITY_EDITOR
            // Etiqueta con índice de pareja
            Vector2 cellSize = mazeRenderer.LogicalCellTileSize;
            UnityEditor.Handles.Label(posA + Vector3.up * cellSize.y * 0.6f,
                $"P{(pair.PairIndex + 1):D2}-A", new GUIStyle { normal = { textColor = baseColor } });
            UnityEditor.Handles.Label(posB + Vector3.up * cellSize.y * 0.6f,
                $"P{(pair.PairIndex + 1):D2}-B", new GUIStyle { normal = { textColor = baseColor } });
#endif
        }
    }

    // ── Lógica interna de generación ──────────────────────────────────────────────

    private int CalculateRequestedPairs(MazeData mazeData, int mazeWidth, int mazeHeight,
                                        DifficultySettings settings)
    {
        if (settings.maximumTravelCavePairs <= 0)
        {
            if (enableDebug) Debug.Log("[TravelCavePairManager] maximumTravelCavePairs es <= 0. No se generarán cuevas de viaje rápido.");
            return 0;
        }

        // Verificar dimensiones mínimas
        if (mazeWidth  < settings.minimumMapWidthForTravelCaves ||
            mazeHeight < settings.minimumMapHeightForTravelCaves)
        {
            if (enableDebug) Debug.Log($"[TravelCavePairManager] Mapa ({mazeWidth}x{mazeHeight}) es menor al mínimo ({settings.minimumMapWidthForTravelCaves}x{settings.minimumMapHeightForTravelCaves}).");
            return 0;
        }

        // Contar celdas transitables en la región principal
        int walkable = 0;
        for (int x = 0; x < mazeWidth;  x++)
        for (int y = 0; y < mazeHeight; y++)
            if (mazeData.IsCellWalkableAndMain(x, y)) walkable++;

        // Regla por celdas transitables (con límites máximos razonables si el ScriptableObject serializado traía valores viejos)
        int limit1 = Mathf.Min(settings.minWalkableCellsForOnePair, 35);
        int limit2 = Mathf.Min(settings.minWalkableCellsForTwoPairs, 70);
        int limit3 = Mathf.Min(settings.minWalkableCellsForThreePairs, 120);

        int maxBySize;
        if      (walkable < limit1) maxBySize = 0;
        else if (walkable < limit2) maxBySize = 1;
        else if (walkable < limit3) maxBySize = 2;
        else                        maxBySize = settings.maximumTravelCavePairs;

        int result = Mathf.Min(maxBySize, settings.maximumTravelCavePairs);
        if (enableDebug)
        {
            Debug.Log($"[TravelCavePairManager] Celdas transitables: {walkable}. Parejas permitidas por tamaño: {maxBySize} (umbrales: {limit1}/{limit2}/{limit3}), " +
                      $"maximumTravelCavePairs asignado: {settings.maximumTravelCavePairs}. Solicitadas final: {result}.");
        }

        return result;
    }

    private static HashSet<Vector2Int> BuildExcludedSet(
        HashSet<Vector2Int> missionCells,
        HashSet<Vector2Int> barrierCells,
        List<Vector2Int> axeZoneCells)
    {
        var excluded = new HashSet<Vector2Int>();
        if (missionCells  != null) foreach (var c in missionCells)  excluded.Add(c);
        if (barrierCells  != null) foreach (var c in barrierCells)  excluded.Add(c);
        if (axeZoneCells  != null) foreach (var c in axeZoneCells)  excluded.Add(c);
        return excluded;
    }

    private TravelCavePair TryPlaceOnePair(
        int pairIndex,
        MazeCellType[,] maze,
        MazeData mazeData,
        GameObject cavePrefab,
        Transform itemsContainer,
        MazeTilemapRenderer mazeRenderer,
        Vector2Int startCell,
        HashSet<Vector2Int> excluded,
        HashSet<Vector2Int> barrierCells,
        DifficultySettings settings,
        System.Random random)
    {
        int mazeWidth  = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        // ── a. Construir pool de candidatos válidos ────────────────────────────────
        var candidates = new List<Vector2Int>();
        for (int x = 1; x < mazeWidth - 1; x++)
        {
            for (int y = 1; y < mazeHeight - 1; y++)
            {
                var cell = new Vector2Int(x, y);
                if (excluded.Contains(cell)) continue;
                if (!mazeData.IsCellWalkableAndMain(x, y)) continue;
                candidates.Add(cell);
            }
        }

        // ── b. Pre-filtrar: alcanzables desde el inicio sin hacha ──────────────────
        var validCandidates = new List<Vector2Int>();
        foreach (var cand in candidates)
        {
            var reachResult = MazePathfinder.FindWalkingPath(
                startCell, cand, mazeData, false, barrierCells);
            if (!reachResult.PathExists) continue;

            if (safeAreaRadius > 0 && !IsSafeAreaClear(cand, excluded, mazeData, mazeWidth, mazeHeight))
                continue;

            validCandidates.Add(cand);
        }

        if (validCandidates.Count < 2)
        {
            if (enableDebug)
                Debug.Log($"[TravelCavePairManager] Pareja {(pairIndex + 1):D2}: candidatos válidos insuficientes ({validCandidates.Count}).");
            return null;
        }

        // ── c. Bucle de intentos con relajación dinámica ───────────────────────────
        int currentMinDist   = settings.minimumPathDistanceBetweenTravelCaves;
        int currentMinSaving = settings.minimumShortcutSaving;

        for (int round = 1; round <= 3; round++)
        {
            for (int attempt = 0; attempt < maximumAttemptsPerPair; attempt++)
            {
                int idxA = random.Next(validCandidates.Count);
                int idxB = random.Next(validCandidates.Count);
                int guard = 0;
                while (idxA == idxB && guard < 20) { idxB = random.Next(validCandidates.Count); guard++; }
                if (idxA == idxB) continue;

                var candA = validCandidates[idxA];
                var candB = validCandidates[idxB];

                // Verificar que las celdas no se superponen con las reservadas por esta pareja
                if (excluded.Contains(candA) || excluded.Contains(candB)) continue;

                // Calcular distancia de camino entre A y B sin portal
                var pathAB = MazePathfinder.FindWalkingPath(
                    candA, candB, mazeData, false, barrierCells);

                if (!pathAB.PathExists || pathAB.WalkingSteps < currentMinDist) continue;

                float estimatedSaving = pathAB.WalkingSteps - settings.teleportCost;
                if (estimatedSaving < currentMinSaving) continue;

                // ── Éxito: instanciar la pareja ────────────────────────────────────
                return InstantiatePair(
                    pairIndex, candA, candB, pathAB,
                    estimatedSaving, settings.teleportCost,
                    cavePrefab, itemsContainer, mazeRenderer, mazeData);
            }

            // Relajar para la siguiente ronda
            currentMinDist   = Mathf.Max(6, currentMinDist   - 2);
            currentMinSaving = Mathf.Max(3, currentMinSaving  - 2);

            if (enableDebug)
                Debug.Log($"[TravelCavePairManager] Pareja {(pairIndex + 1):D2}: ronda {round} sin éxito. " +
                          $"Relajando a distMin={currentMinDist} savingMin={currentMinSaving}.");
        }

        return null;
    }

    private TravelCavePair InstantiatePair(
        int pairIndex,
        Vector2Int cellA, Vector2Int cellB,
        PathfindResult normalPath,
        float estimatedSaving, float teleportCost,
        GameObject cavePrefab,
        Transform itemsContainer,
        MazeTilemapRenderer mazeRenderer,
        MazeData mazeData)
    {
        var pair = new TravelCavePair
        {
            PairIndex        = pairIndex,
            CellA            = cellA,
            CellB            = cellB,
            TeleportCost     = teleportCost,
            NormalPathDistance = normalPath.WalkingSteps,
            EstimatedSaving  = estimatedSaving,
            IsActive         = true,
            NormalPath       = normalPath.Cells,
        };

        // Calcular celdas reservadas del área segura
        pair.ReservedCellsA = GetSafeAreaCells(cellA, safeAreaRadius, mazeData.Width, mazeData.Height);
        pair.ReservedCellsB = GetSafeAreaCells(cellB, safeAreaRadius, mazeData.Width, mazeData.Height);

        // Instanciar GameObjects
        Vector3 posA = mazeRenderer.GetWorldPosition(cellA);
        Vector3 posB = mazeRenderer.GetWorldPosition(cellB);

        pair.GameObjectA = Object.Instantiate(cavePrefab, posA, Quaternion.identity, itemsContainer);
        pair.GameObjectA.name = pair.NameA;

        pair.GameObjectB = Object.Instantiate(cavePrefab, posB, Quaternion.identity, itemsContainer);
        pair.GameObjectB.name = pair.NameB;

        // Configurar portales
        pair.PortalA = pair.GameObjectA.GetComponentInChildren<CavePortal>();
        pair.PortalB = pair.GameObjectB.GetComponentInChildren<CavePortal>();

        Transform exitA = FindExitPoint(pair.GameObjectA);
        Transform exitB = FindExitPoint(pair.GameObjectB);

        if (pair.PortalA != null)
        {
            pair.PortalA.DestinationExitPoint = exitB;
            pair.PortalA.enabled = true;
        }
        if (pair.PortalB != null)
        {
            pair.PortalB.DestinationExitPoint = exitA;
            pair.PortalB.enabled = true;
        }

        // Asignar PairIndex al CaveTraveler (vía los portales)
        // El CaveTraveler del jugador no existe aquí, se asigna al entrar en contacto.
        // Guardamos el pairIndex en el portal para que sea accesible.
        var cavePortalA = pair.PortalA;
        if (cavePortalA != null) cavePortalA.PairIndex = pairIndex;
        var cavePortalB = pair.PortalB;
        if (cavePortalB != null) cavePortalB.PairIndex = pairIndex;

        // Marcar celdas como ocupadas en MazeData
        mazeData.MarkCellsAsOccupied(cellA, 1, 1);
        mazeData.MarkCellsAsOccupied(cellB, 1, 1);

        return pair;
    }

    private static Transform FindExitPoint(GameObject caveGO)
    {
        Transform exit = caveGO.transform.Find("ExitPoint");
        if (exit == null) exit = caveGO.transform.Find("EntranceTrigger");
        if (exit == null) exit = caveGO.transform;
        return exit;
    }

    private bool IsSafeAreaClear(Vector2Int center, HashSet<Vector2Int> excluded,
                                  MazeData mazeData, int mazeWidth, int mazeHeight)
    {
        for (int dx = -safeAreaRadius; dx <= safeAreaRadius; dx++)
        {
            for (int dy = -safeAreaRadius; dy <= safeAreaRadius; dy++)
            {
                int x = center.x + dx;
                int y = center.y + dy;
                if (x < 1 || x >= mazeWidth - 1 || y < 1 || y >= mazeHeight - 1) return false;
                var cell = new Vector2Int(x, y);
                if (excluded.Contains(cell)) return false;
                if (!mazeData.IsCellWalkableAndMain(x, y)) return false;
            }
        }
        return true;
    }

    private static List<Vector2Int> GetSafeAreaCells(Vector2Int center, int radius, int width, int height)
    {
        var cells = new List<Vector2Int>();
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            int x = center.x + dx;
            int y = center.y + dy;
            if (x >= 0 && x < width && y >= 0 && y < height)
                cells.Add(new Vector2Int(x, y));
        }
        return cells;
    }
}
