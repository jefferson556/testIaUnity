using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    private static GameUIManager instance;
    public static GameUIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindAnyObjectByType<GameUIManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GameUIManager");
                    instance = go.AddComponent<GameUIManager>();
                }
            }
            return instance;
        }
    }

    private Canvas hudCanvas;
    private Text controlsText;
    private Text zoomStatusText;
    private Text timerText;
    private Text gameModeText; // NUEVO: Texto para Modo de Juego

    // Elementos de la Pantalla de Carga
    private GameObject loadingOverlayGO;
    private CanvasGroup loadingCanvasGroup;
    private Text loadingTitleText;
    private Text loadingSubText;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            EnsureHUDAndOverlay();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureHUDAndOverlay()
    {
        if (hudCanvas == null)
        {
            CreateHUD();
        }
        if (loadingOverlayGO == null)
        {
            CreateLoadingOverlay();
        }
    }

    private void CreateHUD()
    {
        // 1. Crear el Canvas GameObject
        GameObject canvasGO = new GameObject("GameHUDCanvas");
        canvasGO.transform.SetParent(transform);
        
        hudCanvas = canvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 100; // Mantener sobre la escena de juego
        
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Obtener la fuente por defecto de Unity
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // 2. Crear Panel Inferior Oscuro
        GameObject panelGO = new GameObject("BottomPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.65f); // Negro semitransparente

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0.12f); // 12% del alto de pantalla
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 3. Crear Texto de Controles (lado izquierdo)
        GameObject controlsGO = new GameObject("ControlsLabel");
        controlsGO.transform.SetParent(panelGO.transform, false);
        
        controlsText = controlsGO.AddComponent<Text>();
        controlsText.font = defaultFont;
        controlsText.fontSize = 13;
        controlsText.alignment = TextAnchor.MiddleLeft;
        controlsText.color = Color.white;
        string sName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isTutorial = sName == "MazeLevel_Train" || sName == "laberinto";
        if (isTutorial)
        {
            controlsText.text = " CONTROLES: W, A, S, D / Flechas (Moverse)  |  E (Interactuar)  |  SHIFT (Zoom)  |  R (Reintentar)";
        }
        else
        {
            controlsText.text = " CONTROLES: W, A, S, D / Flechas (Moverse)  |  E (Interactuar)  |  SHIFT (Zoom)  |  R (Reintentar)  |  Q (Alternar IA)";
        }

        RectTransform controlsRect = controlsGO.GetComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(0.01f, 0.5f);
        controlsRect.anchorMax = new Vector2(0.75f, 0.9f);
        controlsRect.pivot = new Vector2(0f, 0.5f);
        controlsRect.offsetMin = Vector2.zero;
        controlsRect.offsetMax = Vector2.zero;

        // 4. Crear Texto de Temporizador (lado derecho)
        GameObject timerGO = new GameObject("TimerLabel");
        timerGO.transform.SetParent(panelGO.transform, false);
        
        timerText = timerGO.AddComponent<Text>();
        timerText.font = defaultFont;
        timerText.fontSize = 15;
        timerText.fontStyle = FontStyle.Bold;
        timerText.alignment = TextAnchor.MiddleRight;
        timerText.color = Color.yellow;
        timerText.text = "TIEMPO: 0s";

        RectTransform timerRect = timerGO.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0.76f, 0.5f);
        timerRect.anchorMax = new Vector2(0.99f, 0.9f);
        timerRect.pivot = new Vector2(1f, 0.5f);
        timerRect.offsetMin = Vector2.zero;
        timerRect.offsetMax = Vector2.zero;

        // 5. Crear Texto de Zoom Status
        GameObject zoomGO = new GameObject("ZoomStatusLabel");
        zoomGO.transform.SetParent(panelGO.transform, false);
        
        zoomStatusText = zoomGO.AddComponent<Text>();
        zoomStatusText.font = defaultFont;
        zoomStatusText.fontSize = 14;
        zoomStatusText.alignment = TextAnchor.MiddleLeft;
        zoomStatusText.color = Color.cyan;
        zoomStatusText.text = " Zoom: Listo (Mantén Shift)";

        RectTransform zoomRect = zoomGO.GetComponent<RectTransform>();
        zoomRect.anchorMin = new Vector2(0.01f, 0.1f);
        zoomRect.anchorMax = new Vector2(0.50f, 0.5f);
        zoomRect.pivot = new Vector2(0f, 0.5f);
        zoomRect.offsetMin = Vector2.zero;
        zoomRect.offsetMax = Vector2.zero;

        // 6. Crear Texto de Modo (Humano/IA) (Solo si no es tutorial)
        if (!isTutorial)
        {
            GameObject modeGO = new GameObject("GameModeLabel");
            modeGO.transform.SetParent(panelGO.transform, false);
            
            gameModeText = modeGO.AddComponent<Text>();
            gameModeText.font = defaultFont;
            gameModeText.fontSize = 15;
            gameModeText.fontStyle = FontStyle.Bold;
            gameModeText.alignment = TextAnchor.MiddleRight;
            gameModeText.text = "<color=green>Modo: Jugador</color>";
            gameModeText.supportRichText = true;

            RectTransform modeRect = modeGO.GetComponent<RectTransform>();
            modeRect.anchorMin = new Vector2(0.51f, 0.1f);
            modeRect.anchorMax = new Vector2(0.99f, 0.5f);
            modeRect.pivot = new Vector2(1f, 0.5f);
            modeRect.offsetMin = Vector2.zero;
            modeRect.offsetMax = Vector2.zero;

            // Escuchar cambios de GameModeManager
            GameModeManager.OnGameModeChanged += HandleGameModeChanged;
        }
    }

    private void CreateLoadingOverlay()
    {
        if (hudCanvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Panel de Pantalla Completa Oscura (Orden de Render superior)
        loadingOverlayGO = new GameObject("LoadingOverlayPanel");
        loadingOverlayGO.transform.SetParent(hudCanvas.transform, false);

        RectTransform rect = loadingOverlayGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bgImage = loadingOverlayGO.AddComponent<Image>();
        bgImage.color = new Color(0.06f, 0.08f, 0.12f, 1f); // Oscuro 100% opaco

        loadingCanvasGroup = loadingOverlayGO.AddComponent<CanvasGroup>();
        loadingCanvasGroup.alpha = 1f;
        loadingCanvasGroup.blocksRaycasts = true;

        // Texto Título
        GameObject titleGO = new GameObject("LoadingTitleText");
        titleGO.transform.SetParent(loadingOverlayGO.transform, false);

        loadingTitleText = titleGO.AddComponent<Text>();
        loadingTitleText.font = font;
        loadingTitleText.fontSize = 28;
        loadingTitleText.fontStyle = FontStyle.Bold;
        loadingTitleText.alignment = TextAnchor.MiddleCenter;
        loadingTitleText.color = new Color(1f, 0.85f, 0.3f); // Dorado brillante

        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.5f);
        titleRect.anchorMax = new Vector2(0.9f, 0.65f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // Texto Subtítulo / Detalles
        GameObject subGO = new GameObject("LoadingSubText");
        subGO.transform.SetParent(loadingOverlayGO.transform, false);

        loadingSubText = subGO.AddComponent<Text>();
        loadingSubText.font = font;
        loadingSubText.fontSize = 16;
        loadingSubText.alignment = TextAnchor.MiddleCenter;
        loadingSubText.color = Color.white;

        RectTransform subRect = subGO.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.1f, 0.35f);
        subRect.anchorMax = new Vector2(0.9f, 0.48f);
        subRect.offsetMin = Vector2.zero;
        subRect.offsetMax = Vector2.zero;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "laberinto")
        {
            loadingTitleText.text = "CARGANDO LABERINTO";
            loadingSubText.text = "Preparando mapa del laberinto...";
        }
        else
        {
            loadingTitleText.text = "CARGANDO MAPA PROCEDURAL";
            loadingSubText.text = "Generando laberinto y verificando rutas de navegación...";
        }

        loadingOverlayGO.SetActive(false);
    }

    /// <summary>
    /// Muestra la pantalla de carga opaca cubriendo toda la pantalla.
    /// </summary>
    public void ShowLoadingScreen(string message = "Cargando mapa...", string title = "")
    {
        EnsureHUDAndOverlay();

        if (loadingOverlayGO == null) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (loadingTitleText != null)
        {
            if (!string.IsNullOrEmpty(title))
            {
                loadingTitleText.text = title;
            }
            else if (sceneName == "laberinto")
            {
                loadingTitleText.text = "CARGANDO LABERINTO";
            }
            else
            {
                loadingTitleText.text = "CARGANDO MAPA PROCEDURAL";
            }
        }

        if (loadingSubText != null && !string.IsNullOrEmpty(message))
        {
            loadingSubText.text = message;
        }

        loadingOverlayGO.SetActive(true);
        loadingCanvasGroup.alpha = 1f;
        loadingCanvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Oculta la pantalla de carga con una transición suave (fade out).
    /// </summary>
    public void HideLoadingScreen(float fadeDuration = 0.4f)
    {
        if (loadingOverlayGO == null || !loadingOverlayGO.activeSelf) return;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutLoadingScreenRoutine(fadeDuration));
    }

    private IEnumerator FadeOutLoadingScreenRoutine(float duration)
    {
        float timer = 0f;
        float startAlpha = loadingCanvasGroup.alpha;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            loadingCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, timer / duration);
            yield return null;
        }

        loadingCanvasGroup.alpha = 0f;
        loadingCanvasGroup.blocksRaycasts = false;
        loadingOverlayGO.SetActive(false);
        fadeCoroutine = null;
    }

    private void OnDestroy()
    {
        GameModeManager.OnGameModeChanged -= HandleGameModeChanged;
    }

    private void HandleGameModeChanged(PlayerControlMode mode)
    {
        if (gameModeText != null)
        {
            if (GameModeManager.Instance != null && GameModeManager.Instance.IsLoadingAI)
            {
                gameModeText.text = "<color=#FFA500>Modo: Cargando IA...</color>";
            }
            else if (mode == PlayerControlMode.AI)
            {
                gameModeText.text = "<color=#00FFFF>Modo: IA</color>";
            }
            else
            {
                gameModeText.text = "<color=#00FF00>Modo: Jugador</color>";
            }
        }
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (DifficultyMetricsCollector.Instance != null)
            {
                DifficultyMetricsCollector.Instance.SetTerminationReason("RESTART");
                DifficultyMetricsCollector.Instance.OnLevelEnded(false);
                DifficultyMetricsCollector.Instance.RecordRestart();
            }
            
            DynamicLevelManager levelManager = Object.FindAnyObjectByType<DynamicLevelManager>();
            if (levelManager != null)
                levelManager.StartGeneration();
        }

        // Actualizar temporizador de nivel
        if (timerText != null)
        {
            DynamicLevelManager levelManager = FindAnyObjectByType<DynamicLevelManager>();
            if (levelManager != null && levelManager.IsTimerActive && !levelManager.IsTrainingModeActive)
            {
                // MODO PROCEDURAL: Mostrar cuenta regresiva
                timerText.text = $"TIME LEFT: {Mathf.Max(0, Mathf.FloorToInt(levelManager.CurrentLevelTimeLimit))}s";
                timerText.color = levelManager.CurrentLevelTimeLimit <= 10f ? Color.red : Color.yellow;
            }
            else if (DifficultyMetricsCollector.Instance != null)
            {
                // MODO ENTRENAMIENTO: Mostrar tiempo transcurrido normal
                float elapsed = DifficultyMetricsCollector.Instance.CurrentLevelElapsedTime;
                timerText.text = $"ELAPSED: {Mathf.FloorToInt(elapsed)}s";
                timerText.color = Color.yellow;
            }
        }

        if (zoomStatusText == null) return;

        CameraZoomController zoomCtrl = CameraZoomController.Instance;
        if (zoomCtrl == null)
        {
            zoomStatusText.text = " Zoom: No disponible (Falta CameraZoomController)";
            return;
        }

        if (zoomCtrl.IsCoolingDown)
        {
            zoomStatusText.color = Color.red;
            zoomStatusText.text = $" Zoom: RECARGANDO (Faltan {zoomCtrl.CooldownTime:F1}s)";
        }
        else if (zoomCtrl.IsZoomedOut)
        {
            zoomStatusText.color = Color.yellow;
            zoomStatusText.text = $" Zoom: ACTIVO ({zoomCtrl.RemainingTime:F1}s restantes)";
        }
        else
        {
            zoomStatusText.color = Color.cyan;
            zoomStatusText.text = $" Zoom: LISTO (Mantén Shift para usar - Máx: {zoomCtrl.RemainingTime:F1}s)";
        }
    }
}
