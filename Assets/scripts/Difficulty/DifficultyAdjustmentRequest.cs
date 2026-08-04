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
    
    [Header("Dimensiones y Complejidad")]
    public bool overrideMapWidth;
    public int mapWidth;

    public bool overrideMapHeight;
    public int mapHeight;

    public bool overrideExtraConnections;
    public int extraConnections;

    [Header("Distancias y Posiciones")]
    public bool overrideMinPlayerToCaveADistance;
    public float minPlayerToCaveADistance;

    public bool overrideMinAxeToStartAndMetaDistance;
    public float minAxeToStartAndMetaDistance;

    public bool overrideMinKeyToAxeDistance;
    public float minKeyToAxeDistance;

    public bool overrideMinKeyToMetaDistance;
    public float minKeyToMetaDistance;

    public bool overrideMinPlayerToMetaDistance;
    public float minPlayerToMetaDistance;

    [Header("Cuevas de Viaje Rápido")]
    public bool overrideEnableTravelCaves;
    public bool enableTravelCaves;

    public bool overrideMaximumTravelCavePairs;
    public int maximumTravelCavePairs;

    public bool overrideAxeZoneSize;
    public Vector2Int axeZoneSize;

    [Header("Destructibles")]
    public bool overrideDestructibleWallsPercentage;
    public float destructibleWallsPercentage;

    public bool overrideMissionDestructiblesHealth;
    public int missionDestructiblesHealth;

    [Header("Jugador y Ayudas")]
    public bool overridePlayerMoveSpeed;
    public float playerMoveSpeed;

    public bool overrideHintsAvailable;
    public int hintsAvailable;

    public bool overrideHintDelaySeconds;
    public float hintDelaySeconds;

    [Header("Ajuste por Score General (opcional)")]
    public bool adjustByScore;
    public float targetScore;

    [Header("Control de Ejecución")]
    [Tooltip("Si es verdadero, reinicia/regenera el nivel inmediatamente (para debug). De lo contrario, se aplica para la siguiente generación/nivel.")]
    public bool applyImmediately = false;

    [Header("Metadatos de la Solicitud")]
    public float confidence = 1f;
    public string reason = "Manual adjustment";
    public string requesterId = "Simulator";
}
