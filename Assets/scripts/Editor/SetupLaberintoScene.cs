using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SetupLaberintoScene
{
    [MenuItem("Tools/Setup Laberinto Scene & UI")]
    public static void ExecuteSetup()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SetupLaberintoScene] No se puede ejecutar la configuración durante Play Mode.");
            return;
        }

        Debug.Log("=== INICIANDO CONFIGURACIÓN DE ESCENA LABERINTO & UI ===");

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "laberinto")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/laberinto.unity");
            Debug.Log("Escena abierta: Assets/Scenes/laberinto.unity");
        }

        // 1. Configurar UserProfileManager
        SetupProfileManager();

        // 2. Configurar EventSystem
        SetupEventSystem();

        // 3. Configurar UI Banner Superior (Perfil)
        SetupTopProfileUI();

        // 4. Configurar UI Banner Inferior (GameUIManager & Controles)
        SetupGameUIManager();

        // 5. Configurar Cámara & Zoom Controller
        SetupCameraAndZoom();

        // 6. Configurar Luz 2D Global
        SetupGlobalLight2D();

        // 7. Configurar Jugador (CatPlayer / Cat GameObject)
        SetupPlayerComponents();

        // Guardar escena y assets
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== CONFIGURACIÓN DE LABERINTO COMPLETADA EXITOSAMENTE ===");
    }

    private static void SetupProfileManager()
    {
        UserProfileManager manager = Object.FindAnyObjectByType<UserProfileManager>();
        if (manager == null)
        {
            GameObject managerGO = new GameObject("UserProfileManager");
            manager = managerGO.AddComponent<UserProfileManager>();
            Debug.Log("Se creó GameObject 'UserProfileManager'.");
        }
    }

    private static void SetupEventSystem()
    {
        EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject es = new GameObject("EventSystem");
            eventSystem = es.AddComponent<EventSystem>();
        }

        StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            Object.DestroyImmediate(legacyModule);
        }

        if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    private static void SetupTopProfileUI()
    {
        GameObject canvasGO = GameObject.Find("HubCanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("HubCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        UserProfileUIController uiController = canvasGO.GetComponent<UserProfileUIController>();
        if (uiController == null)
        {
            uiController = canvasGO.AddComponent<UserProfileUIController>();
        }

        Transform topHudTransform = canvasGO.transform.Find("TopHUDPanel");
        if (topHudTransform == null)
        {
            topHudTransform = CreateTopHUDPanel(canvasGO.transform);
        }

        Transform modalTransform = canvasGO.transform.Find("ProfileModalPanel");
        if (modalTransform == null)
        {
            modalTransform = CreateProfileModalPanel(canvasGO.transform);
        }
        if (modalTransform != null)
        {
            modalTransform.gameObject.SetActive(false);
        }

        // Vincular referencias mediante SerializedObject
        SerializedObject uiSO = new SerializedObject(uiController);
        uiSO.FindProperty("topHudPanel").objectReferenceValue = topHudTransform.gameObject;
        uiSO.FindProperty("profileInfoText").objectReferenceValue = topHudTransform.Find("ProfileInfoText")?.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("hubInstructionText").objectReferenceValue = topHudTransform.Find("InstructionText")?.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("changeProfileButton").objectReferenceValue = topHudTransform.Find("ChangeProfileButton")?.GetComponent<Button>();

        uiSO.FindProperty("profileModalPanel").objectReferenceValue = modalTransform.gameObject;

        Transform createPanel = modalTransform.Find("Card/CreateTabPanel");
        Transform searchPanel = modalTransform.Find("Card/SearchTabPanel");

        uiSO.FindProperty("createTabPanel").objectReferenceValue = createPanel?.gameObject;
        uiSO.FindProperty("searchTabPanel").objectReferenceValue = searchPanel?.gameObject;

        if (createPanel != null)
        {
            uiSO.FindProperty("firstNameInput").objectReferenceValue = createPanel.Find("FirstNameInput")?.GetComponent<TMP_InputField>();
            uiSO.FindProperty("lastNameInput").objectReferenceValue = createPanel.Find("LastNameInput")?.GetComponent<TMP_InputField>();
            uiSO.FindProperty("ageInput").objectReferenceValue = createPanel.Find("AgeInput")?.GetComponent<TMP_InputField>();
            uiSO.FindProperty("educationInput").objectReferenceValue = createPanel.Find("EducationInput")?.GetComponent<TMP_InputField>();
            uiSO.FindProperty("usernameInput").objectReferenceValue = createPanel.Find("UsernameInput")?.GetComponent<TMP_InputField>();
            uiSO.FindProperty("createSaveButton").objectReferenceValue = createPanel.Find("SaveButton")?.GetComponent<Button>();
            uiSO.FindProperty("createStatusText").objectReferenceValue = createPanel.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        }

        if (searchPanel != null)
        {
            uiSO.FindProperty("searchUsernameInput").objectReferenceValue = searchPanel.Find("SearchInput")?.GetComponent<TMP_InputField>();
            uiSO.FindProperty("searchButton").objectReferenceValue = searchPanel.Find("SearchButton")?.GetComponent<Button>();
            uiSO.FindProperty("searchStatusText").objectReferenceValue = searchPanel.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        }

        Transform tabHeader = modalTransform.Find("Card/TabHeader");
        if (tabHeader != null)
        {
            uiSO.FindProperty("showCreateTabButton").objectReferenceValue = tabHeader.Find("TabCreateBtn")?.GetComponent<Button>();
            uiSO.FindProperty("showSearchTabButton").objectReferenceValue = tabHeader.Find("TabSearchBtn")?.GetComponent<Button>();
        }

        uiSO.ApplyModifiedProperties();
    }

    private static Transform CreateTopHUDPanel(Transform parent)
    {
        GameObject hudGO = new GameObject("TopHUDPanel");
        hudGO.transform.SetParent(parent, false);

        RectTransform rt = hudGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 120f);

        Image bg = hudGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.2f, 0.85f);

        // ProfileInfoText
        GameObject infoGO = new GameObject("ProfileInfoText");
        infoGO.transform.SetParent(hudGO.transform, false);
        RectTransform infoRt = infoGO.AddComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0.02f, 0.55f);
        infoRt.anchorMax = new Vector2(0.8f, 0.95f);
        infoRt.anchoredPosition = Vector2.zero;
        infoRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI infoTMP = infoGO.AddComponent<TextMeshProUGUI>();
        infoTMP.fontSize = 20;
        infoTMP.color = Color.yellow;
        infoTMP.text = "<b>Jugador:</b> --- | <b>Nombre:</b> --- | <b>Edad:</b> - | <b>Educación:</b> ---";

        // InstructionText
        GameObject instGO = new GameObject("InstructionText");
        instGO.transform.SetParent(hudGO.transform, false);
        RectTransform instRt = instGO.AddComponent<RectTransform>();
        instRt.anchorMin = new Vector2(0.02f, 0.05f);
        instRt.anchorMax = new Vector2(0.8f, 0.5f);
        instRt.anchoredPosition = Vector2.zero;
        instRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI instTMP = instGO.AddComponent<TextMeshProUGUI>();
        instTMP.fontSize = 22;
        instTMP.color = Color.white;
        instTMP.text = "<b>Jugador</b>, ¡supera el laberinto y encuentra la salida!";

        // ChangeProfileButton
        GameObject btnGO = new GameObject("ChangeProfileButton");
        btnGO.transform.SetParent(hudGO.transform, false);
        RectTransform btnRt = btnGO.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.83f, 0.2f);
        btnRt.anchorMax = new Vector2(0.98f, 0.8f);
        btnRt.anchoredPosition = Vector2.zero;
        btnRt.sizeDelta = Vector2.zero;

        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.color = new Color(0.2f, 0.5f, 0.8f, 1f);
        btnGO.AddComponent<Button>();

        GameObject btnTxtGO = new GameObject("Text");
        btnTxtGO.transform.SetParent(btnGO.transform, false);
        RectTransform btnTxtRt = btnTxtGO.AddComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI btnTMP = btnTxtGO.AddComponent<TextMeshProUGUI>();
        btnTMP.fontSize = 18;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = Color.white;
        btnTMP.text = "Perfil / Buscar";

        return hudGO.transform;
    }

    private static Transform CreateProfileModalPanel(Transform parent)
    {
        GameObject modalGO = new GameObject("ProfileModalPanel");
        modalGO.transform.SetParent(parent, false);

        RectTransform rt = modalGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Image overlay = modalGO.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.75f);

        // Card Container
        GameObject cardGO = new GameObject("Card");
        cardGO.transform.SetParent(modalGO.transform, false);
        RectTransform cardRt = cardGO.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(650f, 550f);

        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = new Color(0.12f, 0.16f, 0.24f, 0.98f);

        // TabHeader
        GameObject headerGO = new GameObject("TabHeader");
        headerGO.transform.SetParent(cardGO.transform, false);
        RectTransform headerRt = headerGO.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 0.88f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.sizeDelta = Vector2.zero;

        CreateTabButton(headerGO.transform, "TabCreateBtn", "Crear Nuevo Perfil", new Vector2(0f, 0f), new Vector2(0.5f, 1f));
        CreateTabButton(headerGO.transform, "TabSearchBtn", "Buscar Perfil (Apodo)", new Vector2(0.5f, 0f), new Vector2(1f, 1f));

        // CreateTabPanel
        GameObject createPanel = new GameObject("CreateTabPanel");
        createPanel.transform.SetParent(cardGO.transform, false);
        RectTransform createRt = createPanel.AddComponent<RectTransform>();
        createRt.anchorMin = Vector2.zero;
        createRt.anchorMax = new Vector2(1f, 0.88f);
        createRt.sizeDelta = Vector2.zero;

        CreateInputField(createPanel.transform, "FirstNameInput", "Nombre", new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.88f));
        CreateInputField(createPanel.transform, "LastNameInput", "Apellido", new Vector2(0.1f, 0.60f), new Vector2(0.9f, 0.73f));
        CreateInputField(createPanel.transform, "AgeInput", "Edad", new Vector2(0.1f, 0.45f), new Vector2(0.48f, 0.58f));
        CreateInputField(createPanel.transform, "EducationInput", "Educación (ej: Secundaria, Univ.)", new Vector2(0.52f, 0.45f), new Vector2(0.9f, 0.58f));
        CreateInputField(createPanel.transform, "UsernameInput", "Apodo / Nombre del Personaje (Obligatorio)", new Vector2(0.1f, 0.30f), new Vector2(0.9f, 0.43f));

        CreateButton(createPanel.transform, "SaveButton", "Guardar y Empezar", new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.25f));
        CreateStatusLabel(createPanel.transform, "StatusText", new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.1f));

        // SearchTabPanel
        GameObject searchPanel = new GameObject("SearchTabPanel");
        searchPanel.transform.SetParent(cardGO.transform, false);
        RectTransform searchRt = searchPanel.AddComponent<RectTransform>();
        searchRt.anchorMin = Vector2.zero;
        searchRt.anchorMax = new Vector2(1f, 0.88f);
        searchRt.sizeDelta = Vector2.zero;

        CreateInputField(searchPanel.transform, "SearchInput", "Ingrese el Apodo / Username a buscar", new Vector2(0.1f, 0.60f), new Vector2(0.9f, 0.75f));
        CreateButton(searchPanel.transform, "SearchButton", "Buscar Perfil", new Vector2(0.25f, 0.40f), new Vector2(0.75f, 0.55f));
        CreateStatusLabel(searchPanel.transform, "StatusText", new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.35f));

        return modalGO.transform;
    }

    private static void CreateTabButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);
        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = Vector2.zero;

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.2f, 0.25f, 0.35f);
        btnGO.AddComponent<Button>();

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRt = txtGO.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 18;
        tmp.color = Color.white;
    }

    private static void CreateInputField(Transform parent, string name, string placeholder, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject inputGO = new GameObject(name);
        inputGO.transform.SetParent(parent, false);
        RectTransform rt = inputGO.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = Vector2.zero;

        Image img = inputGO.AddComponent<Image>();
        img.color = new Color(0.05f, 0.08f, 0.12f);

        TMP_InputField field = inputGO.AddComponent<TMP_InputField>();

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(inputGO.transform, false);
        RectTransform textRt = textGO.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 0f);
        textRt.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.fontSize = 18;
        textTMP.color = Color.white;
        field.textComponent = textTMP;

        GameObject phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(inputGO.transform, false);
        RectTransform phRt = phGO.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(10f, 0f);
        phRt.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.fontSize = 18;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        phTMP.text = placeholder;
        field.placeholder = phTMP;
    }

    private static void CreateButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);
        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = Vector2.zero;

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.15f, 0.6f, 0.35f);
        btnGO.AddComponent<Button>();

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRt = txtGO.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 20;
        tmp.color = Color.white;
    }

    private static void CreateStatusLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject labelGO = new GameObject(name);
        labelGO.transform.SetParent(parent, false);
        RectTransform rt = labelGO.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 18;
        tmp.text = "";
    }

    private static void SetupGameUIManager()
    {
        GameUIManager manager = Object.FindAnyObjectByType<GameUIManager>();
        if (manager == null)
        {
            GameObject managerGO = new GameObject("GameUIManager");
            manager = managerGO.AddComponent<GameUIManager>();
            Debug.Log("Se creó GameObject 'GameUIManager'.");
        }
    }

    private static void SetupCameraAndZoom()
    {
        CameraZoomController zoomController = Object.FindAnyObjectByType<CameraZoomController>();
        if (zoomController == null)
        {
            GameObject camGO = GameObject.Find("Main Camera");
            if (camGO == null) camGO = Camera.main != null ? Camera.main.gameObject : null;

            if (camGO != null)
            {
                zoomController = camGO.AddComponent<CameraZoomController>();
                Debug.Log("Se añadió 'CameraZoomController' al objeto de Cámara.");
            }
            else
            {
                GameObject zoomGO = new GameObject("CameraZoomController");
                zoomController = zoomGO.AddComponent<CameraZoomController>();
                Debug.Log("Se creó GameObject 'CameraZoomController'.");
            }
        }
    }

    private static void SetupGlobalLight2D()
    {
        Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        int[] allSortingLayerIDs = SortingLayer.layers.Select(l => l.id).ToArray();

        if (lights.Length == 0)
        {
            GameObject lightGO = new GameObject("Global Light 2D");
            Light2D newLight = lightGO.AddComponent<Light2D>();
            newLight.lightType = Light2D.LightType.Global;
            newLight.intensity = 1.0f;
            newLight.color = Color.white;
            lights = new Light2D[] { newLight };
            Debug.Log("Se creó GameObject 'Global Light 2D'.");
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
            }
        }
    }

    private static void SetupPlayerComponents()
    {
        GameObject player = GameObject.Find("CatPlayer");
        if (player == null) player = GameObject.Find("Cat");
        if (player == null)
        {
            CatMovement mv = Object.FindAnyObjectByType<CatMovement>();
            if (mv != null) player = mv.gameObject;
        }

        if (player != null)
        {
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb == null) rb = player.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            CatMovement catMv = player.GetComponent<CatMovement>();
            if (catMv == null) catMv = player.AddComponent<CatMovement>();
            catMv.enabled = true;

            Animator anim = player.GetComponentInChildren<Animator>();
            SpriteRenderer sr = player.GetComponentInChildren<SpriteRenderer>();

            SerializedObject catSO = new SerializedObject(catMv);
            if (anim != null) catSO.FindProperty("animator").objectReferenceValue = anim;
            if (sr != null) catSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            catSO.ApplyModifiedProperties();

            CatInputReader catInput = player.GetComponent<CatInputReader>();
            if (catInput == null) catInput = player.AddComponent<CatInputReader>();
            catInput.enabled = true;

            CatInventory inventory = player.GetComponent<CatInventory>();
            if (inventory == null) inventory = player.AddComponent<CatInventory>();

            AxeObstacleBreaker breaker = player.GetComponent<AxeObstacleBreaker>();
            if (breaker == null) breaker = player.AddComponent<AxeObstacleBreaker>();

            MazeAgent agent = player.GetComponent<MazeAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            Debug.Log($"Componentes de jugador verificados y configurados en '{player.name}'.");
        }
    }
}
