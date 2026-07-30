using UnityEngine;

[RequireComponent(typeof(CatInventory))]
[RequireComponent(typeof(CatInputReader))]
[RequireComponent(typeof(CatMovement))]
public class AxeObstacleBreaker : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private BreakableTilemap breakableTilemap;

    [SerializeField]
    private Animator animator;

    [Header("Configuración")]
    [SerializeField, Min(0.1f)]
    private float interactionDistance = 0.8f;

    private CatInventory inventory;
    private CatInputReader inputReader;
    private CatMovement catMovement;

    private void Awake()
    {
        inventory = GetComponent<CatInventory>();
        inputReader = GetComponent<CatInputReader>();
        catMovement = GetComponent<CatMovement>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.InteractPressed += TryBreakObstacle;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.InteractPressed -= TryBreakObstacle;
        }
    }

    private void TryBreakObstacle()
    {
        if (!inventory.HasAxe)
        {
            Debug.Log("Necesitas recoger el hacha primero.");
            return;
        }

        // Reproducir la animación de ataque Cat_Attack
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.Play("Cat_Attack", 0, 0f);
        }

        Vector3 targetPosition =
            transform.position +
            (Vector3)(
                catMovement.FacingDirection *
                interactionDistance
            );

        // 1. Detección de Objetos Destruibles 2D (Prefabs de árboles, rocas, etc.)
        Collider2D hitCollider = Physics2D.OverlapCircle(targetPosition, 0.5f);
        if (hitCollider != null)
        {
            DestructibleObject destructible = hitCollider.GetComponentInParent<DestructibleObject>();
            if (destructible != null)
            {
                destructible.Hit(1);
                Debug.Log($"¡Obstáculo destruible cortado con animación Cat_Attack! ({destructible.gameObject.name})");
                return;
            }
        }

        // 2. Detección de Tilemaps destructibles
        if (breakableTilemap != null)
        {
            bool obstacleRemoved =
                breakableTilemap.TryBreakAtWorldPosition(
                    targetPosition,
                    out Vector3Int removedCell
                );

            if (obstacleRemoved)
            {
                Debug.Log($"¡Obstáculo de Tilemap cortado con animación Cat_Attack! Celda eliminada: {removedCell}");
                return;
            }
        }

        Debug.Log("No hay un obstáculo cortable delante.");
    }

    private void OnDrawGizmosSelected()
    {
        CatMovement movement =
            GetComponent<CatMovement>();

        if (movement == null)
        {
            return;
        }

        Vector3 targetPosition =
            transform.position +
            (Vector3)(
                movement.FacingDirection *
                interactionDistance
            );

        Gizmos.DrawLine(
            transform.position,
            targetPosition
        );

        Gizmos.DrawWireSphere(
            targetPosition,
            0.5f
        );
    }
}
