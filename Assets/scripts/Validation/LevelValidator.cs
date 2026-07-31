using System.Collections.Generic;
using UnityEngine;

public class LevelValidator : MonoBehaviour
{
    /// <summary>
    /// Realiza una búsqueda de caminos lógica (BFS) sobre la rejilla de MazeData,
    /// simulando la posesión de hacha, uso de portales de cuevas y barreras destructibles.
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
        if (end.x < 0 || end.x >= width || end.y < 0 || end.y >= height) return false;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == end)
            {
                return true;
            }

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
                        // Verificar que sea transitable lógicamente en MazeData
                        if (mazeData.IsWalkable(neighbor.x, neighbor.y))
                        {
                            // Si es una celda con barrera destructible
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
    /// Retorna la ruta completa de celdas (List<Vector2Int>) que conecta start con end,
    /// o una lista vacía si no existe camino transitable.
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
        if (end.x < 0 || end.x >= width || end.y < 0 || end.y >= height) return path;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
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
                        if (mazeData.IsWalkable(neighbor.x, neighbor.y))
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
}
