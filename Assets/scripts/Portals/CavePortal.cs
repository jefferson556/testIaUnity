using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CavePortal : MonoBehaviour
{
    [Header("Exit Point")]
    [SerializeField]
    private Transform destinationExitPoint;

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

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
