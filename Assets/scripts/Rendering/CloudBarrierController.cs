using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CloudBarrierController : MonoBehaviour
{
    [Header("Cloud Asset Settings (Asignación en Inspector)")]
    [Tooltip("Arrastra aquí los sprites de nubes (ej. clouds-sp-sheet).")]
    [SerializeField]
    private Sprite[] cloudSprites;

    [Tooltip("Prefab de nube opcional si deseas instanciar objetos complejos.")]
    [SerializeField]
    private GameObject customCloudPrefab;

    [Header("Layout Settings")]
    [SerializeField]
    private Vector2 barrierSize = new Vector2(16f, 6f);

    [SerializeField]
    private int cloudColumns = 8;

    [SerializeField]
    private int cloudRows = 3;

    [SerializeField]
    private float cloudScale = 2.5f;

    [SerializeField]
    private string sortingLayerName = "Decoration";

    [SerializeField]
    private int sortingOrder = 20;

    [Header("Animation")]
    [SerializeField]
    private bool enableFloatingAnimation = true;

    [SerializeField]
    private float floatSpeed = 0.5f;

    [SerializeField]
    private float floatAmount = 0.15f;

    private BoxCollider2D boxCollider;
    private Vector3 initialPosition;

    public Sprite[] CloudSprites
    {
        get => cloudSprites;
        set => cloudSprites = value;
    }

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        initialPosition = transform.position;
        ConfigureCollider();
    }

    private void Start()
    {
        if (transform.childCount == 0 && (cloudSprites != null && cloudSprites.Length > 0 || customCloudPrefab != null))
        {
            BuildCloudLayer();
        }
    }

    private void Update()
    {
        if (enableFloatingAnimation)
        {
            float newY = initialPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmount;
            transform.position = new Vector3(initialPosition.x, newY, initialPosition.z);
        }
    }

    public void ConfigureCollider()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false; // Bloqueo físico sólido
        boxCollider.size = barrierSize;
        boxCollider.offset = Vector2.zero;
    }

    [ContextMenu("Reconstruir Capa de Nubes")]
    public void BuildCloudLayer()
    {
        // Limpiar hijos existentes
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(transform.GetChild(i).gameObject);
            else
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        ConfigureCollider();

        if (cloudSprites == null || cloudSprites.Length == 0)
        {
            return;
        }

        float stepX = barrierSize.x / Mathf.Max(1, cloudColumns - 1);
        float stepY = barrierSize.y / Mathf.Max(1, cloudRows - 1);
        Vector2 startPos = new Vector2(-barrierSize.x / 2f, -barrierSize.y / 2f);

        int spriteIndex = 0;
        for (int r = 0; r < cloudRows; r++)
        {
            for (int c = 0; c < cloudColumns; c++)
            {
                GameObject cloudObj;
                if (customCloudPrefab != null)
                {
                    cloudObj = Instantiate(customCloudPrefab, transform);
                }
                else
                {
                    cloudObj = new GameObject($"Cloud_{r}_{c}");
                    cloudObj.transform.SetParent(transform);
                    SpriteRenderer sr = cloudObj.AddComponent<SpriteRenderer>();
                    sr.sprite = cloudSprites[spriteIndex % cloudSprites.Length];
                    sr.sortingLayerName = sortingLayerName;
                    sr.sortingOrder = sortingOrder + r;
                    spriteIndex++;
                }

                // Posicionamiento con variación ligera para mayor naturalidad
                float offsetX = (Random.value - 0.5f) * 0.4f;
                float offsetY = (Random.value - 0.5f) * 0.4f;
                cloudObj.transform.localPosition = new Vector3(startPos.x + c * stepX + offsetX, startPos.y + r * stepY + offsetY, 0f);
                cloudObj.transform.localScale = Vector3.one * (cloudScale * (0.9f + Random.value * 0.2f));
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1.0f, 0.4f);
        Gizmos.DrawCube(transform.position, new Vector3(barrierSize.x, barrierSize.y, 1f));
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(barrierSize.x, barrierSize.y, 1f));
    }
}
