using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyProfile", menuName = "Game/Difficulty Profile")]
public class DifficultyProfile : ScriptableObject
{
    [SerializeField]
    private string profileName;

    [SerializeField]
    private DifficultySettings settings;

    public string ProfileName => !string.IsNullOrWhiteSpace(profileName) ? profileName : name;
    public DifficultySettings Settings => settings;
}
