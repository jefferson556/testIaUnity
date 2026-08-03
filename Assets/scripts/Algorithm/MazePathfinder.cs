using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio de pathfinding para el laberinto procedural.
/// Soporta portales bidireccionales con costos configurables.
///
/// Estrategia:
///   - Si teleportCost == normalStepCost para TODOS los portales activos: BFS (O(V+E), rápido).
///   - Si algún portal tiene costo diferente: Dijkstra con min-heap (O((V+E) log V)).
///
/// No usa MonoBehaviour; es una clase estática para evitar FindAnyObjectByType.
/// </summary>
public static class MazePathfinder
{
    private const float DEFAULT_STEP_COST = 1f;

    // ── API pública ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Calcula el camino óptimo de <paramref name="start"/> a <paramref name="end"/>
    /// sin usar ningún portal (ruta caminando pura).
    /// </summary>
    public static PathfindResult FindWalkingPath(
        Vector2Int start,
        Vector2Int end,
        MazeData mazeData,
        bool hasAxe,
        HashSet<Vector2Int> barriers,
        float normalStepCost = DEFAULT_STEP_COST)
    {
        return FindPath(start, end, mazeData, hasAxe, null, barriers, normalStepCost);
    }

    /// <summary>
    /// Calcula el camino óptimo de <paramref name="start"/> a <paramref name="end"/>
    /// pudiendo usar los portales activos en <paramref name="portals"/>.
    /// </summary>
    public static PathfindResult FindPathWithPortals(
        Vector2Int start,
        Vector2Int end,
        MazeData mazeData,
        bool hasAxe,
        IList<PortalConnection> portals,
        HashSet<Vector2Int> barriers,
        float normalStepCost = DEFAULT_STEP_COST)
    {
        return FindPath(start, end, mazeData, hasAxe, portals, barriers, normalStepCost);
    }

    // ── Implementación interna ────────────────────────────────────────────────────

    private static PathfindResult FindPath(
        Vector2Int start,
        Vector2Int end,
        MazeData mazeData,
        bool hasAxe,
        IList<PortalConnection> portals,
        HashSet<Vector2Int> barriers,
        float normalStepCost)
    {
        int width  = mazeData.Width;
        int height = mazeData.Height;

        if (!InBounds(start, width, height) || !InBounds(end, width, height))
            return PathfindResult.NoPath();

        // Decidir algoritmo: BFS si todos los costos son iguales, Dijkstra si no
        bool useBFS = AllCostsEqual(portals, normalStepCost);

        return useBFS
            ? RunBFS(start, end, mazeData, hasAxe, portals, barriers, width, height)
            : RunDijkstra(start, end, mazeData, hasAxe, portals, barriers, width, height, normalStepCost);
    }

    // ── BFS (todos los costos iguales) ────────────────────────────────────────────

    private static PathfindResult RunBFS(
        Vector2Int start, Vector2Int end,
        MazeData mazeData, bool hasAxe,
        IList<PortalConnection> portals,
        HashSet<Vector2Int> barriers,
        int width, int height)
    {
        var queue    = new Queue<Vector2Int>();
        var visited  = new HashSet<Vector2Int>();
        var parent   = new Dictionary<Vector2Int, Vector2Int>();
        // Para portales: guardamos si el nodo fue alcanzado via portal y qué pairIndex
        var portalUsed = new Dictionary<Vector2Int, int>(); // celda → pairIndex que la puso en cola via portal

        queue.Enqueue(start);
        visited.Add(start);

        var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == end)
                return BuildResult(true, current, start, parent, portalUsed, 1f, 1f);

            // Movimiento normal
            foreach (var dir in directions)
            {
                var neighbor = current + dir;
                if (!InBounds(neighbor, width, height) || visited.Contains(neighbor)) continue;
                if (!CanEnter(neighbor, mazeData, hasAxe, barriers, end)) continue;

                visited.Add(neighbor);
                parent[neighbor] = current;
                queue.Enqueue(neighbor);
            }

