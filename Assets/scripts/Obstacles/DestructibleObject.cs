using System;
using System.Collections.Generic;
using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    [Header("Destructible Configuration")]
    [SerializeField]
    private bool requiresAxe = true;

    [SerializeField, Min(1)]
    private int health = 1;

    [Header("Drop & FX References")]
    [SerializeField]
    private Transform dropPoint;

    [SerializeField]
    private GameObject destroyEffectPrefab;

    [SerializeField]
    private AudioClip destroySound;

    public bool RequiresAxe => requiresAxe;
    public int Health => health;
    public Transform DropPoint => dropPoint != null ? dropPoint : transform;

    public event Action<DestructibleObject> OnDestroyed;

    private List<Vector3Int> reservedGridCells = new List<Vector3Int>();

    public void SetReservedCells(IEnumerable<Vector3Int> cells)
    {
        reservedGridCells.Clear();
        if (cells != null)
        {
            reservedGridCells.AddRange(cells);
        }
    }

    public IReadOnlyList<Vector3Int> ReservedGridCells => reservedGridCells;

    public void SetHealth(int val)
    {
        health = Mathf.Max(1, val);
    }

    public void Hit(int damage = 1)
    {
        health -= damage;
        if (health <= 0)
        {
            DestroyObject();
        }
    }

    public void DestroyObject()
    {
        if (destroyEffectPrefab != null)
        {
            Instantiate(
                destroyEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        if (destroySound != null)
        {
            AudioSource.PlayClipAtPoint(
                destroySound,
                transform.position
            );
        }

        OnDestroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
