using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resultado detallado de una búsqueda de camino (Dijkstra / BFS) realizada por MazePathfinder.
/// Contiene toda la información necesaria para métricas, Gizmos y clasificación de comportamiento.
/// </summary>
public class PathfindResult
{
    // ── Existencia ───────────────────────────────────────────────────────────────
    /// <summary>True si existe al menos una ruta entre origen y destino.</summary>
    public bool PathExists;

    // ── Costo ────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Costo total del camino en unidades del pathfinder.
    /// Cada paso normal cuesta normalStepCost. Cada teletransporte cuesta TeleportCost.
    /// </summary>
    public float TotalCost;

    // ── Secuencia de celdas ──────────────────────────────────────────────────────
    /// <summary>
    /// Lista ordenada de celdas lógicas del laberinto desde origen hasta destino.
    /// Incluye las celdas de entrada/salida de los portales usados.
    /// </summary>
    public List<Vector2Int> Cells = new List<Vector2Int>();

    // ── Portales usados ──────────────────────────────────────────────────────────
    /// <summary>
    /// Índices (PairIndex) de los portales utilizados en esta ruta.
    /// Puede estar vacío si no se usaron portales.
    /// </summary>
    public List<int> PortalPairIndicesUsed = new List<int>();

    // ── Estadísticas de la ruta ──────────────────────────────────────────────────
    /// <summary>Número de teletransportes realizados.</summary>
    public int TeleportCount;

    /// <summary>Número de pasos normales (celdas caminadas, sin contar saltos de portal).</summary>
    public int WalkingSteps;

    /// <summary>
    /// True si la ruta usa al menos un portal.
    /// Equivale a TeleportCount > 0.
    /// </summary>
    public bool UsesCaves => TeleportCount > 0;

    // ── Fábrica de resultado vacío ────────────────────────────────────────────────
    /// <summary>
    /// Crea un resultado que indica que no existe camino.
    /// Evita referencias null en código cliente.
    /// </summary>
    public static PathfindResult NoPath()
    {
        return new PathfindResult
        {
            PathExists = false,
            TotalCost  = float.MaxValue,
            TeleportCount = 0,
            WalkingSteps  = 0,
        };
    }

    public override string ToString()
    {
        if (!PathExists) return "PathfindResult: NO PATH";
        return $"PathfindResult: cost={TotalCost:F1} walk={WalkingSteps} portals={TeleportCount} cells={Cells.Count}";
    }
}
