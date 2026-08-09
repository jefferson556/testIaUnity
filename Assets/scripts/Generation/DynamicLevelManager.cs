using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


public class DynamicLevelManager : MonoBehaviour
{
    [Header("Generación Principal")]
    [SerializeField]
    private MazeGenerator mazeGenerator;

    [SerializeField]
    private MazeTilemapRenderer mazeRenderer;

    [Header("Spawners de Objetos (Asigna uno de los dos)")]
    [SerializeField]
    private LevelObjectSpawner levelObjectSpawner;

    [SerializeField]
    private MazeGameplayObjectSpawner gameplayObjectSpawner;

    [Header("Jugador (Gato) - Ajustes de Posición")]
    [Tooltip("Recomendado por rendimiento: arrastra el GameObject CatPlayer de la jerarquía de la escena.")]
    [SerializeField]
    private Transform playerTransform;

    [Tooltip("Respaldo opcional si no hay un jugador en la escena.")]
    [SerializeField]
    private GameObject playerPrefab;

    [Tooltip("Collider de movimiento físico del jugador (opcional, se auto-buscará si está vacío).")]
    [SerializeField]
    private Collider2D playerMovementCollider;

    [Tooltip("Máscara de colisión para verificar obstáculos físicos en el spawn (debe incluir Wall).")]
    [SerializeField]
    private LayerMask obstacleLayer;

    [Header("Hacha - Ajustes de Aparición")]
    [Tooltip("Recomendado por rendimiento: arrastra el GameObject Hacha de la jerarquía de la escena.")]
    [SerializeField]
    private Transform axeTransform;

    [Tooltip("Respaldo opcional si no hay un hacha en la escena.")]
    [SerializeField]
    private GameObject axePrefab;

#pragma warning disable 0414
    [SerializeField, Min(1)]
    private int minimumAxeDistanceFromPlayer = 3;

    [Header("Accessible Zone Settings")]
    [SerializeField]
    private bool generateAccessibleZone = true;
#pragma warning restore 0414

    [SerializeField]
    private Vector2Int accessibleZoneSize = new Vector2Int(3, 3);

    [Header("Contenedores de Jerarquía")]
    [SerializeField]
    private Transform itemsContainer;

    [Header("Configuración de Regeneración")]
    [SerializeField]
    [Min(1)]
    private int maximumGenerationAttempts = 40;

    private GameObject spawnedAxeInstance;
    private Coroutine generationCoroutine;

    [Header("Misión - Prefabs")]
    [SerializeField] private GameObject cavePrefab;
    [SerializeField] private GameObject keyPrefab;
    [SerializeField] private GameObject doorPrefab;
    [SerializeField] private GameObject missionDestructiblePrefab;

    private GameObject spawnedCuevaAInstance;
    private GameObject spawnedCuevaBInstance;
    private GameObject spawnedKeyInstance;
    private GameObject spawnedDoorInstance;
    private List<GameObject> spawnedMissionDestructibles = new List<GameObject>();

    // Temporizador de Nivel (solo para el modo jugador)
    private float currentLevelTimeLimit;
    private bool isTimerActive;
    private bool isTrainingModeActive;

    public float CurrentLevelTimeLimit => currentLevelTimeLimit;
    public bool IsTimerActive => isTimerActive;
    public bool IsTrainingModeActive => isTrainingModeActive;

    // La configuración de cuevas opcionales se lee de DifficultySettings.
    // El Inspector local ya no muestra campos separados para travel caves;
    // todos se configuran en DifficultySettings (o DifficultyProfile).

    // Campo de respaldo actualizado cada generación desde DifficultySettings.enableTravelCaves.
    private bool enableTravelCaves = true;

    // Administrador de múltiples parejas de cuevas opcionales.
    // Se obtiene automáticamente del mismo GameObject en Awake.
    private TravelCavePairManager travelCavePairManager;

    // Lista de conexiones de portal activas — construida tras GeneratePairs.
    private List<PortalConnection> activePortalConnections = new List<PortalConnection>();

    // Rutas óptimas calculadas al recoger la llave (se calculan en OnKeyCollected).
    private PathfindResult keyToGoalWalkingResult;
    private PathfindResult keyToGoalMechanicResult;
    // Índices de las parejas que usa la ruta óptima con mecánicas (para Gizmos).
    private List<int> optimalPortalPairIndices = new List<int>();


    [Header("Depuración Visual - Gizmos")]
    [SerializeField] private bool showDebugGizmos = true;
    [Tooltip("Muestra las rutas obligatorias de la misión (Inicio→CuevaA, CuevaB→Hacha, etc.)")]
    [SerializeField] private bool showMissionPathsGizmos = true;
    [Tooltip("Muestra la ruta óptima calculada (Dijkstra) desde la llave hasta la meta en Verde")]
    [SerializeField] private bool showOptimalKeyToGoalGizmo = true;
    [Tooltip("Muestra las ubicaciones y conexiones de las cuevas opcionales de viaje rápido")]
    [SerializeField] private bool showTravelCaveGizmos = true;
    [Tooltip("Muestra las zonas marcadas (Llave, Meta, Barreras)")]
    [SerializeField] private bool showZoneGizmos = true;

    private List<Vector2Int> startToCaveAPath = new List<Vector2Int>();
    private List<Vector2Int> caveBToAxePath = new List<Vector2Int>();
    private List<Vector2Int> axeToBarrierPath = new List<Vector2Int>();
    private List<Vector2Int> keyToMetaPath = new List<Vector2Int>();
    private List<Vector2Int> keyZoneCells = new List<Vector2Int>();
    private List<Vector2Int> metaZoneCells = new List<Vector2Int>();
    private List<Vector2Int> axeZoneCells = new List<Vector2Int>();
    private HashSet<Vector2Int> barrierCells = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> destructibleWallsCells = new HashSet<Vector2Int>();

    private Vector2Int cuevaA;
    private Vector2Int cuevaB;
    private Vector2Int axeCell;
    private Vector2Int keyCell;
    private Vector2Int metaCell;

    public static DynamicLevelManager Instance { get; private set; }

