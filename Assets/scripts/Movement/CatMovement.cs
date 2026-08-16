using UnityEngine;

public class CatMovement : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 4f;

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = value;
    }

    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    private CatInputReader inputReader;
    private Vector2 movement;
    public Vector2 FacingDirection { get; set; } = Vector2.down;

    // --- Soporte para ML-Agents ---
    public bool IsAIControlled { get; set; } = false;
    public Vector2 AIMoveInput { get; set; } = Vector2.zero;

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        EnsureComponents();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
        }
    }

    private void EnsureComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (inputReader == null) inputReader = GetComponent<CatInputReader>();
        if (inputReader == null) inputReader = gameObject.AddComponent<CatInputReader>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.transform.localPosition = Vector3.zero;
        }
    }

    private void Update()
    {
        if (inputReader == null) EnsureComponents();

        if (IsAIControlled)
        {
            movement = AIMoveInput;
        }
        else if (inputReader != null)
        {
            movement = inputReader.MoveInput;
        }

        UpdateFacingDirection();
        UpdateAnimation();
        UpdateSpriteDirection();
    }

    private void FixedUpdate()
    {
        // Si la IA controla, el MazeAgent se encarga de mover el Rigidbody (linearVelocity)
        if (IsAIControlled) return;

        if (rb == null) EnsureComponents();
        if (rb == null) return;

        if (rb.bodyType != RigidbodyType2D.Dynamic)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
        }

        rb.MovePosition(
            rb.position +
            movement * moveSpeed * Time.fixedDeltaTime
        );
    }

    private void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }

        bool isMoving = movement.sqrMagnitude > 0.01f;
        bool isAttacking = animator.GetCurrentAnimatorStateInfo(0).IsName("Cat_Attack");

        if (isAttacking)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (isMoving)
            {
                // Si el jugador se empieza a mover, interrumpimos el ataque inmediatamente
                animator.Play("Cat_Idle");
            }
            else
            {
                if (stateInfo.normalizedTime >= 0.95f)
                {
                    animator.Play("Cat_Idle");
                }
                return; // Evitamos actualizar parámetros de movimiento mientras ataca y está estático
            }
        }

        bool isMovingSide = false;
        bool isMovingForward = false;
        bool isMovingBackward = false;

        if (isMoving)
        {
            float absX = Mathf.Abs(movement.x);
            float absY = Mathf.Abs(movement.y);

            if (absX > absY)
            {
                isMovingSide = true;
            }
            else if (movement.y > 0.01f)
            {
                isMovingForward = true;
            }
            else if (movement.y < -0.01f)
            {
                isMovingBackward = true;
            }
        }

        animator.SetBool("isMoving", isMovingSide);
        animator.SetBool("isMovingForward", isMovingForward);
        animator.SetBool("isMovingBackward", isMovingBackward);
    }

    private void UpdateSpriteDirection()
    {
        float absX = Mathf.Abs(movement.x);
        float absY = Mathf.Abs(movement.y);

        if (absX <= absY)
        {
            return;
        }

        if (movement.x > 0.01f)
        {
            spriteRenderer.flipX = false;
        }
        else if (movement.x < -0.01f)
        {
            spriteRenderer.flipX = true;
        }
    }

    private void UpdateFacingDirection()
    {
        if (movement.sqrMagnitude <= 0.01f)
        {
            return;
        }

        float absX = Mathf.Abs(movement.x);
        float absY = Mathf.Abs(movement.y);

        if (absX > absY)
        {
            FacingDirection = movement.x > 0f
                ? Vector2.right
                : Vector2.left;

            return;
        }

        FacingDirection = movement.y > 0f
            ? Vector2.up
            : Vector2.down;
    }
}
