using UnityEngine;

public class TrackSpawnPoint : MonoBehaviour
{
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