using UnityEngine;

[CreateAssetMenu(fileName = "TrainingConfig", menuName = "Training/Config")]
public sealed class TrainingConfig : ScriptableObject
{
    [Header("Modo de Entrenamiento")]
    public bool trainingMode = true;

    [Header("Configuración de Obstáculos y Herramientas")]
    // Desactivar la barrera de destructibles obligatorios solo en entrenamiento básico
    public bool disableMandatoryDestructibleBarrier = true;

    // Empezar cada episodio con el hacha en el inventario automáticamente
    public bool startWithAxe = true;

    // Porcentaje de destructibles aleatorios en el laberinto (0 = sin destructibles)
    [Range(0f, 1f)]
    public float destructiblePercentage = 0f;

    [Header("Fase Actual")]
    public int trainingPhase = 0;
}
