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
        SetupButtonListeners();

        if (UserProfileManager.Instance != null)
        {
            UserProfileManager.Instance.OnActiveProfileChanged += HandleActiveProfileChanged;
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
            if (UserProfileManager.Instance != null && UserProfileManager.Instance.ActiveProfile != null)
            {
                UpdateHUD(UserProfileManager.Instance.ActiveProfile);
            }
            CloseModal();
        }
        else
        {
            OpenModal();
        }
    }

    private void OnDestroy()
    {
        if (UserProfileManager.Instance != null)
        {
            UserProfileManager.Instance.OnActiveProfileChanged -= HandleActiveProfileChanged;
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

    private void UpdateHUD(UserProfileData profile)
    {
        if (profile == null) return;

        if (profileInfoText != null)
        {
            profileInfoText.text = $"<b>Jugador:</b> {profile.username}  |  <b>Nombre:</b> {profile.GetFullName()}  |  <b>Edad:</b> {profile.age}  |  <b>Educación:</b> {profile.education}";
        }

        if (hubInstructionText != null)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "laberinto" || sceneName.Contains("Procedural") || sceneName.Contains("Maze"))
            {
                hubInstructionText.text = $"<b>{profile.username}</b>, ¡supera el laberinto y encuentra la salida!";
            }
            else
            {
                hubInstructionText.text = $"<b>{profile.username}</b>, diríjase a la entrada del laberinto para empezar el demo.";
            }
        }
    }

    private void SetStatus(TextMeshProUGUI label, string message)
    {
        if (label != null) label.text = message;
    }
}
