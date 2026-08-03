using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validador y calculador de rutas del laberinto.
///
/// FIRMAS ORIGINALES (compatibilidad total):
///   CanPathfind(..., Vector2Int cuevaA, ...) — misión obligatoria, una pareja fija.
///   GetPath(..., Vector2Int cuevaA, ...) — misión obligatoria, una pareja fija.
///
/// FIRMAS NUEVAS (múltiples parejas):
///   CanPathfind(..., IList<PortalConnection> portals, ...)
///   GetPath(..., IList<PortalConnection> portals, ...)
///
/// Para pathfinding con costos iguales (BFS) o distintos (Dijkstra), usar MazePathfinder.
/// LevelValidator mantiene su propio BFS para la validación de misión (no requiere costos).
/// </summary>
public class LevelValidator : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // REGIÓN: Firmas originales (compatibilidad — una pareja obligatoria)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Realiza una búsqueda BFS sobre la rejilla de MazeData,
    /// simulando la posesión de hacha, uso de portales de cuevas y barreras destructibles.
    /// Firma original — conservada para compatibilidad con DynamicLevelManager.
    /// </summary>
    public static bool CanPathfind(
        Vector2Int start,
        Vector2Int end,
        MazeData mazeData,
        bool hasAxe,
        bool useCaves,
        Vector2Int cuevaA,
        Vector2Int cuevaBExit,
        Vector2Int cuevaB,
        Vector2Int cuevaAExit,
        HashSet<Vector2Int> barriers,
        int width,
        int height
    )
    {
        if (start.x < 0 || start.x >= width || start.y < 0 || start.y >= height) return false;
        if (end.x   < 0 || end.x   >= width || end.y   < 0 || end.y   >= height) return false;

        Queue<Vector2Int> queue   = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == end)
                return true;

            // Si pisamos Cueva A y podemos usar portales, nos teletransportamos al punto seguro de salida de la Cueva B
            if (useCaves && current == cuevaA)
            {
                if (!visited.Contains(cuevaBExit))
                {
                    visited.Add(cuevaBExit);
                    queue.Enqueue(cuevaBExit);
                }
            }

            // Si pisamos Cueva B y podemos usar portales, nos teletransportamos al punto seguro de salida de la Cueva A
            if (useCaves && current == cuevaB)
            {
                if (!visited.Contains(cuevaAExit))
                {
                    visited.Add(cuevaAExit);
                    queue.Enqueue(cuevaAExit);
                }
            }

            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (neighbor.x >= 0 && neighbor.x < width && neighbor.y >= 0 && neighbor.y < height)
                {
                    if (!visited.Contains(neighbor))
                    {
                        bool canStep = (neighbor == end) ? mazeData.IsCellWalkableIgnoreOccupied(neighbor.x, neighbor.y) : mazeData.IsWalkable(neighbor.x, neighbor.y);
                        if (canStep)
                        {
                            if (barriers.Contains(neighbor))
                            {
                                if (hasAxe)
                                {
                                    visited.Add(neighbor);
                                    queue.Enqueue(neighbor);
                                }
                            }
                            else
                            {
                                visited.Add(neighbor);
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Retorna la ruta completa de celdas (List&lt;Vector2Int&gt;) que conecta start con end,
    /// o una lista vacía si no existe camino transitable.
    /// Firma original — conservada para compatibilidad con DynamicLevelManager.
    /// </summary>
    public static List<Vector2Int> GetPath(
        Vector2Int start,
        Vector2Int end,
        MazeData mazeData,
        bool hasAxe,
        bool useCaves,
        Vector2Int cuevaA,
        Vector2Int cuevaBExit,
        Vector2Int cuevaB,
        Vector2Int cuevaAExit,
        HashSet<Vector2Int> barriers,
        int width,
        int height
    )
    {
        List<Vector2Int> path = new List<Vector2Int>();
        if (start.x < 0 || start.x >= width || start.y < 0 || start.y >= height) return path;
        if (end.x   < 0 || end.x   >= width || end.y   < 0 || end.y   >= height) return path;

        Queue<Vector2Int> queue    = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited  = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parentMap = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        bool found = false;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == end)
            {
                found = true;
                break;
            }

            // Si pisamos Cueva A y podemos usar portales, nos teletransportamos al punto seguro de salida de la Cueva B
            if (useCaves && current == cuevaA)
            {
                if (!visited.Contains(cuevaBExit))
                {
                    visited.Add(cuevaBExit);
                    parentMap[cuevaBExit] = current;
                    queue.Enqueue(cuevaBExit);
                }
            }

            // Si pisamos Cueva B y podemos usar portales, nos teletransportamos al punto seguro de salida de la Cueva A
            if (useCaves && current == cuevaB)
            {
                if (!visited.Contains(cuevaAExit))
                {
                    visited.Add(cuevaAExit);
                    parentMap[cuevaAExit] = current;
                    queue.Enqueue(cuevaAExit);
                }
            }

            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (neighbor.x >= 0 && neighbor.x < width && neighbor.y >= 0 && neighbor.y < height)
                {
                    if (!visited.Contains(neighbor))
                    {
                        bool canStep = (neighbor == end) ? mazeData.IsCellWalkableIgnoreOccupied(neighbor.x, neighbor.y) : mazeData.IsWalkable(neighbor.x, neighbor.y);
                        if (canStep)
                        {
                            if (barriers.Contains(neighbor))
                            {
                                if (hasAxe)
                                {
                                    visited.Add(neighbor);
                                    parentMap[neighbor] = current;
                                    queue.Enqueue(neighbor);
                                }
                            }
                            else
                            {
                                visited.Add(neighbor);
                                parentMap[neighbor] = current;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }
            }
        }

        if (found)
        {
            Vector2Int curr = end;
            while (curr != start)
            {
                path.Add(curr);
                curr = parentMap[curr];
            }
            path.Add(start);
            path.Reverse();
        }

        return path;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // REGIÓN: Nuevas sobrecargas con IList<PortalConnection> (múltiples parejas)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifica si existe un camino entre start y end considerando una lista de portales.
    /// Usa BFS (todos los portales se tratan igual que pasos normales a efectos de alcanzabilidad).
    /// Para ruta óptima con costos, usar MazePathfinder.FindPathWithPortals.
    /// </summary>
    public static bool CanPathfind(
        Vector2Int start,
        Vector2Int end,
        MazeData mazeData,
        bool hasAxe,
        IList<PortalConnection> portals,
        HashSet<Vector2Int> barriers,
        int width,
        int height
    )
    {
        if (start.x < 0 || start.x >= width || start.y < 0 || start.y >= height) return false;
        if (end.x   < 0 || end.x   >= width || end.y   < 0 || end.y   >= height) return false;

        Queue<Vector2Int> queue    = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited  = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == end) return true;

            // Saltos de portal
            if (portals != null)
            {
                foreach (var portal in portals)
                {
                    if (!portal.IsActive) continue;

                    Vector2Int exitCell = Vector2Int.zero;
                    bool triggered = false;

                    if (current == portal.EntryA) { exitCell = portal.ExitB; triggered = true; }
                    else if (current == portal.EntryB) { exitCell = portal.ExitA; triggered = true; }

                    if (triggered && exitCell.x >= 0 && exitCell.x < width &&
                        exitCell.y >= 0 && exitCell.y < height && !visited.Contains(exitCell))
                    {
                        bool canStepExit = (exitCell == end) ? mazeData.IsCellWalkableIgnoreOccupied(exitCell.x, exitCell.y) : mazeData.IsWalkable(exitCell.x, exitCell.y);
                        if (canStepExit)
                        {
                            visited.Add(exitCell);
                            queue.Enqueue(exitCell);
                        }
                    }
                }
            }

            // Movimiento normal
            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;
                if (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height) continue;
                if (visited.Contains(neighbor)) continue;
                bool canStep = (neighbor == end) ? mazeData.IsCellWalkableIgnoreOccupied(neighbor.x, neighbor.y) : mazeData.IsWalkable(neighbor.x, neighbor.y);
                if (!canStep) continue;
                if (barriers != null && barriers.Contains(neighbor) && !hasAxe) continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return false;
    }

    /// <summary>
    /// Retorna la ruta de celdas (BFS) considerando una lista de portales.
    /// Para ruta óptima con costos Dijkstra, usar MazePathfinder.FindPathWithPortals.
    /// </summary>
    public static List<Vector2Int> GetPath(
        Vector2Int start,
        Vector2Int end,
        MazeData mazeData,
        bool hasAxe,
        IList<PortalConnection> portals,
        HashSet<Vector2Int> barriers,
        int width,
        int height
    )
    {
        List<Vector2Int> path = new List<Vector2Int>();
        if (start.x < 0 || start.x >= width || start.y < 0 || start.y >= height) return path;
        if (end.x   < 0 || end.x   >= width || end.y   < 0 || end.y   >= height) return path;

        Queue<Vector2Int> queue    = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited  = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parentMap = new Dictionary<Vector2Int, Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        bool found = false;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == end) { found = true; break; }

            // Saltos de portal
            if (portals != null)
            {
                foreach (var portal in portals)
                {
                    if (!portal.IsActive) continue;

                    Vector2Int exitCell = Vector2Int.zero;
                    bool triggered = false;

                    if (current == portal.EntryA) { exitCell = portal.ExitB; triggered = true; }
                    else if (current == portal.EntryB) { exitCell = portal.ExitA; triggered = true; }

                    if (triggered && exitCell.x >= 0 && exitCell.x < width &&
                        exitCell.y >= 0 && exitCell.y < height && !visited.Contains(exitCell))
                    {
                        bool canStepExit = (exitCell == end) ? mazeData.IsCellWalkableIgnoreOccupied(exitCell.x, exitCell.y) : mazeData.IsWalkable(exitCell.x, exitCell.y);
                        if (canStepExit)
                        {
                            visited.Add(exitCell);
                            parentMap[exitCell] = current;
                            queue.Enqueue(exitCell);
                        }
                    }
                }
            }

            // Movimiento normal
            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;
                if (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height) continue;
                if (visited.Contains(neighbor)) continue;
                bool canStep = (neighbor == end) ? mazeData.IsCellWalkableIgnoreOccupied(neighbor.x, neighbor.y) : mazeData.IsWalkable(neighbor.x, neighbor.y);
                if (!canStep) continue;
                if (barriers != null && barriers.Contains(neighbor) && !hasAxe) continue;

                visited.Add(neighbor);
                parentMap[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        if (found)
        {
            Vector2Int curr = end;
            while (curr != start)
            {
                path.Add(curr);
                curr = parentMap[curr];
            }
            path.Add(start);
            path.Reverse();
        }

        return path;
    }
}
