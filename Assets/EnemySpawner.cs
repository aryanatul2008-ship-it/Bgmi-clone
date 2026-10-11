using UnityEngine;
using UnityEngine.AI;

/// Spawns bots at random points on the NavMesh around "center",
/// never closer than minDistanceFromPlayer to the Player (tag "Player").
public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public int count = 15;
    public float radius = 80f;
    public float minDistanceFromPlayer = 12f;
    public Transform center;

    Transform player;

    void Start()
    {
        if (center == null) center = transform;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        for (int i = 0; i < count; i++) SpawnOne();
    }

    void SpawnOne()
    {
        for (int tries = 0; tries < 50; tries++)
        {
            Vector3 p = center.position + Random.insideUnitSphere * radius;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 10f, NavMesh.AllAreas)) continue;

            if (player != null)
            {
                Vector3 d = hit.position - player.position;
                d.y = 0f;
                if (d.magnitude < minDistanceFromPlayer) continue; // too close, try another point
            }

            Instantiate(enemyPrefab, hit.position, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            return;
        }
        Debug.LogWarning("EnemySpawner: couldn't find a valid spawn point. Is the NavMesh baked and the radius big enough?");
    }
}