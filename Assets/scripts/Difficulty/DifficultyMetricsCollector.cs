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

    public DifficultyMetrics CurrentMetrics => metrics;

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
        visitedCells.Clear();
        levelStartTime = Time.time;
        idleTimer = 0f;
        isMovingLastFrame = false;
        
        playerMovement = FindAnyObjectByType<CatMovement>();
        if (playerMovement != null)
        {
            lastPosition = playerMovement.transform.position;
            SubscribeToPlayerEvents(playerMovement.gameObject);
        }
        
        mazeData = FindAnyObjectByType<MazeData>();
        isCollecting = true;
        
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

        // Registrar distancia recorrida
        Vector3 currentPos = playerMovement.transform.position;
        float dist = Vector3.Distance(lastPosition, currentPos);
        if (dist > 0.01f)
        {
            metrics.distanceTraveled += dist;
            lastPosition = currentPos;
            metrics.movementCount++;
            isMovingLastFrame = true;
            idleTimer = 0f;
        }
        else
        {
            if (isMovingLastFrame)
            {
                idleTimer += Time.deltaTime;
                if (idleTimer >= 1.0f) // 1 segundo detenido
                {
                    metrics.idleCount++;
                    isMovingLastFrame = false;
                }
            }
        }

        // Registrar exploración del mapa
        Vector3 origin = mazeData.MapOrigin;
        Vector2Int cellSize = mazeData.CellSize;
        if (cellSize.x > 0 && cellSize.y > 0)
        {
            int cellX = Mathf.FloorToInt((currentPos.x - origin.x) / cellSize.x);
            int cellY = Mathf.FloorToInt((currentPos.y - origin.y) / cellSize.y);
            if (cellX >= 0 && cellX < mazeData.Width && cellY >= 0 && cellY < mazeData.Height)
            {
                if (mazeData.IsWalkable(cellX, cellY))
                {
                    Vector2Int cell = new Vector2Int(cellX, cellY);
                    if (visitedCells.Add(cell))
                    {
                        int totalWalkable = GetTotalWalkableCellsCount(mazeData);
                        metrics.explorationPercentage = totalWalkable > 0 ? (float)visitedCells.Count / totalWalkable : 0f;
                    }
                }
            }
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
        }
    }

    private void HandleAxeCollected()
    {
        metrics.timeToFindAxe = Time.time - levelStartTime;
        metrics.objectivesCollected++;
        Debug.Log($"[Metrics] Hacha recolectada en {metrics.timeToFindAxe:F2} segundos.");
    }

    private void HandleKeyCollected()
    {
        metrics.timeToFindKey = Time.time - levelStartTime;
        metrics.objectivesCollected++;
        Debug.Log($"[Metrics] Llave recolectada en {metrics.timeToFindKey:F2} segundos.");
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
        Debug.Log($"[Metrics] Cueva de viaje rápido utilizada. Total: {metrics.cavesUsed}");
    }

    public void OnLevelCompleted()
    {
        if (!isCollecting) return;

        StopCollecting();
        
        metrics.totalLevelTime = Time.time - levelStartTime;
        metrics.timeToReachHouse = metrics.totalLevelTime;
        
        Debug.Log($"[MetricsCollector] Nivel completado. Tiempo total: {metrics.totalLevelTime:F2}s. Distancia: {metrics.distanceTraveled:F1}m.");
        
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.RegisterLevelCompletion(metrics);
        }
    }

    public void RecordRestart()
    {
        metrics.restartCount++;
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
