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

    [SerializeField, Min(1)]
    private int minimumAxeDistanceFromPlayer = 3;

    [Header("Accessible Zone Settings")]
    [SerializeField]
    private bool generateAccessibleZone = true;

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

    [Header("Depuración Visual - Gizmos")]
    [SerializeField] private bool showDebugGizmos = true;

    private List<Vector2Int> startToCaveAPath = new List<Vector2Int>();
    private List<Vector2Int> caveBToAxePath = new List<Vector2Int>();
    private List<Vector2Int> axeToBarrierPath = new List<Vector2Int>();
    private List<Vector2Int> keyToMetaPath = new List<Vector2Int>();
    private List<Vector2Int> keyZoneCells = new List<Vector2Int>();
    private List<Vector2Int> metaZoneCells = new List<Vector2Int>();
    private HashSet<Vector2Int> barrierCells = new HashSet<Vector2Int>();

    private void Start()
    {
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
            Debug.LogError("[LevelGeneration] AMBOS spawners (LevelObjectSpawner y GameplayObjectSpawner) están asignados en el Inspector. Esto puede provocar objetos duplicados o comportamiento indefinido. Por favor, asigna solo uno.", this);
        }
    }

    private IEnumerator GenerateLevelRoutine()
    {
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

        ValidateSpawners();
        DisablePlayerControl();

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

            // 4. Pre-calcular el origen del Tilemap para inicializar MazeData
            mazeRenderer.PreCalculateOrigin(maze);

            // 5. Inicializar Fuente de Verdad (MazeData)
            MazeData mazeData = GetComponent<MazeData>();
            if (mazeData == null) mazeData = gameObject.AddComponent<MazeData>();
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
                if (TryPlaceMission(maze, mazeData, missionSeed))
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

            Debug.Log($"[LevelGeneration] Nivel válido en intento {(acceptedSeed - baseSeed)}. Seed: {acceptedSeed}.");
            Debug.Log($"[LevelGeneration] Direcciones libres: {finalFreeDirs}/4.");
            Debug.Log($"[LevelGeneration] Celdas alcanzables: {finalReachableCells}.");
        }

        generationCoroutine = null;
    }

    private IEnumerator ClearPreviousAttemptRoutine()
    {
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
            playerTransform.gameObject.SetActive(false);
        }

        // Esperamos 1 frame para que Unity procese las destrucciones físicas y garbage collection de GameObjects
        yield return null;
    }

    private void PreparePlayerForValidation(Vector3 spawnWorldPosition)
    {
        if (playerTransform != null && !playerTransform.gameObject.scene.IsValid())
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
            playerTransform = newPlayer.transform;
        }

        if (playerTransform != null)
        {
            playerTransform.position = spawnWorldPosition;
            playerTransform.gameObject.SetActive(true);

            var rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
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

    private void EnablePlayerControl()
    {
        if (playerTransform != null)
        {
            var movement = playerTransform.GetComponent<CatMovement>();
            if (movement != null)
            {
                movement.enabled = true;
            }

            var rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
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
        Vector3Int playerSpawnCell = new Vector3Int(startCell.x, startCell.y, 0);

        LevelObjectSpawner spawnerToUse = levelObjectSpawner;
        if (spawnerToUse == null)
        {
            spawnerToUse = GetComponentInChildren<LevelObjectSpawner>();
            if (spawnerToUse == null) spawnerToUse = FindAnyObjectByType<LevelObjectSpawner>();
        }

        if (spawnerToUse != null)
        {
            spawnerToUse.SpawnDestructibles(
                maze,
                mazeRenderer.CurrentOrigin,
                mazeRenderer.LogicalCellTileSize,
                playerSpawnCell,
                playerSpawnCell,
                visualSeed
            );
            foreach (var cell in spawnerToUse.OccupiedCells)
            {
                if (cell == playerSpawnCell) continue;
                mazeData.MarkCellsAsOccupied(new Vector2Int(cell.x, cell.y), 1, 1);
            }
            return true;
        }

        MazeGameplayObjectSpawner altSpawner = gameplayObjectSpawner;
        if (altSpawner == null)
        {
            altSpawner = GetComponentInChildren<MazeGameplayObjectSpawner>();
            if (altSpawner == null) altSpawner = FindAnyObjectByType<MazeGameplayObjectSpawner>();
        }

        if (altSpawner != null)
        {
            altSpawner.SpawnDestructibles(
                maze,
                mazeRenderer.CurrentOrigin,
                mazeRenderer.LogicalCellTileSize,
                playerSpawnCell,
                playerSpawnCell,
                visualSeed
            );
            foreach (var cell in altSpawner.OccupiedCells)
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

        startToCaveAPath.Clear();
        caveBToAxePath.Clear();
        axeToBarrierPath.Clear();
        keyToMetaPath.Clear();
        keyZoneCells.Clear();
        barrierCells.Clear();
    }

    private void ResetMazeDataToBaseState(MazeCellType[,] maze, MazeData mazeData)
    {
        mazeData.Initialize(maze, mazeRenderer.CurrentOrigin, mazeRenderer.LogicalCellTileSize);
        
        // Volver a marcar las celdas ocupadas por destructibles base del spawner
        LevelObjectSpawner spawnerToUse = levelObjectSpawner != null ? levelObjectSpawner : GetComponentInChildren<LevelObjectSpawner>();
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

    private bool TryPlaceMission(MazeCellType[,] maze, MazeData mazeData, int seed)
    {
        System.Random random = new System.Random(seed);
        Vector2Int startCell = mazeGenerator.StartCell;

        ClearMissionObjects();
        ResetMazeDataToBaseState(maze, mazeData);

        int mazeWidth = maze.GetLength(0);
        int mazeHeight = maze.GetLength(1);

        // --- CUEVA A ---
        List<Vector2Int> candidatesForA = new List<Vector2Int>();
        for (int x = 1; x < mazeWidth - 1; x++)
        {
            for (int y = 1; y < mazeHeight - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (cell == startCell) continue;
                if (mazeData.IsCellWalkableAndMain(x, y) && Vector2Int.Distance(cell, startCell) >= 2)
                {
                    candidatesForA.Add(cell);
                }
            }
        }

        if (candidatesForA.Count == 0)
        {
            Debug.LogWarning("[MissionGen] No se encontraron espacios lógicos para Cueva A.");
            return false;
        }

        Vector2Int cuevaA = candidatesForA[random.Next(candidatesForA.Count)];

        // --- ZONA DEL HACHA Y CUEVA B ---
        Vector2Int axeCell = mazeData.SelectFurthestCell(startCell, seed, minimumAxeDistanceFromPlayer);
        if (axeCell == startCell || !mazeData.IsCellWalkableAndMain(axeCell.x, axeCell.y))
        {
            Debug.LogWarning("[MissionGen] El hacha lógica de origen no es accesible o es inválida.");
            return false;
        }

        List<Vector2Int> blockedConnections;
        List<Vector2Int> axeZoneCells = IsolateAndBuildAxeZone(axeCell, accessibleZoneSize, mazeData, maze, out blockedConnections);

        Vector2Int cuevaB = Vector2Int.zero;
        bool foundB = false;
        foreach (var cell in axeZoneCells)
        {
            if (cell != axeCell)
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
                if (axeZoneCells.Contains(cell) || cell == cuevaA || cell == startCell) continue;
                
                if (mazeData.IsCellWalkableAndMain(x, y))
                {
                    int neighborsCount = GetWalkableNeighborsCount(cell, mazeData);
                    if (neighborsCount == 1) // Callejón sin salida
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
                    if (axeZoneCells.Contains(cell) || cell == cuevaA || cell == startCell) continue;
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

        // Buscar un candidato que no interfiera y que sus accesos puedan cerrarse
        Vector2Int keyCell = Vector2Int.zero;
        HashSet<Vector2Int> tempBarriers = new HashSet<Vector2Int>();
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
            if (neighbors.Count == 0 || neighbors.Count > 2) continue; // Descartar si está aislado o tiene demasiados accesos

            // Comprobar si los accesos a bloquear están en rutas protegidas
            bool neighborProtected = false;
            foreach (var neighbor in neighbors)
            {
                if (neighbor == startCell || neighbor == cuevaA || neighbor == cuevaB || neighbor == axeCell || tempProtectedPath.Contains(neighbor))
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
            Debug.LogWarning("[MissionGen] No se pudo encontrar un callejón para la llave cuyos accesos no bloqueasen la ruta protegida.");
            return false;
        }

        // --- META (PUERTA) ---
        List<Vector2Int> candidatesForDoor = new List<Vector2Int>();
        float minMetaDistance = (mazeWidth + mazeHeight) * 0.35f;

        for (int x = 1; x < mazeWidth - 1; x++)
        {
            for (int y = 1; y < mazeHeight - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                // Evitar colisionar con zonas clave ya colocadas
                if (axeZoneCells.Contains(cell) || cell == keyCell || tempBarriers.Contains(cell) || cell == cuevaA || cell == startCell) continue;

                // Debe estar lejos del spawn
                float dist = Vector2Int.Distance(cell, startCell);
                if (dist < minMetaDistance) continue;

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

        // Si no hay candidatos con distancia estricta, rebajamos la distancia mínima
        if (candidatesForDoor.Count == 0)
        {
            for (int x = 1; x < mazeWidth - 1; x++)
            {
                for (int y = 1; y < mazeHeight - 1; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (axeZoneCells.Contains(cell) || cell == keyCell || tempBarriers.Contains(cell) || cell == cuevaA || cell == startCell) continue;
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
        }

        if (candidatesForDoor.Count == 0)
        {
            Debug.LogWarning("[MissionGen] No se encontró espacio para colocar la meta lejana bloqueable.");
            return false;
        }

        // Buscar un candidato de meta que no interfiera y que sus accesos puedan cerrarse
        Vector2Int metaCell = Vector2Int.zero;
        HashSet<Vector2Int> tempMetaBarriers = new HashSet<Vector2Int>();
        bool metaPlaced = false;

        // Desordenar candidatos para selección aleatoria estructurada
        List<Vector2Int> shuffledMetaCandidates = new List<Vector2Int>(candidatesForDoor);
        for (int i = 0; i < shuffledMetaCandidates.Count; i++)
        {
            int rnd = random.Next(i, shuffledMetaCandidates.Count);
            Vector2Int temp = shuffledMetaCandidates[i];
            shuffledMetaCandidates[i] = shuffledMetaCandidates[rnd];
            shuffledMetaCandidates[rnd] = temp;
        }

        foreach (var candidate in shuffledMetaCandidates)
        {
            List<Vector2Int> neighbors = GetWalkableNeighborsList(candidate, mazeData);
            if (neighbors.Count == 0 || neighbors.Count > 2) continue;

            // Comprobar si los accesos a bloquear están en rutas protegidas, o si tocan la llave/barreras de la llave
            bool neighborProtected = false;
            foreach (var neighbor in neighbors)
            {
                if (neighbor == startCell || neighbor == cuevaA || neighbor == cuevaB || neighbor == axeCell || 
                    neighbor == keyCell || tempBarriers.Contains(neighbor) || tempProtectedPath.Contains(neighbor))
                {
                    neighborProtected = true;
                    break;
                }
            }

            if (neighborProtected) continue;

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
            Debug.LogWarning("[MissionGen] No se pudo encontrar un callejón para la meta cuyos accesos no bloqueasen la ruta protegida o la llave.");
            return false;
        }

        // Combinamos las barreras de la llave y de la meta para la validación lógica BFS
        HashSet<Vector2Int> allBarriers = new HashSet<Vector2Int>(tempBarriers);
        foreach (var b in tempMetaBarriers)
        {
            allBarriers.Add(b);
        }

        // --- VALIDACIÓN DE LOS 3 ESTADOS LÓGICOS DE MISIÓN ---
        bool canReachCaveA = LevelValidator.CanPathfind(startCell, cuevaA, mazeData, false, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
        bool canReachAxeFromB = LevelValidator.CanPathfind(cuevaB, axeCell, mazeData, false, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
        bool canReachKeyWithoutAxe = LevelValidator.CanPathfind(startCell, keyCell, mazeData, false, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);
        bool canReachMetaWithoutAxe = LevelValidator.CanPathfind(startCell, metaCell, mazeData, false, true, cuevaA, cuevaB, cuevaB, cuevaA, allBarriers, mazeWidth, mazeHeight);

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

        Vector3 posA = mazeRenderer.GetWorldPosition(cuevaA);
        spawnedCuevaAInstance = Instantiate(cavePrefab, posA, Quaternion.identity, itemsContainer);
        spawnedCuevaAInstance.name = "Cave_A_Entrance";

        Vector3 posB = mazeRenderer.GetWorldPosition(cuevaB);
        spawnedCuevaBInstance = Instantiate(cavePrefab, posB, Quaternion.identity, itemsContainer);
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
        spawnedKeyInstance = Instantiate(keyPrefab, posKey, Quaternion.identity, itemsContainer);
        spawnedKeyInstance.name = "Mission_Key";

        Vector3 posMeta = mazeRenderer.GetWorldPosition(metaCell);
        spawnedDoorInstance = Instantiate(doorPrefab, posMeta, Quaternion.identity, itemsContainer);
        spawnedDoorInstance.name = "Maze_Goal_Door";

        LevelObjectSpawner spawnerToUse = levelObjectSpawner != null ? levelObjectSpawner : GetComponentInChildren<LevelObjectSpawner>();

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

                GameObject spawnedBar = Instantiate(missionDestructiblePrefab, worldPos, Quaternion.identity, itemsContainer);
                spawnedBar.name = $"Mission_Barrier_{bar.x}_{bar.y}_{offset.x}_{offset.y}";
                spawnedMissionDestructibles.Add(spawnedBar);

                DestructibleObject comp = spawnedBar.GetComponent<DestructibleObject>();
                if (comp == null) comp = spawnedBar.AddComponent<DestructibleObject>();
                comp.SetReservedCells(reservedCells);

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

    private List<Vector2Int> GetWalkableNeighborsList(Vector2Int cell, MazeData mazeData)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int neighbor = cell + dir;
            if (mazeData.IsCellWalkable(neighbor.x, neighbor.y))
            {
                list.Add(neighbor);
            }
        }
        return list;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || mazeRenderer == null) return;
        if (!Application.isPlaying) return;
        if (keyZoneCells == null || metaZoneCells == null || barrierCells == null) return;
        if (startToCaveAPath == null || caveBToAxePath == null || axeToBarrierPath == null || keyToMetaPath == null) return;

        // 1. Dibujar Zona de la Llave en Dorado/Amarillo
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.4f);
        foreach (var cell in keyZoneCells)
        {
            Vector3 worldPos = mazeRenderer.GetWorldPosition(cell);
            Vector2 size = mazeRenderer.LogicalCellTileSize;
            Gizmos.DrawWireCube(worldPos, new Vector3(size.x, size.y, 0.1f));
        }

        // 1b. Dibujar Zona de la Meta en Azul
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.4f);
        foreach (var cell in metaZoneCells)
        {
            Vector3 worldPos = mazeRenderer.GetWorldPosition(cell);
            Vector2 size = mazeRenderer.LogicalCellTileSize;
            Gizmos.DrawWireCube(worldPos, new Vector3(size.x, size.y, 0.1f));
        }

        // 2. Dibujar celdas de barreras destructibles en Naranja
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        foreach (var cell in barrierCells)
        {
            Vector3 worldPos = mazeRenderer.GetWorldPosition(cell);
            Vector2 size = mazeRenderer.LogicalCellTileSize;
            Gizmos.DrawCube(worldPos, new Vector3(size.x, size.y, 0.1f));
        }

        // 3. Dibujar rutas lógicas en colores
        DrawPathGizmo(startToCaveAPath, Color.green);
        DrawPathGizmo(caveBToAxePath, Color.cyan);
        DrawPathGizmo(axeToBarrierPath, Color.red);
        DrawPathGizmo(keyToMetaPath, Color.blue);
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
}
