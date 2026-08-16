using UnityEngine;

[System.Serializable]
public class LevelLoadConfig
{
    [Tooltip("Si es true, determina la dificultad por difficultyScore. Si es false, se carga por el nombre de perfil nameLevel.")]
    public bool takeDifficultyScore;

    [Tooltip("Puntaje de dificultad entre 0.0 y 1.0 (usado cuando takeDifficultyScore es true).")]
    [Range(0f, 1f)]
    public float difficultyScore = 0.5f;

    [Tooltip("Nombre del perfil a cargar (usado cuando takeDifficultyScore es false). Ej: Easy, Normal, Hard.")]
    public string nameLevel = "Normal";

    [Tooltip("Si es true, forzará la regeneración del nivel inmediatamente.")]
    public bool applyImmediately = false;

    [Tooltip("Valores personalizados opcionales para sobrescribir sobre el perfil o score base.")]
    public DifficultyAdjustmentRequest customSettings = new DifficultyAdjustmentRequest();
}
