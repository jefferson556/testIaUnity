[System.Serializable]
public class DifficultyMetrics
{
    public float totalLevelTime;
    public float timeToFindAxe;
    public float timeToFindKey;
    public float timeToReachHouse;
    
    public int movementCount;
    public int idleCount;
    public int destructibleHits;
    public int failedHitsWithoutAxe;
    public int cavesUsed;
    public int backtrackingCount;
    public float distanceTraveled;
    public float explorationPercentage;
    public int hintsUsed;
    public int restartCount;
    public int errorCount;
    public int objectivesCollected;

    // Nuevas metricas
    public bool levelCompleted;
    public float maxTimeLimitInSeconds;
    
    // Dataset Metrics
    public bool axeCollected;
    public bool keyCollected;
    public string terminationReason;
    public int maxEpisodeSteps;
    public int episodeStepCount;
    public string episodeId;
    public string agentVersion;

    /// <summary>
    /// Métricas detalladas del segmento llave → meta.
    /// Calculadas por KeyToGoalTracker al finalizar dicho segmento.
    /// </summary>
    public KeyToGoalMetrics keyToGoal = new KeyToGoalMetrics();
}

