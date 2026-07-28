using UnityEngine;

[RequireComponent(typeof(CatInventory))]
[RequireComponent(typeof(CatInputReader))]
[RequireComponent(typeof(CatMovement))]
public class AxeObstacleBreaker : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField]
    private BreakableTilemap breakableTilemap;

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

        if (breakableTilemap == null)
        {
            Debug.LogError(
                "Debes asignar BreakableObstacles en AxeObstacleBreaker.",
                this
            );

            enabled = false;
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
            Debug.Log("Necesitas recoger el hacha.");
            return;
        }

        Vector3 targetPosition =
            transform.position +
            (Vector3)(
                catMovement.FacingDirection *
                interactionDistance
            );

        bool obstacleRemoved =
            breakableTilemap.TryBreakAtWorldPosition(
                targetPosition,
                out Vector3Int removedCell
            );

        if (!obstacleRemoved)
        {
            Debug.Log(
                "No hay un obstáculo cortable delante."
            );

            return;
        }

        Debug.Log(
            $"¡Obstáculo cortado! Celda eliminada: {removedCell}"
        );
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
            0.15f
        );
    }
}
