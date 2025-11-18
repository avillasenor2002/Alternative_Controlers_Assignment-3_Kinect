using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Logic for generating content (obstacles/collectibles) on a track tile

/// </summary>
public class TrackSegment : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Spawn points on the track (put SpawnPoints empty object under). If empty, will auto-find TrackSpawnPoint components.")]
    public Transform[] spawnPoints;

    // To remember what was generated on this segment, for cleanup when recycled
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    /// <summary>
    /// Generate random obstacles/pickups on the track segment
    /// </summary>
    public void GenerateContents(
        List<GameObject> obstaclePrefabs,
        List<GameObject> pickupPrefabs,
        float obstacleSpawnChance,
        float pickupSpawnChance)
    {
        ClearContents();

        // Auto-find spawn points if not manually set
        Transform[] pointsToUse = spawnPoints;
        if (pointsToUse == null || pointsToUse.Length == 0)
        {
            // Try to find TrackSpawnPoint components automatically
            TrackSpawnPoint[] trackSpawnPoints = GetComponentsInChildren<TrackSpawnPoint>(true);
            if (trackSpawnPoints != null && trackSpawnPoints.Length > 0)
            {
                pointsToUse = new Transform[trackSpawnPoints.Length];
                for (int i = 0; i < trackSpawnPoints.Length; i++)
                {
                    pointsToUse[i] = trackSpawnPoints[i].transform;
                }
            }
        }

        if (pointsToUse == null || pointsToUse.Length == 0)
        {
            Debug.LogWarning($"TrackSegment on '{gameObject.name}': No spawn points found! Please add TrackSpawnPoint components or set spawnPoints array.");
            return;
        }

        foreach (Transform point in pointsToUse)
        {
            // Check if this spawn point has TrackSpawnPoint component for type filtering
            TrackSpawnPoint trackSpawnPoint = point.GetComponent<TrackSpawnPoint>();
            SpawnType? preferredType = trackSpawnPoint != null ? trackSpawnPoint.spawnType : null;

            float roll = Random.value;
            bool shouldSpawnObstacle = false;
            bool shouldSpawnPickup = false;

            // Determine what to spawn based on TrackSpawnPoint type or random
            if (preferredType == null || preferredType == SpawnType.Random)
            {
                // Random logic: try obstacles first, then pickups
                if (obstaclePrefabs != null && obstaclePrefabs.Count > 0 && roll < obstacleSpawnChance)
                {
                    shouldSpawnObstacle = true;
                }
                else if (pickupPrefabs != null && pickupPrefabs.Count > 0 && roll < obstacleSpawnChance + pickupSpawnChance)
                {
                    shouldSpawnPickup = true;
                }
            }
            else if (preferredType == SpawnType.Obstacle)
            {
                // Force obstacle spawn if probability allows
                if (obstaclePrefabs != null && obstaclePrefabs.Count > 0 && roll < obstacleSpawnChance)
                {
                    shouldSpawnObstacle = true;
                }
            }
            else if (preferredType == SpawnType.Pickup)
            {
                // Force pickup spawn if probability allows
                if (pickupPrefabs != null && pickupPrefabs.Count > 0 && roll < pickupSpawnChance)
                {
                    shouldSpawnPickup = true;
                }
            }

            // Spawn obstacle
            if (shouldSpawnObstacle)
            {
                GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Count)];
                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, point.position, point.rotation, transform);
                    if (obj != null)
                    {
                        // Compensate for parent scale to maintain original prefab scale
                        CompensateParentScale(obj.transform, prefab.transform.localScale);
                        spawnedObjects.Add(obj);
                    }
                }
            }
            // Spawn pickup
            else if (shouldSpawnPickup)
            {
                GameObject prefab = pickupPrefabs[Random.Range(0, pickupPrefabs.Count)];
                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, point.position, point.rotation, transform);
                    if (obj != null)
                    {
                        // Compensate for parent scale to maintain original prefab scale
                        CompensateParentScale(obj.transform, prefab.transform.localScale);
                        spawnedObjects.Add(obj);
                    }
                }
            }
            // Otherwise leave this point empty
        }
    }

    /// <summary>
    /// Clear previously generated objects on this track segment (when this segment is recycled)
    /// </summary>
    public void ClearContents()
    {
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            if (spawnedObjects[i] != null)
            {
                Destroy(spawnedObjects[i]);
            }
        }
        spawnedObjects.Clear();
    }

    /// <summary>
    /// Compensate for parent scale to ensure object maintains its original prefab scale
    /// </summary>
    private void CompensateParentScale(Transform child, Vector3 desiredLocalScale)
    {
        if (child == null || child.parent == null) return;

        try
        {
            // Only modify scale at runtime to avoid editor persistence issues
            if (!Application.isPlaying) return;

            // Get parent's lossy scale (world scale, accounting for all parent scales)
            // Use try-catch to handle any potential Unity internal issues
            Vector3 parentLossyScale;
            try
            {
                parentLossyScale = child.parent.lossyScale;
            }
            catch
            {
                // If lossyScale access fails, use localScale as fallback
                parentLossyScale = child.parent.localScale;
            }

            // Calculate inverse scale to compensate
            float invX = Mathf.Approximately(parentLossyScale.x, 0f) ? 1f : 1f / parentLossyScale.x;
            float invY = Mathf.Approximately(parentLossyScale.y, 0f) ? 1f : 1f / parentLossyScale.y;
            float invZ = Mathf.Approximately(parentLossyScale.z, 0f) ? 1f : 1f / parentLossyScale.z;

            // Apply compensation to maintain original scale
            child.localScale = new Vector3(
                desiredLocalScale.x * invX,
                desiredLocalScale.y * invY,
                desiredLocalScale.z * invZ
            );
        }
        catch (System.Exception)
        {
            // Silently fail if there's an issue with scale compensation
            // This prevents Unity internal assertion errors
        }
    }
}
