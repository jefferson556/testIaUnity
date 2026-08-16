using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserProfileUIController : MonoBehaviour
{
    [Header("Top HUD Panel (Barra Superior)")]
    [SerializeField] private GameObject topHudPanel;
    [SerializeField] private TextMeshProUGUI profileInfoText;
    [SerializeField] private TextMeshProUGUI hubInstructionText;
    [SerializeField] private Button changeProfileButton;
    [SerializeField] private Button deleteProfileButton; // Botón opcional para eliminar el perfil actual

    [Header("Profile Modal Panel")]
    [SerializeField] private GameObject profileModalPanel;
    [SerializeField] private GameObject createTabPanel;
    [SerializeField] private GameObject searchTabPanel;

    [Header("Create Profile Form Inputs")]
    [SerializeField] private TMP_InputField firstNameInput;
    [SerializeField] private TMP_InputField lastNameInput;
    [SerializeField] private TMP_InputField ageInput;
    [SerializeField] private TMP_InputField educationInput;
    [SerializeField] private TMP_InputField usernameInput; // Apodo / Nombre del personaje
    [SerializeField] private Button createSaveButton;
    [SerializeField] private TextMeshProUGUI createStatusText;

    [Header("Search Profile Form Inputs")]
    [SerializeField] private TMP_InputField searchUsernameInput;
    [SerializeField] private Button searchButton;
    [SerializeField] private TextMeshProUGUI searchStatusText;

    [Header("Tab Switch Buttons")]
    [SerializeField] private Button showCreateTabButton;
    [SerializeField] private Button showSearchTabButton;

    private void Start()
    {
        EnsureDeleteProfileButton();
        SetupButtonListeners();

        if (UserProfileManager.Instance != null)
        {
            UserProfileManager.Instance.OnActiveProfileChanged += HandleActiveProfileChanged;
        }

        GameModeManager.OnGameModeChanged += HandleGameModeChanged;

        // Suscribirse al tutorial si estamos en MazeLevel_Train
        if (MazeTutorialController.Instance != null)
        {
            MazeTutorialController.Instance.OnTutorialStepChanged += HandleTutorialStepChanged;
        }

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isSampleScene = (sceneName == "SampleScene");

        if (UserProfileManager.Instance != null && UserProfileManager.Instance.ActiveProfile != null)
        {
            UpdateHUD(UserProfileManager.Instance.ActiveProfile);
            CloseModal();
        }
        else if (!isSampleScene)
        {
            if (UserProfileManager.Instance != null && UserProfileManager.Instance.Profiles.Count > 0)
            {
                UserProfileManager.Instance.SelectProfile(UserProfileManager.Instance.Profiles[UserProfileManager.Instance.Profiles.Count - 1].username);
            }
            var activeProfile = UserProfileManager.Instance != null ? UserProfileManager.Instance.ActiveProfile : null;
            UpdateHUD(activeProfile);
            CloseModal();
        }
        else
        {
            OpenModal();
        }
    }

    private void OnEnable()
    {
        GameModeManager.OnGameModeChanged += HandleGameModeChanged;
        // Fallback: si MazeTutorialController se creó después de Start()
        if (MazeTutorialController.Instance != null)
        {
            MazeTutorialController.Instance.OnTutorialStepChanged -= HandleTutorialStepChanged;
            MazeTutorialController.Instance.OnTutorialStepChanged += HandleTutorialStepChanged;
        }
    }

    private void OnDestroy()
    {
        if (UserProfileManager.Instance != null)
        {
            UserProfileManager.Instance.OnActiveProfileChanged -= HandleActiveProfileChanged;
        }
        GameModeManager.OnGameModeChanged -= HandleGameModeChanged;
        if (MazeTutorialController.Instance != null)
        {
            MazeTutorialController.Instance.OnTutorialStepChanged -= HandleTutorialStepChanged;
        }
    }

    private void HandleGameModeChanged(PlayerControlMode mode)
    {
        // No sobreescribir el HUD del tutorial
        if (GameModeManager.Instance != null && GameModeManager.Instance.IsTutorialScene) return;

        if (UserProfileManager.Instance != null && UserProfileManager.Instance.ActiveProfile != null)
        {
            UpdateHUD(UserProfileManager.Instance.ActiveProfile);
        }
    }

    private void HandleTutorialStepChanged(MazeTutorialController.TutorialStep step)
    {
        if (hubInstructionText != null)
        {
            hubInstructionText.text = MazeTutorialController.GetMessageForStep(step);
        }
    }

    private void SetupButtonListeners()
    {
        if (createSaveButton != null)
            createSaveButton.onClick.AddListener(OnCreateSaveClicked);

        if (searchButton != null)
            searchButton.onClick.AddListener(OnSearchClicked);

        if (showCreateTabButton != null)
            showCreateTabButton.onClick.AddListener(() => SwitchTab(true));

        if (showSearchTabButton != null)
            showSearchTabButton.onClick.AddListener(() => SwitchTab(false));

        if (changeProfileButton != null)
            changeProfileButton.onClick.AddListener(OpenModal);

        if (deleteProfileButton != null)
            deleteProfileButton.onClick.AddListener(OnDeleteProfileClicked);
    }

    private void EnsureDeleteProfileButton()
    {
        if (deleteProfileButton != null) return;

        if (changeProfileButton != null)
        {
            Transform parent = changeProfileButton.transform.parent;
            if (parent != null)
            {
                // Re-ajustar ancho del botón original "Perfil / Buscar"
                RectTransform changeRt = changeProfileButton.GetComponent<RectTransform>();
                if (changeRt != null)
                {
                    changeRt.anchorMin = new Vector2(0.68f, 0.2f);
                    changeRt.anchorMax = new Vector2(0.83f, 0.8f);
                }

                // Crear botón rojo "Eliminar Perfil Actual"
                GameObject deleteBtnGO = new GameObject("DeleteProfileButton");
                deleteBtnGO.transform.SetParent(parent, false);

                RectTransform delRt = deleteBtnGO.AddComponent<RectTransform>();
                delRt.anchorMin = new Vector2(0.84f, 0.2f);
                delRt.anchorMax = new Vector2(0.99f, 0.8f);
                delRt.anchoredPosition = Vector2.zero;
                delRt.sizeDelta = Vector2.zero;

                Image btnBg = deleteBtnGO.AddComponent<Image>();
                btnBg.color = new Color(0.85f, 0.25f, 0.25f, 1f); // Rojo elegante

                deleteProfileButton = deleteBtnGO.AddComponent<Button>();

                GameObject btnTxtGO = new GameObject("Text");
                btnTxtGO.transform.SetParent(deleteBtnGO.transform, false);
                RectTransform btnTxtRt = btnTxtGO.AddComponent<RectTransform>();
                btnTxtRt.anchorMin = Vector2.zero;
                btnTxtRt.anchorMax = Vector2.one;
                btnTxtRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI btnTMP = btnTxtGO.AddComponent<TextMeshProUGUI>();
                btnTMP.fontSize = 14;
                btnTMP.alignment = TextAlignmentOptions.Center;
                btnTMP.color = Color.white;
                btnTMP.text = "Eliminar Perfil Actual";

                deleteProfileButton.onClick.AddListener(OnDeleteProfileClicked);
            }
        }
    }

    public void OnDeleteProfileClicked()
    {
        if (UserProfileManager.Instance == null || UserProfileManager.Instance.ActiveProfile == null)
        {
            OpenModal();
            SwitchTab(true);
            return;
        }

        string deletedName = UserProfileManager.Instance.ActiveProfile.username;
        UserProfileManager.Instance.DeleteActiveProfile();

        // Limpiar inputs del formulario
        if (firstNameInput != null) firstNameInput.text = "";
        if (lastNameInput != null) lastNameInput.text = "";
        if (ageInput != null) ageInput.text = "";
        if (educationInput != null) educationInput.text = "";
        if (usernameInput != null) usernameInput.text = "";

        // Abrir inmediatamente la ventana modal en la pestaña de Crear Perfil
        OpenModal();
        SwitchTab(true);

        SetStatus(createStatusText, $"<color=yellow>Perfil '{deletedName}' eliminado. Por favor complete los datos del nuevo perfil.</color>");
    }

    public void OpenModal()
    {
        if (profileModalPanel != null)
            profileModalPanel.SetActive(true);

        Time.timeScale = 0f; // Pausar juego mientras selecciona perfil

        // Si existen perfiles guardados, abrir en pestaña de búsqueda por defecto
        bool hasProfiles = UserProfileManager.Instance != null && UserProfileManager.Instance.Profiles.Count > 0;
        SwitchTab(!hasProfiles);
    }

    public void CloseModal()
    {
        if (profileModalPanel != null)
            profileModalPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    private void SwitchTab(bool showCreate)
    {
        if (createTabPanel != null) createTabPanel.SetActive(showCreate);
        if (searchTabPanel != null) searchTabPanel.SetActive(!showCreate);

        if (createStatusText != null) createStatusText.text = "";
        if (searchStatusText != null) searchStatusText.text = "";
    }

    private void OnCreateSaveClicked()
    {
        string fName = firstNameInput != null ? firstNameInput.text : "";
        string lName = lastNameInput != null ? lastNameInput.text : "";
        string edu = educationInput != null ? educationInput.text : "";
        string uName = usernameInput != null ? usernameInput.text : "";

        int age = 0;
        if (ageInput != null && !int.TryParse(ageInput.text, out age))
        {
            SetStatus(createStatusText, "<color=red>Ingrese una edad válida.</color>");
            return;
        }

        if (UserProfileManager.Instance == null)
        {
            SetStatus(createStatusText, "<color=red>Error: UserProfileManager no encontrado.</color>");
            return;
        }

        if (UserProfileManager.Instance.CreateOrUpdateProfile(fName, lName, age, edu, uName, out string err))
        {
            SetStatus(createStatusText, "<color=green>Perfil guardado correctamente!</color>");
            CloseModal();
        }
        else
        {
            SetStatus(createStatusText, $"<color=red>{err}</color>");
        }
    }

    private void OnSearchClicked()
    {
        string queryUsername = searchUsernameInput != null ? searchUsernameInput.text : "";
        if (string.IsNullOrWhiteSpace(queryUsername))
        {
            SetStatus(searchStatusText, "<color=red>Ingrese el apodo / username a buscar.</color>");
            return;
        }

        if (UserProfileManager.Instance == null)
        {
            SetStatus(searchStatusText, "<color=red>Error: UserProfileManager no encontrado.</color>");
            return;
        }

        if (UserProfileManager.Instance.SelectProfile(queryUsername))
        {
            SetStatus(searchStatusText, "<color=green>Perfil encontrado y seleccionado!</color>");
            CloseModal();
        }
        else
        {
            SetStatus(searchStatusText, $"<color=red>No se encontró el apodo '{queryUsername}'.</color>");
        }
    }

    private void HandleActiveProfileChanged(UserProfileData profile)
    {
        UpdateHUD(profile);
    }

    public void ForceUpdateTutorialText(string message)
    {
        if (hubInstructionText != null)
        {
            hubInstructionText.text = message;
        }
    }

    private void UpdateHUD(UserProfileData profile)
    {
        string username = profile != null ? profile.username : "Jugador";

        if (profileInfoText != null)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isTutorial = sceneName == "MazeLevel_Train" || sceneName == "laberinto";

            if (sceneName == "SampleScene" || isTutorial)
            {
                profileInfoText.text = $"<b>Jugador:</b> {username}";
            }
            else
            {
                string modeStatus = "<color=#5cb85c>Modo: Jugador</color>";
                if (GameModeManager.Instance != null)
                {
                    if (GameModeManager.Instance.IsLoadingAI)
                    {
                        modeStatus = "<color=#f0ad4e>Cargando IA...</color>";
                    }
                    else if (GameModeManager.Instance.CurrentMode == PlayerControlMode.AI)
                    {
                        modeStatus = "<color=#5bc0de>Modo: IA</color>";
                    }
                }
                    
                profileInfoText.text = $"<b>Jugador:</b> {username}\n<size=80%>{modeStatus} | Presione Q para alternar</size>";
            }
        }

        if (hubInstructionText != null)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isTutorial = sceneName == "MazeLevel_Train" || sceneName == "laberinto";
            bool isProcedural = sceneName.Contains("Procedural");

            if (isTutorial)
            {
                MazeTutorialController.EnsureInstanceExists();
                var step = MazeTutorialController.Instance != null 
                    ? MazeTutorialController.Instance.CurrentStep 
                    : MazeTutorialController.TutorialStep.FindCave;
                hubInstructionText.text = MazeTutorialController.GetMessageForStep(step);
            }
            else if (isProcedural)
            {
                if (GameModeManager.Instance != null && GameModeManager.Instance.IsLoadingAI)
                {
                    hubInstructionText.text = $"<b>{username}</b>, cargando modelo de IA... Por favor espere un momento.";
                }
                else if (GameModeManager.Instance != null && GameModeManager.Instance.CurrentMode == PlayerControlMode.AI)
                {
                    hubInstructionText.text = $"<b>{username}</b>, ¡modo IA activado! (Presione Q para tomar control manual)";
                }
                else
                {
                    hubInstructionText.text = $"<b>{username}</b>, ¡supera el laberinto! (Usa WASD para moverte | Q para alternar IA)";
                }
            }
            else
            {
                bool hasCompleted = profile != null && profile.hasCompletedTutorial;
                if (hasCompleted)
                {
                    hubInstructionText.text = $"<b>{username}</b>, diríjase al portal para jugar en el mapa procedural.";
                }
                else
                {
                    hubInstructionText.text = $"<b>{username}</b>, diríjase a la entrada del laberinto para empezar el demo.";
                }
            }
        }
    }

    private void SetStatus(TextMeshProUGUI label, string message)
    {
        if (label != null) label.text = message;
    }
}
