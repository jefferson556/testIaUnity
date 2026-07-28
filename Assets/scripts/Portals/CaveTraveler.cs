using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CaveTraveler : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField, Min(0f)]
    private float teleportCooldown = 0.5f;

    private Rigidbody2D rb;
    private float nextTeleportTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public bool TryTeleport(Transform destination)
    {
        if (destination == null)
        {
            Debug.LogError(
                "El portal no tiene configurado un punto de salida.",
                this
            );

            return false;
        }

        if (Time.time < nextTeleportTime)
        {
            return false;
        }

        nextTeleportTime = Time.time + teleportCooldown;

        rb.position = destination.position;
        rb.linearVelocity = Vector2.zero;

        Debug.Log($"El gato viajó hacia {destination.name}.");

        return true;
    }
}