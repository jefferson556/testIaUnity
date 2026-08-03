using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa una pareja de cuevas opcionales de viaje rápido generadas proceduralmente.
/// Almacena tanto los datos lógicos (celdas) como las referencias a los GameObjects instanciados.
/// </summary>
[System.Serializable]
public class TravelCavePair
{
    // ── Identificación ──────────────────────────────────────────────────────────
    /// <summary>Índice de la pareja (0-based). Usado en nombres y métricas.</summary>
    public int PairIndex;

    // ── Celdas lógicas ──────────────────────────────────────────────────────────
    /// <summary>Celda de entrada de la cueva A (coordenadas lógicas del laberinto).</summary>
    public Vector2Int CellA;

    /// <summary>Celda de entrada de la cueva B (coordenadas lógicas del laberinto).</summary>
    public Vector2Int CellB;

    // ── Referencias a GameObjects ───────────────────────────────────────────────
    /// <summary>GameObject instanciado para la cueva A.</summary>
    public GameObject GameObjectA;

    /// <summary>GameObject instanciado para la cueva B.</summary>
    public GameObject GameObjectB;

    // ── Referencias a portales ──────────────────────────────────────────────────
    /// <summary>Componente CavePortal en la cueva A.</summary>
    public CavePortal PortalA;

    /// <summary>Componente CavePortal en la cueva B.</summary>
    public CavePortal PortalB;

    // ── Costos y métricas de calidad ────────────────────────────────────────────
    /// <summary>Costo configurable de usar el teletransporte (unidades de Dijkstra).</summary>
    public float TeleportCost;

    /// <summary>Distancia (en pasos de celda) del camino normal entre A y B sin portal.</summary>
    public int NormalPathDistance;

    /// <summary>
    /// Ahorro estimado al usar el portal: NormalPathDistance - TeleportCost.
    /// Puede ser negativo si TeleportCost > NormalPathDistance.
    /// </summary>
    public float EstimatedSaving;

    // ── Estado ──────────────────────────────────────────────────────────────────
    /// <summary>
    /// True si la pareja está activa en la escena. False si fue destruida o deshabilitada.
    /// </summary>
    public bool IsActive;

    // ── Celdas reservadas ───────────────────────────────────────────────────────
    /// <summary>
    /// Celdas del área segura alrededor de la entrada A.
    /// Excluidas del pool de candidatos de futuras parejas.
    /// </summary>
    public List<Vector2Int> ReservedCellsA = new List<Vector2Int>();

    /// <summary>
    /// Celdas del área segura alrededor de la entrada B.
    /// Excluidas del pool de candidatos de futuras parejas.
    /// </summary>
    public List<Vector2Int> ReservedCellsB = new List<Vector2Int>();

    // ── Debug / Gizmos ──────────────────────────────────────────────────────────
    /// <summary>
    /// Ruta normal (sin portal) entre CellA y CellB.
    /// Usada para visualización en Gizmos y cálculo de NormalPathDistance.
    /// </summary>
    public List<Vector2Int> NormalPath = new List<Vector2Int>();

    // ── Nombres formateados ─────────────────────────────────────────────────────
    /// <summary>Nombre del GameObject A formateado con el índice (ej. "TravelCave_Pair_01_A").</summary>
    public string NameA => $"TravelCave_Pair_{(PairIndex + 1):D2}_A";

    /// <summary>Nombre del GameObject B formateado con el índice (ej. "TravelCave_Pair_01_B").</summary>
    public string NameB => $"TravelCave_Pair_{(PairIndex + 1):D2}_B";

    /// <summary>
    /// Obtiene todas las celdas reservadas por esta pareja (A + B).
    /// Útil para excluirlas del pool de candidatos de nuevas parejas.
    /// </summary>
    public HashSet<Vector2Int> GetAllReservedCells()
    {
        var set = new HashSet<Vector2Int>();
        set.Add(CellA);
        set.Add(CellB);
        foreach (var c in ReservedCellsA) set.Add(c);
        foreach (var c in ReservedCellsB) set.Add(c);
        return set;
    }
}
