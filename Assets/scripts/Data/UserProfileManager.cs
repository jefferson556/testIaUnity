using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class UserProfileManager : MonoBehaviour
{
    public static UserProfileManager Instance { get; private set; }

    [SerializeField]
    private List<UserProfileData> profiles = new List<UserProfileData>();

    private UserProfileData activeProfile;
    private string saveFilePath;

    public UserProfileData ActiveProfile => activeProfile;
    public IReadOnlyList<UserProfileData> Profiles => profiles.AsReadOnly();

    public event Action<UserProfileData> OnActiveProfileChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        saveFilePath = Path.Combine(Application.persistentDataPath, "user_profiles.json");
        LoadProfiles();
    }

    public void LoadProfiles()
    {
        profiles.Clear();
        if (File.Exists(saveFilePath))
        {
            try
            {
                string json = File.ReadAllText(saveFilePath);
                UserProfileListWrapper wrapper = JsonUtility.FromJson<UserProfileListWrapper>(json);
                if (wrapper != null && wrapper.profiles != null && wrapper.profiles.Count > 0)
                {
                    profiles = wrapper.profiles;
                    if (activeProfile == null)
                    {
                        activeProfile = profiles.LastOrDefault();
                    }
                    Debug.Log($"[UserProfileManager] Se cargaron {profiles.Count} perfiles desde {saveFilePath}. Perfil activo: {activeProfile?.username}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserProfileManager] Error al cargar perfiles: {ex.Message}");
            }
        }

        if (activeProfile == null && profiles.Count > 0)
        {
            activeProfile = profiles.LastOrDefault();
        }
        else if (activeProfile == null && profiles.Count == 0)
        {
            activeProfile = new UserProfileData("Gatito", "Jeff P", 14, "Sec", "Gatito");
            profiles.Add(activeProfile);
        }
    }

    public void SaveProfiles()
    {
        try
        {
            UserProfileListWrapper wrapper = new UserProfileListWrapper { profiles = this.profiles };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"[UserProfileManager] Perfiles guardados en {saveFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserProfileManager] Error al guardar perfiles: {ex.Message}");
        }
    }

    public UserProfileData SearchProfileByUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        return profiles.FirstOrDefault(p => 
            p.username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool CreateOrUpdateProfile(string firstName, string lastName, int age, string education, string username, out string errorMessage)
    {
        errorMessage = "";

        if (string.IsNullOrWhiteSpace(username))
        {
            errorMessage = "El apodo / username es obligatorio.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            errorMessage = "El nombre es obligatorio.";
            return false;
        }

        username = username.Trim();
        firstName = firstName.Trim();
        lastName = (lastName ?? "").Trim();
        education = (education ?? "").Trim();

        UserProfileData existing = SearchProfileByUsername(username);
        if (existing != null)
        {
            // Actualizar perfil existente
            existing.firstName = firstName;
            existing.lastName = lastName;
            existing.age = age;
            existing.education = education;
            activeProfile = existing;
        }
        else
        {
            // Crear nuevo perfil
            UserProfileData newProfile = new UserProfileData(firstName, lastName, age, education, username);
            profiles.Add(newProfile);
            activeProfile = newProfile;
        }

        SaveProfiles();
        OnActiveProfileChanged?.Invoke(activeProfile);
        return true;
    }

    public bool SelectProfile(string username)
    {
        UserProfileData profile = SearchProfileByUsername(username);
        if (profile != null)
        {
            activeProfile = profile;
            OnActiveProfileChanged?.Invoke(activeProfile);
            return true;
        }
        return false;
    }

    [Serializable]
    private class UserProfileListWrapper
    {
        public List<UserProfileData> profiles = new List<UserProfileData>();
    }
}
