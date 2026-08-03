using UnityEngine;

public class DebugPhys : MonoBehaviour
{
    public LayerMask obstacleLayer;

    private void Start()
    {
        // Try to copy the obstacle layer from DynamicLevelManager
        var dlm = FindAnyObjectByType<DynamicLevelManager>();
        if (dlm != null)
        {
            var obstacleLayerField = typeof(DynamicLevelManager).GetField("obstacleLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (obstacleLayerField != null)
            {
                obstacleLayer = (LayerMask)obstacleLayerField.GetValue(dlm);
            }
        }
        Debug.Log($"[DebugPhys] Initialized. Target Obstacle Layer: {obstacleLayer.value}");
    }

    private void Update()
    {
        var col = GetComponent<CapsuleCollider2D>();
        if (col != null)
        {
            Vector2 origin = (Vector2)transform.position + col.offset;
            Collider2D[] hits = Physics2D.OverlapCapsuleAll(origin, col.size, col.direction, 0f, obstacleLayer);
            foreach (var hit in hits)
            {
                Debug.Log($"[DebugPhys] Overlapping with: {hit.name} on layer: {LayerMask.LayerToName(hit.gameObject.layer)}", hit.gameObject);
            }

            Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            float castDistance = 1.0f;
            foreach (var dir in directions)
            {
                RaycastHit2D[] castHits = Physics2D.CapsuleCastAll(origin, col.size * 0.9f, col.direction, 0f, dir, castDistance, obstacleLayer);
                foreach (var hit in castHits)
                {
                    Debug.Log($"[DebugPhys] Cast {dir} hit: {hit.collider.name} at distance: {hit.distance}");
                }
            }
        }
    }
}
