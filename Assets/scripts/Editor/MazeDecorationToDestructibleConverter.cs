#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MazeDecorationToDestructibleConverter : EditorWindow
{
    private MazeDecorationPattern sourcePattern;
    private string outputPrefabFolder = "Assets/prefabs/Destructibles";
    private string outputPatternFolder = "Assets/Data/Maze/Destructibles";
    private float defaultSelectionWeight = 1.0f;

    [MenuItem("Maze/Tools/Convert Decoration to Destructible")]
    public static void ShowWindow()
    {
        GetWindow<MazeDecorationToDestructibleConverter>("Convert Decoration");
    }

    private void OnGUI()
    {
        GUILayout.Label("Convert MazeDecorationPattern to Destructible", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        sourcePattern = (MazeDecorationPattern)EditorGUILayout.ObjectField(
            "Source Decoration Pattern",
            sourcePattern,
            typeof(MazeDecorationPattern),
            false
        );

        outputPrefabFolder = EditorGUILayout.TextField("Prefab Output Folder", outputPrefabFolder);
        outputPatternFolder = EditorGUILayout.TextField("Pattern Asset Output Folder", outputPatternFolder);
        defaultSelectionWeight = EditorGUILayout.FloatField("Default Selection Weight", defaultSelectionWeight);

        EditorGUILayout.Space();

        if (GUILayout.Button("Convert Pattern", GUILayout.Height(30)))
        {
            ConvertSelectedPattern();
        }
    }

    private void ConvertSelectedPattern()
    {
        if (sourcePattern == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a valid MazeDecorationPattern.", "OK");
            return;
        }

        if (!sourcePattern.IsConfigured)
        {
            EditorUtility.DisplayDialog("Error", "The selected pattern is not properly configured.", "OK");
            return;
        }

        EnsureFolderExists(outputPrefabFolder);
        EnsureFolderExists(outputPatternFolder);

        string baseName = sourcePattern.name.Replace(" ", "");
        string prefabPath = Path.Combine(outputPrefabFolder, $"{baseName}Destructible.prefab").Replace("\\", "/");
        string patternAssetPath = Path.Combine(outputPatternFolder, $"{baseName}DestructiblePattern.asset").Replace("\\", "/");

        GameObject rootObject = new GameObject($"{baseName}Destructible");

        int width = sourcePattern.Width;
        int height = sourcePattern.Height;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileBase tileBase = sourcePattern.GetTile(x, y);
                if (tileBase == null)
                {
                    continue;
                }

                Sprite tileSprite = ExtractSpriteFromTile(tileBase);
                if (tileSprite == null)
                {
                    continue;
                }

                GameObject childTile = new GameObject($"Tile_{x}_{y}");
                childTile.transform.SetParent(rootObject.transform);
                childTile.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0f);

                SpriteRenderer sr = childTile.AddComponent<SpriteRenderer>();
                sr.sprite = tileSprite;
                sr.sortingLayerName = "BreakableObjects";
                sr.sortingOrder = 100;
            }
        }

        DestructibleObject destructibleComp = rootObject.AddComponent<DestructibleObject>();
        BoxCollider2D collider = rootObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, height);
        collider.offset = new Vector2(width * 0.5f, height * 0.5f);

        GameObject dropPointChild = new GameObject("DropPoint");
        dropPointChild.transform.SetParent(rootObject.transform);
        dropPointChild.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
        DestroyImmediate(rootObject);

        MazeDestructiblePattern destructiblePattern = ScriptableObject.CreateInstance<MazeDestructiblePattern>();

        SerializedObject serializedPattern = new SerializedObject(destructiblePattern);
        serializedPattern.FindProperty("id").stringValue = $"{baseName.ToLower()}_destructible";
        serializedPattern.FindProperty("displayName").stringValue = $"{baseName} Destructible";
        serializedPattern.FindProperty("prefab").objectReferenceValue = savedPrefab;
        
        // Las celdas lógicas del laberinto para ocupación en matriz (1 celda lógica)
        serializedPattern.FindProperty("width").intValue = 1;
        serializedPattern.FindProperty("height").intValue = 1;
        serializedPattern.FindProperty("selectionWeight").floatValue = defaultSelectionWeight;
        serializedPattern.FindProperty("minimumDistanceFromPlayer").intValue = 2;
        serializedPattern.FindProperty("minimumDistanceFromCave").intValue = 2;
        serializedPattern.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(destructiblePattern, patternAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Success",
            $"Converted pattern successfully!\n\nPrefab: {prefabPath}\nPattern Asset: {patternAssetPath}",
            "OK"
        );
    }

    private Sprite ExtractSpriteFromTile(TileBase tileBase)
    {
        if (tileBase is Tile standardTile)
        {
            return standardTile.sprite;
        }

        return null;
    }

    private void EnsureFolderExists(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
    }
}
#endif
