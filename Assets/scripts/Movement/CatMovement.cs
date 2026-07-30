using UnityEngine;

public class CatMovement : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    private CatInputReader inputReader;
    private Vector2 movement;
    public Vector2 FacingDirection { get; private set; } = Vector2.down;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputReader = GetComponent<CatInputReader>();

        if (rb == null)
        {
            Debug.LogError("Falta Rigidbody2D en body.", this);
            enabled = false;
            return;
        }

        if (inputReader == null)
        {
            Debug.LogError("Falta CatInputReader en body.", this);
            enabled = false;
            return;
        }

        if (animator == null)
        {
            Debug.LogError(
                "Debes asignar el Animator del objeto art en el Inspector.",
                this
            );

            enabled = false;
            return;
        }

        if (spriteRenderer == null)
        {
            Debug.LogError(
                "Debes asignar el SpriteRenderer del objeto art en el Inspector.",
                this
            );

            enabled = false;
        }
        else
        {
            spriteRenderer.transform.localPosition = Vector3.zero;
        }
    }

    private void Update()
    {
        movement = inputReader.MoveInput;

        UpdateFacingDirection();
        UpdateAnimation();
        UpdateSpriteDirection();
    }

    private void FixedUpdate()
    {
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
            if (!isMoving && stateInfo.normalizedTime < 0.95f)
            {
                return;
            }

            if (!isMoving && stateInfo.normalizedTime >= 0.95f)
            {
                animator.Play("Cat_Idle");
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
