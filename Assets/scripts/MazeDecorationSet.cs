using UnityEngine;

[CreateAssetMenu(
    fileName = "MazeDecorationSet",
    menuName = "Maze/Decoration Set"
)]
public class MazeDecorationSet : ScriptableObject
{
    [Header("Path Back Decorations")]
    [SerializeField]
    private MazeDecorationPattern[]
        pathBackPatterns;

    [SerializeField, Range(0f, 1f)]
    private float pathBackProbability = 0.08f;

    [Header("Interior Wall Back Decorations")]
    [SerializeField]
    private MazeDecorationPattern[]
        wallBackPatterns;

    [SerializeField, Range(0f, 1f)]
    private float wallBackProbability = 0.20f;

    [Header("Interior Wall Front Decorations")]
    [SerializeField]
    private MazeDecorationPattern[]
        wallFrontPatterns;

    [SerializeField, Range(0f, 1f)]
    private float wallFrontProbability = 0.12f;

    [Header("Map Border Back Decorations")]
    [SerializeField]
    private MazeDecorationPattern[]
        borderBackPatterns;

    [SerializeField, Range(0f, 1f)]
    private float borderBackProbability = 0.65f;

    [Header("Map Border Front Decorations")]
    [SerializeField]
    private MazeDecorationPattern[]
        borderFrontPatterns;

    [SerializeField, Range(0f, 1f)]
    private float borderFrontProbability = 0.45f;

    public MazeDecorationPattern[]
        PathBackPatterns =>
            pathBackPatterns;

    public MazeDecorationPattern[]
        WallBackPatterns =>
            wallBackPatterns;

    public MazeDecorationPattern[]
        WallFrontPatterns =>
            wallFrontPatterns;

    public MazeDecorationPattern[]
        BorderBackPatterns =>
            borderBackPatterns;

    public MazeDecorationPattern[]
        BorderFrontPatterns =>
            borderFrontPatterns;

    public float PathBackProbability =>
        pathBackProbability;

    public float WallBackProbability =>
        wallBackProbability;

    public float WallFrontProbability =>
        wallFrontProbability;

    public float BorderBackProbability =>
        borderBackProbability;

    public float BorderFrontProbability =>
        borderFrontProbability;
}
