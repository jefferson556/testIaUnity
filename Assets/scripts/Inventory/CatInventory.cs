using UnityEngine;

public class CatInventory : MonoBehaviour
{
    public bool HasAxe { get; private set; }
    public bool HasKey { get; private set; }

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
    }

    public void CollectKey()
    {
        if (HasKey)
        {
            return;
        }

        HasKey = true;
        Debug.Log("¡Llave recolectada!");
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
}