using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public sealed class MazeDoor : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;

    [Header("Animación")]
    [SerializeField] private string openBoolName = "Open";

    [SerializeField, Min(0f)]
    private float completionDelay = 1f;

    [Header("Eventos")]
    [SerializeField]
    private UnityEvent onLevelCompleted;

    private int openBoolHash;
    private bool isOpen;

    public event System.Action OnDoorOpened;

    private void Awake()
    {
        ConfigureCollider();

        if (!ConfigureAnimator())
        {
            enabled = false;
        }
    }
  
    private void ConfigureCollider()
    {
        Collider2D doorCollider = GetComponent<Collider2D>();
        if (doorCollider != null)
        {
            doorCollider.isTrigger = true;

            if (doorCollider is BoxCollider2D box)
            {
                if (transform.localPosition.y > 0.5f && box.offset.y >= 0f)
                {
                    box.offset = new Vector2(0f, -0.25f);
                    box.size = new Vector2(Mathf.Max(box.size.x, 0.3f), Mathf.Max(box.size.y, 0.6f));
                }
            }
        }
    }

    private bool ConfigureAnimator()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError(
                "MazeDoor necesita un Animator.",
                this
            );

            return false;
        }

        openBoolHash = Animator.StringToHash(openBoolName);

        if (!HasBoolParameter(openBoolHash))
        {
            Debug.LogError(
                $"El Animator no contiene un parámetro Bool llamado " +
                $"'{openBoolName}'.",
                this
            );

            return false;
        }

        return true;
    }

    private bool HasBoolParameter(int parameterHash)
    {
        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters
        )
        {
            bool isCorrectParameter =
                parameter.nameHash == parameterHash &&
                parameter.type ==
                AnimatorControllerParameterType.Bool;

            if (isCorrectParameter)
            {
                return true;
            }
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[MazeDoor] Colisión detectada con: {other.name}");
        if (isOpen)
        {
            Debug.Log("[MazeDoor] La puerta ya está abierta.");
            return;
        }

        CatInventory inventory = other.GetComponent<CatInventory>();
        if (inventory == null)
        {
            inventory = other.GetComponentInParent<CatInventory>();
        }

        if (inventory == null)
        {
            Debug.Log("[MazeDoor] El objeto que colisionó no tiene CatInventory.");
            return;
        }

        Debug.Log($"[MazeDoor] CatInventory detectado. ¿Tiene llave? {inventory.HasKey}");

        if (!inventory.TryConsumeKey())
        {
            Debug.Log("[MazeDoor] La puerta está cerrada. Necesitas la llave.");
            return;
        }

        OpenDoor();
    }

    private void OpenDoor()
    {
        isOpen = true;

        animator.SetBool(openBoolHash, true);

        Debug.Log(
            "¡Puerta abierta! ¡Nivel completado!"
        );

        OnDoorOpened?.Invoke();

        StartCoroutine(CompleteLevelAfterDelay());
    }

    private IEnumerator CompleteLevelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(
            completionDelay
        );

        if (onLevelCompleted != null && onLevelCompleted.GetPersistentEventCount() > 0)
        {
            onLevelCompleted.Invoke();
        }
        else
        {
            NextLevelLoader levelLoader = GetComponent<NextLevelLoader>();
            if (levelLoader == null)
            {
                levelLoader = Object.FindAnyObjectByType<NextLevelLoader>();
            }

            if (levelLoader != null)
            {
                levelLoader.LoadNextLevel();
            }
            else
            {
                int currentBuildIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
                int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
                if (sceneCount > 0)
                {
                    int nextIndex = (currentBuildIndex + 1) % sceneCount;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(nextIndex);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
            }
        }
    }
}
