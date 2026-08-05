using System.Collections.Generic;
using UnityEngine;

public class MazeData : MonoBehaviour
{
    [System.Serializable]
    public struct CellState
    {
        public bool IsPath;
        public bool IsOccupied;
        public bool IsMainRegion;
        public bool IsAccessibleZone;
    }

    [SerializeField]
    private bool showGizmos = false;

    [SerializeField]
    private Color wallColor = Color.black;

    [SerializeField]
    private Color pathColor = Color.white;

    [SerializeField]
    private Color mainRegionColor = Color.green;

    [SerializeField]
    private Color occupiedColor = Color.red;

    private int width;
    private int height;
    private CellState[,] cells;
    
    private Vector3 mapOrigin = Vector3.zero;
    private Vector2Int cellSize = Vector2Int.one;

    public int Width => width;
    public int Height => height;
    public Vector3 MapOrigin => mapOrigin;
    public Vector2Int CellSize => cellSize;

    public void Initialize(MazeCellType[,] maze, Vector3 origin, Vector2Int logicalCellSize)
    {
        width = maze.GetLength(0);
        height = maze.GetLength(1);
        cells = new CellState[width, height];
        mapOrigin = origin;
        cellSize = logicalCellSize;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y] = new CellState
                {
                    IsPath = maze[x, y] == MazeCellType.Path,
                    IsOccupied = false,
                    IsMainRegion = false,
                    IsAccessibleZone = false
                };
            }
        }
    }

    public void MarkCellsAsOccupied(Vector2Int originCell, int patternWidth, int patternHeight)
    {
        if (cells == null) return;

        for (int x = 0; x < patternWidth; x++)
        {
            for (int y = 0; y < patternHeight; y++)
            {
                int cellX = originCell.x + x;
                int cellY = originCell.y + y;

                if (cellX >= 0 && cellX < width && cellY >= 0 && cellY < height)
                {
                    cells[cellX, cellY].IsOccupied = true;
                }
            }
        }
    }

    public void UnmarkCellAsOccupied(Vector2Int cell)
    {
        UnmarkCellAsOccupied(cell.x, cell.y);
    }

    public void UnmarkCellAsOccupied(int x, int y)
    {
        if (cells == null || x < 0 || x >= width || y < 0 || y >= height) return;
        cells[x, y].IsOccupied = false;
    }

    public void SetCellOccupiedState(int x, int y, bool occupied)
    {
        if (cells == null || x < 0 || x >= width || y < 0 || y >= height) return;
        cells[x, y].IsOccupied = occupied;
    }

    public void CalculateMainRegion(Vector2Int startCell)
    {
        if (cells == null) return;

        int totalWalkable = 0;
        int totalOccupied = 0;

        // Reset main region
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y].IsMainRegion = false;
                if (cells[x, y].IsPath)
                {
                    if (cells[x, y].IsOccupied) totalOccupied++;
                    else totalWalkable++;
                }
            }
        }

        bool[,] visited = new bool[width, height];
        
        // Empezamos siempre desde la celda de inicio
        if (IsCellWalkable(startCell.x, startCell.y))
        {
            List<Vector2Int> mainRegion = GetConnectedRegion(startCell.x, startCell.y, visited);
            foreach (var cell in mainRegion)
            {
                cells[cell.x, cell.y].IsMainRegion = true;
            }
            Debug.Log($"MazeData: Región principal conectada desde el spawn {startCell}. Tamaño: {mainRegion.Count} celdas.", this);
        }
        else
        {
            Debug.LogWarning($"MazeData: La celda de inicio {startCell} no es transitable o está aislada en esta iteración de prueba.", this);
        }
    }

    private List<Vector2Int> GetConnectedRegion(int startX, int startY, bool[,] visited)
    {
        List<Vector2Int> region = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int start = new Vector2Int(startX, startY);
        queue.Enqueue(start);
        visited[startX, startY] = true;
        region.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (var dir in directions)
            {
                int nx = current.x + dir.x;
                int ny = current.y + dir.y;

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (!visited[nx, ny] && IsCellWalkable(nx, ny))
                    {
                        visited[nx, ny] = true;
                        Vector2Int neighbor = new Vector2Int(nx, ny);
                        queue.Enqueue(neighbor);
                        region.Add(neighbor);
                    }
                }
            }
        }

        return region;
    }

    public bool IsWalkable(int x, int y)
    {
        if (cells == null || x < 0 || x >= width || y < 0 || y >= height) return false;
        return (cells[x, y].IsPath || cells[x, y].IsAccessibleZone) && !cells[x, y].IsOccupied;
    }

    public bool IsCellWalkableIgnoreOccupied(int x, int y)
    {
        if (cells == null || x < 0 || x >= width || y < 0 || y >= height) return false;
        return cells[x, y].IsPath || cells[x, y].IsAccessibleZone;
    }

    public bool IsCellWalkable(int x, int y)
    {
        return IsWalkable(x, y);
    }

    public bool IsCellWalkableAndMain(int x, int y)
    {
        return IsWalkable(x, y) && cells[x, y].IsMainRegion;
    }

    public void ConvertToAccessibleZone(int x, int y)
    {
        if (cells == null || x < 0 || x >= width || y < 0 || y >= height) return;
        cells[x, y].IsAccessibleZone = true;
        cells[x, y].IsPath = true; // Garantiza transitabilidad por otros sistemas
    }

    public void SetCellAsWall(int x, int y)
    {
        if (cells == null || x < 0 || x >= width || y < 0 || y >= height) return;
        cells[x, y].IsPath = false;
        cells[x, y].IsAccessibleZone = false;
        cells[x, y].IsMainRegion = false;
    }

    public Vector2Int GetNearestWalkableCell(Vector2Int cell)
    {
        if (IsWalkable(cell.x, cell.y)) return cell;

        // BFS en espiral para encontrar la celda transitable más cercana
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
                                    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };

        for (int r = 1; r <= 3; r++)
        {
            foreach (var dir in directions)
            {
                Vector2Int cand = cell + dir * r;
                if (IsWalkable(cand.x, cand.y))
                {
                    return cand;
                }
            }
        }

        return cell; // Fallback
    }

    public Vector2Int GetValidStartCell(Vector2Int preferredStart)
    {
        if (IsCellWalkableAndMain(preferredStart.x, preferredStart.y))
        {
            return preferredStart;
        }

        // Buscar una alternativa segura
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (IsCellWalkableAndMain(x, y))
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        return preferredStart; // Fallback
    }

    public Vector2Int SelectFurthestCell(Vector2Int startCell, int seed, int minDistance)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        Vector2Int furthestCell = startCell;
        float maxDist = -1f;

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (IsCellWalkableAndMain(x, y))
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (cell == startCell) continue;

                    float dist = Vector2Int.Distance(cell, startCell);

                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        furthestCell = cell;
                    }

                    if (dist >= minDistance)
                    {
                        candidates.Add(cell);
                    }
                }
            }
        }

        if (candidates.Count > 0)
        {
            System.Random random = new System.Random(seed);
            return candidates[random.Next(candidates.Count)];
        }

        return furthestCell;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || cells == null) return;

        Vector3 size = new Vector3(cellSize.x, cellSize.y, 0.1f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CellState state = cells[x, y];
                
                Vector3 worldPos = mapOrigin + new Vector3(
                    x * cellSize.x + cellSize.x * 0.5f,
                    y * cellSize.y + cellSize.y * 0.5f,
                    0.05f // Pequeño offset Z para no solapar con los Gizmos de ruta
                );

                Color baseColor;
                if (state.IsOccupied)
                {
                    baseColor = occupiedColor;
                }
                else if (state.IsAccessibleZone)
                {
                    baseColor = new Color(0f, 1f, 1f, 0.5f); // Cyan semitransparente
                }
                else if (state.IsMainRegion)
                {
                    baseColor = mainRegionColor;
                }
                else if (state.IsPath)
                {
                    baseColor = pathColor;
                }
                else
                {
                    baseColor = wallColor;
                }

                // Usar color semitransparente para no tapar los Gizmos de caminos de DynamicLevelManager
                Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.25f);
                Gizmos.DrawCube(worldPos, size * 0.95f);
                Gizmos.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.6f);
                Gizmos.DrawWireCube(worldPos, size * 0.95f);
            }
        }
    }
}
