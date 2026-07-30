using UnityEngine;

[CreateAssetMenu(
    fileName = "NewMazeDestructiblePattern",
    menuName = "Maze/Destructibles/Destructible Pattern"
)]
public class MazeDestructiblePattern : ScriptableObject
{
    [Header("Pattern Identification")]
    [SerializeField]
    private string id = "destructible_pattern";

    [SerializeField]
    private string displayName = "Destructible Pattern";

    [Header("Visual & Prefab Reference")]
    [SerializeField]
    private GameObject prefab;

    [Header("Pattern Dimensions")]
    [SerializeField, Min(1)]
    private int width = 1;

    [SerializeField, Min(1)]
    private int height = 1;

    [SerializeField]
    private Vector2Int pivotCell = Vector2Int.zero;

    [SerializeField]
    private Vector2 positionOffset = Vector2.zero;

    [Header("Selection & Spawn Limits")]
    [SerializeField, Min(0f)]
    private float selectionWeight = 1f;

    [SerializeField, Min(0)]
    private int minimumAmount = 0;

    [SerializeField, Min(0)]
    private int maximumAmount = 10;

    [Header("Difficulty & Placement Constraints")]
    [SerializeField, Min(0)]
    private int minimumDifficulty = 0;

    [SerializeField, Min(0)]
    private int minimumDistanceFromPlayer = 2;

    [SerializeField, Min(0)]
    private int minimumDistanceFromCave = 2;

    [Header("Behavior Flags")]
    [SerializeField]
    private bool blocksMovement = true;

    [SerializeField]
    private bool requiresAxe = true;

    [SerializeField]
    private bool avoidMainPath = false;

    [SerializeField]
    private bool allowedInDeadEnds = true;

    [SerializeField]
    private bool reserveAllPatternCells = true;

    public string Id => id;
    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    public int Width => width;
    public int Height => height;
    public Vector2Int PivotCell => pivotCell;
    public Vector2 PositionOffset => positionOffset;
    public float SelectionWeight => selectionWeight;
    public int MinimumAmount => minimumAmount;
    public int MaximumAmount => maximumAmount;
    public int MinimumDifficulty => minimumDifficulty;
    public int MinimumDistanceFromPlayer => minimumDistanceFromPlayer;
    public int MinimumDistanceFromCave => minimumDistanceFromCave;
    public bool BlocksMovement => blocksMovement;
    public bool RequiresAxe => requiresAxe;
    public bool AvoidMainPath => avoidMainPath;
    public bool AllowedInDeadEnds => allowedInDeadEnds;
    public bool ReserveAllPatternCells => reserveAllPatternCells;

    public bool IsConfigured =>
        prefab != null && width > 0 && height > 0;

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        selectionWeight = Mathf.Max(0f, selectionWeight);
        minimumAmount = Mathf.Max(0, minimumAmount);
        maximumAmount = Mathf.Max(minimumAmount, maximumAmount);
    }
}
