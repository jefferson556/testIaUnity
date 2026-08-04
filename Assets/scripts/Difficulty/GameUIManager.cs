using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    private Canvas hudCanvas;
    private Text controlsText;
    private Text zoomStatusText;
    private Text timerText;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            CreateHUD();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void CreateHUD()
    {
        // 1. Crear el Canvas GameObject
        GameObject canvasGO = new GameObject("GameHUDCanvas");
        canvasGO.transform.SetParent(transform);
        
        hudCanvas = canvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
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
        controlsText.text = " CONTROLES: W, A, S, D / Flechas (Moverse)  |  E (Interactuar)  |  SHIFT (Zoom)";

        RectTransform controlsRect = controlsGO.GetComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(0.01f, 0.5f);
        controlsRect.anchorMax = new Vector2(0.75f, 0.9f);
        controlsRect.pivot = new Vector2(0f, 0.5f);
        controlsRect.offsetMin = Vector2.zero;
        controlsRect.offsetMax = Vector2.zero;

        // 4. Crear Texto de Temporizador (lado derecho en el mismo panel de botones/controles)
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
        zoomRect.anchorMax = new Vector2(0.99f, 0.5f);
        zoomRect.pivot = new Vector2(0f, 0.5f);
        zoomRect.offsetMin = Vector2.zero;
        zoomRect.offsetMax = Vector2.zero;
    }

    private void Update()
    {
        // Actualizar temporizador de nivel en segundos directos
        if (timerText != null && DifficultyMetricsCollector.Instance != null)
        {
            float elapsed = DifficultyMetricsCollector.Instance.CurrentLevelElapsedTime;
            timerText.text = $"TIEMPO: {Mathf.FloorToInt(elapsed)}s";
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
