using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewMazeDestructibleSet",
    menuName = "Maze/Destructibles/Destructible Set"
)]
public class MazeDestructibleSet : ScriptableObject
{
    [Header("Destructible Patterns List")]
    [SerializeField]
    private List<MazeDestructiblePattern> patterns =
        new List<MazeDestructiblePattern>();

    [Header("Spawn Configuration")]
    [SerializeField, Range(0f, 1f)]
    private float globalSpawnProbability = 0.8f;

    [SerializeField, Min(0)]
    private int minimumTotalAmount = 3;

    [SerializeField, Min(0)]
    private int maximumTotalAmount = 8;

    [SerializeField, Min(0)]
    private int minimumSpacing = 1;

    [SerializeField, Min(1)]
    private int placementAttemptsPerObject = 50;

    [Header("Validation Flags")]
    [SerializeField]
    private bool validateConnectivity = true;

    [SerializeField]
    private bool avoidBlockingMainSolution = false;

    [SerializeField]
    private bool useWeightedSelection = true;

    [Header("Random Seed Settings")]
    [SerializeField]
    private bool useSeed = false;

    [SerializeField]
    private int seed = 12345;

    public List<MazeDestructiblePattern> Patterns => patterns;
    public float GlobalSpawnProbability => globalSpawnProbability;
    public int MinimumTotalAmount => minimumTotalAmount;
    public int MaximumTotalAmount => maximumTotalAmount;
    public int MinimumSpacing => minimumSpacing;
    public int PlacementAttemptsPerObject => placementAttemptsPerObject;
    public bool ValidateConnectivity => validateConnectivity;
    public bool AvoidBlockingMainSolution => avoidBlockingMainSolution;
    public bool UseWeightedSelection => useWeightedSelection;
    public bool UseSeed => useSeed;
    public int Seed => seed;

    private void OnValidate()
    {
        minimumTotalAmount = Mathf.Max(0, minimumTotalAmount);
        maximumTotalAmount = Mathf.Max(minimumTotalAmount, maximumTotalAmount);
        placementAttemptsPerObject = Mathf.Max(1, placementAttemptsPerObject);
        minimumSpacing = Mathf.Max(0, minimumSpacing);
    }
}
