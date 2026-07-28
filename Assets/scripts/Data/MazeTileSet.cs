using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    fileName = "MazeTileSet",
    menuName = "Maze/Tile Set"
)]
public class MazeTileSet : ScriptableObject
{
    [Header("Main 1x1 Tiles")]
    [SerializeField]
    private TileBase groundTile;

    [SerializeField]
    private TileBase pathTile;

    [Tooltip(
        "Fallback 1x1 wall used to fill each " +
        "logical wall cell."
    )]
    [SerializeField]
    private TileBase wallTile;

    [Header("Additional 1x1 Variations")]
    [SerializeField]
    private TileBase[] additionalGroundTiles;

    [SerializeField]
    private TileBase[] additionalPathTiles;

    [SerializeField]
    private TileBase[] additionalWallTiles;

    [Header("Wall Patterns")]
    [SerializeField]
    private MazeWallPattern[] wallPatterns;

    [Tooltip(
        "Probability of replacing part of a wall " +
        "block with a configured wall pattern."
    )]
    [SerializeField, Range(0f, 1f)]
    private float wallPatternProbability = 0.50f;

    public MazeWallPattern[] WallPatterns =>
        wallPatterns;

    public float WallPatternProbability =>
        wallPatternProbability;

    public TileBase GetGroundTile(
        System.Random random)
    {
        return GetRandomTile(
            groundTile,
            additionalGroundTiles,
            random
        );
    }

    public TileBase GetPathTile(
        System.Random random)
    {
        return GetRandomTile(
            pathTile,
            additionalPathTiles,
            random
        );
    }

    public TileBase GetWallTile(
        System.Random random)
    {
        return GetRandomTile(
            wallTile,
            additionalWallTiles,
            random
        );
    }

    private TileBase GetRandomTile(
        TileBase mainTile,
        TileBase[] additionalTiles,
        System.Random random)
    {
        int additionalCount =
            CountValidTiles(additionalTiles);

        int totalTileCount =
            additionalCount +
            (mainTile != null ? 1 : 0);

        if (totalTileCount == 0)
        {
            return null;
        }

        int selectedIndex =
            random.Next(totalTileCount);

        if (mainTile != null)
        {
            if (selectedIndex == 0)
            {
                return mainTile;
            }

            selectedIndex--;
        }

        if (additionalTiles == null)
        {
            return mainTile;
        }

        foreach (TileBase tile in additionalTiles)
        {
            if (tile == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return tile;
            }

            selectedIndex--;
        }

        return mainTile;
    }

    private int CountValidTiles(
        TileBase[] tiles)
    {
        if (tiles == null)
        {
            return 0;
        }

        int validTileCount = 0;

        foreach (TileBase tile in tiles)
        {
            if (tile != null)
            {
                validTileCount++;
            }
        }

        return validTileCount;
    }
}
