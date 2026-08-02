[System.Serializable]
public class DifficultyHistoryEntry
{
    public int levelNumber;
    public string timestamp;
    public string previousSettingsJson;
    public string requestedSettingsJson;
    public string appliedSettingsJson;
    public string reason;
    public string origin;
    public string metricsJson;
    public bool valuesWereClamped;
}
