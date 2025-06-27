using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Settings")]
    public float damage = 10f;
    public float lifetime = 5f;

    private string damageTag;

    public void SetDamageTag(string tag)
    {
        this.damageTag = tag;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Companion"))
        {
            return;
        }

        if (other.CompareTag(damageTag))
        {
            if (damageTag == "Enemy")
            {
                EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                }
            }
            else if (damageTag == "Player")
            {
                PlayerController player = other.GetComponentInParent<PlayerController>();
                if (player != null)
                {
                    player.TakeDamage(damage);
                }
            }
            
            Destroy(gameObject);
            return;
        }
        if (!other.isTrigger && !other.CompareTag("Player") && !other.CompareTag("Enemy"))
        {
            Destroy(gameObject);
        }
    }
}