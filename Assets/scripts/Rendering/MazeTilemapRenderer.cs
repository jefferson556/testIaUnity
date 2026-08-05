using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum OuterBorderFillMode
{
    WallsAndDecorations,
    GroundAndDecorationsOnly,
    MixedDecorations
}

public class MazeTilemapRenderer : MonoBehaviour
{
    [Header("Main Tilemaps")]
    [SerializeField]
    private Tilemap groundTilemap;

    [SerializeField]
    private Tilemap pathTilemap;

    [SerializeField]
    private Tilemap wallTilemap;

    [Header("Decoration Tilemaps")]
    [SerializeField]
    private Tilemap decorationBackTilemap;

    [SerializeField]
    private Tilemap decorationFrontTilemap;

    [Header("Visual Sets")]
    [SerializeField]
    private MazeTileSet tileSet;

    [SerializeField]
    private MazeDecorationSet decorationSet;

    [Header("Logical Cell Visual Size")]
    [Tooltip(
        "Number of Tilemap cells used to draw " +
        "one logical maze cell."
    )]
    [SerializeField]
    private Vector2Int logicalCellTileSize =
        new Vector2Int(3, 3);

    [Header("Accessible Zone Settings")]
    [SerializeField]
    private TileBase accessibleZoneTile;

    [Header("Outer Border / Margin Settings")]
    [SerializeField]
    [Tooltip("Número de celdas lógicas extra alrededor del laberinto para cubrir el fondo de la cámara.")]
    private int outerMarginPadding = 6;

    [SerializeField]
    [Tooltip("Modo de relleno para el margen exterior del mapa.")]
    private OuterBorderFillMode outerFillMode = OuterBorderFillMode.MixedDecorations;

    [SerializeField, Range(0.1f, 3f)]
    [Tooltip("Multiplicador de densidad de decoraciones en el margen exterior.")]
    private float outerDecorationDensity = 1.8f;

    [Header("Map Position")]
    [SerializeField]
    private bool centerMaze = true;

    [SerializeField]
    private Vector2Int manualOrigin;

    private Vector3Int currentOrigin;

    public Vector3Int CurrentOrigin => currentOrigin;
    public Vector2Int LogicalCellTileSize => logicalCellTileSize;
    public Tilemap PathTilemap => pathTilemap;

