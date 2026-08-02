using UnityEngine;

public class CatInventory : MonoBehaviour
{
    public bool HasAxe { get; private set; }
    public bool HasKey { get; private set; }

    public event System.Action OnAxeCollected;
    public event System.Action OnKeyCollected;

    public void CollectAxe()
    {
        if (HasAxe)
        {
            return;
        }

        HasAxe = true;

        Debug.Log(
            "¡Hacha recolectada! Acércate a un tronco y presiona E."
        );
        OnAxeCollected?.Invoke();
    }

    public void CollectKey()
    {
        if (HasKey)
        {
            return;
        }

        HasKey = true;
        Debug.Log("¡Llave recolectada!");
        OnKeyCollected?.Invoke();
    }

    public bool TryConsumeKey()
    {
        if (!HasKey)
        {
            return false;
        }

        HasKey = false;
        return true;
    }

    public void ResetInventory()
    {
        HasAxe = false;
        HasKey = false;
        Debug.Log("Inventario del gato reiniciado.");
    }
}
