#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class FixAttackAnimation
{
    [MenuItem("Tools/Fix Cat Attack Animation")]
    public static void FixAnimation()
    {
        string animPath = "Assets/art/character/Animations/Cat_Attack.anim";
        string asepath = "Assets/art/character/CatCharacterAnimations1.1/AsepriteFiles/Outlined_15_Cat_Attack.aseprite";

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
        if (clip == null)
        {
            Debug.LogError($"[FixAnimation] No se encontró el clip de animación en '{animPath}'");
            return;
        }

        // Cargar todos los assets dentro del archivo .aseprite (incluyendo sprites)
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(asepath);
        List<Sprite> sprites = new List<Sprite>();
        foreach (var asset in subAssets)
        {
            if (asset is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        if (sprites.Count == 0)
        {
            Debug.LogError($"[FixAnimation] No se encontraron sprites en '{asepath}'. Asegúrate de que el archivo .aseprite esté importado correctamente por el importador de Aseprite en Unity.");
            return;
        }

        // Ordenar los sprites por nombre para asegurar la secuencia correcta (Frame_0, Frame_1, etc.)
        sprites.Sort((a, b) => a.name.CompareTo(b.name));

        // Crear una curva de fotogramas clave (Keyframes) para la propiedad m_Sprite
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        
        // Ajustamos la tasa de muestreo a 8 fps (como la original)
        float frameTime = 1f / 8f; 
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * frameTime,
                value = sprites[i]
            };
        }

        // Aplicar los nuevos keyframes al clip
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        
        // Configurar la tasa de muestreo
        clip.frameRate = 8;

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[FixAnimation] ¡ÉXITO! Se actualizaron {sprites.Count} fotogramas de animación en '{animPath}' usando los sprites con pivote correcto de '{asepath}'.");
    }
}
#endif
