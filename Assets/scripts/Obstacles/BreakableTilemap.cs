using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class BreakableTilemap : MonoBehaviour
{
    private Tilemap tilemap;

    private void Awake()
    {
        tilemap = GetComponent<Tilemap>();

        if (tilemap == null)
        {
            Debug.LogError(
                "BreakableTilemap necesita un componente Tilemap.",
                this
            );

            enabled = false;
        }
    }

    public bool TryBreakAtWorldPosition(
        Vector3 worldPosition,
        out Vector3Int removedCell
    )
    {
        if (tilemap == null) tilemap = GetComponent<Tilemap>();
        if (tilemap == null) tilemap = GetComponentInChildren<Tilemap>();
        if (tilemap == null)
        {
            removedCell = Vector3Int.zero;
            return false;
        }

        removedCell = tilemap.WorldToCell(worldPosition);

        if (!tilemap.HasTile(removedCell))
        {
            return false;
        }

        tilemap.SetTile(removedCell, null);
        return true;
    }
}