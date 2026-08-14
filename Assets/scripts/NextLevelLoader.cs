using UnityEngine;
using UnityEngine.SceneManagement;

public class NextLevelLoader : MonoBehaviour
{
    [Header("Nivel siguiente")]
    [Tooltip(
        "Déjalo vacío para cargar la siguiente escena " +
        "según el orden del Build."
    )]
    [SerializeField]
    private string nextSceneName;

    public void LoadNextLevel()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            LoadSceneByName();
            return;
        }

        LoadNextSceneByBuildIndex();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || 
            other.GetComponent<CatMovement>() != null || 
            other.GetComponentInParent<CatMovement>() != null || 
            other.name.Contains("Cat") || 
            other.name.Contains("Player"))
        {
            Debug.Log($"[NextLevelLoader] Player entered trigger on {gameObject.name}. Loading level...");
            LoadNextLevel();
        }
    }

    private void LoadSceneByName()
    {
        if (!Application.CanStreamedLevelBeLoaded(
                nextSceneName
            ))
        {
            Debug.LogError(
                $"La escena '{nextSceneName}' " +
                "no está agregada al Build."
            );

            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void LoadNextSceneByBuildIndex()
    {
        int currentIndex =
            SceneManager.GetActiveScene().buildIndex;

        int nextIndex = currentIndex + 1;

        if (
            nextIndex >=
            SceneManager.sceneCountInBuildSettings
        )
        {
            Debug.Log(
                "Nivel completado. No existe otra escena " +
                "configurada después de esta."
            );

            return;
        }

        SceneManager.LoadScene(nextIndex);
    }
}
