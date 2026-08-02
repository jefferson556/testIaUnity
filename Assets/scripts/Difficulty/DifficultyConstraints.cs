using UnityEngine;

[System.Serializable]
public class IntDifficultyConstraint
{
    public int minimum;
    public int maximum;
    public int maximumChangePerLevel;
    public int defaultValue;

    public int Clamp(int previous, int requested)
    {
        int clamped = Mathf.Clamp(requested, minimum, maximum);
        int change = clamped - previous;
        if (Mathf.Abs(change) > maximumChangePerLevel)
        {
            clamped = previous + (int)Mathf.Sign(change) * maximumChangePerLevel;
        }
        return clamped;
    }
}

[System.Serializable]
public class FloatDifficultyConstraint
{
    public float minimum;
    public float maximum;
    public float maximumChangePerLevel;
    public float defaultValue;

    public float Clamp(float previous, float requested)
    {
        float clamped = Mathf.Clamp(requested, minimum, maximum);
        float change = clamped - previous;
        if (Mathf.Abs(change) > maximumChangePerLevel)
        {
            clamped = previous + Mathf.Sign(change) * maximumChangePerLevel;
        }
        return clamped;
    }
}

[System.Serializable]
public class DifficultyConstraints
{
    [Header("Dimensiones y Complejidad")]
    public IntDifficultyConstraint mapWidth = new IntDifficultyConstraint { minimum = 9, maximum = 35, maximumChangePerLevel = 2, defaultValue = 15 };
    public IntDifficultyConstraint mapHeight = new IntDifficultyConstraint { minimum = 9, maximum = 35, maximumChangePerLevel = 2, defaultValue = 15 };
    public IntDifficultyConstraint extraConnections = new IntDifficultyConstraint { minimum = 0, maximum = 20, maximumChangePerLevel = 2, defaultValue = 2 };

    [Header("Distancias y Posiciones")]
    public FloatDifficultyConstraint minPlayerToCaveADistance = new FloatDifficultyConstraint { minimum = 1f, maximum = 15f, maximumChangePerLevel = 2f, defaultValue = 2f };
    public FloatDifficultyConstraint minAxeToStartAndMetaDistance = new FloatDifficultyConstraint { minimum = 3f, maximum = 20f, maximumChangePerLevel = 2f, defaultValue = 8f };
    public FloatDifficultyConstraint minKeyToAxeDistance = new FloatDifficultyConstraint { minimum = 1f, maximum = 15f, maximumChangePerLevel = 2f, defaultValue = 4f };
    public FloatDifficultyConstraint minKeyToMetaDistance = new FloatDifficultyConstraint { minimum = 1f, maximum = 15f, maximumChangePerLevel = 2f, defaultValue = 4f };
    public FloatDifficultyConstraint minPlayerToMetaDistance = new FloatDifficultyConstraint { minimum = 3f, maximum = 20f, maximumChangePerLevel = 2f, defaultValue = 8f };

    [Header("Cuevas")]
    public IntDifficultyConstraint minimumPathDistanceBetweenTravelCaves = new IntDifficultyConstraint { minimum = 5, maximum = 20, maximumChangePerLevel = 2, defaultValue = 10 };
    public IntDifficultyConstraint minimumShortcutSaving = new IntDifficultyConstraint { minimum = 3, maximum = 15, maximumChangePerLevel = 2, defaultValue = 8 };
    public IntDifficultyConstraint travelCavePairs = new IntDifficultyConstraint { minimum = 0, maximum = 3, maximumChangePerLevel = 1, defaultValue = 1 };
    public IntDifficultyConstraint axeZoneSizeX = new IntDifficultyConstraint { minimum = 2, maximum = 5, maximumChangePerLevel = 1, defaultValue = 3 };
    public IntDifficultyConstraint axeZoneSizeY = new IntDifficultyConstraint { minimum = 2, maximum = 5, maximumChangePerLevel = 1, defaultValue = 3 };

    [Header("Destructibles")]
    public FloatDifficultyConstraint destructibleWallsPercentage = new FloatDifficultyConstraint { minimum = 0f, maximum = 0.5f, maximumChangePerLevel = 0.1f, defaultValue = 0.10f };
    public IntDifficultyConstraint missionDestructiblesHealth = new IntDifficultyConstraint { minimum = 1, maximum = 10, maximumChangePerLevel = 1, defaultValue = 1 };

    [Header("Jugador y Ayudas")]
    public FloatDifficultyConstraint playerMoveSpeed = new FloatDifficultyConstraint { minimum = 2f, maximum = 8f, maximumChangePerLevel = 1f, defaultValue = 4f };
    public IntDifficultyConstraint hintsAvailable = new IntDifficultyConstraint { minimum = 0, maximum = 10, maximumChangePerLevel = 1, defaultValue = 3 };
    public FloatDifficultyConstraint hintDelaySeconds = new FloatDifficultyConstraint { minimum = 5f, maximum = 60f, maximumChangePerLevel = 5f, defaultValue = 15f };
    public FloatDifficultyConstraint hintIntensity = new FloatDifficultyConstraint { minimum = 0.1f, maximum = 1.0f, maximumChangePerLevel = 0.2f, defaultValue = 1.0f };

    [Header("Zoom de Cámara")]
    public FloatDifficultyConstraint zoomOutMaxDuration = new FloatDifficultyConstraint { minimum = 0.5f, maximum = 20f, maximumChangePerLevel = 2f, defaultValue = 4f };
    public FloatDifficultyConstraint zoomOutCooldown = new FloatDifficultyConstraint { minimum = 0.5f, maximum = 30f, maximumChangePerLevel = 3f, defaultValue = 3f };
    public FloatDifficultyConstraint zoomOutSize = new FloatDifficultyConstraint { minimum = 5f, maximum = 15f, maximumChangePerLevel = 2f, defaultValue = 9f };
    public FloatDifficultyConstraint normalZoomSize = new FloatDifficultyConstraint { minimum = 2f, maximum = 8f, maximumChangePerLevel = 1f, defaultValue = 4f };
}
