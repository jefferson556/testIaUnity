using System;
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [Header("Dimensiones del laberinto")]
    [SerializeField, Min(5)]
    private int width = 15;

    [SerializeField, Min(5)]
    private int height = 15;

    [Header("Generación aleatoria")]
    [Tooltip(
        "Genera un laberinto diferente cada vez."
    )]
    [SerializeField]
    private bool useRandomSeed = true;

    [Tooltip(
        "Permite repetir el mismo laberinto cuando " +
        "Use Random Seed está desactivado."
    )]
    [SerializeField]
    private int seed = 12345;

    [Header("Dificultad")]
    [Tooltip(
        "Abre rutas alternativas. Un valor alto " +
        "hace que el laberinto sea más fácil."
    )]
    [SerializeField, Range(0, 20)]
    private int extraConnections = 2;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public Vector2Int StartCell { get; private set; }

    public int LastUsedSeed { get; private set; }

    public MazeCellType[,] Generate()
    {
        int mazeWidth = GetValidDimension(width);
        int mazeHeight = GetValidDimension(height);

        LastUsedSeed = GetGenerationSeed();

        System.Random random =
            new System.Random(LastUsedSeed);

        MazeCellType[,] maze =
            CreateWallMatrix(mazeWidth, mazeHeight);

        StartCell = new Vector2Int(1, 1);

        maze[StartCell.x, StartCell.y] =
            MazeCellType.Path;

        GeneratePaths(maze, random);

        CreateExtraConnections(
            maze,
            random,
            extraConnections
        );

        Debug.Log(
            $"Laberinto generado: " +
            $"{mazeWidth}x{mazeHeight}. " +
            $"Semilla: {LastUsedSeed}. " +
            $"Conexiones adicionales: {extraConnections}.",
            this
        );

        return maze;
    }

    private MazeCellType[,] CreateWallMatrix(
        int mazeWidth,
        int mazeHeight)
    {
        MazeCellType[,] maze =
            new MazeCellType[mazeWidth, mazeHeight];

        for (int x = 0; x < mazeWidth; x++)
        {
            for (int y = 0; y < mazeHeight; y++)
            {
                maze[x, y] = MazeCellType.Wall;
            }
        }

        return maze;
    }

    private void GeneratePaths(
        MazeCellType[,] maze,
        System.Random random)
    {
        Stack<Vector2Int> pendingCells =
            new Stack<Vector2Int>();

        pendingCells.Push(StartCell);

        while (pendingCells.Count > 0)
        {
            Vector2Int current =
                pendingCells.Peek();

            List<Vector2Int> availableDirections =
                GetAvailableDirections(current, maze);

            if (availableDirections.Count == 0)
            {
                pendingCells.Pop();
                continue;
            }

            int randomIndex =
                random.Next(
                    availableDirections.Count
                );

            Vector2Int direction =
                availableDirections[randomIndex];

            Vector2Int middleCell =
                current + direction;

            Vector2Int destinationCell =
                current + direction * 2;

            maze[middleCell.x, middleCell.y] =
                MazeCellType.Path;

            maze[destinationCell.x, destinationCell.y] =
                MazeCellType.Path;

            pendingCells.Push(destinationCell);
        }
    }

    private List<Vector2Int> GetAvailableDirections(
        Vector2Int current,
        MazeCellType[,] maze)
    {
        List<Vector2Int> availableDirections =
            new List<Vector2Int>();

        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        foreach (Vector2Int direction in Directions)
        {
            Vector2Int destination =
                current + direction * 2;

            bool isInsideMaze =
                destination.x > 0 &&
                destination.x < mazeWidth - 1 &&
                destination.y > 0 &&
                destination.y < mazeHeight - 1;

            if (!isInsideMaze)
            {
                continue;
            }

            bool isUnvisited =
                maze[destination.x, destination.y] ==
                MazeCellType.Wall;

            if (isUnvisited)
            {
                availableDirections.Add(direction);
            }
        }

        return availableDirections;
    }

    private void CreateExtraConnections(
        MazeCellType[,] maze,
        System.Random random,
        int connectionsToCreate)
    {
        if (connectionsToCreate <= 0)
        {
            return;
        }

        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        int createdConnections = 0;
        int attempts = 0;
        int maximumAttempts =
            connectionsToCreate * 30;

        while (
            createdConnections < connectionsToCreate &&
            attempts < maximumAttempts)
        {
            attempts++;

            int x = random.Next(1, mazeWidth - 1);
            int y = random.Next(1, mazeHeight - 1);

            if (maze[x, y] != MazeCellType.Wall)
            {
                continue;
            }

            if (!CanOpenWall(maze, x, y))
            {
                continue;
            }

            maze[x, y] = MazeCellType.Path;
            createdConnections++;
        }
    }

    private bool CanOpenWall(
        MazeCellType[,] maze,
        int x,
        int y)
    {
        bool connectsHorizontalPaths =
            maze[x - 1, y] == MazeCellType.Path &&
            maze[x + 1, y] == MazeCellType.Path;

        bool connectsVerticalPaths =
            maze[x, y - 1] == MazeCellType.Path &&
            maze[x, y + 1] == MazeCellType.Path;

        return connectsHorizontalPaths ||
               connectsVerticalPaths;
    }

    private int GetValidDimension(int dimension)
    {
        int validDimension =
            Mathf.Max(5, dimension);

        if (validDimension % 2 == 0)
        {
            validDimension++;
        }

        return validDimension;
    }

    private int GetGenerationSeed()
    {
        if (useRandomSeed)
        {
            return Guid.NewGuid().GetHashCode();
        }

        return seed;
    }
}