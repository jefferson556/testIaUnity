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
        doorCollider.isTrigger = true;
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
        if (isOpen)
        {
            return;
        }

        CatInventory inventory =
            other.GetComponent<CatInventory>();

        if (inventory == null)
        {
            inventory =
                other.GetComponentInParent<CatInventory>();
        }

        if (inventory == null)
        {
            return;
        }

        if (!inventory.TryConsumeKey())
        {
            Debug.Log(
                "La puerta está cerrada. Necesitas la llave."
            );

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

        StartCoroutine(CompleteLevelAfterDelay());
    }

    private IEnumerator CompleteLevelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(
            completionDelay
        );

        onLevelCompleted?.Invoke();
    }
}