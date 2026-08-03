using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CavePortal : MonoBehaviour
{
    [Header("Exit Point")]
    [SerializeField]
    private Transform destinationExitPoint;

    public Transform DestinationExitPoint
    {
        get => destinationExitPoint;
        set => destinationExitPoint = value;
    }

    /// <summary>
    /// Índice de la pareja de cuevas a la que pertenece este portal.
    /// -1 para portales de misión obligatoria (Cave_A_Entrance / Cave_B_Exit).
    /// Asignado por TravelCavePairManager al instanciar portales opcionales.
    /// </summary>
    public int PairIndex { get; set; } = -1;

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        if (destinationExitPoint == null)
        {
            Debug.LogError(
                $"{name} no tiene configurado un Destination Exit Point.",
                this
            );

            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CaveTraveler traveler =
            other.GetComponent<CaveTraveler>();

        if (traveler == null)
        {
            traveler =
                other.GetComponentInParent<CaveTraveler>();
        }

        if (traveler == null)
        {
            return;
        }

        // Comunicar el índice de pareja antes de teletransportar,
        // para que OnTeleportWithPairId lo reciba correctamente.
        traveler.CurrentPairIndex = PairIndex;
        traveler.TryTeleport(destinationExitPoint);
    }

    private void OnDrawGizmosSelected()
    {
        if (destinationExitPoint == null)
        {
            return;
        }

        Gizmos.DrawLine(
            transform.position,
            destinationExitPoint.position
        );

        Gizmos.DrawWireSphere(
            destinationExitPoint.position,
            0.15f
        );
    }
}
