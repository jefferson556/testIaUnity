using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    fileName = "MazeDecorationPattern",
    menuName = "Maze/Decoration Pattern"
)]
public class MazeDecorationPattern : ScriptableObject
{
    [Header("Pattern Dimensions")]
    [SerializeField, Min(1)]
    private int width = 1;

    [SerializeField, Min(1)]
    private int height = 1;

    [Header("Ordered Tiles")]
    [Tooltip(
        "Assign tiles from left to right, " +
        "starting with the top row. " +
        "Null entries are allowed for transparent spaces."
    )]
    [SerializeField]
    private TileBase[] tiles;

    public int Width => width;

    public int Height => height;

    public bool IsConfigured =>
        width > 0 &&
        height > 0 &&
        tiles != null &&
        tiles.Length == width * height;

    public TileBase GetTile(
        int localX,
        int localY)
    {
        if (!IsConfigured)
        {
            return null;
        }

        if (localX < 0 ||
            localX >= width ||
            localY < 0 ||
            localY >= height)
        {
            return null;
        }

        int rowFromTop =
            height - 1 - localY;

        int index =
            rowFromTop * width + localX;

        return tiles[index];
    }

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        int requiredTileCount =
            width * height;

        if (tiles == null ||
            tiles.Length != requiredTileCount)
        {
            System.Array.Resize(
                ref tiles,
                requiredTileCount
            );
        }
    }
}
