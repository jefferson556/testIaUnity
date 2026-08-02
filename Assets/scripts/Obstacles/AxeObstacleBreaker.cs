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
    private Collider2D playerCollider;

    public event System.Action OnFailedHitNoAxe;
    public event System.Action OnObstacleHit;

    private void Awake()
    {
        inventory = GetComponent<CatInventory>();
        inputReader = GetComponent<CatInputReader>();
        catMovement = GetComponent<CatMovement>();
        playerCollider = GetComponent<Collider2D>();

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
            OnFailedHitNoAxe?.Invoke();
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

        Vector2 offset = playerCollider != null ? playerCollider.offset : Vector2.zero;
        Vector3 origin = transform.position + (Vector3)offset;
        Vector3 targetPosition =
            origin +
            (Vector3)(
                catMovement.FacingDirection *
                interactionDistance
            );

        // 1. Detección de Objetos Destruibles 2D (Prefabs de árboles, rocas, etc.)
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(targetPosition, 0.5f);
        foreach (var hitCollider in hitColliders)
        {
            // Ignorar el colisionador del propio jugador
            if (hitCollider.gameObject == gameObject || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            DestructibleObject destructible = hitCollider.GetComponentInParent<DestructibleObject>();
            if (destructible != null)
            {
                destructible.Hit(1);
                Debug.Log($"¡Obstáculo destructible cortado con animación Cat_Attack! ({destructible.gameObject.name})");
                OnObstacleHit?.Invoke();
                return;
            }
        }

        // 2. Detección de Tilemaps destructibles
        if (breakableTilemap != null)
        {
            // Para el tilemap usamos transform.position directamente como origen para evitar desfases de celdas
            Vector3 tilemapTargetPosition =
                transform.position +
                (Vector3)(
                    catMovement.FacingDirection *
                    interactionDistance
                );

            bool obstacleRemoved =
                breakableTilemap.TryBreakAtWorldPosition(
                    tilemapTargetPosition,
                    out Vector3Int removedCell
                );

            if (obstacleRemoved)
            {
                Debug.Log($"¡Obstáculo de Tilemap cortado con animación Cat_Attack! Celda eliminada: {removedCell}");
                OnObstacleHit?.Invoke();
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

        Collider2D col = GetComponent<Collider2D>();
        Vector2 offset = col != null ? col.offset : Vector2.zero;
        Vector3 origin = transform.position + (Vector3)offset;

        Vector3 targetPosition =
            origin +
            (Vector3)(
                movement.FacingDirection *
                interactionDistance
            );

        Gizmos.color = Color.white;
        Gizmos.DrawLine(
            origin,
            targetPosition
        );

        Gizmos.DrawWireSphere(
            targetPosition,
            0.5f
        );
    }
}