    public Vector2Int MetaCell => metaCell;
    public HashSet<Vector2Int> BarrierCells => barrierCells;
    public List<PortalConnection> ActivePortalConnections => activePortalConnections;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        // Auto-resolver referencias serializadas al contenedor local (para entrenamiento paralelo)
        ResolveLocalReferences();
    }

    /// <summary>
    /// Busca los componentes locales dentro del mismo contenedor padre (TrainingArea).
    /// Esto permite que cada DynamicLevelManager duplicado se vincule a sus propios
    /// componentes locales sin necesidad de reasignar manualmente en el Inspector.
    /// Solo sobrescribe si encuentra un componente local válido.
    /// </summary>
    private void ResolveLocalReferences()
    {
        Transform root = transform.parent != null ? transform.parent : transform;

        // Resolver MazeGenerator local
        MazeGenerator localGenerator = root.GetComponentInChildren<MazeGenerator>();
        if (localGenerator != null)
        {
            mazeGenerator = localGenerator;
        }

        // Resolver MazeTilemapRenderer local
        MazeTilemapRenderer localRenderer = root.GetComponentInChildren<MazeTilemapRenderer>();
        if (localRenderer != null)
        {
            mazeRenderer = localRenderer;
        }

        // Resolver LevelObjectSpawner local
        LevelObjectSpawner localSpawner = root.GetComponentInChildren<LevelObjectSpawner>();
        if (localSpawner != null)
        {
            levelObjectSpawner = localSpawner;
        }

        // Resolver playerTransform (CatMovement) local
        CatMovement localCat = root.GetComponentInChildren<CatMovement>();
        if (localCat != null)
        {
            playerTransform = localCat.transform;
        }
    }

    private void Start()
    {
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowLoadingScreen("Cargando mapa procedural...");
        }
        StartGeneration();
    }

    public void StartGeneration()
    {
        if (generationCoroutine != null)
        {
            StopCoroutine(generationCoroutine);
        }

        generationCoroutine = StartCoroutine(GenerateLevelRoutine());
    }

    // Mantenemos la firma pública original para compatibilidad con otros scripts
    public void GenerateLevel()
    {
        StartGeneration();
    }

    private void ValidateSpawners()
    {
        if (levelObjectSpawner != null && gameplayObjectSpawner != null)
        {
            Debug.LogWarning("[LevelGeneration] AMBOS spawners (LevelObjectSpawner y GameplayObjectSpawner) están asignados en el Inspector. Asegúrate de configurar las referencias correctamente.", this);
        }
    }

    private IEnumerator GenerateLevelRoutine()
    {
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowLoadingScreen("Generando mapa procedural...");
        }

        yield return null; // Garantizar 1 frame de renderizado de la interfaz negra antes del trabajo pesado

        if (mazeGenerator == null || mazeRenderer == null)
        {
            Debug.LogError("[LevelGeneration] ERROR - Faltan referencias del generador (MazeGenerator) o renderizador (MazeTilemapRenderer) en el Inspector.", this);
            yield break;
        }

        if (obstacleLayer.value == 0)
        {
            Debug.LogError("[LevelGeneration] ERROR - La máscara 'Obstacle Layer' está vacía o no configurada en el Inspector. Deteniendo generación.", this);
            yield break;
        }

        // Obtener dificultad de la autoridad central
        DifficultySettings settings = null;
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.TryLoadConfigFromJSONFile();
            settings = DifficultyManager.Instance.CurrentSettings;
        }
        if (settings == null)
        {
            settings = new DifficultySettings();
        }

        TrainingConfig tConfig = Resources.Load<TrainingConfig>("TrainingConfig");
        isTrainingModeActive = (tConfig != null && tConfig.trainingMode);
        
        currentLevelTimeLimit = settings.maxTimeLimitInSeconds;
        isTimerActive = false;

        // Detener recopilación activa de métricas por si acaso
        if (DifficultyMetricsCollector.Instance != null)
        {
            DifficultyMetricsCollector.Instance.StopCollecting();
        }

        // Configurar MazeGenerator
        mazeGenerator.Width = settings.mapWidth;
        mazeGenerator.Height = settings.mapHeight;
        mazeGenerator.ExtraConnections = settings.extraConnections;

        // Configurar otros parámetros de la corrutina
        accessibleZoneSize = settings.axeZoneSize;
        enableTravelCaves = settings.enableTravelCaves;

        ValidateSpawners();
        DisablePlayerControl();

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowLoadingScreen("Generando mapa procedural...");
        }

        bool levelAccepted = false;
        int baseSeed = mazeGenerator.LastUsedSeed;
        if (baseSeed == 0)
        {
            baseSeed = UnityEngine.Random.Range(1000, 99999);
        }

        // Respaldar configuración de semilla original del MazeGenerator para no romper configuraciones en el Inspector
        bool originalUseRandomSeed = true;
        int originalSeed = 12345;


        var useRandomSeedField = typeof(MazeGenerator).GetField("useRandomSeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var seedField = typeof(MazeGenerator).GetField("seed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        if (useRandomSeedField != null && seedField != null)
        {
            originalUseRandomSeed = (bool)useRandomSeedField.GetValue(mazeGenerator);
            originalSeed = (int)seedField.GetValue(mazeGenerator);
        }

        MazeCellType[,] maze = null;
        Vector2Int startCell = Vector2Int.zero;
        int acceptedSeed = 0;
        int finalFreeDirs = 0;
        int finalReachableCells = 0;

        for (int attempt = 1; attempt <= maximumGenerationAttempts; attempt++)
        {
            int currentSeed = baseSeed + attempt;
            Debug.Log($"[LevelGeneration] Intento {attempt}/{maximumGenerationAttempts}. Seed: {currentSeed}");

            // 1. Limpieza completa del intento anterior
            yield return StartCoroutine(ClearPreviousAttemptRoutine());

            // 2. Forzar semilla temporal en el generador usando Reflexión
            if (useRandomSeedField != null && seedField != null)
            {
                useRandomSeedField.SetValue(mazeGenerator, false);
                seedField.SetValue(mazeGenerator, currentSeed);
            }

            // 3. Generar la matriz lógica del laberinto
            maze = mazeGenerator.Generate();
            startCell = mazeGenerator.StartCell;

            destructibleWallsCells.Clear();
            ReplaceWallsWithDestructibles(maze, currentSeed, startCell, settings);

            // 4. Pre-calcular el origen del Tilemap para inicializar MazeData
            mazeRenderer.PreCalculateOrigin(maze);

            // 5. Inicializar Fuente de Verdad (MazeData)
            MazeData mazeData = GetComponent<MazeData>();
            if (mazeData == null) mazeData = gameObject.AddComponent<MazeData>();

            // Asegurar que TravelCavePairManager existe en el mismo GameObject
            if (travelCavePairManager == null)
            {
                travelCavePairManager = GetComponent<TravelCavePairManager>();
                if (travelCavePairManager == null)
                    travelCavePairManager = gameObject.AddComponent<TravelCavePairManager>();
            }
            mazeData.Initialize(maze, mazeRenderer.CurrentOrigin, mazeRenderer.LogicalCellTileSize);

            // 6. Pintar el laberinto visualmente (esto crea la base del nivel)
            mazeRenderer.Render(maze, currentSeed, startCell);

            // 7. Colocar temporalmente al jugador para validación física
            Vector3 playerWorldPos = mazeRenderer.GetWorldPosition(startCell);
            PreparePlayerForValidation(playerWorldPos);

            // 8. Esperar a que los colliders se actualicen en la escena
            Physics2D.SyncTransforms();
            yield return null;
            yield return new WaitForFixedUpdate();
            Physics2D.SyncTransforms();

            // 9. Validar Collider del Jugador
            Collider2D playerCollider = GetPlayerCollider();
            if (playerCollider == null)
            {
                RejectAttempt(attempt, currentSeed, "no se pudo encontrar el Collider2D del jugador");
                continue;
            }

            // 10. Validar solapamiento directo (Paredes)
            string overlappingObjectName;
            if (!ValidatePlayerOverlap(playerCollider, out overlappingObjectName))
            {
                RejectAttempt(attempt, currentSeed, $"jugador solapado con {overlappingObjectName}");
                continue;
            }

            // 11. Validar movimiento básico (Casts direccionales)
            int freeDirections;
            if (!ValidatePlayerMovement(playerCollider, out freeDirections))
            {
                RejectAttempt(attempt, currentSeed, "bloqueado en las cuatro direcciones");
                continue;
            }

            // 12. Validación lógica básica de la celda de inicio
            if (maze[startCell.x, startCell.y] == MazeCellType.Wall)
            {
                RejectAttempt(attempt, currentSeed, $"celda inicial {startCell} está marcada como pared en el algoritmo");
                continue;
            }

            // 13. Colocar destructibles provisionalmente
            bool spawnedDestructibles = TrySpawnDestructibles(maze, currentSeed, startCell, mazeData);
            if (!spawnedDestructibles)
            {
                RejectAttempt(attempt, currentSeed, "error al instanciar destructibles");
                continue;
            }

            // 14. Recalcular región navegable (Main Region) considerando destructibles
            mazeData.CalculateMainRegion(startCell);

            // 15. Validar accesibilidad post-destructibles
            if (!mazeData.IsCellWalkableAndMain(startCell.x, startCell.y))
            {
                RejectAttempt(attempt, currentSeed, "los destructibles bloquearon la celda de inicio del jugador");
                continue;
            }

            int reachableCellsCount = GetReachableCellsCount(mazeData, maze.GetLength(0), maze.GetLength(1));
            if (reachableCellsCount < 10) // Umbral mínimo de jugabilidad
            {
                RejectAttempt(attempt, currentSeed, $"los destructibles dejaron muy pocas celdas alcanzables ({reachableCellsCount})");
                continue;
            }

            // 16. Intentar colocar la misión procedural en este mapa base
            bool missionPlaced = false;
            for (int missionAttempt = 1; missionAttempt <= 50; missionAttempt++)
            {
                int missionSeed = currentSeed * 100 + missionAttempt;
                if (TryPlaceMission(maze, mazeData, missionSeed, settings))
                {
                    missionPlaced = true;
                    break;
                }
            }

            if (!missionPlaced)
            {
                RejectAttempt(attempt, currentSeed, "no se pudo colocar una misión soluble válida en este mapa base");
                continue;
            }

            // 17. Intentar colocar las cuevas de viaje rápido adicionales
            TryPlaceTravelCaves(maze, mazeData, currentSeed);

            // Guardar métricas del éxito
            acceptedSeed = currentSeed;
            finalFreeDirs = freeDirections;
            finalReachableCells = reachableCellsCount;
            levelAccepted = true;
            break;
        }

        // Restablecer la configuración original del generador para no ensuciar el Inspector
        if (useRandomSeedField != null && seedField != null)
        {
            useRandomSeedField.SetValue(mazeGenerator, originalUseRandomSeed);
            seedField.SetValue(mazeGenerator, originalSeed);
        }

        if (!levelAccepted)
        {
            // Limpieza completa en caso de fallo absoluto
            yield return StartCoroutine(ClearPreviousAttemptRoutine());
            if (playerTransform != null)
            {
                playerTransform.gameObject.SetActive(false);
            }
            Debug.LogError($"[LevelGeneration] FATAL: Se alcanzaron los {maximumGenerationAttempts} intentos sin éxito. El jugador permanece deshabilitado y no se generó el hacha.", this);
        }
        else
        {
            // Nivel Aceptado: Colocar definitivamente al jugador, sincronizar y habilitar control
            Physics2D.SyncTransforms();
            EnablePlayerControl();

            // Aplicar velocidad del jugador según la dificultad y reiniciar inventario
            if (playerTransform != null)
            {
                CatMovement movement = playerTransform.GetComponent<CatMovement>();
                if (movement == null) movement = playerTransform.GetComponentInChildren<CatMovement>();
                if (movement != null)
                {
                    movement.MoveSpeed = settings.playerMoveSpeed;
                }

                CatInventory inventory = playerTransform.GetComponent<CatInventory>();
                if (inventory == null) inventory = playerTransform.GetComponentInChildren<CatInventory>();
                if (inventory != null)
                {
                    inventory.ResetInventory();
                }
            }

            // Inicializar y actualizar controladores de cámara y UI automáticamente
            if (CameraZoomController.Instance != null)
            {
                CameraZoomController.Instance.UpdateSettingsFromDifficulty();
            }
            else
            {
                Camera mainCam = Camera.main;
                if (mainCam != null && mainCam.GetComponent<CameraZoomController>() == null)
                {
                    mainCam.gameObject.AddComponent<CameraZoomController>();
                }
            }

            if (GameUIManager.Instance == null)
            {
                GameObject uiManagerGO = new GameObject("GameUIManagerAutoCreated");
                uiManagerGO.AddComponent<GameUIManager>();
            }

            // Suscribirse a la puerta para el fin de nivel
            if (spawnedDoorInstance != null)
            {
                MazeDoor door = spawnedDoorInstance.GetComponent<MazeDoor>();
                if (door == null) door = spawnedDoorInstance.GetComponentInChildren<MazeDoor>();
                if (door != null)
                {
                    door.OnDoorOpened += OnLevelCompletedFromDoor;
                }
            }

            // Iniciar recopilación de métricas
            if (DifficultyMetricsCollector.Instance != null)
            {
                DifficultyMetricsCollector.Instance.StartCollecting();
            }

            Debug.Log($"[LevelGeneration] Nivel válido en intento {(acceptedSeed - baseSeed)}. Seed: {acceptedSeed}.");
            Debug.Log($"[LevelGeneration] Direcciones libres: {finalFreeDirs}/4.");
            Debug.Log($"[LevelGeneration] Celdas alcanzables: {finalReachableCells}.");

            // Pausa breve para una transición fluida al ocultar la pantalla de carga
            yield return new WaitForSeconds(0.4f);

            // Rehabilitar control y ocultar pantalla de carga
            EnablePlayerControl();
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.HideLoadingScreen();
            }
        }

        generationCoroutine = null;
    }

    private IEnumerator ClearPreviousAttemptRoutine()
    {
        // Desuscribirse de la puerta antes de destruirla
        if (spawnedDoorInstance != null)
        {
            MazeDoor door = spawnedDoorInstance.GetComponent<MazeDoor>();
            if (door == null) door = spawnedDoorInstance.GetComponentInChildren<MazeDoor>();
            if (door != null)
            {
                door.OnDoorOpened -= OnLevelCompletedFromDoor;
            }
        }

        // 1. Limpiar Tilemaps del renderizador
        if (mazeRenderer != null)
        {
            mazeRenderer.Clear();
        }

        // 2. Limpiar objetos de los spawners
        if (levelObjectSpawner != null)
        {
            levelObjectSpawner.ClearOccupiedCells();
        }
        if (gameplayObjectSpawner != null)
        {
            gameplayObjectSpawner.ClearOccupiedCells();
        }

        // 3. Limpiar hacha instanciada
        if (spawnedAxeInstance != null)
        {
            Destroy(spawnedAxeInstance);
            spawnedAxeInstance = null;
        }

        // Limpiar objetos de misión
        ClearMissionObjects();

        // 4. Desactivar temporalmente el jugador
        if (playerTransform != null)
        {
            var mazeAgent = playerTransform.GetComponent<MazeAgent>();
            if (mazeAgent == null || !mazeAgent.enabled)
            {
                playerTransform.gameObject.SetActive(false);
            }
            else
            {
                var rb = playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null) rb.simulated = false;
            }
        }

        // Esperamos 1 frame para que Unity procese las destrucciones físicas y garbage collection de GameObjects
        yield return null;
    }

    private void PreparePlayerForValidation(Vector3 spawnWorldPosition)
    {
        // Forzar siempre la asignación del gato local perteneciente al contenedor raíz
        Transform root = transform.parent != null ? transform.parent : transform;
        CatMovement localCat = root.GetComponentInChildren<CatMovement>();
        if (localCat != null)
        {
            playerTransform = localCat.transform;
        }
        else if (playerTransform != null && !playerTransform.gameObject.scene.IsValid())
        {
            playerTransform = null;
        }

        if (playerTransform == null)
        {
            CatMovement movementComp = FindAnyObjectByType<CatMovement>();
            if (movementComp != null)
            {
                playerTransform = movementComp.transform;
            }
        }

        if (playerTransform == null && playerPrefab != null)
        {
            GameObject newPlayer = Instantiate(playerPrefab, spawnWorldPosition, Quaternion.identity);
            if (transform.parent != null) newPlayer.transform.SetParent(transform.parent);
            playerTransform = newPlayer.transform;
        }

        if (playerTransform != null)
        {
            playerTransform.position = spawnWorldPosition;
            var mazeAgent = playerTransform.GetComponent<MazeAgent>();
            if (mazeAgent == null || !mazeAgent.enabled)
            {
                playerTransform.gameObject.SetActive(true);
                var rb = playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
            }


            var rbStatic = playerTransform.GetComponent<Rigidbody2D>();
            if (rbStatic != null)
            {
                rbStatic.linearVelocity = Vector2.zero;
                rbStatic.angularVelocity = 0f;
            }
        }
    }

    private void DisablePlayerControl()
    {
        if (playerTransform != null)
        {
            var movement = playerTransform.GetComponent<CatMovement>();
            if (movement != null)
            {
                movement.enabled = false;
            }

            var rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = false; // Desactivar simulación para evitar que caiga mientras validamos
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private void Update()
    {
        if (isTimerActive && !isTrainingModeActive)
        {
            currentLevelTimeLimit -= Time.deltaTime;
            if (currentLevelTimeLimit <= 0)
            {
                TriggerTimeOut();
            }
        }
    }

    private void TriggerTimeOut()
    {
        isTimerActive = false;
        DisablePlayerControl();
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowLoadingScreen("Goal not reached. Restarting map...");
        }
        StartCoroutine(TimeOutRestartRoutine());
    }

    private IEnumerator TimeOutRestartRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        StartGeneration();
    }

    private void EnablePlayerControl()
    {
        if (playerTransform != null)
        {
            var mazeAgent = playerTransform.GetComponent<MazeAgent>();
            var movement = playerTransform.GetComponent<CatMovement>();
            if (movement != null)
            {
                movement.enabled = (mazeAgent == null || !mazeAgent.enabled);
            }

            var rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (mazeAgent != null && mazeAgent.enabled)
            {
                mazeAgent.OnGenerationFinished();
            }

            if (!isTrainingModeActive)
            {
                isTimerActive = true;
                Debug.Log($"[ProceduralTimer] Temporizador ACTIVADO. Límite: {currentLevelTimeLimit}s.");
            }
            else
            {
                Debug.Log("[ProceduralTimer] Temporizador DESACTIVADO porque Training Mode está activo.");
            }
        }
    }

    private Collider2D GetPlayerCollider()
    {
        if (playerMovementCollider != null)
        {
            return playerMovementCollider;
        }

        if (playerTransform != null)
        {
            // Intentar buscar primer collider que no sea trigger (collider físico)
            Collider2D[] colliders = playerTransform.GetComponentsInChildren<Collider2D>(true);
            foreach (var col in colliders)
            {
                if (!col.isTrigger)
                {
                    playerMovementCollider = col;
                    return col;
                }
            }

            if (colliders.Length > 0)
            {
                playerMovementCollider = colliders[0];
                return colliders[0];
            }
        }

        return null;
    }

    private bool ValidatePlayerOverlap(Collider2D playerCollider, out string overlappingObjectName)
    {
        overlappingObjectName = "";
        if (playerCollider == null) return false;

        Bounds bounds = playerCollider.bounds;
        // Reducción del tamaño al 90% para evitar falsos positivos por contacto rasante con los bordes
        Vector2 validationSize = new Vector2(bounds.size.x * 0.9f, bounds.size.y * 0.9f);
        Vector2 origin = (Vector2)playerTransform.position;

        if (playerCollider is CapsuleCollider2D capsule)
        {
            origin += capsule.offset;
            Collider2D hit = Physics2D.OverlapCapsule(origin, validationSize, capsule.direction, 0f, obstacleLayer);
            if (hit != null)
            {
                overlappingObjectName = hit.name;
                return false;
            }
        }
        else if (playerCollider is BoxCollider2D box)
        {
            origin += box.offset;
            Collider2D hit = Physics2D.OverlapBox(origin, validationSize, 0f, obstacleLayer);
            if (hit != null)
            {
                overlappingObjectName = hit.name;
                return false;
            }
        }
        else if (playerCollider is CircleCollider2D circle)
        {
            origin += circle.offset;
            float validationRadius = circle.radius * 0.9f;
            Collider2D hit = Physics2D.OverlapCircle(origin, validationRadius, obstacleLayer);
            if (hit != null)
            {
                overlappingObjectName = hit.name;
                return false;
            }
        }
        else
        {
            // Fallback genérico usando OverlapArea
            Vector2 min = (Vector2)bounds.min + new Vector2(bounds.size.x * 0.05f, bounds.size.y * 0.05f);
            Vector2 max = (Vector2)bounds.max - new Vector2(bounds.size.x * 0.05f, bounds.size.y * 0.05f);
            Collider2D hit = Physics2D.OverlapArea(min, max, obstacleLayer);
            if (hit != null)
            {
                overlappingObjectName = hit.name;
                return false;
            }
        }

        return true;
    }

    private bool ValidatePlayerMovement(Collider2D playerCollider, out int freeDirections)
    {
        freeDirections = 0;
        if (playerCollider == null) return false;

        Vector2 origin = (Vector2)playerTransform.position;
        float castDistance = GetLogicalCellWorldSize() * 0.25f;

        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        if (playerCollider is CapsuleCollider2D capsule)
        {
            origin += capsule.offset;
            Vector2 validationSize = new Vector2(capsule.size.x * 0.9f, capsule.size.y * 0.9f);
            foreach (var dir in directions)
            {
                RaycastHit2D hit = Physics2D.CapsuleCast(origin, validationSize, capsule.direction, 0f, dir, castDistance, obstacleLayer);
                if (hit.collider == null)
                {
                    freeDirections++;
                }
            }
        }
        else if (playerCollider is BoxCollider2D box)
        {
            origin += box.offset;
            Vector2 validationSize = new Vector2(box.size.x * 0.9f, box.size.y * 0.9f);
            foreach (var dir in directions)
            {
                RaycastHit2D hit = Physics2D.BoxCast(origin, validationSize, 0f, dir, castDistance, obstacleLayer);
                if (hit.collider == null)
                {
                    freeDirections++;
                }
            }
        }
        else if (playerCollider is CircleCollider2D circle)
        {
            origin += circle.offset;
            float validationRadius = circle.radius * 0.9f;
            foreach (var dir in directions)
            {
                RaycastHit2D hit = Physics2D.CircleCast(origin, validationRadius, dir, castDistance, obstacleLayer);
                if (hit.collider == null)
                {
                    freeDirections++;
                }
            }
        }
        else
        {
            foreach (var dir in directions)
            {
                RaycastHit2D hit = Physics2D.Raycast(origin, dir, castDistance, obstacleLayer);
                if (hit.collider == null)
                {
                    freeDirections++;
                }
            }
        }

        return freeDirections > 0;
    }

    private float GetLogicalCellWorldSize()
    {
        if (mazeRenderer != null)
        {
            var tilemap = mazeRenderer.GetComponentInChildren<Tilemap>();
            if (tilemap != null && tilemap.layoutGrid != null)
            {
                return tilemap.layoutGrid.cellSize.x * mazeRenderer.LogicalCellTileSize.x;
            }
            return mazeRenderer.LogicalCellTileSize.x;
        }
        return 3.0f;
    }

    private int GetReachableCellsCount(MazeData mazeData, int w, int h)
    {
        int count = 0;
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (mazeData.IsCellWalkableAndMain(x, y))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private bool TrySpawnDestructibles(MazeCellType[,] maze, int visualSeed, Vector2Int startCell, MazeData mazeData)
    {
        TrainingModeInitializer trainingInit = FindAnyObjectByType<TrainingModeInitializer>();
        if (trainingInit != null && trainingInit.enabled && trainingInit.Config != null && trainingInit.Config.trainingMode)
        {
            if (trainingInit.Config.destructiblePercentage <= 0f)
            {
                Debug.Log("[Training] Saltando generación de destructibles aleatorios (destructiblePercentage = 0).");
                return true;
            }
        }

        Vector3Int playerSpawnCell = new Vector3Int(startCell.x, startCell.y, 0);

        BaseObjectSpawner spawner = levelObjectSpawner != null ? (BaseObjectSpawner)levelObjectSpawner : gameplayObjectSpawner;
        if (spawner == null)
        {
            spawner = GetComponentInChildren<BaseObjectSpawner>();
            if (spawner == null) spawner = FindAnyObjectByType<BaseObjectSpawner>();
        }

        if (spawner != null)
        {
            spawner.SpawnDestructibles(
                maze,
                mazeRenderer.CurrentOrigin,
                mazeRenderer.LogicalCellTileSize,
                playerSpawnCell,
                playerSpawnCell,
                visualSeed
            );
            foreach (var cell in spawner.OccupiedCells)
            {
                if (cell == playerSpawnCell) continue;
                mazeData.MarkCellsAsOccupied(new Vector2Int(cell.x, cell.y), 1, 1);
            }
            return true;
        }

        return false;
    }

    private void RejectAttempt(int attempt, int seed, string reason)
    {
        Debug.LogWarning($"[LevelGeneration] Intento rechazado: {reason}.");
    }

    private void SetupAxe(Vector3 spawnWorldPosition)
    {
        if (axeTransform != null && !axeTransform.gameObject.scene.IsValid())
        {
            axeTransform = null;
        }

        if (axeTransform == null)
        {
            CollectibleItem[] items = FindObjectsByType<CollectibleItem>();
            foreach (var item in items)
            {
                if (item.gameObject.name.ToLower().Contains("hacha") || item.gameObject.name.ToLower().Contains("axe"))
                {
                    axeTransform = item.transform;
                    break;
                }
            }
        }

        if (axeTransform != null)
        {
            axeTransform.position = spawnWorldPosition;
            axeTransform.gameObject.SetActive(true);
        }
        else if (axePrefab != null)
        {
            if (spawnedAxeInstance != null)
            {
                Destroy(spawnedAxeInstance);
            }

            Transform parent = itemsContainer != null ? itemsContainer : transform;
            spawnedAxeInstance = Instantiate(axePrefab, spawnWorldPosition, Quaternion.identity, parent);
        }
        else
        {
            Debug.LogWarning("DynamicLevelManager: No se asignó ni Axe Transform ni Axe Prefab en el Inspector.", this);
        }
    }

    private List<Vector2Int> GenerateAccessibleZoneForAxe(Vector2Int rootCell, Vector2Int size, MazeData mazeData, MazeCellType[,] maze)
    {
        List<Vector2Int> zoneCells = new List<Vector2Int>();
        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        // Asegurar que la zona tenga al menos tamaño 2x1 o 1x2 para albergar Cueva B y el Hacha
        int targetWidth = size.x;
        int targetHeight = size.y;
        if (targetWidth <= 1 && targetHeight <= 1)
        {
            targetWidth = 2;
            targetHeight = 1;
        }
        targetWidth = Mathf.Max(1, targetWidth);
        targetHeight = Mathf.Max(1, targetHeight);

        // Intentar centrar la zona de tamaño en rootCell, pero ajustar para no tocar los bordes del mapa (0 y width-1/height-1)
        int halfWidth = targetWidth / 2;
        int halfHeight = targetHeight / 2;

        int startX = rootCell.x - halfWidth;
        int startY = rootCell.y - halfHeight;

        // Limitar para mantener un margen de 1 celda con respecto a los bordes exteriores del laberinto
        startX = Mathf.Clamp(startX, 1, mazeWidth - 1 - targetWidth);
        startY = Mathf.Clamp(startY, 1, mazeHeight - 1 - targetHeight);

        for (int x = 0; x < targetWidth; x++)
        {
            for (int y = 0; y < targetHeight; y++)
            {
                int cellX = startX + x;
                int cellY = startY + y;

                if (cellX > 0 && cellX < mazeWidth - 1 && cellY > 0 && cellY < mazeHeight - 1)
                {
                    Vector2Int cell = new Vector2Int(cellX, cellY);
                    zoneCells.Add(cell);
                    mazeData.ConvertToAccessibleZone(cellX, cellY);
                }
            }
        }

        return zoneCells;
    }

    private void ClearMissionObjects()
    {
        if (spawnedCuevaAInstance != null) Destroy(spawnedCuevaAInstance);
        if (spawnedCuevaBInstance != null) Destroy(spawnedCuevaBInstance);
        if (spawnedKeyInstance != null) Destroy(spawnedKeyInstance);
        if (spawnedDoorInstance != null) Destroy(spawnedDoorInstance);
        foreach (var obj in spawnedMissionDestructibles)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedMissionDestructibles.Clear();

        // Limpiar todas las parejas de cuevas opcionales
        if (travelCavePairManager != null)
            travelCavePairManager.ClearAllPairs();

        activePortalConnections.Clear();
        keyToGoalWalkingResult = null;
        keyToGoalMechanicResult = null;
        optimalPortalPairIndices.Clear();

        startToCaveAPath.Clear();
        caveBToAxePath.Clear();
        axeToBarrierPath.Clear();
        keyToMetaPath.Clear();
        keyZoneCells.Clear();
        metaZoneCells.Clear();
        barrierCells.Clear();

        cuevaA = Vector2Int.zero;
        cuevaB = Vector2Int.zero;
        axeCell = Vector2Int.zero;
        keyCell = Vector2Int.zero;
        metaCell = Vector2Int.zero;
        axeZoneCells.Clear();

        // Cancelar tracking de llave→meta si estaba activo
        if (KeyToGoalTracker.Instance != null)
            KeyToGoalTracker.Instance.CancelTracking();
    }

    private void ResetMazeDataToBaseState(MazeCellType[,] maze, MazeData mazeData)
    {
        mazeData.Initialize(maze, mazeRenderer.CurrentOrigin, mazeRenderer.LogicalCellTileSize);

        // Volver a marcar las celdas ocupadas por destructibles base del spawner

        BaseObjectSpawner spawnerToUse = levelObjectSpawner != null ? (BaseObjectSpawner)levelObjectSpawner : gameplayObjectSpawner;
        if (spawnerToUse == null)
        {
            spawnerToUse = GetComponentInChildren<BaseObjectSpawner>();
            if (spawnerToUse == null) spawnerToUse = FindAnyObjectByType<BaseObjectSpawner>();
        }
        if (spawnerToUse != null)
        {
            foreach (var cell in spawnerToUse.OccupiedCells)
            {
                if (new Vector2Int(cell.x, cell.y) == mazeGenerator.StartCell) continue;
                mazeData.MarkCellsAsOccupied(new Vector2Int(cell.x, cell.y), 1, 1);
            }
        }

        // Recalcular la región principal conexa básica
        mazeData.CalculateMainRegion(mazeGenerator.StartCell);
    }

    private void ReplaceWallsWithDestructibles(MazeCellType[,] maze, int seed, Vector2Int startCell, DifficultySettings settings)
    {
        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);
        System.Random random = new System.Random(seed);
        float percentage = settings.destructibleWallsPercentage;

        for (int x = 1; x < mazeWidth - 1; x++)
        {
            for (int y = 1; y < mazeHeight - 1; y++)
            {
                if (maze[x, y] == MazeCellType.Wall)
                {
                    // Evitar que la pared esté en contacto directo con el spawn del jugador para no obstruir el inicio
                    if (Mathf.Abs(x - startCell.x) <= 1 && Mathf.Abs(y - startCell.y) <= 1) continue;

                    if (random.NextDouble() < percentage)
                    {
                        maze[x, y] = MazeCellType.Path;
                        destructibleWallsCells.Add(new Vector2Int(x, y));
                    }
                }
            }
        }
    }

    private List<Vector2Int> IsolateAndBuildAxeZone(Vector2Int rootCell, Vector2Int size, MazeData mazeData, MazeCellType[,] maze, out List<Vector2Int> blockedConnections)
    {
        blockedConnections = new List<Vector2Int>();
        List<Vector2Int> zoneCells = new List<Vector2Int>();
        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        // Asegurar que la zona tenga al menos tamaño 2x1 o 1x2 para albergar Cueva B y el Hacha
        int targetWidth = size.x;
        int targetHeight = size.y;
        if (targetWidth <= 1 && targetHeight <= 1)
        {
            targetWidth = 2;
            targetHeight = 1;
        }
        targetWidth = Mathf.Max(1, targetWidth);
        targetHeight = Mathf.Max(1, targetHeight);

        int halfWidth = targetWidth / 2;
        int halfHeight = targetHeight / 2;

        int startX = rootCell.x - halfWidth;
        int startY = rootCell.y - halfHeight;

        startX = Mathf.Clamp(startX, 1, mazeWidth - 1 - targetWidth);
        startY = Mathf.Clamp(startY, 1, mazeHeight - 1 - targetHeight);

        HashSet<Vector2Int> zoneSet = new HashSet<Vector2Int>();
        for (int x = 0; x < targetWidth; x++)
        {
            for (int y = 0; y < targetHeight; y++)
            {
                int cellX = startX + x;
                int cellY = startY + y;
                Vector2Int cell = new Vector2Int(cellX, cellY);
                zoneCells.Add(cell);
                zoneSet.Add(cell);
            }
        }

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var cell in zoneCells)
        {
            foreach (var dir in directions)
            {
                Vector2Int neighbor = cell + dir;
                if (neighbor.x > 0 && neighbor.x < mazeWidth - 1 && neighbor.y > 0 && neighbor.y < mazeHeight - 1)
                {
                    if (!zoneSet.Contains(neighbor))
                    {
                        if (maze[neighbor.x, neighbor.y] == MazeCellType.Path)
                        {
                            blockedConnections.Add(neighbor);
                        }
                    }
                }
            }
        }

        ResetMazeDataToBaseState(maze, mazeData);

        foreach (var cell in zoneCells)
        {
            mazeData.ConvertToAccessibleZone(cell.x, cell.y);
        }

        foreach (var conn in blockedConnections)
        {
            mazeData.SetCellAsWall(conn.x, conn.y);
        }

        // Recalcular la región principal conexa para reflejar las conexiones bloqueadas de la zona del hacha
        mazeData.CalculateMainRegion(mazeGenerator.StartCell);

        return zoneCells;
    }

    private bool TryPlaceMission(MazeCellType[,] maze, MazeData mazeData, int seed, DifficultySettings settings)
    {
        System.Random random = new System.Random(seed);
        Vector2Int startCell = mazeGenerator.StartCell;

        ClearMissionObjects();
        ResetMazeDataToBaseState(maze, mazeData);

        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        // --- META (PUERTA) ---
        // Buscamos la celda más lejana al inicio que pueda servir como meta (callejón o pasillo con 1 o 2 vecinos)
        List<Vector2Int> candidatesForDoor = new List<Vector2Int>();
        for (int x = 1; x < mazeWidth - 1; x++)
        {
            for (int y = 1; y < mazeHeight - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (cell == startCell) continue;

                if (mazeData.IsCellWalkableAndMain(x, y))
                {
                    int neighborsCount = GetWalkableNeighborsCount(cell, mazeData);
                    if (neighborsCount == 1 || neighborsCount == 2)
                    {
                        candidatesForDoor.Add(cell);
                    }
                }
            }
        }

        if (candidatesForDoor.Count == 0)
        {
            Debug.LogWarning("[MissionGen] No se encontró ningún candidato estructural para colocar la meta.");
            return false;
        }

        // Ordenamos los candidatos de mayor a menor distancia respecto al punto de inicio (spawn)
        candidatesForDoor.Sort((a, b) =>
        {
            float distA = Vector2Int.Distance(a, startCell);
            float distB = Vector2Int.Distance(b, startCell);
            return distB.CompareTo(distA); // Orden descendente (más lejano primero)
        });

        // Seleccionamos los candidatos que estén al menos a la distancia mínima parametrizada
        float minDoorDistanceThreshold = settings.minPlayerToMetaDistance;
        List<Vector2Int> farDoorCandidates = new List<Vector2Int>();
        foreach (var cand in candidatesForDoor)
        {
            if (Vector2Int.Distance(cand, startCell) >= minDoorDistanceThreshold)
            {
                farDoorCandidates.Add(cand);
            }
        }

        // Aseguramos tener al menos 3 opciones para dar variabilidad
        if (farDoorCandidates.Count < 3)
        {
            farDoorCandidates.Clear();
            for (int i = 0; i < Mathf.Min(3, candidatesForDoor.Count); i++)
            {
                farDoorCandidates.Add(candidatesForDoor[i]);
            }
        }

        // Desordenar (shuffle) únicamente el subconjunto de candidatos lejanos
        for (int i = 0; i < farDoorCandidates.Count; i++)
        {
            int rnd = random.Next(i, farDoorCandidates.Count);
            Vector2Int temp = farDoorCandidates[i];
            farDoorCandidates[i] = farDoorCandidates[rnd];
            farDoorCandidates[rnd] = temp;
        }

        metaCell = Vector2Int.zero;
        HashSet<Vector2Int> tempMetaBarriers = new HashSet<Vector2Int>();
        HashSet<Vector2Int> tempBarriers = new HashSet<Vector2Int>();
        bool metaPlaced = false;

        foreach (var candidate in farDoorCandidates)
        {
            List<Vector2Int> neighbors = GetWalkableNeighborsList(candidate, mazeData);
            if (neighbors.Count == 0 || neighbors.Count > 2) continue;

            // La meta no puede ser adyacente al jugador
            bool touchesStart = false;
            foreach (var neighbor in neighbors)
            {
                if (neighbor == startCell)
                {
                    touchesStart = true;
                    break;
                }
            }
            if (touchesStart) continue;

            metaCell = candidate;
            tempMetaBarriers.Clear();
            foreach (var neighbor in neighbors)
            {
                tempMetaBarriers.Add(neighbor);
            }
            metaPlaced = true;
            break;
        }

        if (!metaPlaced)
        {
            Debug.LogWarning("[MissionGen] No se pudo encontrar espacio aleatorio para la meta en las celdas más lejanas.");
            return false;
        }

        // --- ZONA DEL HACHA Y CUEVA B ---
        // Buscamos candidatos lejanos al spawn pero también lejanos a la meta para colocar el hacha en otra esquina
        List<Vector2Int> candidatesForAxe = new List<Vector2Int>();
        for (int x = 1; x < mazeWidth - 1; x++)
        {
            for (int y = 1; y < mazeHeight - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (cell == startCell || cell == metaCell || tempMetaBarriers.Contains(cell)) continue;

                if (mazeData.IsCellWalkableAndMain(x, y))
                {
                    float distToStart = Vector2Int.Distance(cell, startCell);
                    float distToMeta = Vector2Int.Distance(cell, metaCell);

                    if (distToStart >= settings.minAxeToStartAndMetaDistance && distToMeta >= settings.minAxeToStartAndMetaDistance)
                    {
                        candidatesForAxe.Add(cell);
                    }
                }
            }
        }

        // Si la restricción de distancia es muy estricta, rebajamos el límite
        if (candidatesForAxe.Count == 0)
        {
            for (int x = 1; x < mazeWidth - 1; x++)
            {
                for (int y = 1; y < mazeHeight - 1; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (cell == startCell || cell == metaCell || tempMetaBarriers.Contains(cell)) continue;
                    if (mazeData.IsCellWalkableAndMain(x, y))
                    {
                        float distToStart = Vector2Int.Distance(cell, startCell);
                        float distToMeta = Vector2Int.Distance(cell, metaCell);
                        float minAxeDistFallback = settings.minAxeToStartAndMetaDistance * 0.6f;
                        if (distToStart >= minAxeDistFallback && distToMeta >= minAxeDistFallback)
                        {
                            candidatesForAxe.Add(cell);
                        }
                    }
                }
            }
        }

        if (candidatesForAxe.Count == 0)
        {
            Debug.LogWarning("[MissionGen] No se encontró candidato para la zona del hacha separado de la meta.");
            return false;
        }

        axeCell = candidatesForAxe[random.Next(candidatesForAxe.Count)];

        List<Vector2Int> blockedConnections;
        axeZoneCells = IsolateAndBuildAxeZone(axeCell, accessibleZoneSize, mazeData, maze, out blockedConnections);

        cuevaB = Vector2Int.zero;
        bool foundB = false;
        foreach (var cell in axeZoneCells)
        {
            if (cell != axeCell && !tempBarriers.Contains(cell) && !tempMetaBarriers.Contains(cell) && !destructibleWallsCells.Contains(cell))
            {
                cuevaB = cell;
                foundB = true;
                break;
            }
        }

        if (!foundB)
        {
            Debug.LogWarning("[MissionGen] La zona del hacha es demasiado pequeña para albergar a Cueva B.");
            return false;
        }

        // --- CUEVA A ---
        List<Vector2Int> candidatesForA = new List<Vector2Int>();
        for (int x = 1; x < mazeWidth - 1; x++)
        {
            for (int y = 1; y < mazeHeight - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (cell == startCell || cell == metaCell || tempMetaBarriers.Contains(cell) || tempBarriers.Contains(cell) || axeZoneCells.Contains(cell) || destructibleWallsCells.Contains(cell)) continue;

                if (mazeData.IsCellWalkableAndMain(x, y) && Vector2Int.Distance(cell, startCell) >= settings.minPlayerToCaveADistance)
                {
                    candidatesForA.Add(cell);
                }
            }
        }

        if (candidatesForA.Count == 0)
        {
            Debug.LogWarning("[MissionGen] No se encontró espacio para Cueva A fuera de la zona del hacha y de la meta.");
            return false;
        }

        cuevaA = candidatesForA[random.Next(candidatesForA.Count)];

        // --- CALCULAR CAMINO PROTEGIDO INICIAL ---
        List<Vector2Int> tempProtectedPath = LevelValidator.GetPath(startCell, cuevaA, mazeData, false, false, cuevaA, cuevaB, cuevaB, cuevaA, new HashSet<Vector2Int>(), mazeWidth, mazeHeight);
        if (tempProtectedPath.Count == 0)
        {
            Debug.LogWarning("[MissionGen] No se pudo trazar la ruta protegida inicial.");
            return false;
        }

        // --- LLAVE ---
        List<Vector2Int> candidatesForKey = new List<Vector2Int>();
        for (int x = 1; x < mazeWidth - 1; x++)
        {
            for (int y = 1; y < mazeHeight - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (axeZoneCells.Contains(cell) || cell == startCell || cell == cuevaA || cell == metaCell || tempMetaBarriers.Contains(cell)) continue;

                if (mazeData.IsCellWalkableAndMain(x, y))
                {
                    int neighborsCount = GetWalkableNeighborsCount(cell, mazeData);
                    if (neighborsCount == 1) // Callejón sin salida preferido
                    {
                        candidatesForKey.Add(cell);
                    }
                }
            }
        }

        if (candidatesForKey.Count == 0)
        {
            for (int x = 1; x < mazeWidth - 1; x++)
            {
                for (int y = 1; y < mazeHeight - 1; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (axeZoneCells.Contains(cell) || cell == startCell || cell == cuevaA || cell == metaCell || tempMetaBarriers.Contains(cell)) continue;
                    if (mazeData.IsCellWalkableAndMain(x, y))
                    {
                        candidatesForKey.Add(cell);
                    }
                }
            }
        }

        if (candidatesForKey.Count == 0)
        {
            Debug.LogWarning("[MissionGen] No se encontró espacio para colocar la llave.");
            return false;
        }

        keyCell = Vector2Int.zero;
        tempBarriers.Clear();
        bool keyPlaced = false;

        // Desordenar candidatos para selección aleatoria estructurada
        List<Vector2Int> shuffledCandidates = new List<Vector2Int>(candidatesForKey);
        for (int i = 0; i < shuffledCandidates.Count; i++)
        {
            int rnd = random.Next(i, shuffledCandidates.Count);
            Vector2Int temp = shuffledCandidates[i];
            shuffledCandidates[i] = shuffledCandidates[rnd];
            shuffledCandidates[rnd] = temp;
        }

        foreach (var candidate in shuffledCandidates)
        {
            List<Vector2Int> neighbors = GetWalkableNeighborsList(candidate, mazeData);
            if (neighbors.Count == 0 || neighbors.Count > 2) continue;

            bool neighborProtected = false;
            foreach (var neighbor in neighbors)
            {
                if (neighbor == startCell || neighbor == cuevaA || neighbor == cuevaB || neighbor == axeCell ||

                    neighbor == metaCell || tempMetaBarriers.Contains(neighbor) || tempProtectedPath.Contains(neighbor))
                {
                    neighborProtected = true;
                    break;
                }
            }

            if (neighborProtected) continue;

            keyCell = candidate;
            tempBarriers.Clear();
            foreach (var neighbor in neighbors)
            {
                tempBarriers.Add(neighbor);
            }
            keyPlaced = true;
            break;
        }

        if (!keyPlaced)
        {
            Debug.LogWarning("[MissionGen] No se pudo encontrar un callejón para la llave cuyos accesos no bloqueasen la ruta protegida o la meta.");
            return false;
        }

        // Combinamos las barreras de la llave, de la meta y las paredes destructibles
        HashSet<Vector2Int> allBarriers = new HashSet<Vector2Int>(tempBarriers);
        foreach (var b in tempMetaBarriers)
        {
            allBarriers.Add(b);
        }
        foreach (var b in destructibleWallsCells)
        {
            allBarriers.Add(b);
        }

        // Excluir de allBarriers las celdas reservadas especiales para que NINGUNA barrera se cree sobre cuevas, inicio, hacha, llave o meta
        HashSet<Vector2Int> reservedKeyCells = new HashSet<Vector2Int>
        {
            startCell, cuevaA, cuevaB, axeCell, keyCell, metaCell
        };
        allBarriers.ExceptWith(reservedKeyCells);

        // --- VALIDACIÓN DE LOS 3 ESTADOS LÓGICOS DE MISIÓN ---
        TrainingConfig tConfig = Resources.Load<TrainingConfig>("TrainingConfig");
        bool initialHasAxe = (tConfig != null && tConfig.trainingMode && tConfig.startWithAxe);

        bool canReachCaveA = LevelValidator.CanPathfind(startCell, cuevaA, mazeData, initialHasAxe, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
        bool canReachAxeFromB = LevelValidator.CanPathfind(cuevaB, axeCell, mazeData, initialHasAxe, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);

        if (!canReachCaveA)
        {
            Debug.LogWarning("[MissionGen] Falló la validación: Cueva A no es alcanzable.");
            return false;
        }
        if (!canReachAxeFromB)
        {
            Debug.LogWarning("[MissionGen] Falló la validación: El hacha no es alcanzable desde Cueva B.");
            return false;
        }

        if (!initialHasAxe)
        {
            bool canReachKeyWithoutAxe = LevelValidator.CanPathfind(startCell, keyCell, mazeData, false, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
            bool canReachMetaWithoutAxe = LevelValidator.CanPathfind(startCell, metaCell, mazeData, false, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);

            if (canReachKeyWithoutAxe)
            {
                Debug.LogWarning("[MissionGen] Falló la validación: La llave es accesible SIN poseer el hacha.");
                return false;
            }
            if (canReachMetaWithoutAxe)
            {
                Debug.LogWarning("[MissionGen] Falló la validación: La meta es accesible SIN poseer el hacha.");
                return false;
            }
        }

        bool canReachKeyWithAxe = LevelValidator.CanPathfind(startCell, keyCell, mazeData, true, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
        if (!canReachKeyWithAxe)
        {
            Debug.LogWarning("[MissionGen] Falló la validación: La llave no es alcanzable incluso CON el hacha.");
            return false;
        }

        bool canReachMetaWithKey = LevelValidator.CanPathfind(keyCell, metaCell, mazeData, true, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
        if (!canReachMetaWithKey)
        {
            Debug.LogWarning("[MissionGen] Falló la validación: La meta no es alcanzable después de obtener la llave.");
            return false;
        }

        // --- TODO COMPROBADO Y VÁLIDO: PROCEDER A INSTANCIAR FÍSICAMENTE ---


        foreach (var conn in blockedConnections)
        {
            mazeRenderer.PaintWallCell(conn, random);
        }
        mazeRenderer.PaintAccessibleZone(axeZoneCells);

        Transform safeContainer = itemsContainer != null ? itemsContainer : transform;
        Vector3 posA = mazeRenderer.GetWorldPosition(cuevaA);
        spawnedCuevaAInstance = Instantiate(cavePrefab, posA, Quaternion.identity, safeContainer);
        spawnedCuevaAInstance.name = "Cave_A_Entrance";

        Vector3 posB = mazeRenderer.GetWorldPosition(cuevaB);
        spawnedCuevaBInstance = Instantiate(cavePrefab, posB, Quaternion.identity, safeContainer);
        spawnedCuevaBInstance.name = "Cave_B_Exit";

        CavePortal portalA = spawnedCuevaAInstance.GetComponentInChildren<CavePortal>();
        CavePortal portalB = spawnedCuevaBInstance.GetComponentInChildren<CavePortal>();

        // Encontrar los transform de los ExitPoint pre-existentes dentro del prefab de la cueva
        Transform exitA = spawnedCuevaAInstance.transform.Find("ExitPoint");
        if (exitA == null) exitA = spawnedCuevaAInstance.transform.Find("EntranceTrigger");
        if (exitA == null) exitA = spawnedCuevaAInstance.transform;

        Transform exitB = spawnedCuevaBInstance.transform.Find("ExitPoint");
        if (exitB == null) exitB = spawnedCuevaBInstance.transform.Find("EntranceTrigger");
        if (exitB == null) exitB = spawnedCuevaBInstance.transform;

        if (portalA != null)
        {
            portalA.DestinationExitPoint = exitB;
            portalA.enabled = true;
        }
        if (portalB != null)
        {
            portalB.DestinationExitPoint = exitA;
            portalB.enabled = true;
        }

        Vector3 posAxe = mazeRenderer.GetWorldPosition(axeCell);
        SetupAxe(posAxe);

        Vector3 posKey = mazeRenderer.GetWorldPosition(keyCell);
        spawnedKeyInstance = Instantiate(keyPrefab, posKey, Quaternion.identity, safeContainer);
        spawnedKeyInstance.name = "Mission_Key";

        Vector3 posMeta = mazeRenderer.GetWorldPosition(metaCell);
        spawnedDoorInstance = Instantiate(doorPrefab, posMeta, Quaternion.identity, safeContainer);
        spawnedDoorInstance.name = "Maze_Goal_Door";
        try
        {
            spawnedDoorInstance.tag = "Goal";
            foreach (Transform child in spawnedDoorInstance.transform)
            {
                child.gameObject.tag = "Goal";
            }
        }
        catch (System.Exception) { }

        BaseObjectSpawner spawnerToUse = levelObjectSpawner != null ? (BaseObjectSpawner)levelObjectSpawner : gameplayObjectSpawner;
        if (spawnerToUse == null)
        {
            spawnerToUse = GetComponentInChildren<BaseObjectSpawner>();
            if (spawnerToUse == null) spawnerToUse = FindAnyObjectByType<BaseObjectSpawner>();
        }

        TrainingModeInitializer trainingInit = FindAnyObjectByType<TrainingModeInitializer>();
        bool skipMissionBarriers = (trainingInit != null && trainingInit.enabled && trainingInit.Config != null && trainingInit.Config.trainingMode && trainingInit.Config.disableMandatoryDestructibleBarrier);

        if (!skipMissionBarriers)
        {
            foreach (var bar in allBarriers)
            {
                // Decidir la dirección de bloqueo en base a los vecinos de bar en el laberinto
                bool hasHorizontalNeighbors = (bar.x > 0 && mazeData.IsCellWalkable(bar.x - 1, bar.y)) ||

                                              (bar.x < mazeWidth - 1 && mazeData.IsCellWalkable(bar.x + 1, bar.y));

                Vector3Int centerTile = new Vector3Int(
                    mazeRenderer.CurrentOrigin.x + bar.x * mazeRenderer.LogicalCellTileSize.x + mazeRenderer.LogicalCellTileSize.x / 2,
                    mazeRenderer.CurrentOrigin.y + bar.y * mazeRenderer.LogicalCellTileSize.y + mazeRenderer.LogicalCellTileSize.y / 2,
                    mazeRenderer.CurrentOrigin.z
                );

                // Bloqueamos la celda lógicamente completa pintando destructibles sobre todos sus tiles de camino internos (matriz completa)
                int sizeX = mazeRenderer.LogicalCellTileSize.x;
                int sizeY = mazeRenderer.LogicalCellTileSize.y;

                List<Vector3Int> tileOffsets = new List<Vector3Int>();
                int halfX = sizeX / 2;
                int halfY = sizeY / 2;

                for (int dx = -halfX; dx <= halfX; dx++)
                {
                    for (int dy = -halfY; dy <= halfY; dy++)
                    {
                        Vector3Int offset = new Vector3Int(dx, dy, 0);
                        Vector3Int targetTile = centerTile + offset;

                        if (mazeRenderer.PathTilemap != null && mazeRenderer.PathTilemap.HasTile(targetTile))
                        {
                            tileOffsets.Add(offset);
                        }
                    }
                }

                if (tileOffsets.Count == 0)
                {
                    tileOffsets.Add(Vector3Int.zero);
                }

                List<Vector3Int> reservedCells = new List<Vector3Int> { new Vector3Int(bar.x, bar.y, 0) };

                // Obtener sorting layer y sorting order del spawner para garantizar visibilidad
                string sLayer = "BreakableObjects";
                int sOrder = 100;
                if (spawnerToUse != null)
                {
                    sLayer = spawnerToUse.TargetSortingLayer;
                    sOrder = spawnerToUse.TargetSortingOrder;
                }
                int layerID = !string.IsNullOrEmpty(sLayer) ? SortingLayer.NameToID(sLayer) : 0;
                bool isValidLayer = SortingLayer.IsValid(layerID);

                foreach (var offset in tileOffsets)
                {
                    Vector3Int targetTile = centerTile + offset;
                    Vector3 worldPos = mazeRenderer.PathTilemap != null ? mazeRenderer.PathTilemap.GetCellCenterWorld(targetTile) : mazeRenderer.GetWorldPosition(bar);

                    GameObject spawnedBar = Instantiate(missionDestructiblePrefab, worldPos, Quaternion.identity, safeContainer);
                    spawnedBar.name = $"Mission_Barrier_{bar.x}_{bar.y}_{offset.x}_{offset.y}";
                    spawnedMissionDestructibles.Add(spawnedBar);

                    // Asegurar que esté en la capa física 'Wall' para que el sensor RayPerception lo vea
                    int wallLayer = LayerMask.NameToLayer("Wall");
                    if (wallLayer != -1)
                    {
                        spawnedBar.layer = wallLayer;
                        foreach (Transform child in spawnedBar.GetComponentsInChildren<Transform>(true))
                        {
                            child.gameObject.layer = wallLayer;
                        }
                    }

                    DestructibleObject comp = spawnedBar.GetComponent<DestructibleObject>();
                    if (comp == null) comp = spawnedBar.AddComponent<DestructibleObject>();
                    comp.SetReservedCells(reservedCells);
                    comp.SetHealth(settings.missionDestructiblesHealth);

                    // Configurar SpriteRenderers
                    SpriteRenderer[] renderers = spawnedBar.GetComponentsInChildren<SpriteRenderer>();
                    foreach (var sr in renderers)
                    {
                        if (isValidLayer)
                        {
                            sr.sortingLayerID = layerID;
                        }
                        sr.sortingOrder = sOrder;
                    }
                }


                mazeData.MarkCellsAsOccupied(bar, 1, 1);
            }
        }
        else
        {
            Debug.Log("[DynamicLevelManager] 🚫 Omite la creación de barreras destructibles (Mission_Barrier) por TrainingConfig.");
        }

        mazeData.MarkCellsAsOccupied(cuevaA, 1, 1);
        mazeData.MarkCellsAsOccupied(cuevaB, 1, 1);
        mazeData.MarkCellsAsOccupied(keyCell, 1, 1);
        mazeData.MarkCellsAsOccupied(metaCell, 1, 1);

        // Guardar rutas finales del mapa aceptado para dibujo de Gizmos
        startToCaveAPath = tempProtectedPath;
        caveBToAxePath = LevelValidator.GetPath(cuevaB, axeCell, mazeData, false, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
        axeToBarrierPath = LevelValidator.GetPath(startCell, keyCell, mazeData, true, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
        keyToMetaPath = LevelValidator.GetPath(keyCell, metaCell, mazeData, true, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);

        keyZoneCells = new List<Vector2Int> { keyCell };
        metaZoneCells = new List<Vector2Int> { metaCell };
        barrierCells = allBarriers;

        Debug.Log("[MissionGen] ¡Éxito! Misión procedural soluble e instanciada.");
        return true;
    }

    private Vector2Int GetWalkableNeighbor(Vector2Int cell, MazeData mazeData)
    {
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int neighbor = cell + dir;
            if (mazeData.IsCellWalkable(neighbor.x, neighbor.y))
            {
                return neighbor;
            }
        }
        return cell;
    }

    private int GetWalkableNeighborsCount(Vector2Int cell, MazeData mazeData)
    {
        int count = 0;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int neighbor = cell + dir;
            if (mazeData.IsCellWalkable(neighbor.x, neighbor.y))
            {
                count++;
            }
        }
        return count;
    }

    private List<Vector2Int> GetWalkableNeighborsList(Vector2Int cell, MazeData mazeData, bool ignoreOccupied = false)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int neighbor = cell + dir;
            bool walkable = ignoreOccupied

                ? mazeData.IsCellWalkableIgnoreOccupied(neighbor.x, neighbor.y)
                : mazeData.IsCellWalkable(neighbor.x, neighbor.y);

            if (walkable)
            {
                list.Add(neighbor);
            }
        }
        return list;
    }

    private bool TryPlaceTravelCaves(MazeCellType[,] maze, MazeData mazeData, int seed)
    {
        DifficultySettings settings = null;
        if (DifficultyManager.Instance != null)
            settings = DifficultyManager.Instance.CurrentSettings;
        if (settings == null)
            settings = new DifficultySettings();

        enableTravelCaves = settings.enableTravelCaves;

        if (!settings.enableTravelCaves)
        {
            Debug.LogWarning("[TravelCaves] Las cuevas opcionales están DESHABILITADAS en la configuración de dificultad (settings.enableTravelCaves = false).");
            return true;
        }

        // Delegar en TravelCavePairManager (soporta múltiples parejas)
        if (travelCavePairManager == null)
        {
            travelCavePairManager = GetComponent<TravelCavePairManager>();
            if (travelCavePairManager == null)
                travelCavePairManager = gameObject.AddComponent<TravelCavePairManager>();
        }

        // Construir conjunto de celdas de misión a excluir
        var missionCells = new HashSet<Vector2Int>
        {
            mazeGenerator.StartCell, cuevaA, cuevaB, axeCell, keyCell, metaCell
        };
        foreach (var b in barrierCells) missionCells.Add(b);

        travelCavePairManager.GeneratePairs(
            maze, mazeData,
            cavePrefab, itemsContainer, mazeRenderer,
            mazeGenerator.StartCell,
            missionCells, barrierCells, axeZoneCells,
            settings, seed);

        // Construir lista de conexiones activas para el pathfinder
        activePortalConnections = travelCavePairManager.BuildPortalConnections(mazeData);

        // Registrar el evento de llave recogida para calcular rutas óptimas
        // (lo hacemos aquí, después de conocer los portales disponibles)
        SubscribeToKeyCollectionForPathfinding();

        return true;
    }

    // ── Pathfinding llave→meta ───────────────────────────────────────────────────

    private CatInventory subscribedInventory;

    private void SubscribeToKeyCollectionForPathfinding()
    {
        // Desuscribir primero para evitar doble suscripción
        if (subscribedInventory != null)
        {
            subscribedInventory.OnKeyCollected -= OnKeyCollectedForPathfinding;
            subscribedInventory = null;
        }

        if (playerTransform == null) return;

        subscribedInventory = playerTransform.GetComponent<CatInventory>();
        if (subscribedInventory == null)
            subscribedInventory = playerTransform.GetComponentInChildren<CatInventory>();

        if (subscribedInventory != null)
            subscribedInventory.OnKeyCollected += OnKeyCollectedForPathfinding;
    }

    private void OnKeyCollectedForPathfinding()
    {
        MazeData mazeData = GetComponent<MazeData>();
        if (mazeData == null) return;

        DifficultySettings settings = null;
        if (DifficultyManager.Instance != null)
            settings = DifficultyManager.Instance.CurrentSettings;
        if (settings == null)
            settings = new DifficultySettings();

        // 1. Determinar celda inicial (priorizar posición actual del jugador, fallback a celda transitable más cercana o keyCell)
        Vector2Int playerCell = playerTransform != null

            ? mazeRenderer.GetCellFromWorldPosition(playerTransform.position)
            : keyCell;

        Vector2Int startCellForPath = mazeData.IsWalkable(playerCell.x, playerCell.y)
            ? playerCell
            : mazeData.GetNearestWalkableCell(keyCell);

        Vector2Int goalCellForPath = metaCell;

        // 2. Calcular ruta óptima caminando (sin portales)
        keyToGoalWalkingResult = MazePathfinder.FindWalkingPath(
            startCellForPath, goalCellForPath, mazeData, true, barrierCells, 1f);

        // 3. Calcular ruta óptima con portales opcionales activos
        keyToGoalMechanicResult = MazePathfinder.FindPathWithPortals(
            startCellForPath, goalCellForPath, mazeData, true,
            activePortalConnections, barrierCells,
            1f);

        // 4. Registrar los portales usados por la ruta óptima para Gizmos
        optimalPortalPairIndices = keyToGoalMechanicResult != null
            ? new List<int>(keyToGoalMechanicResult.PortalPairIndicesUsed)
            : new List<int>();

        // 5. Emitir Log de Diagnóstico Estructurado (de una sola ejecución)
        Vector3 playerWorldPos = playerTransform != null ? playerTransform.position : Vector3.zero;
        Vector3 keyWorldPos = spawnedKeyInstance != null ? spawnedKeyInstance.transform.position : mazeRenderer.GetWorldPosition(keyCell);
        Vector3 goalWorldPos = spawnedDoorInstance != null ? spawnedDoorInstance.transform.position : mazeRenderer.GetWorldPosition(metaCell);

        int width = mazeData.Width;
        int height = mazeData.Height;

        bool startInBounds = startCellForPath.x >= 0 && startCellForPath.x < width && startCellForPath.y >= 0 && startCellForPath.y < height;
        bool startWalkable = mazeData.IsWalkable(startCellForPath.x, startCellForPath.y);
        bool startOccupied = !startWalkable;
        int startNeighbors = GetWalkableNeighborsList(startCellForPath, mazeData, true).Count;

        bool goalInBounds = goalCellForPath.x >= 0 && goalCellForPath.x < width && goalCellForPath.y >= 0 && goalCellForPath.y < height;
        bool goalWalkable = mazeData.IsCellWalkableIgnoreOccupied(goalCellForPath.x, goalCellForPath.y);
        bool goalOccupied = false;
        int goalNeighbors = GetWalkableNeighborsList(goalCellForPath, mazeData, true).Count;

        int activeDestructiblesCount = spawnedMissionDestructibles != null ? spawnedMissionDestructibles.Count : 0;
        int activePortalPairsCount = travelCavePairManager != null ? travelCavePairManager.GeneratedPairs.Count : 0;

        bool walkingFound = keyToGoalWalkingResult != null && keyToGoalWalkingResult.PathExists;
        bool mechanicFound = keyToGoalMechanicResult != null && keyToGoalMechanicResult.PathExists;

        System.Text.StringBuilder diagLog = new System.Text.StringBuilder();
        diagLog.AppendLine("[KeyToGoal Pathfinding]");
        diagLog.AppendLine($"Player world position: {playerWorldPos}");
        diagLog.AppendLine($"Key world position: {keyWorldPos}");
        diagLog.AppendLine($"Start cell: {startCellForPath}");
        diagLog.AppendLine($"Start in bounds: {startInBounds}");
        diagLog.AppendLine($"Start walkable: {startWalkable}");
        diagLog.AppendLine($"Start occupied: {startOccupied}");
        diagLog.AppendLine($"Start walkable neighbors: {startNeighbors}");
        diagLog.AppendLine();
        diagLog.AppendLine($"Goal world position: {goalWorldPos}");
        diagLog.AppendLine($"Goal cell: {goalCellForPath}");
        diagLog.AppendLine($"Goal in bounds: {goalInBounds}");
        diagLog.AppendLine($"Goal walkable: {goalWalkable}");
        diagLog.AppendLine($"Goal occupied: {goalOccupied}");
        diagLog.AppendLine($"Goal walkable neighbors: {goalNeighbors}");
        diagLog.AppendLine();
        diagLog.AppendLine($"Active destructibles: {activeDestructiblesCount}");
        diagLog.AppendLine($"Active portal pairs: {activePortalPairsCount}");
        diagLog.AppendLine($"Walking path found: {walkingFound}");
        diagLog.AppendLine($"Mechanic path found: {mechanicFound}");

        if (!walkingFound || !mechanicFound)
        {
            if (!startInBounds) diagLog.AppendLine("Path failed: start cell is out of bounds.");
            else if (!startWalkable) diagLog.AppendLine("Path failed: start cell is not walkable.");
            else if (!goalInBounds) diagLog.AppendLine("Path failed: goal cell is out of bounds.");
            else if (!goalWalkable) diagLog.AppendLine("Path failed: goal cell is not walkable.");
            else if (goalNeighbors == 0) diagLog.AppendLine("Path failed: no accessible neighbor near goal.");
            else diagLog.AppendLine("Path failed: no path found between start and goal.");
        }

        Debug.Log(diagLog.ToString());

        // Iniciar tracking del segmento llave→meta
        EnsureKeyToGoalTracker();
        if (KeyToGoalTracker.Instance != null)
        {
            KeyToGoalTracker.Instance.StartTracking(
                keyToGoalWalkingResult,
                keyToGoalMechanicResult,
                mazeData,
                stepCost: 1f,
                portalCost: settings.teleportCost);
        }
    }

    private void EnsureKeyToGoalTracker()
    {
        if (KeyToGoalTracker.Instance == null)
        {
            new GameObject("KeyToGoalTracker").AddComponent<KeyToGoalTracker>();
        }
    }

    // ── Stub del método original — ahora es un NO-OP para evitar referencias obsoletas
    // El bloque original TryPlaceTravelCaves tenía 159 líneas de código que se reemplazó
    // por la llamada a TravelCavePairManager.GeneratePairs() arriba.
    // Estas helpers privadas ya no son necesarias pero se mantienen como guard:

    private bool OBSOLETE_TryPlaceTravelCaves_OriginalGuard()
    {
        // Este método solo existe para no romper referencias internas heredadas.
        // No se llama desde ningún lugar. Puede eliminarse en una limpieza futura.
        return true;
    }

    private bool IsSafeAreaClear(Vector2Int center, int radius, MazeData mazeData, Vector2Int startCell, int mazeWidth, int mazeHeight)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = center.x + dx;
                int y = center.y + dy;

                if (x < 1 || x >= mazeWidth - 1 || y < 1 || y >= mazeHeight - 1) return false;

                Vector2Int cell = new Vector2Int(x, y);

                // No puede colisionar con elementos importantes de la misión principal
                if (cell == startCell || cell == cuevaA || cell == cuevaB || cell == axeCell || cell == keyCell || cell == metaCell) return false;
                if (axeZoneCells.Contains(cell) || barrierCells.Contains(cell)) return false;

                // Debe ser caminable (no pared ni ocupada)
                if (!mazeData.IsCellWalkableAndMain(x, y)) return false;
            }
        }
        return true;
    }

    private List<Vector2Int> GetSafeAreaCells(Vector2Int center, int radius, int width, int height)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = center.x + dx;
                int y = center.y + dy;
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
        }
        return cells;
    }


    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || mazeRenderer == null) return;
        if (!Application.isPlaying) return;
        if (keyZoneCells == null || metaZoneCells == null || barrierCells == null) return;
        if (startToCaveAPath == null || caveBToAxePath == null || axeToBarrierPath == null || keyToMetaPath == null) return;

        // 1. Zonas (Llave, Meta, Barreras)
        if (showZoneGizmos)
        {
            // Zona de la Llave — Dorado
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.4f);
            foreach (var cell in keyZoneCells)
            {
                Vector3 worldPos = mazeRenderer.GetWorldPosition(cell);
                Vector2 size = mazeRenderer.LogicalCellTileSize;
                Gizmos.DrawWireCube(worldPos, new Vector3(size.x, size.y, 0.1f));
            }

            // Zona de la Meta — Azul
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.4f);
            foreach (var cell in metaZoneCells)
            {
                Vector3 worldPos = mazeRenderer.GetWorldPosition(cell);
                Vector2 size = mazeRenderer.LogicalCellTileSize;
                Gizmos.DrawWireCube(worldPos, new Vector3(size.x, size.y, 0.1f));
            }

            // Barreras destructibles — Naranja
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            foreach (var cell in barrierCells)
            {
                Vector3 worldPos = mazeRenderer.GetWorldPosition(cell);
                Vector2 size = mazeRenderer.LogicalCellTileSize;
                Gizmos.DrawCube(worldPos, new Vector3(size.x, size.y, 0.1f));
            }
        }

        // 2. Rutas de misión obligatoria
        if (showMissionPathsGizmos)
        {
            DrawPathGizmo(startToCaveAPath, Color.green);
            DrawPathGizmo(caveBToAxePath, Color.cyan);
            DrawPathGizmo(axeToBarrierPath, Color.red);
            DrawPathGizmo(keyToMetaPath, Color.blue);
        }

        // 3. Ruta óptima llave→meta con mecánicas (Dijkstra en Verde)
        if (showOptimalKeyToGoalGizmo && keyToGoalMechanicResult != null && keyToGoalMechanicResult.PathExists)
        {
            DrawPathGizmo(keyToGoalMechanicResult.Cells, new Color(0.3f, 1f, 0.3f));
        }

        // 4. Parejas de cuevas opcionales (via TravelCavePairManager)
        if (showTravelCaveGizmos && enableTravelCaves && travelCavePairManager != null)
        {
            travelCavePairManager.DrawGizmos(mazeRenderer, optimalPortalPairIndices);
        }
    }


    private void DrawPathGizmo(List<Vector2Int> path, Color color)
    {
        if (path == null || path.Count < 2) return;
        Gizmos.color = color;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 p1 = mazeRenderer.GetWorldPosition(path[i]);
            Vector3 p2 = mazeRenderer.GetWorldPosition(path[i + 1]);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawWireSphere(p1, 0.15f);
        }
        Gizmos.DrawWireSphere(mazeRenderer.GetWorldPosition(path[path.Count - 1]), 0.15f);
    }

    private void OnLevelCompletedFromDoor()
    {
        if (spawnedDoorInstance != null)
        {
            MazeDoor doorComp = spawnedDoorInstance.GetComponent<MazeDoor>();
            if (doorComp == null) doorComp = spawnedDoorInstance.GetComponentInChildren<MazeDoor>();
            if (doorComp != null)
            {
                doorComp.OnDoorOpened -= OnLevelCompletedFromDoor;
            }
        }

        // Validación de finalización de nivel
        bool pathExistedOrMechanic = (keyToGoalWalkingResult != null && keyToGoalWalkingResult.PathExists) ||
                                     (keyToGoalMechanicResult != null && keyToGoalMechanicResult.PathExists);
        bool hadBarriersToBreak = barrierCells != null && barrierCells.Count > 0;

        if (!pathExistedOrMechanic && !hadBarriersToBreak)
        {
            Debug.LogWarning($"[LevelGeneration] Advertencia: El jugador llegó a la meta sin una ruta directa caminable registrada previamente.\n" +
                             $"  StartCell: {keyCell}, GoalCell: {metaCell}");
        }
        else
        {
            Debug.Log($"[LevelGeneration] ¡Nivel completado exitosamente! El jugador abrió la puerta de la meta.");
        }

        if (DifficultyMetricsCollector.Instance != null)
        {
            DifficultyMetricsCollector.Instance.OnLevelCompleted();
        }

        StartCoroutine(AutoRegenerateLevelRoutine());
    }

    private IEnumerator AutoRegenerateLevelRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);
    }
}
