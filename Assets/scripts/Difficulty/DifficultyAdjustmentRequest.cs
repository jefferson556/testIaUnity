using UnityEngine;

public enum DifficultyAdjustmentMode
{
    Absolute,
    Relative
}

[System.Serializable]
public class DifficultyAdjustmentRequest
{
    public DifficultyAdjustmentMode mode = DifficultyAdjustmentMode.Absolute;
    
    [Header("Parámetros (usar 0 en Relativo si no cambia, o el valor deseado en Absoluto)")]
    public int mapWidth;
    public int mapHeight;
    public int extraConnections;
    public float minPlayerToCaveADistance;
    public float minAxeToStartAndMetaDistance;
    public float destructibleWallsPercentage;
    public int missionDestructiblesHealth;
    public float playerMoveSpeed;
    public float hintDelaySeconds;

    [Header("Ajuste por Score General (opcional)")]
    public bool adjustByScore;
    public float targetScore;

    [Header("Metadatos de la Solicitud")]
    public float confidence = 1f;
    public string reason = "Manual adjustment";
    public string requesterId = "Simulator";
}
