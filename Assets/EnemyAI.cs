using UnityEngine;
using UnityEngine.AI;

/// Patrol -> spot player (range + FOV + line of sight) -> chase -> shoot.
/// Needs: NavMeshAgent, Health, and a baked NavMesh. Player must have the "Player" tag.
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    public float sightRange = 40f;
    public float fieldOfView = 120f;
    public float hearRange = 12f;            // inside this range the bot notices you even from behind
    public float eyeHeight = 1.6f;
    public Vector3 aimOffset = new Vector3(0f, 0.3f, 0f); // offset from the Player's pivot (pivot = capsule centre)

    [Header("Movement")]
    public float attackRange = 25f;
    public float patrolRadius = 30f;

    [Header("Shooting")]
    public float fireRate = 2f;      // shots per second
    public float damage = 6f;
    public float inaccuracy = 0.06f; // higher = worse aim
    public LayerMask sightMask = ~0;

    NavMeshAgent agent;
    Transform target;
    Health targetHealth;
    float nextShot;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            target = p.transform;
            targetHealth = p.GetComponentInParent<Health>();
        }
        else
        {
            Debug.LogError("EnemyAI: no object with the tag 'Player' found. Set the Player's Tag to Player.");
        }
        PickPatrolPoint();
    }

    void Update()
    {
        if (target == null || (targetHealth != null && targetHealth.IsDead)) { Patrol(); return; }

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist < sightRange && CanSeeTarget(dist))
        {
            // Chase until in range, then stop and shoot
            if (dist > attackRange * 0.8f)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
            }
            else agent.isStopped = true;

            FaceTarget();
            if (dist <= attackRange) TryShoot(dist);
        }
        else Patrol();
    }

    bool CanSeeTarget(float dist)
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toTarget = (target.position + aimOffset) - eye;

        // The field-of-view check only applies outside hearRange
        if (dist > hearRange && Vector3.Angle(transform.forward, toTarget) > fieldOfView * 0.5f) return false;

        if (Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit, sightRange, sightMask, QueryTriggerInteraction.Ignore))
            return hit.transform.root == target.root;
        return false;
    }

    void FaceTarget()
    {
        Vector3 flat = target.position - transform.position;
        flat.y = 0;
        if (flat.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 8f * Time.deltaTime);
    }

    void TryShoot(float dist)
    {
        if (Time.time < nextShot) return;
        nextShot = Time.time + 1f / fireRate;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 aim = (target.position + aimOffset) - eye;
        aim += Random.insideUnitSphere * inaccuracy * dist; // worse accuracy at distance
        if (Physics.Raycast(eye, aim.normalized, out RaycastHit hit, sightRange, sightMask, QueryTriggerInteraction.Ignore))
        {
            Health h = hit.collider.GetComponentInParent<Health>();
            if (h != null && h.isPlayer) h.TakeDamage(damage);
            Debug.DrawLine(eye, hit.point, Color.magenta, 0.2f);
        }
    }

    void Patrol()
    {
        agent.isStopped = false;
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 1f)
            PickPatrolPoint();
    }

    void PickPatrolPoint()
    {
        Vector3 random = transform.position + Random.insideUnitSphere * patrolRadius;
        if (NavMesh.SamplePosition(random, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }
}