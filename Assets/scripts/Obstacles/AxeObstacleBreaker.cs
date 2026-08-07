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
        if (breakableTilemap == null)
        {
            breakableTilemap = FindAnyObjectByType<BreakableTilemap>();
        }
    }

    private void Update()
    {
        // Detectar pulsación de la tecla E o Espacio mediante el paquete New Input System
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
        {
            TryBreakObstacle();
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

    public void TryBreakObstacle()
    {
        ExecuteAxeAttack(Vector2.zero);
    }

    public bool TryBreakObstacleInDirection(Vector2 direction)
    {
        return ExecuteAxeAttack(direction);
    }

    public bool BreakSpecificObstacle(GameObject targetObj, Vector2 overrideDirection = default)
    {
        return ExecuteAxeAttack(overrideDirection);
    }

    public bool ExecuteAxeAttack(Vector2 directionOverride = default)
    {
        // 1. REPRODUCIR SIEMPRE LA ANIMACIÓN DE ATAQUE "Cat_Attack" AL PRESIONAR LA TECLA E
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.Play("Cat_Attack", 0, 0f);
        }

        if (inventory != null && !inventory.HasAxe)
        {
            Debug.Log("[AxeBreaker] ⚠️ Necesitas recoger el hacha primero.");
            OnFailedHitNoAxe?.Invoke();
            return false;
        }

        if (breakableTilemap == null)
        {
            breakableTilemap = FindAnyObjectByType<BreakableTilemap>();
        }

        Vector2 facing = directionOverride != Vector2.zero ? directionOverride : (catMovement != null ? catMovement.FacingDirection : Vector2.down);
        if (facing == Vector2.zero) facing = Vector2.down;

        Vector3 origin = transform.position;

        // Puntos de prueba frontales: 1.0m, 0.7m y origen
        Vector3[] testPoints = new Vector3[]
        {
            origin + (Vector3)(facing.normalized * 1.0f),
            origin + (Vector3)(facing.normalized * 0.7f),
            origin
        };

        // 2. Detección en BreakableTilemap
        if (breakableTilemap != null)
        {
            foreach (var testPos in testPoints)
            {
                bool obstacleRemoved = breakableTilemap.TryBreakAtWorldPosition(testPos, out Vector3Int removedCell);
                if (obstacleRemoved)
                {
                    Debug.Log($"¡Obstáculo de Tilemap cortado! Celda eliminada: {removedCell} en dirección {facing}");
                    OnObstacleHit?.Invoke();
                    return true;
                }
            }
        }

        // 3. Detección en Objetos Destruibles (Prefabs)
        foreach (var testPos in testPoints)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(testPos, 0.6f);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject == gameObject || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                DestructibleObject destructible = hitCollider.GetComponentInParent<DestructibleObject>();
                if (destructible == null) destructible = hitCollider.GetComponent<DestructibleObject>();
                if (destructible != null)
                {
                    destructible.Hit(1);
                    Debug.Log($"¡Obstáculo destructible cortado! ({destructible.gameObject.name})");
                    OnObstacleHit?.Invoke();
                    return true;
                }
            }
        }

        // 4. Fallback omnidireccional en las 4 direcciones si está pegado a la pared
        if (breakableTilemap != null)
        {
            Vector3[] fallbackDirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right };
            foreach (var fDir in fallbackDirs)
            {
                Vector3 fallbackPos = origin + fDir * 0.8f;
                bool obstacleRemoved = breakableTilemap.TryBreakAtWorldPosition(fallbackPos, out Vector3Int removedCell);
                if (obstacleRemoved)
                {
                    Debug.Log($"¡Obstáculo de Tilemap cortado! Celda eliminada por cercanía: {removedCell}");
                    OnObstacleHit?.Invoke();
                    return true;
                }
            }
        }

        Debug.Log("No hay un obstáculo cortable cerca.");
        return false;
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
