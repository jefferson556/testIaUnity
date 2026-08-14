using UnityEngine;

/// <summary>
/// Gestor centralizado de logs para controlar qué categorías de depuración
/// se muestran en la consola de Unity, evitando la saturación durante entrenamientos.
/// </summary>
public class GameLogger : MonoBehaviour
{
    public static GameLogger Instance { get; private set; }

    [Header("Interruptor Maestro")]
    [Tooltip("Apaga o enciende todos los logs de GameLogger globalmente.")]
    [SerializeField] private bool masterEnable = true;

    [Header("Categorías de Logs")]
    [Tooltip("Logs continuos de IA (interacciones con botón E, colisiones con paredes).")]
    [SerializeField] private bool enableAgentLogs = true;

    [Tooltip("Logs de métricas continuas (intentos fallidos de hacha, golpes a destructibles, uso de cuevas).")]
    [SerializeField] private bool enableMetricsLogs = true;

    [Tooltip("Logs del adaptador de dificultad (decisiones PPO de ajuste de nivel).")]
    [SerializeField] private bool enableAdapterLogs = true;

    [Tooltip("Logs de generación de nivel y resumen de mapa.")]
    [SerializeField] private bool enableLevelGenLogs = true;

    public bool MasterEnable => masterEnable;
    public bool EnableAgentLogs => enableAgentLogs;
    public bool EnableMetricsLogs => enableMetricsLogs;
    public bool EnableAdapterLogs => enableAdapterLogs;
    public bool EnableLevelGenLogs => enableLevelGenLogs;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private static GameLogger EnsureInstance()
    {
        if (Instance == null)
        {
            Instance = FindAnyObjectByType<GameLogger>();
            if (Instance == null)
            {
                GameObject go = new GameObject("GameLogger");
                Instance = go.AddComponent<GameLogger>();
                DontDestroyOnLoad(go);
            }
        }
        return Instance;
    }

    public static void LogAgent(string message)
    {
        var inst = EnsureInstance();
        if (inst != null && inst.masterEnable && inst.enableAgentLogs)
        {
            Debug.Log(message);
        }
    }

    public static void LogMetrics(string message)
    {
        var inst = EnsureInstance();
        if (inst != null && inst.masterEnable && inst.enableMetricsLogs)
        {
            Debug.Log(message);
        }
    }

    public static void LogAdapter(string message)
    {
        var inst = EnsureInstance();
        if (inst != null && inst.masterEnable && inst.enableAdapterLogs)
        {
            Debug.Log(message);
        }
    }

    public static void LogLevelGen(string message)
    {
        var inst = EnsureInstance();
        if (inst != null && inst.masterEnable && inst.enableLevelGenLogs)
        {
            Debug.Log(message);
        }
    }
}
