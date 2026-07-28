using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    fileName = "MazeWallPattern",
    menuName = "Maze/Wall Pattern"
)]
public class MazeWallPattern : ScriptableObject
{
    [Header("Dimensiones")]
    [SerializeField, Min(1)]
    private int width = 2;

    [SerializeField, Min(1)]
    private int height = 2;

    [Header("Tiles ordenados desde arriba")]
    [Tooltip(
        "Coloca los tiles de izquierda a derecha, " +
        "comenzando por la fila superior."
    )]
    [SerializeField]
    private TileBase[] tiles;

    public int Width => width;

    public int Height => height;

    public int Area => width * height;

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

        // Unity crece hacia arriba, pero el array se
        // configura visualmente desde la fila superior.
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
    }
}