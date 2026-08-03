using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Métricas del segmento llave → meta del jugador.
/// Se calculan en dos momentos:
///   1. Al recoger la llave: rutas óptimas (datos de referencia).
///   2. Al llegar a la meta: comportamiento real del jugador.
/// </summary>
[System.Serializable]
public class KeyToGoalMetrics
{
    // ── NUEVAS MÉTRICAS PRINCIPALES ───────────────────────────────────────────────

    /// <summary>
    /// True únicamente cuando el pathfinding encuentre correctamente una ruta desde la posición
    /// del jugador al recoger la llave hasta la celda real de acceso a la meta.
    /// </summary>
    public bool keyToGoalPathDataValid = false;

    /// <summary>Tiempo real (segundos) desde que recogió la llave hasta que llegó a la meta.</summary>
    public float keyToGoalTime;

    /// <summary>Distancia mínima válida (en celdas) desde la celda real del jugador al recoger la llave hasta la meta.</summary>
    public int keyToGoalOptimalDistance;

    /// <summary>Total de pasos de celda recorridos realmente por el jugador (excluye teletransporte).</summary>
    public int keyToGoalActualDistance;

    /// <summary>Distancia adicional recorrida: max(0, actualDistance - optimalDistance).</summary>
    public int keyToGoalExtraDistance;

    /// <summary>Número de celdas visitadas más de una vez durante llave → meta.</summary>
    public int keyToGoalRepeatedCells;

    /// <summary>Proporción de celdas repetidas: repeatedCells / actualDistance.</summary>
    public float keyToGoalRepeatedCellRatio;

    /// <summary>Eficiencia del trayecto: optimalDistance / actualDistance.</summary>
    public float keyToGoalEfficiency;

    /// <summary>Clasificación del estilo de navegación del jugador.</summary>
    public NavigationStyle keyToGoalNavigationState;

    /// <summary>Cantidad total de teletransportes válidos completados.</summary>
    public int keyToGoalCaveUses;

    /// <summary>Cantidad de parejas de portal distintas utilizadas.</summary>
    public int keyToGoalUniqueCavePairsUsed;

    /// <summary>Teletransportes opcionales que redujeron significativamente el costo restante.</summary>
    public int keyToGoalUsefulCaveUses;

    /// <summary>Teletransportes opcionales que cambiaron poco el costo restante.</summary>
    public int keyToGoalNeutralCaveUses;

    /// <summary>Teletransportes opcionales que aumentaron el costo restante.</summary>
    public int keyToGoalUnproductiveCaveUses;

    /// <summary>Teletransportes que no pudieron ser evaluados con datos válidos (incluyendo cuevas obligatorias).</summary>
    public int keyToGoalUnevaluatedCaveUses;

    /// <summary>Registro específico del número de usos de la cueva obligatoria de misión.</summary>
    public int keyToGoalMandatoryCaveUses;

    // ── DIAGNÓSTICO Y COMPATIBILIDAD HISTÓRICA (OBSOLETOS) ─────────────────────────

    [System.Obsolete("Usar keyToGoalOptimalDistance en su lugar.")]
    public int keyToGoalOptimalWalkingDistance;

    [System.Obsolete("Usar keyToGoalActualDistance en su lugar.")]
    public int keyToGoalActualWalkingDistance;

    [System.Obsolete("Usar keyToGoalNavigationState en su lugar.")]
    public NavigationStyle keyToGoalNavigationStyle;

    [System.Obsolete("Usar keyToGoalUsefulCaveUses / keyToGoalNeutralCaveUses / keyToGoalUnproductiveCaveUses en su lugar.")]
    public bool keyToGoalUsedOptimalCave;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public float keyToGoalOptimalWalkingCost;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public float keyToGoalOptimalMechanicCost;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public int keyToGoalOptimalMechanicWalkingDistance;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public int keyToGoalOptimalPortalUses;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public bool keyToGoalOptimalUsesCaves;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public float keyToGoalPotentialSaving;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public float keyToGoalActualCost;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public float keyToGoalActualSaving;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public float keyToGoalWalkingEfficiency;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public float keyToGoalMechanicEfficiency;

    [System.Obsolete("Campos de costo antiguo obsoletos.")]
    public bool keyToGoalIgnoredUsefulCave;

    /// <summary>Mantenido únicamente para diagnóstico/gizmos.</summary>
    public List<int> keyToGoalCavePairIndicesUsed = new List<int>();
}

/// <summary>
/// Clasificación del estilo de navegación en el segmento llave→meta.
/// </summary>
public enum NavigationStyle
{
    NotEvaluated = 0,
    Efficient = 1,
    Exploratory = 2,
    Struggling = 3
}
