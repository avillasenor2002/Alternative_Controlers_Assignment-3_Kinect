using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temple Run style track generation & obstacle/pickup random system
/// </summary>
public class TempleRunTrackManager : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("Player (or camera) Transform, used to determine front/back position")]
    public Transform player;

    [Header("Track Settings")]
    [Tooltip("Track Prefab (must have TrackSegment component)")]
    public GameObject trackPrefab;

    [Tooltip("Length of each track in Z-axis direction")]
    public float trackLength = 10f;

    [Tooltip("Track movement speed (towards player)")]
    public float trackSpeed = 8f;

    [Tooltip("Number of initial tracks to spawn")]
    public int initialTrackCount = 5;

    [Tooltip("Ensure track length ahead of player (Z distance)")]
    public float spawnDistanceAhead = 60f;

    [Tooltip("Distance behind player where tracks can be recycled (Z distance)")]
    public float despawnBehindDistance = 40f;

    [Header("Spawn Contents Settings")]
    [Tooltip("Available obstacle Prefab list")]
    public List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Tooltip("Available pickup Prefab list")]
    public List<GameObject> pickupPrefabs = new List<GameObject>();

    [Range(0f, 1f)]
    [Tooltip("Probability of generating obstacles at each spawn point")]
    public float obstacleSpawnChance = 0.6f;

    [Range(0f, 1f)]
    [Tooltip("Probability of generating pickups at each spawn point (if no obstacles are generated)")]
    public float pickupSpawnChance = 0.3f;

    // Internal use: current tracks in play (queue for easy dequeue/enqueue from start/end)
    private readonly Queue<GameObject> activeTracks = new Queue<GameObject>();

    private void Start()
    {
        if (player == null)
        {
            // Simple try to auto-find, if not found report error
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (trackPrefab == null)
        {
            Debug.LogError("TempleRunTrackManager: trackPrefab is not set!");
            enabled = false;
            return;
        }

        // Initialize: place initialTrackCount tracks in front of player
        float startZ = player != null ? player.position.z : 0f;
        for (int i = 0; i < initialTrackCount; i++)
        {
            Vector3 pos = new Vector3(0f, 0f, startZ + i * trackLength);
            SpawnNewTrack(pos);
        }
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        // 1. Move all tracks backward towards player (assuming player is near origin)
        foreach (GameObject track in activeTracks)
        {
            if (track != null)
            {
                track.transform.Translate(0f, 0f, -trackSpeed * delta, Space.World);
            }
        }

        // 2. Check if need to spawn another track ahead
        EnsureTrackAhead();

        // 3. Check if any track is too far behind player, can be recycled
        RecycleBehindTrack();
    }

    /// <summary>
    /// Spawn a new track at specified position and generate contents
    /// </summary>
    private GameObject SpawnNewTrack(Vector3 position)
    {
        GameObject newTrack = Instantiate(trackPrefab, position, Quaternion.identity, transform);

        TrackSegment segment = newTrack.GetComponent<TrackSegment>();
        if (segment != null)
        {
            segment.GenerateContents(obstaclePrefabs, pickupPrefabs, obstacleSpawnChance, pickupSpawnChance);
        }
        else
        {
            Debug.LogWarning("TempleRunTrackManager: trackPrefab 上没有 TrackSegment 组件，无法生成障碍物/拾取物。");
        }

        activeTracks.Enqueue(newTrack);
        return newTrack;
    }

    /// <summary>
    /// Ensure player has enough tracks ahead, if not, spawn one more at the front
    /// </summary>
    private void EnsureTrackAhead()
    {
        if (activeTracks.Count == 0) return;

        // Find the frontmost track (Z max)
        float maxZ = float.MinValue;
        foreach (GameObject track in activeTracks)
        {
            if (track != null && track.transform.position.z > maxZ)
            {
                maxZ = track.transform.position.z;
            }
        }

        float playerZ = player != null ? player.position.z : 0f;

        // If the distance from the frontmost track to player is less than spawnDistanceAhead, spawn one more at the front
        while (maxZ - playerZ < spawnDistanceAhead)
        {
            maxZ += trackLength;
            SpawnNewTrack(new Vector3(0f, 0f, maxZ));
        }
    }

    /// <summary>
    /// Recycle tracks that are too far behind player: not destroy, but move to front for reuse
    /// </summary>
    private void RecycleBehindTrack()
    {
        if (activeTracks.Count == 0) return;

        float playerZ = player != null ? player.position.z : 0f;

        // Here only check the first track per frame, if it is too far behind player, recycle and enqueue to the front
        GameObject firstTrack = activeTracks.Peek();
        if (firstTrack == null) return;

        float distanceBehind = playerZ - firstTrack.transform.position.z;

        if (distanceBehind > despawnBehindDistance)
        {
            // Dequeue this track
            firstTrack = activeTracks.Dequeue();

            // Find the Z of the frontmost track
            float maxZ = float.MinValue;
            foreach (GameObject track in activeTracks)
            {
                if (track != null && track.transform.position.z > maxZ)
                {
                    maxZ = track.transform.position.z;
                }
            }

            if (maxZ == float.MinValue)
            {
                maxZ = playerZ;
            }

            // Move this track to the front
            float newZ = maxZ + trackLength;
            firstTrack.transform.position = new Vector3(0f, 0f, newZ);

            // Clear+re-generate obstacles/pickups
            TrackSegment segment = firstTrack.GetComponent<TrackSegment>();
            if (segment != null)
            {
                segment.ClearContents();
                segment.GenerateContents(obstaclePrefabs, pickupPrefabs, obstacleSpawnChance, pickupSpawnChance);
            }

            // Re-enqueue to the queue
            activeTracks.Enqueue(firstTrack);
        }
    }
}
