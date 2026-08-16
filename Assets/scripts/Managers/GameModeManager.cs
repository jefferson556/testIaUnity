using UnityEngine;
using Unity.MLAgents;

public enum PlayerControlMode
{
    Human,
    AI
}

/// <summary>
/// Gestiona el intercambio de control entre el Jugador (Humano) y la IA (ML-Agents) en la escena principal.
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitSceneLoadedCallback()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        EnsureInstanceExists();
    }

    public static void EnsureInstanceExists()
    {
        if (Instance == null && Object.FindAnyObjectByType<GameModeManager>() == null)
        {
            GameObject go = new GameObject("GameModeManager");
            go.AddComponent<GameModeManager>();
            Debug.Log("[GameModeManager] Creado automáticamente para escena: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    [Header("Referencias del Jugador")]
    [SerializeField] private GameObject playerObject;
    
    [Header("UI (Opcional)")]
    [SerializeField] private TMPro.TextMeshProUGUI modeStatusText;

    private CatMovement catMovement;
    private CatInputReader catInputReader;
    private MazeAgent mazeAgent;
    private DecisionRequester decisionRequester;

    public PlayerControlMode CurrentMode { get; private set; } = PlayerControlMode.Human;

    /// <summary>True si la escena activa es de tutorial (MazeLevel_Train o laberinto). En otras como MazeLevel_Procedural la IA sí está permitida.</summary>
    public bool IsTutorialScene
    {
        get
        {
            string s = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return s == "MazeLevel_Train" || s == "laberinto";
        }
    }
    
    public static event System.Action<PlayerControlMode> OnGameModeChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        EnsurePlayerReferences();

        Debug.Log("[GameModeManager] Inicializado correctamente en escena: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        // En MazeLevel_Train y laberinto (tutorial) siempre modo humano, sin importar trainingMode
        if (IsTutorialScene)
        {
            Debug.Log("[GameModeManager] Escena de tutorial detectada. IA deshabilitada. Modo humano forzado.");
            EnableHumanControl();
            return;
        }

        // En otras escenas (ej. MazeLevel_Procedural), respetar trainingMode o iniciar en modo humano por defecto
        TrainingConfig config = Resources.Load<TrainingConfig>("TrainingConfig");
        if (config != null && config.trainingMode)
        {
            Debug.Log("[GameModeManager] TrainingMode detectado. Iniciando en modo IA automáticamente.");
            EnableAIControl();
        }
        else
        {
            EnableHumanControl();
        }
    }

    public void EnsurePlayerReferences()
    {
        if (playerObject == null)
        {
            var cat = Object.FindAnyObjectByType<CatMovement>();
            if (cat != null)
            {
                playerObject = cat.gameObject;
            }
        }

        if (playerObject != null)
        {
            if (catMovement == null) catMovement = playerObject.GetComponent<CatMovement>();
            if (catInputReader == null) catInputReader = playerObject.GetComponent<CatInputReader>();
            if (mazeAgent == null) mazeAgent = playerObject.GetComponent<MazeAgent>();
            if (decisionRequester == null) decisionRequester = playerObject.GetComponent<DecisionRequester>();
        }
    }

    private void Update()
    {
        // Bloquear tecla Q en escenas donde no aplica
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "SampleScene" || IsTutorialScene) return;

        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame)
        {
            Debug.Log("[GameModeManager] Tecla Q presionada. Intentando cambiar de modo...");
            ToggleControlMode();
        }
    }

    public void ToggleControlMode()
    {
        if (CurrentMode == PlayerControlMode.Human)
        {
            EnableAIControl();
        }
        else
        {
            EnableHumanControl();
        }
    }

    public bool IsLoadingAI { get; private set; } = false;

    public void EnableHumanControl()
    {
        StopAllCoroutines();
        IsLoadingAI = false;

        if (playerObject == null) return;

        CurrentMode = PlayerControlMode.Human;
        Debug.Log("[GameModeManager] MODO HUMANO ACTIVADO.");

        // 1. Desactivar IA
        if (decisionRequester != null) decisionRequester.enabled = false;
        if (mazeAgent != null) mazeAgent.enabled = false;

        // 2. Limpiar velocidades residuales si el agente estaba moviéndose
        Rigidbody2D rb = playerObject.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 3. Activar Humano
        if (catMovement != null)
        {
            catMovement.IsAIControlled = false;
            catMovement.AIMoveInput = Vector2.zero;
        }
        
        if (catInputReader != null) catInputReader.enabled = true;

        UpdateUI();
        OnGameModeChanged?.Invoke(CurrentMode);
        Debug.Log("[GameModeManager] 🎮 Control MANUAL activado.");
    }

    public void EnableAIControl()
    {
        if (!ValidateComponents()) return;
        if (IsLoadingAI) return;

        StartCoroutine(EnableAIControlRoutine());
    }

    private System.Collections.IEnumerator EnableAIControlRoutine()
    {
        IsLoadingAI = true;
        Debug.Log("[GameModeManager] ⏳ Cargando modelo de IA...");

        // 1. Desactivar Input Humano inmediatamente y detener al jugador
        if (catInputReader != null) catInputReader.enabled = false;
        if (catMovement != null)
        {
            catMovement.IsAIControlled = true;
            catMovement.AIMoveInput = Vector2.zero;
        }

        Rigidbody2D rb = playerObject != null ? playerObject.GetComponent<Rigidbody2D>() : null;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Notificar a la UI para mostrar "Cargando IA..."
        UpdateUI();
        OnGameModeChanged?.Invoke(PlayerControlMode.AI);

        // Esperar 1 segundo para la inicialización completa del modelo de inferencia
        yield return new UnityEngine.WaitForSeconds(1.0f);

        // 2. Activar IA
        CurrentMode = PlayerControlMode.AI;
        IsLoadingAI = false;

        if (mazeAgent != null)
        {
            mazeAgent.enabled = true;
            mazeAgent.RefreshEnvironmentReferences();
            mazeAgent.EndEpisode(); 
        }

        if (decisionRequester != null) decisionRequester.enabled = true;

        UpdateUI();
        OnGameModeChanged?.Invoke(CurrentMode);
        Debug.Log("[GameModeManager] 🤖 Control por IA activado (Inferencia).");
    }

    private bool ValidateComponents()
    {
        if (playerObject == null || mazeAgent == null)
        {
            EnsurePlayerReferences();
        }

        if (playerObject == null) return false;

        if (mazeAgent == null)
        {
            Debug.LogError("[GameModeManager] Falta el componente MazeAgent en el Player.");
            return false;
        }
        
        var bp = playerObject.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if (bp == null)
        {
            Debug.LogError("[GameModeManager] Falta BehaviorParameters en el Player.");
            return false;
        }
        
        if (bp.Model == null && Application.isPlaying)
        {
            Debug.LogWarning("[GameModeManager] ADVERTENCIA: No hay un modelo .onnx asignado en BehaviorParameters.");
        }

        return true;
    }

    private void UpdateUI()
    {
        if (modeStatusText != null)
        {
            if (IsLoadingAI)
            {
                modeStatusText.text = "Modo: Cargando IA...";
                modeStatusText.color = new Color(1.0f, 0.75f, 0.0f); // Amarillo dorado
            }
            else
            {
                modeStatusText.text = CurrentMode == PlayerControlMode.Human ? "Modo: Jugador" : "Modo: IA";
                modeStatusText.color = CurrentMode == PlayerControlMode.Human ? Color.green : Color.blue;
            }
        }
    }
}
