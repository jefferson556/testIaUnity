using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

public static class ActivateAllColliders
{
    [MenuItem("Tools/Activate All Colliders")]
    public static void Execute()
    {
        Debug.Log("=== REVISANDO Y ACTIVANDO TODOS LOS COLISIONADORES EN LA ESCENA ===");

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "SampleScene")
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }

        // 1. Asegurar TilemapCollider2D en capas de obstáculos (Decoration, Meta, Walls, House, BreakableObjects)
        string[] obstacleTilemapNames = new string[] { "Decoration", "Meta", "Walls", "House", "BreakableObjects", "Caves" };

        foreach (string name in obstacleTilemapNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj == null)
            {
                // Buscar dentro de Grid
                GameObject grid = GameObject.Find("Grid");
                if (grid != null)
                {
                    Transform t = grid.transform.Find(name);
                    if (t != null) obj = t.gameObject;
                }
            }

            if (obj != null)
            {
                Tilemap tilemap = obj.GetComponent<Tilemap>();
                if (tilemap != null && tilemap.cellBounds.size.x > 0 && tilemap.cellBounds.size.y > 0)
                {
                    TilemapCollider2D tmCol = obj.GetComponent<TilemapCollider2D>();
                    if (tmCol == null)
                    {
                        tmCol = obj.AddComponent<TilemapCollider2D>();
                        Debug.Log($"Se añadió TilemapCollider2D al Tilemap '{name}'.");
                    }
                    tmCol.enabled = true;
                    EditorUtility.SetDirty(obj);
                }
            }
        }

        // 2. Buscar TODOS los Collider2D en la escena y asegurar enabled = true
        Collider2D[] allColliders = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int activatedCount = 0;

        foreach (Collider2D col in allColliders)
        {
            if (!col.enabled)
            {
                col.enabled = true;
                activatedCount++;
                Debug.Log($"Activado colisionador {col.GetType().Name} en GameObject: '{col.gameObject.name}'");
            }
            else
            {
                Debug.Log($"Colisionador {col.GetType().Name} ya está activo en GameObject: '{col.gameObject.name}' (isTrigger: {col.isTrigger})");
            }
            EditorUtility.SetDirty(col.gameObject);
        }

        // Guardar escena
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log($"=== REVISIÓN COMPLETADA: Total de colisionadores procesados: {allColliders.Length} (Activados: {activatedCount}) ===");
    }
}