            // Saltos de portal
            if (portals != null)
            {
                foreach (var portal in portals)
                {
                    if (!portal.IsActive) continue;

                    Vector2Int exitCell = Vector2Int.zero;
                    bool triggered = false;

                    if (current == portal.EntryA)
                    {
                        exitCell  = portal.ExitB;
                        triggered = true;
                    }
                    else if (current == portal.EntryB)
                    {
                        exitCell  = portal.ExitA;
                        triggered = true;
                    }

                    if (triggered && InBounds(exitCell, width, height) && !visited.Contains(exitCell))
                    {
                        if (IsWalkableForPathfinder(exitCell, mazeData, hasAxe, barriers))
                        {
                            visited.Add(exitCell);
                            parent[exitCell] = current;
                            portalUsed[exitCell] = portal.PairIndex;
                            queue.Enqueue(exitCell);
                        }
                    }
                }
            }
        }

        return PathfindResult.NoPath();
    }

    // ── Dijkstra (costos heterogéneos) ────────────────────────────────────────────

    private static PathfindResult RunDijkstra(
        Vector2Int start, Vector2Int end,
        MazeData mazeData, bool hasAxe,
        IList<PortalConnection> portals,
        HashSet<Vector2Int> barriers,
        int width, int height,
        float normalStepCost)
    {
        // cost[cell], parent[cell], portalUsed[cell → pairIndex]
        var cost       = new Dictionary<Vector2Int, float>();
        var parent     = new Dictionary<Vector2Int, Vector2Int>();
        var portalUsed = new Dictionary<Vector2Int, int>();

        // Min-heap simulado con SortedDictionary<float, Queue>
        // (evita dependencias externas; rendimiento adecuado para laberintos ≤ 50x50)
        var openByPriority = new SortedDictionary<float, Queue<Vector2Int>>();

        cost[start] = 0f;
        Enqueue(openByPriority, start, 0f);

        var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (openByPriority.Count > 0)
        {
            // Extraer el nodo de menor costo
            var firstKey = GetFirstKey(openByPriority);
            var current  = Dequeue(openByPriority, firstKey);

            float currentCost = cost.TryGetValue(current, out float cc) ? cc : float.MaxValue;

            if (current == end)
                return BuildResult(true, current, start, parent, portalUsed, currentCost, normalStepCost);

            // Si encontramos un nodo cuyo costo ya fue superado, lo ignoramos
            // (lazy deletion — el costo almacenado es el canónico)
            if (currentCost > (cost.TryGetValue(current, out float storedCost) ? storedCost : float.MaxValue))
                continue;

            // Movimiento normal
            foreach (var dir in directions)
            {
                var neighbor = current + dir;
                if (!InBounds(neighbor, width, height)) continue;
                if (!CanEnter(neighbor, mazeData, hasAxe, barriers, end)) continue;

                float newCost = currentCost + normalStepCost;
                if (newCost < (cost.TryGetValue(neighbor, out float nc) ? nc : float.MaxValue))
                {
                    cost[neighbor]   = newCost;
                    parent[neighbor] = current;
                    Enqueue(openByPriority, neighbor, newCost);
                }
            }

            // Saltos de portal
            if (portals != null)
            {
                foreach (var portal in portals)
                {
                    if (!portal.IsActive) continue;

                    Vector2Int exitCell = Vector2Int.zero;
                    bool triggered = false;

                    if (current == portal.EntryA)
                    {
                        exitCell  = portal.ExitB;
                        triggered = true;
                    }
                    else if (current == portal.EntryB)
                    {
                        exitCell  = portal.ExitA;
                        triggered = true;
                    }

                    if (triggered && InBounds(exitCell, width, height))
                    {
                        if (IsWalkableForPathfinder(exitCell, mazeData, hasAxe, barriers))
                        {
                            float newCost = currentCost + portal.TeleportCost;
                            if (newCost < (cost.TryGetValue(exitCell, out float ec) ? ec : float.MaxValue))
                            {
                                cost[exitCell]       = newCost;
                                parent[exitCell]     = current;
                                portalUsed[exitCell] = portal.PairIndex;
                                Enqueue(openByPriority, exitCell, newCost);
                            }
                        }
                    }
                }
            }
        }

        return PathfindResult.NoPath();
    }

    // ── Reconstrucción del resultado ──────────────────────────────────────────────

    private static PathfindResult BuildResult(
        bool found,
        Vector2Int end,
        Vector2Int start,
        Dictionary<Vector2Int, Vector2Int> parent,
        Dictionary<Vector2Int, int> portalUsed,
        float totalCost,
        float normalStepCost)
    {
        var result = new PathfindResult
        {
            PathExists = found,
            TotalCost  = found ? totalCost : float.MaxValue,
        };

        if (!found) return result;

        // Reconstruir camino desde end → start
        var path = new List<Vector2Int>();
        var portalsUsedSet = new HashSet<int>();
        var current = end;

        while (current != start)
        {
            path.Add(current);

            if (portalUsed.TryGetValue(current, out int pairIndex))
            {
                result.TeleportCount++;
                portalsUsedSet.Add(pairIndex);
            }
            else
            {
                result.WalkingSteps++;
            }

            current = parent[current];
        }
        path.Add(start);
        path.Reverse();

        result.Cells = path;
        result.PortalPairIndicesUsed = new List<int>(portalsUsedSet);

        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private static bool InBounds(Vector2Int cell, int width, int height)
        => cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

    /// <summary>
    /// Verifica si se puede entrar a una celda en movimiento normal.
    /// Respeta barreras (requieren hacha) y el mapa lógico.
    /// Permite ingresar a la celda objetivo final aunque esté marcada como ocupada por el prefab de la puerta.
    /// </summary>
    private static bool CanEnter(Vector2Int cell, MazeData mazeData, bool hasAxe, HashSet<Vector2Int> barriers, Vector2Int targetEnd = default)
    {
        if (barriers != null && barriers.Contains(cell) && !hasAxe) return false;

        if (targetEnd != default && cell == targetEnd)
        {
            return mazeData.IsCellWalkableIgnoreOccupied(cell.x, cell.y);
        }

        return mazeData.IsWalkable(cell.x, cell.y);
    }

    /// <summary>
    /// Verifica si una celda de salida de portal es alcanzable.
    /// Las celdas de salida de portal pueden estar marcadas como Occupied (por el prefab de la cueva),
    /// por lo que usamos IsWalkable que incluye AccessibleZone, pero excluimos el check de IsOccupied
    /// solo para la celda de salida cuando es una AccessibleZone.
    /// </summary>
    private static bool IsWalkableForPathfinder(Vector2Int cell, MazeData mazeData, bool hasAxe, HashSet<Vector2Int> barriers)
    {
        // Para celdas de salida de portal: basta con que sea Path o AccessibleZone,
        // aunque esté marcada como Occupied por el prefab de la cueva del otro lado.
        // Usamos IsWalkable que ya combina Path/AccessibleZone y NOT Occupied.
        // Si la salida del portal es la celda exacta donde está el prefab, usaremos
        // la celda adyacente (ExitA / ExitB se calculan como adyacentes en TravelCavePairManager).
        if (barriers != null && barriers.Contains(cell) && !hasAxe) return false;
        return mazeData.IsWalkable(cell.x, cell.y);
    }

    private static bool AllCostsEqual(IList<PortalConnection> portals, float normalStepCost)
    {
        if (portals == null || portals.Count == 0) return true;
        foreach (var p in portals)
        {
            if (p.IsActive && !Mathf.Approximately(p.TeleportCost, normalStepCost))
                return false;
        }
        return true;
    }

    // ── Min-heap helpers (SortedDictionary<float, Queue<Vector2Int>>) ──────────────

    private static void Enqueue(SortedDictionary<float, Queue<Vector2Int>> dict, Vector2Int cell, float priority)
    {
        if (!dict.TryGetValue(priority, out var q))
        {
            q = new Queue<Vector2Int>();
            dict[priority] = q;
        }
        q.Enqueue(cell);
    }

    private static float GetFirstKey(SortedDictionary<float, Queue<Vector2Int>> dict)
    {
        foreach (var key in dict.Keys) return key;
        return float.MaxValue;
    }

    private static Vector2Int Dequeue(SortedDictionary<float, Queue<Vector2Int>> dict, float key)
    {
        var q    = dict[key];
        var cell = q.Dequeue();
        if (q.Count == 0) dict.Remove(key);
        return cell;
    }
}
