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

            // 16. Validar y colocar el hacha
            Vector2Int axeCell = mazeData.SelectFurthestCell(startCell, currentSeed, minimumAxeDistanceFromPlayer);
            if (axeCell == startCell || !mazeData.IsCellWalkableAndMain(axeCell.x, axeCell.y))
            {
                RejectAttempt(attempt, currentSeed, "los destructibles desconectaron o hicieron inaccesible el hacha");
                continue;
            }

            // Generar y pintar AccessibleZone si está habilitado
            if (generateAccessibleZone)
            {
                List<Vector2Int> zoneCells = GenerateAccessibleZoneForAxe(axeCell, accessibleZoneSize, mazeData, maze);
                mazeData.CalculateMainRegion(startCell); // Re-calcular región por si se expandió a paredes lógicas
                mazeRenderer.PaintAccessibleZone(zoneCells);
            }

            Vector3 axeWorldPos = mazeRenderer.GetWorldPosition(axeCell);
            SetupAxe(axeWorldPos);

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

        // Asegurar que el tamaño sea de al menos 1x1
        int targetWidth = Mathf.Max(1, size.x);
        int targetHeight = Mathf.Max(1, size.y);

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
}
