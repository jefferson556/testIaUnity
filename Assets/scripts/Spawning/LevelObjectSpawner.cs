using System.Collections.Generic;
using UnityEngine;

public class LevelObjectSpawner : MonoBehaviour
{
    [Header("Destructible Configuration Set")]
    [SerializeField]
    private MazeDestructibleSet destructibleSet;

    [Header("Sorting Layer Settings")]
    [SerializeField]
    private string targetSortingLayer = "BreakableObjects";

    [SerializeField]
    private int targetSortingOrder = 100;

    [Header("Hierarchy Container")]
    [SerializeField]
    private Transform gameplayObjectsContainer;

    private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    private Dictionary<MazeDestructiblePattern, int> patternSpawnCounts =
        new Dictionary<MazeDestructiblePattern, int>();

    public HashSet<Vector3Int> OccupiedCells => occupiedCells;
    public string TargetSortingLayer => targetSortingLayer;
    public int TargetSortingOrder => targetSortingOrder;

    public void SpawnDestructibles(
        MazeCellType[,] mazeMatrix,
        Vector3Int tilemapOrigin,
        Vector2Int logicalCellTileSize,
        Vector3Int playerCellPosition,
        Vector3Int caveCellPosition,
        int seedOverride = -1)
    {
        if (destructibleSet == null || destructibleSet.Patterns == null || destructibleSet.Patterns.Count == 0)
        {
            Debug.LogWarning("LevelObjectSpawner: No MazeDestructibleSet assigned or pattern list is empty.", this);
            return;
        }

        ClearOccupiedCells();
        occupiedCells.Add(playerCellPosition);
        occupiedCells.Add(caveCellPosition);
        patternSpawnCounts.Clear();

        foreach (var pattern in destructibleSet.Patterns)
        {
            if (pattern != null)
            {
                patternSpawnCounts[pattern] = 0;
            }
        }

        System.Random random = InitializeRandomGenerator(seedOverride);

        int mazeWidth = mazeMatrix.GetLength(0);
        int mazeHeight = mazeMatrix.GetLength(1);

        int totalToSpawn = random.Next(
            destructibleSet.MinimumTotalAmount,
            destructibleSet.MaximumTotalAmount + 1
        );

        int totalSpawned = 0;
        int maxTotalAttempts = totalToSpawn * destructibleSet.PlacementAttemptsPerObject;
        int currentAttempt = 0;

        EnsureMinimumPatternAmounts(
            mazeMatrix,
            tilemapOrigin,
            logicalCellTileSize,
            playerCellPosition,
            caveCellPosition,
            random,
            ref totalSpawned
        );

        while (totalSpawned < totalToSpawn && currentAttempt < maxTotalAttempts)
        {
            currentAttempt++;

            MazeDestructiblePattern pattern = SelectPatternWeighted(random);
            if (pattern == null)
            {
                break;
            }

            if (patternSpawnCounts.TryGetValue(pattern, out int count) && count >= pattern.MaximumAmount)
            {
                continue;
            }

            if (TrySpawnPatternAtRandomPosition(
                    pattern,
                    mazeMatrix,
                    tilemapOrigin,
                    logicalCellTileSize,
                    playerCellPosition,
                    caveCellPosition,
                    random))
            {
                patternSpawnCounts[pattern] = count + 1;
                totalSpawned++;
            }
        }

        Debug.Log($"LevelObjectSpawner: ¡ÉXITO! Se instanciaron {totalSpawned} objetos destruibles sobre las celdas de camino del laberinto.", this);
    }

    public void ClearOccupiedCells()
    {
        occupiedCells.Clear();

        if (gameplayObjectsContainer != null)
        {
            for (int i = gameplayObjectsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(gameplayObjectsContainer.GetChild(i).gameObject);
            }
        }
    }

    private System.Random InitializeRandomGenerator(int seedOverride)
    {
        if (seedOverride >= 0)
        {
            return new System.Random(seedOverride);
        }

        if (destructibleSet != null && destructibleSet.UseSeed)
        {
            return new System.Random(destructibleSet.Seed);
        }

        return new System.Random();
    }

