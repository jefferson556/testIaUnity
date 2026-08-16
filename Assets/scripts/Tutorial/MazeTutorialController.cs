using TMPro;
using UnityEngine;

/// <summary>
/// Controlador de tutorial exclusivo para la escena MazeLevel_Train.
/// Escucha eventos reales del gameplay (cueva, hacha, llave, meta) y actualiza
/// el texto del HUD en secuencia. No modifica generación procedural ni IA.
/// </summary>
public class MazeTutorialController : MonoBehaviour
{
    public enum TutorialStep
    {
        FindCave,    // Inicio: buscar la cueva
        FindAxe,     // Encontró la cueva, ahora buscar el hacha
        FindKey,     // Tiene el hacha, ahora buscar la llave
        FindGoal,    // Tiene la llave, ahora buscar la meta
        Completed    // Llegó a la casa
    }

    public static MazeTutorialController Instance { get; private set; }

    public TutorialStep CurrentStep { get; private set; } = TutorialStep.FindCave;

    /// <summary>Disparado cada vez que el tutorial avanza un paso.</summary>
    public event System.Action<TutorialStep> OnTutorialStepChanged;

    // Cache de referencias (se buscan una sola vez tras la generación del nivel)
    private CatInventory inventory;
    private CaveTraveler traveler;
    private MazeDoor door;
    private bool subscribedToEvents;

    public static bool IsTutorialSceneName(string name)
    {
        return name == "MazeLevel_Train" || name == "laberinto";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitSceneLoadedCallback()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (IsTutorialSceneName(scene.name))
        {
            EnsureInstanceExists();
        }
    }

