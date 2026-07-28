using UnityEngine;

public class DynamicLevelManager : MonoBehaviour
{
    [Header("Generación")]
    [SerializeField]
    private MazeGenerator mazeGenerator;

    [SerializeField]
    private MazeTilemapRenderer mazeRenderer;

    private void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        if (mazeGenerator == null ||
            mazeRenderer == null)
        {
            Debug.LogError(
                "Faltan referencias del generador " +
                "o del renderizador.",
                this
            );

            return;
        }

        MazeCellType[,] maze =
            mazeGenerator.Generate();

        mazeRenderer.Render(
    maze,
    mazeGenerator.LastUsedSeed
);
    }
}