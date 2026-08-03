using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CaveTraveler : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField, Min(0f)]
    private float teleportCooldown = 0.5f;

    private Rigidbody2D rb;
    private float nextTeleportTime;

    // ── Eventos ────────────────────────────────────────────────────────────────────

    /// <summary>Disparado cada vez que el jugador usa cualquier portal.</summary>
    public event System.Action OnTeleport;

    /// <summary>
    /// Disparado con el índice de la pareja usada.
    /// Permite a KeyToGoalTracker identificar qué pareja se utilizó.
    /// </summary>
    public event System.Action<int> OnTeleportWithPairId;

    /// <summary>
    /// Índice de la pareja de portal a la que pertenece el último portal activado.
    /// Asignado por CavePortal.OnTriggerEnter2D antes de llamar a TryTeleport.
    /// -1 si no es una cueva de viaje rápido opcional (ej. cueva de misión).
    /// </summary>
    public int CurrentPairIndex { get; set; } = -1;

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

        Debug.Log($"El gato viajó hacia {destination.name}. PairIndex={CurrentPairIndex}");
        OnTeleport?.Invoke();
        OnTeleportWithPairId?.Invoke(CurrentPairIndex);

        return true;
    }
}
