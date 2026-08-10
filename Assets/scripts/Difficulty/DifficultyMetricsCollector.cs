using System.Collections.Generic;
using UnityEngine;

public class DifficultyMetricsCollector : MonoBehaviour
{
    public static DifficultyMetricsCollector Instance { get; private set; }

    [Header("Métricas Activas")]
    [SerializeField]
    private DifficultyMetrics metrics = new DifficultyMetrics();

    private CatMovement playerMovement;
    private CatInventory playerInventory;
    private AxeObstacleBreaker playerBreaker;
    private CaveTraveler playerTraveler;

    private MazeData mazeData;
    private HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();
    private Vector3 lastPosition;
    private float levelStartTime;
    private float idleTimer;
    private bool isMovingLastFrame;
    private bool isCollecting;
    private int currentSessionRestarts = 0;

    // --- Variables de corrección de instrumentación ---
    private Vector2Int lastCell = new Vector2Int(-1, -1);
    private bool justTeleported = false;
    private int cachedTotalWalkableCells = -1;
    private bool isCurrentlyIdle = false;

    public float CurrentLevelElapsedTime
    {
        get
        {
            if (isCollecting)
            {
                return Time.time - levelStartTime;
            }
            return metrics != null ? metrics.totalLevelTime : 0f;
        }
    }

    public DifficultyMetrics CurrentMetrics => metrics;
    public bool IsCollecting => isCollecting;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartCollecting()
    {
        metrics = new DifficultyMetrics();
        MazeAgent agent = FindAnyObjectByType<MazeAgent>();
        if (agent != null)
        {
            metrics.maxTimeLimitInSeconds = agent.MaxStep * Time.fixedDeltaTime;
            metrics.maxEpisodeSteps = agent.MaxStep;
            metrics.agentVersion = "MazeAgent_v2"; // Se asume este nombre por defecto para IA
        }
        else
        {
            metrics.maxEpisodeSteps = 0;
            metrics.agentVersion = "Human";
        }
        
        metrics.episodeId = System.Guid.NewGuid().ToString();

        metrics.restartCount = currentSessionRestarts;
        visitedCells.Clear();
        levelStartTime = Time.time;
        idleTimer = 0f;
        isMovingLastFrame = false;
        
        lastCell = new Vector2Int(-1, -1);
        justTeleported = false;
        cachedTotalWalkableCells = -1;
        isCurrentlyIdle = false;
        
        playerMovement = FindAnyObjectByType<CatMovement>();
        if (playerMovement != null)
        {
            lastPosition = playerMovement.transform.position;
            SubscribeToPlayerEvents(playerMovement.gameObject);
        }
        
        mazeData = FindAnyObjectByType<MazeData>();
        isCollecting = true;
        
        // Cancelar cualquier tracking activo de llave→meta
        if (KeyToGoalTracker.Instance != null)
        {
            KeyToGoalTracker.Instance.CancelTracking();
        }

        Debug.Log("[MetricsCollector] Recopilación de métricas iniciada para el nivel.");
    }

    public void StopCollecting()
    {
        isCollecting = false;
        UnsubscribeFromPlayerEvents();
    }

    private void Update()
    {
        if (!isCollecting) return;

        // Si el jugador fue regenerado y perdimos la referencia, buscarlo de nuevo
        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<CatMovement>();
            if (playerMovement != null)
            {
                lastPosition = playerMovement.transform.position;
                SubscribeToPlayerEvents(playerMovement.gameObject);
            }
            return;
        }

        if (mazeData == null)
        {
            mazeData = FindAnyObjectByType<MazeData>();
            return;
        }

        // Registrar distancia recorrida y idle
        Rigidbody2D rb = playerMovement.GetComponent<Rigidbody2D>();
        Vector3 currentPos = rb != null ? (Vector3)rb.position : playerMovement.transform.position;
        float dist = Vector3.Distance(lastPosition, currentPos);
        bool hasMovedPhysical = dist > 0.01f;
        
