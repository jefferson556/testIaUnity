using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class FixIntroScene
{
    [MenuItem("Tools/Fix Intro Scene")]
    public static void ExecuteFix()
    {
        Debug.Log("--- INICIANDO REPARACIÓN DE SCENA INTRO Y SPRITES ---");

        // 1. Asegurar que la escena activa sea SampleScene
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "SampleScene")
        {
            string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            activeScene = EditorSceneManager.OpenScene(sampleScenePath);
            Debug.Log($"Escena abierta: {sampleScenePath}");
        }

        // 2. Corregir Global Light 2D (Capas de Ordenamiento / Target Sorting Layers)
        FixGlobalLight2D();

        // 3. Verificar Colisionador en CatPlayer
        FixPlayerCollider();

        // 4. Configurar Build Settings (SampleScene -> laberinto)
        FixBuildSettings();

        // 5. Crear / Configurar Portal de Salida al Laberinto en SampleScene
        FixMazePortalTrigger();

        // Guardar la escena y los assets
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("--- REPARACIÓN COMPLETADA EXITOSAMENTE ---");
    }

    private static void FixGlobalLight2D()
    {
        Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        int[] allSortingLayerIDs = SortingLayer.layers.Select(l => l.id).ToArray();

        if (lights.Length == 0)
        {
            // Si no hay luz 2D, crear una Global Light 2D
            GameObject lightGO = new GameObject("Global Light 2D");
            Light2D newLight = lightGO.AddComponent<Light2D>();
            newLight.lightType = Light2D.LightType.Global;
            newLight.intensity = 1.0f;
            newLight.color = Color.white;
            lights = new Light2D[] { newLight };
            Debug.Log("Se creó un objeto 'Global Light 2D'.");
        }

        foreach (Light2D light in lights)
        {
            SerializedObject so = new SerializedObject(light);
            SerializedProperty applyToSortingLayers = so.FindProperty("m_ApplyToSortingLayers");

            if (applyToSortingLayers != null && applyToSortingLayers.isArray)
            {
                applyToSortingLayers.arraySize = allSortingLayerIDs.Length;
                for (int i = 0; i < allSortingLayerIDs.Length; i++)
                {
                    applyToSortingLayers.GetArrayElementAtIndex(i).intValue = allSortingLayerIDs[i];
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(light.gameObject);
                Debug.Log($"Global Light 2D '{light.name}' actualizado para iluminar las {allSortingLayerIDs.Length} capas de ordenamiento.");
            }
        }
    }

    private static void FixPlayerCollider()
    {
        GameObject player = GameObject.Find("CatPlayer");
        if (player != null)
        {
            Collider2D col = player.GetComponent<Collider2D>();
            if (col == null)
            {
                CapsuleCollider2D capsule = player.AddComponent<CapsuleCollider2D>();
                capsule.size = new Vector2(0.8f, 0.8f);
                capsule.offset = new Vector2(0f, 0f);
                EditorUtility.SetDirty(player);
                Debug.Log("Se añadió CapsuleCollider2D a CatPlayer.");
            }
        }
    }

    private static void FixBuildSettings()
    {
        List<EditorBuildSettingsScene> buildScenes = EditorBuildSettings.scenes.ToList();

        string sampleScenePath = "Assets/Scenes/SampleScene.unity";
        string laberintoPath = "Assets/Scenes/laberinto.unity";

        bool hasSampleScene = buildScenes.Any(s => s.path == sampleScenePath);
        bool hasLaberinto = buildScenes.Any(s => s.path == laberintoPath);

        List<EditorBuildSettingsScene> newBuildScenes = new List<EditorBuildSettingsScene>();

        // Asegurar que SampleScene esté en index 0
        newBuildScenes.Add(new EditorBuildSettingsScene(sampleScenePath, true));

        // Asegurar que laberinto esté en index 1
        newBuildScenes.Add(new EditorBuildSettingsScene(laberintoPath, true));

        // Preservar otras escenas si existen
        foreach (var scene in buildScenes)
        {
            if (scene.path != sampleScenePath && scene.path != laberintoPath)
            {
                newBuildScenes.Add(scene);
            }
        }

        EditorBuildSettings.scenes = newBuildScenes.ToArray();
        Debug.Log($"Build Settings actualizados: [0] {sampleScenePath}, [1] {laberintoPath}");
    }

    private static void FixMazePortalTrigger()
    {
        GameObject portalGO = GameObject.Find("MazePortalTrigger");
        if (portalGO == null)
        {
            portalGO = new GameObject("MazePortalTrigger");
            // Posición al final del camino en la parte superior central de la intro (ej: x: -1.5, y: 19.5, z: 0)
            portalGO.transform.position = new Vector3(-1.5f, 19.5f, 0f);

            BoxCollider2D boxCol = portalGO.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(4f, 2f);

            NextLevelLoader levelLoader = portalGO.AddComponent<NextLevelLoader>();
            SerializedObject loaderSO = new SerializedObject(levelLoader);
            loaderSO.FindProperty("nextSceneName").stringValue = "laberinto";
            loaderSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(portalGO);
            Debug.Log($"Se creó 'MazePortalTrigger' en posición {portalGO.transform.position} para transportar a 'laberinto'.");
        }
        else
        {
            NextLevelLoader levelLoader = portalGO.GetComponent<NextLevelLoader>();
            if (levelLoader != null)
            {
                SerializedObject loaderSO = new SerializedObject(levelLoader);
                loaderSO.FindProperty("nextSceneName").stringValue = "laberinto";
                loaderSO.ApplyModifiedProperties();
            }
        }
    }
}