    public static void EnsureInstanceExists()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (IsTutorialSceneName(sceneName) && Instance == null && Object.FindAnyObjectByType<MazeTutorialController>() == null)
        {
            GameObject go = new GameObject("MazeTutorialController");
            go.AddComponent<MazeTutorialController>();
            Debug.Log("[MazeTutorialController] Creado automáticamente para escena: " + sceneName);
        }
    }

    private void Awake()
    {
        // Solo activarse en escenas de tutorial (MazeLevel_Train y laberinto)
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!IsTutorialSceneName(sceneName))
        {
            Destroy(this);
            return;
        }

        // Si el perfil activo ya completó el tutorial previamente, omitirlo y redirigir directamente al mapa procedural
        if (UserProfileManager.Instance != null && UserProfileManager.Instance.ActiveProfile != null && UserProfileManager.Instance.ActiveProfile.hasCompletedTutorial)
        {
            Debug.Log("[MazeTutorial] 🎓 El perfil activo ya completó el tutorial. Redirigiendo a MazeLevel_Procedural.");
            if (Application.CanStreamedLevelBeLoaded("MazeLevel_Procedural"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MazeLevel_Procedural");
                Destroy(gameObject);
                return;
            }
        }

        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    private bool initialMessagePushed;

    private void Start()
    {
        CurrentStep = TutorialStep.FindCave;
        OnTutorialStepChanged?.Invoke(CurrentStep);
        PushCurrentMessageToHUD();
    }

    private void Update()
    {
        if (!initialMessagePushed)
        {
            PushCurrentMessageToHUD();
        }

        if (!subscribedToEvents)
        {
            TrySubscribeToEvents();
        }

        // Safety fallback: sincronizar estado con el inventario real por si algún evento se perdió
        if (inventory != null)
        {
            if (inventory.HasKey && CurrentStep < TutorialStep.FindGoal)
            {
                AdvanceTo(TutorialStep.FindGoal);
            }
            else if (inventory.HasAxe && CurrentStep < TutorialStep.FindKey)
            {
                AdvanceTo(TutorialStep.FindKey);
            }
        }
    }

    public void PushCurrentMessageToHUD()
    {
        string msg = GetMessageForStep(CurrentStep);
        UserProfileUIController ui = FindAnyObjectByType<UserProfileUIController>();
        if (ui != null)
        {
            ui.ForceUpdateTutorialText(msg);
            initialMessagePushed = true;
        }
        else
        {
            var texts = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var txt in texts)
            {
                if (txt.gameObject.name.Contains("Instruction"))
                {
                    txt.text = msg;
                    initialMessagePushed = true;
                }
            }
        }
    }

    private void TrySubscribeToEvents()
    {
        bool inventorySubbed = false;
        bool travelerSubbed = false;

        // Suscribir inventario independientemente
        if (inventory == null)
            inventory = FindAnyObjectByType<CatInventory>();

        if (inventory != null)
        {
            inventory.OnAxeCollected -= HandleAxeCollected;
            inventory.OnAxeCollected += HandleAxeCollected;
            inventory.OnKeyCollected -= HandleKeyCollected;
            inventory.OnKeyCollected += HandleKeyCollected;
            inventorySubbed = true;
        }

        // Suscribir cueva independientemente
        if (traveler == null)
            traveler = FindAnyObjectByType<CaveTraveler>();

        if (traveler != null)
        {
            traveler.OnTeleport -= HandleTeleport;
            traveler.OnTeleport += HandleTeleport;
            travelerSubbed = true;
        }

        // Suscribir puerta independientemente
        if (door == null)
            door = FindAnyObjectByType<MazeDoor>();

        if (door != null)
        {
            door.OnDoorOpened -= HandleDoorOpened;
            door.OnDoorOpened += HandleDoorOpened;
        }

        if (inventorySubbed || travelerSubbed)
        {
            subscribedToEvents = true;
            Debug.Log("[MazeTutorial] Suscrito a eventos de gameplay correctamente.");
        }
    }

    /// <summary>Intenta suscribirse a la puerta si aún no lo estaba (llamado tras regeneración).</summary>
    public void TrySubscribeToDoor()
    {
        if (door != null) return;
        door = FindAnyObjectByType<MazeDoor>();
        if (door != null)
        {
            door.OnDoorOpened -= HandleDoorOpened;
            door.OnDoorOpened += HandleDoorOpened;
            Debug.Log("[MazeTutorial] Suscrito a MazeDoor.OnDoorOpened.");
        }
    }

    // ── Handlers de eventos ──────────────────────────────────────────────────

    private void HandleTeleport()
    {
        if (CurrentStep == TutorialStep.FindCave)
        {
            AdvanceTo(TutorialStep.FindAxe);
        }
    }

    private void HandleAxeCollected()
    {
        if (CurrentStep < TutorialStep.FindKey)
        {
            AdvanceTo(TutorialStep.FindKey);
        }
    }

    private void HandleKeyCollected()
    {
        if (CurrentStep < TutorialStep.FindGoal)
        {
            AdvanceTo(TutorialStep.FindGoal);
        }
    }

    private void HandleDoorOpened()
    {
        if (CurrentStep < TutorialStep.Completed)
        {
            AdvanceTo(TutorialStep.Completed);
        }
    }

    private void AdvanceTo(TutorialStep nextStep)
    {
        CurrentStep = nextStep;
        Debug.Log($"[MazeTutorial] Avanzando a paso: {nextStep}");

        if (nextStep == TutorialStep.Completed)
        {
            if (UserProfileManager.Instance != null && UserProfileManager.Instance.ActiveProfile != null)
            {
                UserProfileManager.Instance.ActiveProfile.hasCompletedTutorial = true;
                UserProfileManager.Instance.SaveProfiles();
                Debug.Log($"[MazeTutorial] 🎓 ¡Tutorial completado! Registrado exitosamente en el perfil: {UserProfileManager.Instance.ActiveProfile.username}");
            }
        }

        OnTutorialStepChanged?.Invoke(nextStep);
        PushCurrentMessageToHUD();
    }

    // ── Textos ───────────────────────────────────────────────────────────────

    /// <summary>Devuelve el mensaje de tutorial correspondiente al paso indicado.</summary>
    public static string GetMessageForStep(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.FindCave:
                return "¡Bienvenido al laberinto!\nPuedes usar shift para ver mas terreno.\nEncuentra la cueva. Te permitirá llegar hasta el hacha que necesitas para poder avanzar.";
            case TutorialStep.FindAxe:
                return "¡Encontraste la cueva! Ahora busca el hacha para poder destruir los obstáculos que bloquean tu camino.";
            case TutorialStep.FindKey:
                return "Ahora busca la llave. Puedes usar el hacha para destruir objetos y recuerda que existen cuevas que pueden ayudarte a desplazarte más rápido.";
            case TutorialStep.FindGoal:
                return "Ahora tienes la llave. Estás a un paso de terminar el laberinto. Encuentra la meta, que tiene forma de casa.";
            case TutorialStep.Completed:
                return "¡Excelente! Completaste el tutorial.";
            default:
                return "";
        }
    }

    // ── Limpieza ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (traveler != null) traveler.OnTeleport -= HandleTeleport;
        if (inventory != null)
        {
            inventory.OnAxeCollected -= HandleAxeCollected;
            inventory.OnKeyCollected -= HandleKeyCollected;
        }
        if (door != null) door.OnDoorOpened -= HandleDoorOpened;

        if (Instance == this) Instance = null;
    }
}
