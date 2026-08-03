using UnityEngine;

/// <summary>
/// Descriptor de una conexión de portal para el sistema de pathfinding.
/// Representa una arista especial (bidireccional) en el grafo del laberinto.
///
/// El pathfinder trata cada PortalConnection como dos aristas:
///   EntryA → ExitB  (costo: TeleportCost)
///   EntryB → ExitA  (costo: TeleportCost)
///
/// Las celdas de entrada son las celdas lógicas donde está el prefab.
/// Las celdas de salida son las celdas adyacentes libres donde aparece el jugador.
/// </summary>
public class PortalConnection
{
    /// <summary>Celda donde está instanciada la cueva A (punto de entrada desde el lado A).</summary>
    public Vector2Int EntryA;

    /// <summary>Celda donde el jugador aparece al salir por el lado B (adyacente al prefab B).</summary>
    public Vector2Int ExitB;

    /// <summary>Celda donde está instanciada la cueva B (punto de entrada desde el lado B).</summary>
    public Vector2Int EntryB;

    /// <summary>Celda donde el jugador aparece al salir por el lado A (adyacente al prefab A).</summary>
    public Vector2Int ExitA;

    /// <summary>
    /// Costo de usar el teletransporte en unidades del pathfinder.
    /// Si este valor == normalStepCost, el BFS es suficiente.
    /// Si difiere, se usa Dijkstra para encontrar el camino de menor costo real.
    /// </summary>
    public float TeleportCost;

    /// <summary>
    /// True si este portal está activo y debe considerarse en el pathfinding.
    /// Se usa para excluir portales cuya entrada está bloqueada por una barrera activa.
    /// </summary>
    public bool IsActive;

    /// <summary>
    /// Índice de la pareja a la que pertenece este portal.
    /// Corresponde a TravelCavePair.PairIndex. Usado en métricas.
    /// </summary>
    public int PairIndex;

    public PortalConnection() { }

    public PortalConnection(Vector2Int entryA, Vector2Int exitB,
                            Vector2Int entryB, Vector2Int exitA,
                            float teleportCost, int pairIndex, bool isActive = true)
    {
        EntryA      = entryA;
        ExitB       = exitB;
        EntryB      = entryB;
        ExitA       = exitA;
        TeleportCost = teleportCost;
        PairIndex   = pairIndex;
        IsActive    = isActive;
    }

    public override string ToString()
    {
        return $"Portal[{PairIndex}] {EntryA}→{ExitB} | {EntryB}→{ExitA} cost={TeleportCost} active={IsActive}";
    }
}
