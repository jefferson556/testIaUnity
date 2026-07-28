using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CollectibleItem : MonoBehaviour
{
    public enum ItemType
    {
        Axe,
        Key
    }

    [SerializeField] private ItemType itemType;

    private bool collected;

    private void Awake()
    {
        Collider2D itemCollider = GetComponent<Collider2D>();
        itemCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
        {
            return;
        }

        CatInventory inventory = other.GetComponent<CatInventory>();

        if (inventory == null)
        {
            inventory = other.GetComponentInParent<CatInventory>();
        }

        if (inventory == null)
        {
            return;
        }

        collected = true;

        switch (itemType)
        {
            case ItemType.Axe:
                inventory.CollectAxe();
                break;

            case ItemType.Key:
                inventory.CollectKey();
                break;
        }

        Destroy(gameObject);
    }
}