        // Guardar estado del flag localmente para el frame y reiniciarlo
        bool isTeleportFrame = justTeleported;
        if (isTeleportFrame)
        {
            justTeleported = false;
        }

        if (hasMovedPhysical)
        {
            if (!isTeleportFrame)
            {
                metrics.distanceTraveled += dist;
            }
            else
            {
                // [Metrics] Teleport ignored for distance calculation
            }
            lastPosition = currentPos;
            
            idleTimer = 0f;
            isCurrentlyIdle = false;
        }
        else
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= 1.0f && !isCurrentlyIdle)
            {
                metrics.idleCount++;
                isCurrentlyIdle = true;
                // Debug.Log($"[Metrics] Idle detected, idleCount={metrics.idleCount}");
            }
        }

        // Registrar exploración del mapa y movementCount
        Vector3 origin = mazeData.MapOrigin;
        Vector2Int cellSize = mazeData.CellSize;
        if (cellSize.x > 0 && cellSize.y > 0)
        {
            int cellX = Mathf.FloorToInt((currentPos.x - origin.x) / cellSize.x);
            int cellY = Mathf.FloorToInt((currentPos.y - origin.y) / cellSize.y);
            if (cellX >= 0 && cellX < mazeData.Width && cellY >= 0 && cellY < mazeData.Height)
            {
                Vector2Int currentCell = new Vector2Int(cellX, cellY);
                
                // Movement Count
                if (lastCell == new Vector2Int(-1, -1))
                {
                    lastCell = currentCell;
                }
                else if (currentCell != lastCell)
                {
                    if (!isTeleportFrame)
                    {
                        metrics.movementCount++;
                        // Debug.Log($"[Metrics] Cell changed: {lastCell} -> {currentCell}, movementCount={metrics.movementCount}");
                    }
                    lastCell = currentCell;
                }

                // Exploration Percentage
                if (mazeData.IsWalkable(cellX, cellY))
                {
                    if (visitedCells.Add(currentCell))
                    {
                        if (cachedTotalWalkableCells == -1)
                        {
                            cachedTotalWalkableCells = GetTotalWalkableCellsCount(mazeData);
                        }
                        metrics.explorationPercentage = cachedTotalWalkableCells > 0 ? (float)visitedCells.Count / cachedTotalWalkableCells : 0f;
                    }
                }
            }
        }

        // Delegar actualización de posición al tracker de llave→meta
        if (KeyToGoalTracker.Instance != null && KeyToGoalTracker.Instance.IsTracking)
        {
            KeyToGoalTracker.Instance.UpdatePlayerPosition(currentPos);
        }
    }

    private int GetTotalWalkableCellsCount(MazeData data)
    {
        int count = 0;
        for (int x = 0; x < data.Width; x++)
        {
            for (int y = 0; y < data.Height; y++)
            {
                if (data.IsWalkable(x, y)) count++;
            }
        }
        return count;
    }

    private void SubscribeToPlayerEvents(GameObject player)
    {
        UnsubscribeFromPlayerEvents();

        playerInventory = player.GetComponent<CatInventory>();
        if (playerInventory != null)
        {
            playerInventory.OnAxeCollected += HandleAxeCollected;
            playerInventory.OnKeyCollected += HandleKeyCollected;
        }

        playerBreaker = player.GetComponent<AxeObstacleBreaker>();
        if (playerBreaker != null)
        {
            playerBreaker.OnObstacleHit += HandleObstacleHit;
            playerBreaker.OnFailedHitNoAxe += HandleFailedHitNoAxe;
        }

        playerTraveler = player.GetComponent<CaveTraveler>();
        if (playerTraveler != null)
        {
            playerTraveler.OnTeleport += HandleTeleport;
            playerTraveler.OnTeleportWithPairId += HandleTeleportWithPairId;
        }
    }

    private void UnsubscribeFromPlayerEvents()
    {
        if (playerInventory != null)
        {
            playerInventory.OnAxeCollected -= HandleAxeCollected;
            playerInventory.OnKeyCollected -= HandleKeyCollected;
        }
        if (playerBreaker != null)
        {
            playerBreaker.OnObstacleHit -= HandleObstacleHit;
            playerBreaker.OnFailedHitNoAxe -= HandleFailedHitNoAxe;
        }
        if (playerTraveler != null)
        {
            playerTraveler.OnTeleport -= HandleTeleport;
            playerTraveler.OnTeleportWithPairId -= HandleTeleportWithPairId;
        }
    }

    private void HandleAxeCollected()
    {
        metrics.axeCollected = true;
        metrics.timeToFindAxe = Time.time - levelStartTime;
        metrics.objectivesCollected++;
        Debug.Log($"[Metrics] Hacha recolectada en {metrics.timeToFindAxe:F2} segundos.");
    }

    private void HandleKeyCollected()
    {
        metrics.keyCollected = true;
        metrics.timeToFindKey = Time.time - levelStartTime;
        metrics.objectivesCollected++;
        Debug.Log($"[Metrics] Llave recolectada en {metrics.timeToFindKey:F2} segundos.");
        // El inicio del tracking de llave→meta es responsabilidad de DynamicLevelManager
        // porque necesita las rutas óptimas calculadas con el estado actual del mapa.
    }

    private void HandleObstacleHit()
    {
        metrics.destructibleHits++;
        Debug.Log($"[Metrics] Destructible golpeado exitosamente. Total: {metrics.destructibleHits}");
    }

    private void HandleFailedHitNoAxe()
    {
        metrics.failedHitsWithoutAxe++;
        Debug.Log($"[Metrics] Intento de golpe fallido (sin hacha). Total: {metrics.failedHitsWithoutAxe}");
    }

    private void HandleTeleport()
    {
        metrics.cavesUsed++;
        justTeleported = true;
        Debug.Log($"[Metrics] Cueva utilizada. Total: {metrics.cavesUsed}");
    }

    private void HandleTeleportWithPairId(int pairIndex)
    {
        // El tracking detallado por PairIndex se hace en KeyToGoalTracker
        if (KeyToGoalTracker.Instance != null && KeyToGoalTracker.Instance.IsTracking)
        {
            KeyToGoalTracker.Instance.RegisterCaveUse(pairIndex);
        }
    }

    public void SetTerminationReason(string reason)
    {
        if (isCollecting)
        {
            metrics.terminationReason = reason;
        }
    }

    public void OnLevelEnded(bool success)
    {
        if (!isCollecting) return;

        StopCollecting();
        
        MazeAgent agent = FindAnyObjectByType<MazeAgent>();
        if (agent != null)
        {
            metrics.episodeStepCount = agent.CurrentEpisodeStepCount;
        }
        
        metrics.levelCompleted = success;
        metrics.totalLevelTime = Time.time - levelStartTime;
        metrics.timeToReachHouse = success ? metrics.totalLevelTime : 0f;

        // Finalizar el tracking de llave→meta y copiar las métricas
        if (KeyToGoalTracker.Instance != null && KeyToGoalTracker.Instance.IsTracking)
        {
            KeyToGoalTracker.Instance.StopTracking();
        }

        if (KeyToGoalTracker.Instance != null && KeyToGoalTracker.Instance.HasCompletedMetrics)
        {
            metrics.keyToGoal = KeyToGoalTracker.Instance.CompletedMetrics;
        }
        
        Debug.Log($"[MetricsCollector] Nivel {(success ? "completado" : "fallido/timeout")}. Tiempo total: {metrics.totalLevelTime:F2}s. Distancia: {metrics.distanceTraveled:F1}m.");
        
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.RegisterLevelEnd(metrics);
        }
        currentSessionRestarts = 0;
    }

    public void RecordRestart()
    {
        currentSessionRestarts++;
        metrics.restartCount = currentSessionRestarts;
    }

    public void RecordError()
    {
        metrics.errorCount++;
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayerEvents();
    }
}
