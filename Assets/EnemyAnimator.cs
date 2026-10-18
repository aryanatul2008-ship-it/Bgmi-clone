using UnityEngine;
using UnityEngine.AI;

/// Put on the Enemy ROOT object (the one with NavMeshAgent + Health + EnemyAI).
/// Drives the bot's character model: idle / walk / run from the agent's speed, and a death animation.
/// When the bot dies it stops thinking, stops moving and its colliders switch off,
/// so a dead bot can't shoot you and bullets pass through the body.
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class EnemyAnimator : MonoBehaviour
{
    public Animator animator;        // on the character model (a child). Found automatically.
    public float damping = 0.1f;

    NavMeshAgent agent;
    Health health;
    bool dead;

    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int DeadParam = Animator.StringToHash("Dead");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("EnemyAnimator: no Animator found. Add the bot's character model as a child of the Enemy.");
            enabled = false;
            return;
        }
        animator.applyRootMotion = false; // the NavMeshAgent moves the bot, not the animation
    }

    void Update()
    {
        if (dead) return;

        if (health.IsDead) { Die(); return; }

        animator.SetFloat(SpeedParam, agent.velocity.magnitude, damping, Time.deltaTime);
    }

    void Die()
    {
        dead = true;

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;

        if (agent.enabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }

        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        animator.SetFloat(SpeedParam, 0f);
        animator.SetBool(DeadParam, true);
    }
}