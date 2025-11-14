using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Temple Run style obstacle spawning system
/// Responsible for generating scrolling tracks ahead of the player and randomly spawning obstacles and pickups on the tracks
/// </summary>
public class TempleRunObstacleSpawner : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("Player Transform (used to calculate spawn positions)")]
    public Transform player;
    
    [Tooltip("Player tag (will auto-find if player is null)")]
    public string playerTag = "Player";

    [Header("Track Settings")]
    [Tooltip("Track prefab (must contain TrackSpawnPoint child objects)")]
    public GameObject trackPrefab;
    
    [Tooltip("Length of each track (Z-axis direction)")]
    public float trackLength = 20f;
    
    [Tooltip("Track movement speed (towards player)")]
    public float trackSpeed = 8f;

    [Header("Object Pool Settings")]
    [Tooltip("Track object pool size")]
    public int poolSize = 5;

    [Header("Spawn Settings")]
    [Tooltip("Interval time (seconds) to check if new track needs to be spawned")]
    public float spawnCheckInterval = 0.5f;
    
    [Tooltip("Number of initial tracks to spawn")]
    public int initialTrackCount = 3;
    
    [Tooltip("Distance ahead of player to start spawning new tracks")]
    public float spawnAheadDistance = 40f;
    
    [Tooltip("Distance behind player to recycle tracks")]
    public float recycleBehindDistance = 20f;

    [Header("Obstacle Configuration")]
    [Tooltip("Obstacle configuration list, can specify spawn position for each Prefab individually")]
    public PrefabSpawnConfig[] obstacleConfigs;
    
    [Tooltip("Obstacle spawn probability (0-1)")]
    [Range(0f, 1f)]
    public float obstacleSpawnChance = 0.3f;

    [Header("Pickup Configuration")]
    [Tooltip("Pickup configuration list, can specify spawn position for each Prefab individually")]
    public PrefabSpawnConfig[] pickupConfigs;
    
    [Tooltip("Pickup spawn probability (0-1)")]
    [Range(0f, 1f)]
    public float pickupSpawnChance = 0.2f;

    [Header("Position Detection Settings")]
    [Tooltip("X coordinate threshold for judging left/center/right")]
    public float positionThreshold = 0.5f;

    [Header("Game State Control (Optional)")]
    [Tooltip("Whether to spawn only when game is playing (requires implementing IGameStateProvider interface)")]
    public bool checkGameState = false;
    
    [Tooltip("Game state provider (if checkGameState is true, set this object)")]
    public MonoBehaviour gameStateProvider;

    // Private variables
    private Queue<GameObject> trackPool = new Queue<GameObject>();
    private List<GameObject> activeTracks = new List<GameObject>();
    private float nextSpawnCheckTime;
    private IGameStateProvider stateProvider;

    void Start()
    {
        // Find player
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
                player = playerObj.transform;
            else
                Debug.LogError($"[TempleRunObstacleSpawner] Player object with tag '{playerTag}' not found!");
        }

        // Check track prefab
        if (trackPrefab == null)
        {
            Debug.LogError("[TempleRunObstacleSpawner] Track prefab is not set!");
            return;
        }

        // Get game state provider
        if (checkGameState && gameStateProvider != null)
        {
            stateProvider = gameStateProvider as IGameStateProvider;
            if (stateProvider == null)
            {
                Debug.LogWarning("[TempleRunObstacleSpawner] gameStateProvider does not implement IGameStateProvider interface!");
            }
        }

        // Initialize object pool
        InitializeTrackPool();
        
        // Spawn initial tracks
        SpawnInitialTracks();
    }

    void Update()
    {
        // Check game state
        if (checkGameState && stateProvider != null && !stateProvider.IsGamePlaying())
            return;

        // Update system
        MoveActiveTracks();
        CheckTrackRecycle();
        CheckSpawnNewTrack();
    }

    /// <summary>
    /// Initialize track object pool
    /// </summary>
    void InitializeTrackPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject track = Instantiate(trackPrefab, transform);
            track.SetActive(false);
            trackPool.Enqueue(track);
        }
    }

    /// <summary>
    /// Spawn initial tracks
    /// </summary>
    void SpawnInitialTracks()
    {
        if (player == null) return;

        for (int i = 0; i < initialTrackCount; i++)
        {
            float zPos = player.position.z + (i * trackLength);
            SpawnTrack(zPos);
        }
    }

    /// <summary>
    /// Move all active tracks
    /// </summary>
    void MoveActiveTracks()
    {
        float moveDistance = trackSpeed * Time.deltaTime;

        foreach (GameObject track in activeTracks)
        {
            if (track != null)
            {
                track.transform.position += Vector3.back * moveDistance;
            }
        }
    }

    /// <summary>
    /// Check and recycle tracks that have passed the player
    /// </summary>
    void CheckTrackRecycle()
    {
        if (player == null) return;

        for (int i = activeTracks.Count - 1; i >= 0; i--)
        {
            GameObject track = activeTracks[i];
            if (track != null)
            {
                float trackFrontZ = track.transform.position.z + trackLength;
                if (trackFrontZ < player.position.z - recycleBehindDistance)
                {
                    RecycleTrack(track);
                    activeTracks.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Check if new track needs to be spawned
    /// </summary>
    void CheckSpawnNewTrack()
    {
        if (Time.time < nextSpawnCheckTime) return;
        nextSpawnCheckTime = Time.time + spawnCheckInterval;

        if (player == null) return;

        // If no active tracks, spawn one immediately
        if (activeTracks.Count == 0)
        {
            SpawnTrack(player.position.z);
            return;
        }

        // Get the frontmost track
        GameObject frontTrack = GetFrontmostTrack();
        if (frontTrack == null) return;

        float frontTrackEndZ = frontTrack.transform.position.z + trackLength;

        // If frontmost track is too close to player, spawn new track
        if (frontTrackEndZ < player.position.z + spawnAheadDistance)
        {
            Debug.Log($"[TempleRunObstacleSpawner] Spawning new track at Z={frontTrackEndZ:F1}");
            SpawnTrack(frontTrackEndZ);
        }
    }

    /// <summary>
    /// Get the frontmost track
    /// </summary>
    GameObject GetFrontmostTrack()
    {
        GameObject frontmost = null;
        float maxZ = float.MinValue;

        foreach (GameObject track in activeTracks)
        {
            if (track != null && track.transform.position.z > maxZ)
            {
                maxZ = track.transform.position.z;
                frontmost = track;
            }
        }

        return frontmost;
    }

    /// <summary>
    /// Spawn track at specified Z position
    /// </summary>
    void SpawnTrack(float zPosition)
    {
        GameObject track;

        // Get from object pool or create new track
        if (trackPool.Count > 0)
        {
            track = trackPool.Dequeue();
            track.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[TempleRunObstacleSpawner] Object pool is empty, creating new track");
            track = Instantiate(trackPrefab, transform);
        }

        // Set track position
        track.transform.position = new Vector3(0f, 0f, zPosition);
        activeTracks.Add(track);

        Debug.Log($"[TempleRunObstacleSpawner] Spawned track at Z={zPosition:F1}");

        // Spawn obstacles and pickups on track
        SpawnItemsOnTrack(track);
    }

    /// <summary>
    /// Spawn obstacles and pickups on track
    /// </summary>
    void SpawnItemsOnTrack(GameObject track)
    {
        // Get all spawn points on track
        TrackSpawnPoint[] spawnPoints = track.GetComponentsInChildren<TrackSpawnPoint>(true);

        if (spawnPoints.Length == 0)
        {
            Debug.LogError($"[TempleRunObstacleSpawner] Track '{track.name}' has no TrackSpawnPoint! Please add TrackSpawnPoint component to track prefab.");
            return;
        }

        Debug.Log($"[TempleRunObstacleSpawner] Found {spawnPoints.Length} spawn points");

        int obstacleCount = 0;
        int pickupCount = 0;

        // Iterate through each spawn point
        foreach (TrackSpawnPoint spawnPoint in spawnPoints)
        {
            bool shouldSpawn = false;
            GameObject prefabToSpawn = null;

            SpawnType typeToUse = spawnPoint.spawnType;

            // If random type, decide whether to spawn obstacle or pickup based on probability
            if (typeToUse == SpawnType.Random)
            {
                float roll = Random.value;
                if (roll < obstacleSpawnChance + pickupSpawnChance)
                {
                    float p = pickupSpawnChance / Mathf.Max(0.0001f, obstacleSpawnChance + pickupSpawnChance);
                    typeToUse = (Random.value < p) ? SpawnType.Pickup : SpawnType.Obstacle;
                }
                else
                {
                    continue; // Don't spawn anything
                }
            }

            // Spawn obstacle
            if (typeToUse == SpawnType.Obstacle)
            {
                if (Random.value < obstacleSpawnChance)
                {
                    prefabToSpawn = SelectPrefabForPosition(obstacleConfigs, spawnPoint);
                    if (prefabToSpawn != null)
                    {
                        shouldSpawn = true;
                        obstacleCount++;
                    }
                }
            }
            // Spawn pickup
            else if (typeToUse == SpawnType.Pickup)
            {
                if (Random.value < pickupSpawnChance)
                {
                    prefabToSpawn = SelectPrefabForPosition(pickupConfigs, spawnPoint);
                    if (prefabToSpawn != null)
                    {
                        shouldSpawn = true;
                        pickupCount++;
                    }
                }
            }

            // Actually spawn object
            if (shouldSpawn && prefabToSpawn != null)
                SpawnItemAtPoint(prefabToSpawn, spawnPoint.transform);
        }

        Debug.Log($"[TempleRunObstacleSpawner] This track spawned {obstacleCount} obstacles, {pickupCount} pickups");
    }

    /// <summary>
    /// Select appropriate Prefab based on spawn point position
    /// </summary>
    GameObject SelectPrefabForPosition(PrefabSpawnConfig[] configs, TrackSpawnPoint spawnPoint)
    {
        if (configs == null || configs.Length == 0)
        {
            return null;
        }

        float xPos = spawnPoint.transform.position.x;

        List<GameObject> validPrefabs = new List<GameObject>();

        // Find all Prefabs that can spawn at this position
        foreach (PrefabSpawnConfig config in configs)
        {
            if (config.prefab == null) continue;

            if (CanSpawnAtPosition(config.spawnPosition, xPos))
            {
                validPrefabs.Add(config.prefab);
            }
        }

        if (validPrefabs.Count == 0)
        {
            return null;
        }

        // Randomly select one
        return validPrefabs[Random.Range(0, validPrefabs.Count)];
    }

    /// <summary>
    /// Determine if the specified spawn mode allows spawning at this X coordinate
    /// </summary>
    bool CanSpawnAtPosition(SpawnPositionMode mode, float xPos)
    {
        bool isLeft = xPos < -positionThreshold;
        bool isCenter = xPos >= -positionThreshold && xPos <= positionThreshold;
        bool isRight = xPos > positionThreshold;

        switch (mode)
        {
            case SpawnPositionMode.All:
                return true;

            case SpawnPositionMode.LeftOnly:
                return isLeft;

            case SpawnPositionMode.CenterOnly:
                return isCenter;

            case SpawnPositionMode.RightOnly:
                return isRight;

            case SpawnPositionMode.LeftAndCenter:
                return isLeft || isCenter;

            case SpawnPositionMode.RightAndCenter:
                return isRight || isCenter;

            case SpawnPositionMode.LeftAndRight:
                return isLeft || isRight;

            default:
                return true;
        }
    }

    /// <summary>
    /// Spawn object at specified position
    /// </summary>
    void SpawnItemAtPoint(GameObject prefab, Transform spawnPoint)
    {
        if (prefab == null || spawnPoint == null) return;

        // Instantiate object
        GameObject item = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);

        // Handle scaling (compensate for parent scale)
        Transform t = item.transform;
        Vector3 prefabLocalScale = prefab.transform.localScale;
        t.localScale = CompensateForParentScale(spawnPoint, prefabLocalScale);

        // Add auto destroy component
        AutoDestroy autoDestroy = item.GetComponent<AutoDestroy>();
        if (autoDestroy == null)
        {
            autoDestroy = item.AddComponent<AutoDestroy>();
        }
        // Calculate lifetime: track length * 3 / speed
        autoDestroy.lifetime = (trackLength * 3) / trackSpeed;

        Debug.Log($"[TempleRunObstacleSpawner] Spawned {prefab.name} at world position {spawnPoint.position}");
    }

    /// <summary>
    /// Compensate for parent scale to ensure object displays correct size
    /// </summary>
    static Vector3 CompensateForParentScale(Transform parent, Vector3 desiredLocalScaleAsPrefab)
    {
        Vector3 s = parent.lossyScale;
        float ix = Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x;
        float iy = Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y;
        float iz = Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z;
        return new Vector3(desiredLocalScaleAsPrefab.x * ix,
                           desiredLocalScaleAsPrefab.y * iy,
                           desiredLocalScaleAsPrefab.z * iz);
    }

    /// <summary>
    /// Recycle track to object pool
    /// </summary>
    void RecycleTrack(GameObject track)
    {
        // Clean up all spawned objects on track
        foreach (Transform child in track.transform)
        {
            TrackSpawnPoint spawnPoint = child.GetComponent<TrackSpawnPoint>();
            if (spawnPoint != null)
            {
                // Destroy all child objects
                for (int i = child.childCount - 1; i >= 0; i--)
                {
                    Destroy(child.GetChild(i).gameObject);
                }
            }
        }

        // Disable and recycle to object pool
        track.SetActive(false);
        trackPool.Enqueue(track);
    }

    /// <summary>
    /// Draw debug information in Scene view
    /// </summary>
    void OnDrawGizmos()
    {
        if (player == null) return;

        // Draw spawn line (green)
        Gizmos.color = Color.green;
        float spawnZ = player.position.z + spawnAheadDistance;
        Gizmos.DrawLine(
            new Vector3(-5f, 0f, spawnZ),
            new Vector3(5f, 0f, spawnZ)
        );

        // Draw recycle line (red)
        Gizmos.color = Color.red;
        float recycleZ = player.position.z - recycleBehindDistance;
        Gizmos.DrawLine(
            new Vector3(-5f, 0f, recycleZ),
            new Vector3(5f, 0f, recycleZ)
        );

        // Draw player position (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, 1f);
    }
}

// ==================== Helper Classes and Enums ====================

/// <summary>
/// Spawn type enum
/// </summary>
public enum SpawnType
{
    Obstacle,  // Obstacle
    Pickup,    // Pickup
    Random     // Random (decided by probability)
}

/// <summary>
/// Spawn position mode enum
/// </summary>
public enum SpawnPositionMode
{
    All,            // All positions can spawn
    LeftOnly,       // Left only (X < -threshold)
    CenterOnly,     // Center only (-threshold <= X <= threshold)
    RightOnly,      // Right only (X > threshold)
    LeftAndCenter,  // Left + center
    RightAndCenter, // Right + center
    LeftAndRight    // Left + right (exclude center)
}

/// <summary>
/// Prefab spawn configuration
/// Used to specify which positions each Prefab can spawn at
/// </summary>
[System.Serializable]
public class PrefabSpawnConfig
{
    [Tooltip("Prefab to spawn")]
    public GameObject prefab;

    [Tooltip("Allowed spawn positions")]
    public SpawnPositionMode spawnPosition = SpawnPositionMode.All;
}

/// <summary>
/// Track spawn point component
/// Add this component to track prefab child objects to mark positions where obstacles or pickups can spawn
/// </summary>
public class TrackSpawnPoint : MonoBehaviour
{
    [Tooltip("Spawn type: obstacle, pickup, or random")]
    public SpawnType spawnType = SpawnType.Obstacle;

    void OnDrawGizmos()
    {
        Gizmos.color = spawnType == SpawnType.Obstacle ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = spawnType == SpawnType.Obstacle ? Color.red : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.3f);
    }
}

/// <summary>
/// Auto destroy component
/// Object automatically destroys after specified time after spawning
/// </summary>
public class AutoDestroy : MonoBehaviour
{
    [Tooltip("Lifetime (seconds)")]
    public float lifetime = 5f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}

/// <summary>
/// Game state provider interface
/// If you need to stop spawning when game is paused, have your GameManager implement this interface
/// </summary>
public interface IGameStateProvider
{
    /// <summary>
    /// Whether the game is currently playing
    /// </summary>
    bool IsGamePlaying();
}

/// <summary>
/// Temple Run style obstacle component
/// Triggers event when player collides with this obstacle
/// </summary>
public class TempleRunObstacle : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Base damage value")]
    [Range(5f, 30f)]
    public float damage = 10f;

    [Header("Behavior Settings")]
    [Tooltip("Whether to destroy immediately on collision")]
    public bool destroyOnHit = true;

    [Header("Events")]
    [Tooltip("Event triggered on collision (damage value)")]
    public System.Action<float> OnObstacleHit;

    private bool consumed = false;

    void OnTriggerEnter(Collider other)
    {
        if (consumed) return;

        // Check if it's the player (can modify tags as needed)
        if (!other.CompareTag("Player") && !other.CompareTag("Human"))
            return;

        consumed = true;

        // Trigger event
        OnObstacleHit?.Invoke(damage);

        // Destroy or disable collider
        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
        else
        {
            Collider col = GetComponent<Collider>();
            if (col) col.enabled = false;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Also support collision detection
        if (consumed) return;

        if (!collision.gameObject.CompareTag("Player") && !collision.gameObject.CompareTag("Human"))
            return;

        consumed = true;

        OnObstacleHit?.Invoke(damage);

        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
        else
        {
            Collider col = GetComponent<Collider>();
            if (col) col.enabled = false;
        }
    }
}

