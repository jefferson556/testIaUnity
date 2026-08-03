using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Métricas del segmento llave → meta del jugador.
/// Se calculan en dos momentos:
///   1. Al recoger la llave: rutas óptimas (datos de referencia).
///   2. Al llegar a la meta: comportamiento real del jugador.
///
/// Estas métricas describen comportamiento dentro del juego.
/// No deben interpretarse como diagnóstico psicológico.
/// </summary>
[System.Serializable]
public class KeyToGoalMetrics
{
    /// <summary>
    /// Indica si las rutas óptimas fueron calculadas exitosamente.
    /// False si el pathfinder no pudo encontrar ruta.
    /// </summary>
    public bool keyToGoalPathDataValid = false;

    /// <summary>
    /// Costo Dijkstra de llave→meta ignorando todos los portales opcionales.
    /// Solo cuenta pasos de celda normales.
    /// </summary>
    public float keyToGoalOptimalWalkingCost;

    /// <summary>
    /// Costo Dijkstra de llave→meta usando todos los portales opcionales activos.
    /// Puede ser igual o menor que keyToGoalOptimalWalkingCost.
    /// Si caminar es más barato, ambos serán iguales y keyToGoalOptimalUsesCaves = false.
    /// </summary>
    public float keyToGoalOptimalMechanicCost;

    /// <summary>
    /// Número de pasos (celdas) de la ruta óptima caminando (sin portales).
    /// </summary>
    public int keyToGoalOptimalWalkingDistance;

    /// <summary>
    /// Número de pasos caminados en la ruta óptima con mecánicas.
    /// Excluye los saltos de portal; solo cuenta las celdas físicamente recorridas.
    /// </summary>
    public int keyToGoalOptimalMechanicWalkingDistance;

    /// <summary>Número de portales que usa la ruta óptima con mecánicas.</summary>
    public int keyToGoalOptimalPortalUses;

    /// <summary>
    /// True si la ruta óptima con mecánicas usa al menos un portal.
    /// False si caminar directamente es igual o más barato.
    /// </summary>
    public bool keyToGoalOptimalUsesCaves;

    /// <summary>
    /// Ahorro potencial: keyToGoalOptimalWalkingCost - keyToGoalOptimalMechanicCost.
    /// 0 si no hay ningún portal que mejore la ruta.
    /// No puede ser negativo (se clampea a 0).
    /// </summary>
    public float keyToGoalPotentialSaving;

    // ── Recorrido real del jugador ─────────────────────────────────────────────────

    /// <summary>Tiempo real (segundos) desde que recogió la llave hasta que llegó a la meta.</summary>
    public float keyToGoalTime;

    /// <summary>
    /// Total de pasos de celda recorridos realmente (incluyendo celdas repetidas por backtracking).
    /// </summary>
    public int keyToGoalActualWalkingDistance;

    /// <summary>
    /// Estimación del costo real del recorrido:
    ///   actualWalkingSteps * normalStepCost + caveUses * teleportCost
    /// </summary>
    public float keyToGoalActualCost;

    /// <summary>Número total de teletransportes realizados en el segmento llave→meta.</summary>
    public int keyToGoalCaveUses;

    /// <summary>
    /// Número de parejas de portal distintas utilizadas (no el total de usos).
    /// Ejemplo: usar la pareja 0 dos veces cuenta como 1.
    /// </summary>
    public int keyToGoalUniqueCavePairsUsed;

    /// <summary>
    /// Índices (PairIndex) de las parejas de portal utilizadas.
    /// Permite saber exactamente qué portales usó el jugador.
    /// </summary>
    public List<int> keyToGoalCavePairIndicesUsed = new List<int>();

    /// <summary>
    /// Número de celdas visitadas más de una vez (indicador de backtracking).
    /// </summary>
    public int keyToGoalRepeatedCells;

    // ── Eficiencias ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Eficiencia de navegación caminando: optWalkingDistance / actualDistance.
    /// 1.0 = perfecto, < 1.0 = el jugador recorrió más distancia de la mínima.
    /// Clampado a [0, 1]. 0 si actualDistance == 0.
    /// Solo significativo cuando caveUses == 0.
    /// </summary>
    public float keyToGoalWalkingEfficiency;

    /// <summary>
    /// Eficiencia de la mecánica: optMechanicCost / actualCost.
    /// 1.0 = jugó de forma óptima con o sin portales.
    /// Clampado a [0, 1]. 0 si actualCost == 0.
    /// </summary>
    public float keyToGoalMechanicEfficiency;

    // ── Ahorro real ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ahorro real comparado con la ruta caminando óptima:
    ///   keyToGoalOptimalWalkingCost - keyToGoalActualCost
    /// Positivo si el jugador fue más eficiente (usó un portal que ayudó).
    /// Negativo si el jugador tardó más de lo que hubiera tardado caminando directamente.
    /// </summary>
    public float keyToGoalActualSaving;

    // ── Indicadores de comportamiento ─────────────────────────────────────────────

    /// <summary>
    /// True si el jugador usó al menos un portal que también estaba en la ruta óptima con mecánicas.
    /// Indica que el jugador descubrió y aprovechó un atajo válido.
    /// </summary>
    public bool keyToGoalUsedOptimalCave;

    /// <summary>
    /// True si había un ahorro potencial disponible (keyToGoalPotentialSaving > 0)
    /// y el jugador no usó ningún portal.
    /// No es un error; puede indicar que el jugador prefirió caminar o no encontró el portal.
    /// </summary>
    public bool keyToGoalIgnoredUsefulCave;

    // ── Clasificación de comportamiento ───────────────────────────────────────────

    /// <summary>
    /// Clasificación del estilo de navegación del jugador en el segmento llave→meta.
    /// Solo describe comportamiento observable dentro del juego, no intención ni habilidad.
    /// </summary>
    public NavigationStyle keyToGoalNavigationStyle;
}

/// <summary>
/// Clasificación del estilo de navegación en el segmento llave→meta.
/// Basada únicamente en datos observables del juego.
/// </summary>
public enum NavigationStyle
{
    /// <summary>Caminó eficientemente sin usar portales (efficiency >= 0.8).</summary>
    Efficient,

    /// <summary>Usó un portal que estaba en la ruta óptima y llegó eficientemente.</summary>
    EfficientWithCave,

    /// <summary>No usó portales aunque había uno que hubiera ahorrado distancia.</summary>
    MissedShortcut,

    /// <summary>Usó un portal pero no el que estaba en la ruta óptima.</summary>
    SuboptimalCave,

    /// <summary>La eficiencia general fue muy baja (muchos desvíos).</summary>
    Lost,

    /// <summary>Valor por defecto / no calculado todavía.</summary>
    Unknown
}
