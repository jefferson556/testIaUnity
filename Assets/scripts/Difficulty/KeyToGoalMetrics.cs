using UnityEngine;

/// <summary>
/// Métricas del segmento llave → meta del jugador.
/// Se calculan al llegar a la meta.
/// </summary>
[System.Serializable]
public class KeyToGoalMetrics
{
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
