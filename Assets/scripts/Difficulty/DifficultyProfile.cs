using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyProfile", menuName = "Game/Difficulty Profile")]
public class DifficultyProfile : ScriptableObject
{
    [SerializeField]
    private DifficultySettings settings;

    public DifficultySettings Settings => settings;
}
