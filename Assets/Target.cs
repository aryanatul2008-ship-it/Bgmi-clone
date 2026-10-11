using UnityEngine;

public class Target : MonoBehaviour
{
    public float health = 50f;

    public void TakeDamage(float amount)
    {
        health -= amount;

        // Destroy the object when health hits zero
        if (health <= 0f)
        {
            Destroy(gameObject);
        }
    }
}