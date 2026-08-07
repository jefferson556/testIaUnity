using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Componente que inicializa el modo de entrenamiento en la escena MazeLevel_Train.
/// Activa el TrainingLevelManager solo cuando la escena activa es MazeLevel_Train.
/// 
/// Instrucciones de uso:
/// 1. Crear un GameObject vacío en la escena MazeLevel_Train llamado "TrainingSystem".
/// 2. Añadir este componente (TrainingModeInitializer) al GameObject.
/// 3. Añadir también el componente TrainingLevelManager al mismo GameObject.
/// 4. Crear un asset TrainingConfig desde el menú: Create → Training → Config.
/// 5. Asignar el asset TrainingConfig en ambos componentes.
/// </summary>
[DefaultExecutionOrder(-100)] // Ejecutar antes que cualquier otro script que inicie generación.
public sealed class TrainingModeInitializer : MonoBehaviour
{
    [SerializeField] private TrainingConfig trainingConfig;
    [SerializeField] private TrainingLevelManager trainingLevelManager;

    public TrainingConfig Config => trainingConfig;

    private void Awake()
    {
        // Verificar que estamos en la escena correcta
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "MazeLevel_Train")
        {
            Debug.Log($"[Training] Escena actual: '{sceneName}'. " +
                      "TrainingModeInitializer solo funciona en 'MazeLevel_Train'. Desactivando.");
            
            // Desactivar el TrainingLevelManager si existe
            if (trainingLevelManager != null)
                trainingLevelManager.enabled = false;
            
            enabled = false;
            return;
        }

        // Verificar que tenemos el config
        if (trainingConfig == null)
        {
            Debug.LogError("[Training] No se asignó TrainingConfig en el Inspector. " +
                           "Crea uno desde el menú: Create → Training → Config.");
            return;
        }

        // Auto-buscar TrainingLevelManager si no está asignado
        if (trainingLevelManager == null)
        {
            trainingLevelManager = GetComponent<TrainingLevelManager>();
            if (trainingLevelManager == null)
                trainingLevelManager = FindAnyObjectByType<TrainingLevelManager>();
        }

        if (trainingLevelManager == null)
        {
            Debug.LogError("[Training] No se encontró TrainingLevelManager. " +
                           "Añádelo al mismo GameObject que TrainingModeInitializer.");
            return;
        }

        // Configurar el modo de entrenamiento
        if (trainingConfig.trainingMode)
        {
            trainingLevelManager.enabled = true;
            Debug.Log("[Training] ✓ Modo de entrenamiento activado. " +
                      $"Fase: {trainingConfig.trainingPhase}. " +
                      $"Barrera desactivada: {trainingConfig.disableMandatoryDestructibleBarrier}.");
        }
        else
        {
            trainingLevelManager.enabled = false;
            Debug.Log("[Training] Modo de entrenamiento desactivado en TrainingConfig.");
        }
    }
}
