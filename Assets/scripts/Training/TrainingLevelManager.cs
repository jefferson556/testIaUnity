using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Componente independiente que valida y ajusta la generación del nivel
/// en la escena MazeLevel_Train para el modo de entrenamiento.
/// 
/// NO hereda ni modifica DynamicLevelManager.
/// Se ejecuta DESPUÉS de que DynamicLevelManager termina la generación,
/// usando las propiedades públicas existentes y MazeData para validar
/// que exista un camino desde el spawn hasta la meta.
/// 
/// Si no existe camino (porque la barrera obligatoria bloquea la meta),
/// solicita regeneración automática llamando a StartGeneration().
/// </summary>
public sealed class TrainingLevelManager : MonoBehaviour
{
    [Header("Configuración de Entrenamiento")]
    [SerializeField] private TrainingConfig trainingConfig;

    [Header("Referencia al generador (auto-buscada si no se asigna)")]
    [SerializeField] private DynamicLevelManager levelManager;

    /// <summary>
    /// Número máximo de regeneraciones automáticas antes de aceptar el nivel.
    /// Evita bucles infinitos si la configuración no permite caminos válidos.
    /// </summary>
    [SerializeField] private int maxRegenerationAttempts = 10;

    /// <summary>
    /// Tiempo en segundos que espera tras la generación antes de validar.
    /// Debe ser suficiente para que DynamicLevelManager termine su corrutina.
    /// </summary>
    [SerializeField] private float validationDelay = 2f;

    private MazeData mazeData;
    private MazeGenerator mazeGenerator;
    private int regenerationCount = 0;
    private bool isValidating = false;

    private void Awake()
    {
        FetchReferences();
    }

    private void FetchReferences()
    {
        if (levelManager == null)
            levelManager = FindAnyObjectByType<DynamicLevelManager>();

        if (levelManager != null)
        {
            if (mazeData == null)
                mazeData = levelManager.GetComponent<MazeData>();
            if (mazeData == null)
                mazeData = FindAnyObjectByType<MazeData>();

            if (mazeGenerator == null)
                mazeGenerator = levelManager.GetComponentInChildren<MazeGenerator>();
            if (mazeGenerator == null)
                mazeGenerator = FindAnyObjectByType<MazeGenerator>();
        }
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForGenerationAndValidate());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>
    /// Espera a que la generación termine (detectando que el jugador tiene
    /// control habilitado) y luego valida el camino.
    /// </summary>
    private IEnumerator WaitForGenerationAndValidate()
    {
        if (trainingConfig == null || !trainingConfig.trainingMode)
            yield break;

        FetchReferences();

        if (levelManager == null)
        {
            Debug.LogError("[Training] No se encontró DynamicLevelManager en la escena. " +
                           "Asegúrate de que esté presente.");
            yield break;
        }

        // Esperar un tiempo fijo para que DynamicLevelManager termine
        // su GenerateLevelRoutine completa (donde se añade e inicializa MazeData).
        yield return new WaitForSeconds(validationDelay);

        // Volver a buscar las referencias tras el tiempo de espera por si MazeData se añadió dinámicamente
        FetchReferences();

        if (mazeData == null)
        {
            Debug.LogError("[Training] No se encontró MazeData tras la generación. " +
                           "Asegúrate de que DynamicLevelManager añade o tiene el componente MazeData.");
            yield break;
        }

        // Esperar a que exista el jugador o agente en la escena
        CatMovement playerMovement = FindAnyObjectByType<CatMovement>();
        MazeAgent mazeAgent = FindAnyObjectByType<MazeAgent>();
        float timeout = 10f; // timeout de seguridad
        float elapsed = 0f;

        while (playerMovement == null && mazeAgent == null)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            if (elapsed >= timeout)
            {
                Debug.LogWarning("[Training] Timeout esperando a que el jugador o agente aparezca.");
                yield break;
            }

            if (playerMovement == null) playerMovement = FindAnyObjectByType<CatMovement>();
            if (mazeAgent == null) mazeAgent = FindAnyObjectByType<MazeAgent>();
        }

        // Dar un frame extra para asegurar sincronización
        yield return null;

        ValidateAndRegenerate();
    }

    /// <summary>
    /// Valida que exista un camino transitable entre el spawn y la meta.
    /// Si no existe, solicita regeneración.
    /// </summary>
    private void ValidateAndRegenerate()
    {
        if (isValidating) return;
        if (trainingConfig == null || !trainingConfig.trainingMode) return;
        if (!trainingConfig.disableMandatoryDestructibleBarrier) return;

        isValidating = true;

        // Obtener la celda del jugador (spawn) desde MazeGenerator
        Vector2Int startCell = Vector2Int.zero;
        if (mazeGenerator != null)
        {
            startCell = mazeGenerator.StartCell;
        }

        // Obtener la celda de la meta desde DynamicLevelManager (propiedad pública)
        Vector2Int goalCell = levelManager.MetaCell;

        if (goalCell == Vector2Int.zero)
        {
            Debug.LogWarning("[Training] MetaCell es (0,0). Es posible que la generación " +
                             "no haya terminado correctamente.");
            isValidating = false;
            return;
        }

        // Validar usando MazePathfinder.FindPathWithPortals
        // Usamos hasAxe=true para que el pathfinder considere que puede
        // atravesar destructibles (en entrenamiento básico queremos
        // confirmar que hay camino "potencial" hasta la meta).
        var barrierCells = levelManager.BarrierCells ?? new HashSet<Vector2Int>();
        var portals = levelManager.ActivePortalConnections;

        PathfindResult result = MazePathfinder.FindPathWithPortals(
            startCell,
            goalCell,
            mazeData,
            true,               // hasAxe = true
            portals,
            barrierCells,
            1f
        );

        if (!result.PathExists)
        {
            regenerationCount++;

            if (regenerationCount >= maxRegenerationAttempts)
            {
                Debug.LogError($"[Training] Se alcanzaron {maxRegenerationAttempts} intentos " +
                               "de regeneración sin encontrar un camino válido. " +
                               "Verifica la configuración de dificultad.");
                isValidating = false;
                return;
            }

            Debug.LogWarning($"[Training] Meta inaccesible desde el spawn " +
                             $"(intento {regenerationCount}/{maxRegenerationAttempts}). " +
                             "Regenerando nivel...");

            isValidating = false;

            // Solicitar regeneración al DynamicLevelManager (método público)
            levelManager.StartGeneration();

            // Reiniciar la espera de validación
            StartCoroutine(WaitForGenerationAndValidate());
        }
        else
        {
            Debug.Log($"[Training] ✓ Camino válido encontrado. " +
                      $"Distancia: {result.TotalCost:F1} pasos. " +
                      $"Spawn: {startCell} → Meta: {goalCell}");
            isValidating = false;
            regenerationCount = 0;
        }
    }
}