    private void EnsureMinimumPatternAmounts(
        MazeCellType[,] mazeMatrix,
        Vector3Int tilemapOrigin,
        Vector2Int logicalCellTileSize,
        Vector3Int playerCellPosition,
        Vector3Int caveCellPosition,
        System.Random random,
        ref int totalSpawned)
    {
        foreach (var pattern in destructibleSet.Patterns)
        {
            if (pattern == null || !pattern.IsConfigured || pattern.MinimumAmount <= 0)
            {
                continue;
            }

            int required = pattern.MinimumAmount;
            int spawnedForPattern = 0;
            int attempts = 0;
            int maxAttempts = required * destructibleSet.PlacementAttemptsPerObject;

            while (spawnedForPattern < required && attempts < maxAttempts)
            {
                attempts++;

                if (TrySpawnPatternAtRandomPosition(
                        pattern,
                        mazeMatrix,
                        tilemapOrigin,
                        logicalCellTileSize,
                        playerCellPosition,
                        caveCellPosition,
                        random))
                {
                    spawnedForPattern++;
                    totalSpawned++;
                    patternSpawnCounts[pattern] = spawnedForPattern;
                }
            }
        }
    }

    private MazeDestructiblePattern SelectPatternWeighted(System.Random random)
    {
        List<MazeDestructiblePattern> candidates = new List<MazeDestructiblePattern>();
        float totalWeight = 0f;

        foreach (var pattern in destructibleSet.Patterns)
        {
            if (pattern == null || !pattern.IsConfigured)
            {
                continue;
            }

            if (patternSpawnCounts.TryGetValue(pattern, out int count) && count >= pattern.MaximumAmount)
            {
                continue;
            }

            candidates.Add(pattern);
            totalWeight += Mathf.Max(0.01f, pattern.SelectionWeight);
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
        {
            return null;
        }

        if (!destructibleSet.UseWeightedSelection)
        {
            return candidates[random.Next(candidates.Count)];
        }

        double roll = random.NextDouble() * totalWeight;
        double accumulated = 0.0;

        foreach (var pattern in candidates)
        {
            accumulated += Mathf.Max(0.01f, pattern.SelectionWeight);
            if (roll <= accumulated)
            {
                return pattern;
            }
        }

        return candidates[candidates.Count - 1];
    }

    private bool TrySpawnPatternAtRandomPosition(
        MazeDestructiblePattern pattern,
        MazeCellType[,] mazeMatrix,
        Vector3Int tilemapOrigin,
        Vector2Int logicalCellTileSize,
        Vector3Int playerCellPosition,
        Vector3Int caveCellPosition,
        System.Random random)
    {
        int mazeWidth = mazeMatrix.GetLength(0);
        int mazeHeight = mazeMatrix.GetLength(1);

        int maxOriginX = Mathf.Max(1, mazeWidth - pattern.Width + 1);
        int maxOriginY = Mathf.Max(1, mazeHeight - pattern.Height + 1);

        int originX = random.Next(0, maxOriginX);
        int originY = random.Next(0, maxOriginY);

        Vector3Int originCell = new Vector3Int(originX, originY, 0);

        if (!ValidateRegionForPattern(
                pattern,
                originCell,
                mazeMatrix,
                playerCellPosition,
                caveCellPosition))
        {
            return false;
        }

        List<Vector3Int> patternCellsToReserve = GetCellsInPatternRegion(
            originCell,
            pattern.Width,
            pattern.Height
        );

        foreach (var cell in patternCellsToReserve)
        {
            occupiedCells.Add(cell);
        }

        Vector3 worldPos = CalculateWorldPosition(
            originCell,
            pattern,
            tilemapOrigin,
            logicalCellTileSize
        );

        Transform parentTransform = gameplayObjectsContainer != null
            ? gameplayObjectsContainer
            : transform;

        GameObject spawnedInstance = Instantiate(
            pattern.Prefab,
            worldPos,
            Quaternion.identity,
            parentTransform
        );

        spawnedInstance.name = $"{pattern.DisplayName}_{originX}_{originY}";

        DestructibleObject destructibleComp = spawnedInstance.GetComponent<DestructibleObject>();
        if (destructibleComp == null)
        {
            destructibleComp = spawnedInstance.AddComponent<DestructibleObject>();
        }

        destructibleComp.SetReservedCells(patternCellsToReserve);

        // Asignación ultra segura de Sorting Layer y Order in Layer (si la capa existe la usa, si no usa la capa por defecto)
        int layerID = !string.IsNullOrEmpty(targetSortingLayer) ? SortingLayer.NameToID(targetSortingLayer) : 0;
        bool isValidLayer = SortingLayer.IsValid(layerID);

        SpriteRenderer[] renderers = spawnedInstance.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (sr != null)
            {
                if (isValidLayer)
                {
                    sr.sortingLayerID = layerID;
                }
                sr.sortingOrder = targetSortingOrder;
            }
        }

        destructibleComp.OnDestroyed += OnDestructibleDestroyed;

        return true;
    }

