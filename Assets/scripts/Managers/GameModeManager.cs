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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null && FindAnyObjectByType<GameModeManager>() == null)
        {
            GameObject go = new GameObject("GameModeManager");
            go.AddComponent<GameModeManager>();
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

    /// <summary>True si la escena activa es una escena de laberinto tutorial (no SampleScene).</summary>
    public bool IsTutorialScene => 
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene";
    
    public static event System.Action<PlayerControlMode> OnGameModeChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (playerObject == null)
        {
            var cat = FindAnyObjectByType<CatMovement>();
            if (cat != null)
            {
                playerObject = cat.gameObject;
            }
            else
            {
                Debug.LogWarning("[GameModeManager] No se pudo encontrar el Player automáticamente (CatMovement no encontrado).");
                return;
            }
        }

        catMovement = playerObject.GetComponent<CatMovement>();
        catInputReader = playerObject.GetComponent<CatInputReader>();
        mazeAgent = playerObject.GetComponent<MazeAgent>();
        decisionRequester = playerObject.GetComponent<DecisionRequester>();

        Debug.Log("[GameModeManager] Inicializado correctamente con el jugador: " + playerObject.name);

        // En MazeLevel_Train (tutorial) siempre modo humano, sin importar trainingMode
        if (IsTutorialScene)
        {
            Debug.Log("[GameModeManager] Escena de tutorial detectada. IA deshabilitada. Modo humano forzado.");
            EnableHumanControl();
            return;
        }

        // En otras escenas, respetar trainingMode
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

    public void EnableHumanControl()
    {
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

        CurrentMode = PlayerControlMode.AI;
        Debug.Log("[GameModeManager] MODO IA ACTIVADO.");

        // 1. Desactivar Input Humano
        if (catInputReader != null) catInputReader.enabled = false;

        // 2. Avisar a CatMovement que la IA tiene el control (evita físicas duplicadas)
        if (catMovement != null)
        {
            catMovement.IsAIControlled = true;
            catMovement.AIMoveInput = Vector2.zero; // Reset inicial
        }

        // 3. Limpiar velocidades residuales del humano
        Rigidbody2D rb = playerObject.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 4. Activar IA y solicitar reset de episodio interno
        if (mazeAgent != null)
        {
            mazeAgent.enabled = true;
            
            // Si el nivel ya está generado, el agente necesita recolectar los objetivos del mapa actual
            mazeAgent.RefreshEnvironmentReferences();
            
            // Forzamos el inicio de episodio lógico (no regenerará el mapa si trainingMode == false)
            mazeAgent.EndEpisode(); 
        }

        if (decisionRequester != null) decisionRequester.enabled = true;

        UpdateUI();
        OnGameModeChanged?.Invoke(CurrentMode);
        Debug.Log("[GameModeManager] 🤖 Control por IA activado (Inferencia).");
    }

    private bool ValidateComponents()
    {
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
            modeStatusText.text = CurrentMode == PlayerControlMode.Human ? "Modo: Jugador" : "Modo: IA";
            modeStatusText.color = CurrentMode == PlayerControlMode.Human ? Color.green : Color.blue;
        }
    }
}