    public void PreCalculateOrigin(MazeCellType[,] maze)
    {
        if (maze == null || !ValidateMainReferences(maze)) return;
        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);
        int renderedWidth = mazeWidth * logicalCellTileSize.x;
        int renderedHeight = mazeHeight * logicalCellTileSize.y;
        currentOrigin = CalculateOrigin(renderedWidth, renderedHeight);
    }

    public void Render(
        MazeCellType[,] maze,
        int visualSeed,
        Vector2Int startCell)
    {
        if (!ValidateMainReferences(maze))
        {
            return;
        }

        Clear();

        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        int renderedWidth =
            mazeWidth * logicalCellTileSize.x;

        int renderedHeight =
            mazeHeight * logicalCellTileSize.y;

        currentOrigin = CalculateOrigin(
            renderedWidth,
            renderedHeight
        );

        System.Random random =
            new System.Random(visualSeed);

        PaintMaze(
            maze,
            random
        );

        PaintDecorations(
            maze,
            random,
            startCell
        );

        RefreshTilemaps();
    }

    public Vector3Int GetTilePosition(
        Vector2Int mazeCell)
    {
        return currentOrigin +
               new Vector3Int(
                   mazeCell.x *
                   logicalCellTileSize.x,

                   mazeCell.y *
                   logicalCellTileSize.y,

                   0
               );
    }

    public Vector3 GetWorldPosition(
        Vector2Int mazeCell)
    {
        Vector3Int blockOrigin =
            GetTilePosition(mazeCell);

        Vector3Int centerTile =
            blockOrigin +
            new Vector3Int(
                logicalCellTileSize.x / 2,
                logicalCellTileSize.y / 2,
                0
            );

        return pathTilemap.GetCellCenterWorld(
            centerTile
        );
    }

    public Vector2Int GetCellFromWorldPosition(Vector3 worldPos)
    {
        if (logicalCellTileSize.x <= 0 || logicalCellTileSize.y <= 0 || pathTilemap == null) return Vector2Int.zero;
        Vector3Int tilePos = pathTilemap.WorldToCell(worldPos);
        int x = (tilePos.x - currentOrigin.x) / logicalCellTileSize.x;
        int y = (tilePos.y - currentOrigin.y) / logicalCellTileSize.y;
        return new Vector2Int(x, y);
    }

    public void Clear()
    {
        groundTilemap?.ClearAllTiles();
        pathTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        decorationBackTilemap?.ClearAllTiles();
        decorationFrontTilemap?.ClearAllTiles();
    }

    public void PaintAccessibleZone(List<Vector2Int> cells)
    {
        if (accessibleZoneTile == null || pathTilemap == null)
        {
            Debug.LogWarning("MazeTilemapRenderer: No accessibleZoneTile or pathTilemap reference.", this);
            return;
        }

        foreach (var cell in cells)
        {
            Vector3Int blockOrigin = GetTilePosition(cell);

            for (int lx = 0; lx < logicalCellTileSize.x; lx++)
            {
                for (int ly = 0; ly < logicalCellTileSize.y; ly++)
                {
                    Vector3Int pos = blockOrigin + new Vector3Int(lx, ly, 0);
                    
                    // Limpiar paredes y decoraciones de la celda
                    wallTilemap?.SetTile(pos, null);
                    decorationBackTilemap?.SetTile(pos, null);
                    decorationFrontTilemap?.SetTile(pos, null);
                    
                    // Pintar el tile transitable exclusivo
                    pathTilemap.SetTile(pos, accessibleZoneTile);
                }
            }
        }
        RefreshTilemaps();
    }

    public void PaintWallCell(Vector2Int cell, System.Random random)
    {
        if (wallTilemap == null || tileSet == null) return;
        Vector3Int blockOrigin = GetTilePosition(cell);
        
        for (int lx = 0; lx < logicalCellTileSize.x; lx++)
        {
            for (int ly = 0; ly < logicalCellTileSize.y; ly++)
            {
                Vector3Int pos = blockOrigin + new Vector3Int(lx, ly, 0);
                pathTilemap?.SetTile(pos, null);
                decorationBackTilemap?.SetTile(pos, null);
                decorationFrontTilemap?.SetTile(pos, null);
                
                wallTilemap.SetTile(pos, tileSet.GetWallTile(random));
            }
        }
        RefreshTilemaps();
    }

    private void PaintMaze(
        MazeCellType[,] maze,
        System.Random random)
    {
        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        int minX = -outerMarginPadding;
        int maxX = mazeWidth + outerMarginPadding;
        int minY = -outerMarginPadding;
        int maxY = mazeHeight + outerMarginPadding;

        for (int mazeX = minX; mazeX < maxX; mazeX++)
        {
            for (int mazeY = minY; mazeY < maxY; mazeY++)
            {
                Vector2Int mazeCell = new Vector2Int(mazeX, mazeY);
                bool isInsideMaze = mazeX >= 0 && mazeX < mazeWidth && mazeY >= 0 && mazeY < mazeHeight;

                if (isInsideMaze)
                {
                    PaintLogicalCell(mazeCell, maze[mazeX, mazeY], random);
                }
                else
                {
                    PaintOuterLogicalCell(mazeCell, random);
                }
            }
        }
    }

    private void PaintOuterLogicalCell(Vector2Int mazeCell, System.Random random)
    {
        Vector3Int blockOrigin = GetTilePosition(mazeCell);

        // Siempre pintar suelo de fondo para tapar la cámara azul
        PaintGroundBlock(blockOrigin, random);

        if (outerFillMode == OuterBorderFillMode.WallsAndDecorations)
        {
            PaintWallBlock(blockOrigin, random);
            TryPaintWallPattern(blockOrigin, random);
        }
        else if (outerFillMode == OuterBorderFillMode.MixedDecorations)
        {
            // 60% Muro con patrón / 40% Suelo base para dar espacio a vegetación y adornos
            if (random.NextDouble() < 0.60)
            {
                PaintWallBlock(blockOrigin, random);
                TryPaintWallPattern(blockOrigin, random);
            }
            else
            {
                PaintPathBlock(blockOrigin, random);
            }
        }
        else // GroundAndDecorationsOnly
        {
            PaintPathBlock(blockOrigin, random);
        }
    }

    private void PaintLogicalCell(
        Vector2Int mazeCell,
        MazeCellType cellType,
        System.Random random)
    {
        Vector3Int blockOrigin =
            GetTilePosition(mazeCell);

        PaintGroundBlock(
            blockOrigin,
            random
        );

        if (cellType == MazeCellType.Path)
        {
            PaintPathBlock(
                blockOrigin,
                random
            );

            return;
        }

        PaintWallBlock(
            blockOrigin,
            random
        );

        TryPaintWallPattern(
            blockOrigin,
            random
        );
    }

    private void PaintGroundBlock(
        Vector3Int blockOrigin,
        System.Random random)
    {
        for (int localX = 0;
             localX < logicalCellTileSize.x;
             localX++)
        {
            for (int localY = 0;
                 localY < logicalCellTileSize.y;
                 localY++)
            {
                Vector3Int position =
                    blockOrigin +
                    new Vector3Int(
                        localX,
                        localY,
                        0
                    );

                groundTilemap.SetTile(
                    position,
                    tileSet.GetGroundTile(random)
                );
            }
        }
    }

    private void PaintPathBlock(
        Vector3Int blockOrigin,
        System.Random random)
    {
        for (int localX = 0;
             localX < logicalCellTileSize.x;
             localX++)
        {
            for (int localY = 0;
                 localY < logicalCellTileSize.y;
                 localY++)
            {
                Vector3Int position =
                    blockOrigin +
                    new Vector3Int(
                        localX,
                        localY,
                        0
                    );

                pathTilemap.SetTile(
                    position,
                    tileSet.GetPathTile(random)
                );
            }
        }
    }

    private void PaintWallBlock(
        Vector3Int blockOrigin,
        System.Random random)
    {
        for (int localX = 0;
             localX < logicalCellTileSize.x;
             localX++)
        {
            for (int localY = 0;
                 localY < logicalCellTileSize.y;
                 localY++)
            {
                Vector3Int position =
                    blockOrigin +
                    new Vector3Int(
                        localX,
                        localY,
                        0
                    );

                wallTilemap.SetTile(
                    position,
                    tileSet.GetWallTile(random)
                );
            }
        }
    }

    private void TryPaintWallPattern(
        Vector3Int blockOrigin,
        System.Random random)
    {
        if (random.NextDouble() >
            tileSet.WallPatternProbability)
        {
            return;
        }

        List<MazeWallPattern> candidates =
            GetWallPatternsThatFit();

        if (candidates.Count == 0)
        {
            return;
        }

        MazeWallPattern selectedPattern =
            candidates[
                random.Next(candidates.Count)
            ];

        PaintWallPatternInsideBlock(
            selectedPattern,
            blockOrigin,
            random
        );
    }

    private List<MazeWallPattern>
        GetWallPatternsThatFit()
    {
        List<MazeWallPattern> candidates =
            new List<MazeWallPattern>();

        MazeWallPattern[] patterns =
            tileSet.WallPatterns;

        if (patterns == null)
        {
            return candidates;
        }

        foreach (MazeWallPattern pattern in patterns)
        {
            if (pattern == null ||
                !pattern.IsConfigured)
            {
                continue;
            }

            if (PatternFitsLogicalCell(
                    pattern.Width,
                    pattern.Height
                ))
            {
                candidates.Add(pattern);
            }
        }

        return candidates;
    }

    private void PaintWallPatternInsideBlock(
        MazeWallPattern pattern,
        Vector3Int blockOrigin,
        System.Random random)
    {
        int horizontalOffset =
            GetRandomOffset(
                logicalCellTileSize.x,
                pattern.Width,
                random
            );

        int verticalOffset = 0;

        for (int localX = 0;
             localX < pattern.Width;
             localX++)
        {
            for (int localY = 0;
                 localY < pattern.Height;
                 localY++)
            {
                TileBase patternTile =
                    pattern.GetTile(
                        localX,
                        localY
                    );

                if (patternTile == null)
                {
                    continue;
                }

                Vector3Int position =
                    blockOrigin +
                    new Vector3Int(
                        horizontalOffset + localX,
                        verticalOffset + localY,
                        0
                    );

                wallTilemap.SetTile(
                    position,
                    patternTile
                );
            }
        }
    }

    private void PaintDecorations(
        MazeCellType[,] maze,
        System.Random random,
        Vector2Int startCell)
    {
        if (decorationSet == null)
        {
            return;
        }

        if (decorationBackTilemap == null ||
            decorationFrontTilemap == null)
        {
            Debug.LogWarning(
                "Decoration Set is assigned, but one or " +
                "both decoration Tilemaps are missing.",
                this
            );

            return;
        }

        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        int minX = -outerMarginPadding;
        int maxX = mazeWidth + outerMarginPadding;
        int minY = -outerMarginPadding;
        int maxY = mazeHeight + outerMarginPadding;

        for (int mazeX = minX; mazeX < maxX; mazeX++)
        {
            for (int mazeY = minY; mazeY < maxY; mazeY++)
            {
                // Evitar pintar decoraciones en la celda de inicio y sus adyacentes inmediatas
                if (Mathf.Abs(mazeX - startCell.x) <= 1 && Mathf.Abs(mazeY - startCell.y) <= 1)
                {
                    continue;
                }

                Vector3Int blockOrigin = GetTilePosition(new Vector2Int(mazeX, mazeY));

                bool isInsideMaze = mazeX >= 0 && mazeX < mazeWidth && mazeY >= 0 && mazeY < mazeHeight;

                if (!isInsideMaze)
                {
                    PaintOuterDecorations(blockOrigin, random);
                    continue;
                }

                if (mazeX == 0 || mazeY == 0 || mazeX == mazeWidth - 1 || mazeY == mazeHeight - 1)
                {
                    PaintBorderDecorations(blockOrigin, random);
                    continue;
                }

                if (maze[mazeX, mazeY] == MazeCellType.Path)
                {
                    TryPaintDecoration(
                        decorationSet.PathBackPatterns,
                        decorationSet.PathBackProbability,
                        decorationBackTilemap,
                        blockOrigin,
                        random
                    );
                    continue;
                }

                PaintInteriorWallDecorations(blockOrigin, random);
            }
        }
    }

    private void PaintBorderDecorations(
        Vector3Int blockOrigin,
        System.Random random)
    {
        TryPaintDecoration(
            decorationSet.BorderBackPatterns,
            decorationSet.BorderBackProbability,
            decorationBackTilemap,
            blockOrigin,
            random
        );

        TryPaintDecoration(
            decorationSet.BorderFrontPatterns,
            decorationSet.BorderFrontProbability,
            decorationFrontTilemap,
            blockOrigin,
            random
        );
    }

    private void PaintOuterDecorations(
        Vector3Int blockOrigin,
        System.Random random)
    {
        TryPaintDecoration(
            decorationSet.BorderBackPatterns,
            decorationSet.BorderBackProbability * outerDecorationDensity,
            decorationBackTilemap,
            blockOrigin,
            random
        );

        TryPaintDecoration(
            decorationSet.BorderFrontPatterns,
            decorationSet.BorderFrontProbability * outerDecorationDensity,
            decorationFrontTilemap,
            blockOrigin,
            random
        );

        TryPaintDecoration(
            decorationSet.WallBackPatterns,
            decorationSet.WallBackProbability * outerDecorationDensity,
            decorationBackTilemap,
            blockOrigin,
            random
        );

        TryPaintDecoration(
            decorationSet.WallFrontPatterns,
            decorationSet.WallFrontProbability * outerDecorationDensity,
            decorationFrontTilemap,
            blockOrigin,
            random
        );

        TryPaintDecoration(
            decorationSet.PathBackPatterns,
            decorationSet.PathBackProbability * outerDecorationDensity,
            decorationBackTilemap,
            blockOrigin,
            random
        );
    }

    private void PaintInteriorWallDecorations(
        Vector3Int blockOrigin,
        System.Random random)
    {
        TryPaintDecoration(
            decorationSet.WallBackPatterns,
            decorationSet.WallBackProbability,
            decorationBackTilemap,
            blockOrigin,
            random
        );

        TryPaintDecoration(
            decorationSet.WallFrontPatterns,
            decorationSet.WallFrontProbability,
            decorationFrontTilemap,
            blockOrigin,
            random
        );
    }

    private void TryPaintDecoration(
        MazeDecorationPattern[] patterns,
        float probability,
        Tilemap targetTilemap,
        Vector3Int blockOrigin,
        System.Random random)
    {
        if (patterns == null ||
            patterns.Length == 0 ||
            probability <= 0f ||
            random.NextDouble() > probability)
        {
            return;
        }

        List<MazeDecorationPattern> candidates =
            GetDecorationPatternsThatFit(
                patterns
            );

        if (candidates.Count == 0)
        {
            return;
        }

        MazeDecorationPattern selectedPattern =
            candidates[
                random.Next(candidates.Count)
            ];

        PaintDecorationPatternInsideBlock(
            selectedPattern,
            targetTilemap,
            blockOrigin,
            random
        );
    }

    private List<MazeDecorationPattern>
        GetDecorationPatternsThatFit(
            MazeDecorationPattern[] patterns)
    {
        List<MazeDecorationPattern> candidates =
            new List<MazeDecorationPattern>();

        foreach (MazeDecorationPattern pattern
                 in patterns)
        {
            if (pattern == null ||
                !pattern.IsConfigured)
            {
                continue;
            }

            if (PatternFitsLogicalCell(
                    pattern.Width,
                    pattern.Height
                ))
            {
                candidates.Add(pattern);
            }
        }

        return candidates;
    }

    private void PaintDecorationPatternInsideBlock(
        MazeDecorationPattern pattern,
        Tilemap targetTilemap,
        Vector3Int blockOrigin,
        System.Random random)
    {
        int horizontalOffset =
            GetRandomOffset(
                logicalCellTileSize.x,
                pattern.Width,
                random
            );

        int verticalOffset =
            GetRandomOffset(
                logicalCellTileSize.y,
                pattern.Height,
                random
            );

        for (int localX = 0;
             localX < pattern.Width;
             localX++)
        {
            for (int localY = 0;
                 localY < pattern.Height;
                 localY++)
            {
                TileBase patternTile =
                    pattern.GetTile(
                        localX,
                        localY
                    );

                if (patternTile == null)
                {
                    continue;
                }

                Vector3Int position =
                    blockOrigin +
                    new Vector3Int(
                        horizontalOffset + localX,
                        verticalOffset + localY,
                        0
                    );

                targetTilemap.SetTile(
                    position,
                    patternTile
                );
            }
        }
    }

    private bool PatternFitsLogicalCell(
        int patternWidth,
        int patternHeight)
    {
        return
            patternWidth <= logicalCellTileSize.x &&
            patternHeight <= logicalCellTileSize.y;
    }

    private int GetRandomOffset(
        int availableSize,
        int patternSize,
        System.Random random)
    {
        int remainingSpace =
            availableSize - patternSize;

        return remainingSpace > 0
            ? random.Next(remainingSpace + 1)
            : 0;
    }

    private Vector3Int CalculateOrigin(
        int renderedWidth,
        int renderedHeight)
    {
        if (!centerMaze)
        {
            return new Vector3Int(
                manualOrigin.x,
                manualOrigin.y,
                0
            );
        }

        return new Vector3Int(
            -(renderedWidth / 2),
            -(renderedHeight / 2),
            0
        );
    }

    private void RefreshTilemaps()
    {
        RefreshTilemap(groundTilemap);
        RefreshTilemap(pathTilemap);
        RefreshTilemap(wallTilemap);
        RefreshTilemap(decorationBackTilemap);
        RefreshTilemap(decorationFrontTilemap);
    }

    private void RefreshTilemap(
        Tilemap targetTilemap)
    {
        if (targetTilemap == null)
        {
            return;
        }

        targetTilemap.RefreshAllTiles();
        targetTilemap.CompressBounds();
    }

    private bool ValidateMainReferences(
        MazeCellType[,] maze)
    {
        if (maze == null)
        {
            Debug.LogError(
                "The maze matrix is null.",
                this
            );

            return false;
        }

        if (groundTilemap == null ||
            pathTilemap == null ||
            wallTilemap == null)
        {
            Debug.LogError(
                "One or more main Tilemap references " +
                "are missing.",
                this
            );

            return false;
        }

        if (tileSet == null)
        {
            Debug.LogError(
                "Maze Tile Set is not assigned.",
                this
            );

            return false;
        }

        if (logicalCellTileSize.x < 1 ||
            logicalCellTileSize.y < 1)
        {
            Debug.LogError(
                "Logical Cell Tile Size values must " +
                "be greater than zero.",
                this
            );

            return false;
        }

        return true;
    }

    private void OnValidate()
    {
        logicalCellTileSize.x =
            Mathf.Max(
                1,
                logicalCellTileSize.x
            );

        logicalCellTileSize.y =
            Mathf.Max(
                1,
                logicalCellTileSize.y
            );
    }
}