    private bool ValidateRegionForPattern(
        MazeDestructiblePattern pattern,
        Vector3Int originCell,
        MazeCellType[,] mazeMatrix,
        Vector3Int playerCellPosition,
        Vector3Int caveCellPosition)
    {
        int mazeWidth = mazeMatrix.GetLength(0);
        int mazeHeight = mazeMatrix.GetLength(1);

        for (int x = 0; x < pattern.Width; x++)
        {
            for (int y = 0; y < pattern.Height; y++)
            {
                int cellX = originCell.x + x;
                int cellY = originCell.y + y;

                if (cellX < 0 || cellX >= mazeWidth || cellY < 0 || cellY >= mazeHeight)
                {
                    return false;
                }

                if (mazeMatrix[cellX, cellY] != MazeCellType.Path)
                {
                    return false;
                }

                Vector3Int checkCell = new Vector3Int(cellX, cellY, 0);

                if (Mathf.Abs(checkCell.x - playerCellPosition.x) <= 1 && Mathf.Abs(checkCell.y - playerCellPosition.y) <= 1)
                {
                    return false;
                }

                if (Mathf.Abs(checkCell.x - caveCellPosition.x) <= 1 && Mathf.Abs(checkCell.y - caveCellPosition.y) <= 1)
                {
                    return false;
                }

                if (occupiedCells.Contains(checkCell))
                {
                    return false;
                }

                if (Vector3Int.Distance(checkCell, playerCellPosition) < pattern.MinimumDistanceFromPlayer)
                {
                    return false;
                }

                if (Vector3Int.Distance(checkCell, caveCellPosition) < pattern.MinimumDistanceFromCave)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private List<Vector3Int> GetCellsInPatternRegion(Vector3Int originCell, int width, int height)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells.Add(new Vector3Int(originCell.x + x, originCell.y + y, 0));
            }
        }
        return cells;
    }

    private Vector3 CalculateWorldPosition(
        Vector3Int originCell,
        MazeDestructiblePattern pattern,
        Vector3Int tilemapOrigin,
        Vector2Int logicalCellTileSize)
    {
        int tileX = tilemapOrigin.x + (originCell.x + pattern.PivotCell.x) * logicalCellTileSize.x;
        int tileY = tilemapOrigin.y + (originCell.y + pattern.PivotCell.y) * logicalCellTileSize.y;

        float worldX = tileX + (logicalCellTileSize.x * 0.5f) + pattern.PositionOffset.x;
        float worldY = tileY + (logicalCellTileSize.y * 0.5f) + pattern.PositionOffset.y;

        return new Vector3(worldX, worldY, -0.1f);
    }

    private void OnDestructibleDestroyed(DestructibleObject destructible)
    {
        if (destructible == null)
        {
            return;
        }

        foreach (var cell in destructible.ReservedGridCells)
        {
            occupiedCells.Remove(cell);
        }
    }
}
