using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using EZCameraShake;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    public enum BehaviorType { Melee, Ranged };

    [Header("Behavior")]
    public BehaviorType behaviorType = BehaviorType.Melee;

    [Header("References")]
    private NavMeshAgent agent;
    private Transform playerTarget;
    private PlayerController playerController;
    private Rigidbody2D rb;

    [Header("AI Settings")]
    public float wanderDistance = 5f;
    public float aggroRadius = 15f;
    public float pathUpdateRate = 0.5f;
    public float moveSpeed = 3.5f;

    [Header("Melee Settings")]
    public float retreatDistance = 2f;
    public float attackRadius = 1.5f;
    public float attackDamage = 10f;
    public float attackCooldown = 2f;

    [Header("Ranged Settings")]
    public LayerMask obstacleLayerMask;
    public GameObject projectilePrefab;
    public float shootingRange = 10f;
    public float stoppingDistance = 7f;
    public float fireRate = 1.5f;
    public float projectileSpeed = 8f;

    [Header("Health & UI")]
    public GameObject deathParticlePrefab;
    public EnemyHealthBar healthBar;
    public float maxHealth = 50f;
    public float currentHealth { get; private set; }

    private float pathUpdateTimer;
    private float fleeCheckTimer;
    private float attackTimer;
    private bool isRetreating;
    private RectInt roomBounds;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody2D>();

        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
    }

    void Start()
    {
        agent.speed = moveSpeed;
        attackTimer = attackCooldown;
        currentHealth = maxHealth;

        if (healthBar != null)
        {
            healthBar.Initialize(this);
        }
        else
        {
            Debug.LogWarning("EnemyHealthBar reference is not set on the EnemyAI script.", this);
        }

        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerTarget = playerController.transform;
        }
        else
        {
            Debug.LogError("EnemyAI: Could not find PlayerController in the scene! Disabling AI.", this);
            this.enabled = false;
        }
    }
    
    public void Initialize(Vector2Int gridPosition, Vector2Int size)
    {
        Vector2Int roomCenter = new Vector2Int(gridPosition.x * size.x, gridPosition.y * size.y);
        this.roomBounds = new RectInt(
            roomCenter.x - size.x / 2,
            roomCenter.y - size.y / 2,
            size.x,
            size.y
        );
    }
    
    void Update()
    {
        attackTimer += Time.deltaTime;
        pathUpdateTimer += Time.deltaTime;
        fleeCheckTimer += Time.deltaTime;
        
        if (pathUpdateTimer >= pathUpdateRate)
        {
            pathUpdateTimer = 0f;
            UpdateAIState();
        }
    }

    void FixedUpdate()
    {
        agent.nextPosition = rb.position;
        
        if (agent.hasPath && !agent.isStopped)
        {
            rb.linearVelocity = agent.velocity;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void TakeDamage(float damageAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damageAmount;
        
        if (currentHealth <= 0)
        {
            StartCoroutine(Die());
        }
    }

    private IEnumerator Die()
    {
        currentHealth = 0; 
        
        this.enabled = false;
        agent.isStopped = true;
        agent.enabled = false;

        CameraShaker.Instance.ShakeOnce(3f, 2f, 0.1f, 0.5f);
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }
        
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            sr.enabled = false;
        }
        
        if (healthBar != null)
        {
            healthBar.gameObject.SetActive(false);
        }

        if (deathParticlePrefab != null)
        {
            GameObject deathParticles = Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
            Destroy(deathParticles, 1.0f); 
        }
        yield return new WaitForSeconds(1.0f);
        Destroy(gameObject);
    }
    
    private void UpdateAIState()
    {
        if (playerTarget == null || playerController.isDead)
        {
            agent.isStopped = true;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);
        if (playerController.isCamouflaged || distanceToPlayer > aggroRadius || !roomBounds.Contains(new Vector2Int(Mathf.RoundToInt(playerTarget.position.x), Mathf.RoundToInt(playerTarget.position.y))))
        {
            if (!agent.hasPath || agent.remainingDistance < 0.5f)
            {
                SetRandomWanderDestination();
            }
            return;
        }
        switch (behaviorType)
        {
            case BehaviorType.Melee:
                HandleMeleeBehavior(distanceToPlayer);
                break;
            case BehaviorType.Ranged:
                HandleRangedBehavior(distanceToPlayer);
                break;
        }
    }

    private void SetRandomWanderDestination()
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized * wanderDistance;
        Vector3 randomPoint = transform.position + new Vector3(randomDirection.x, randomDirection.y, 0);

        randomPoint = ClampPositionToRoom(randomPoint);

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.isStopped = true;
        }
    }
    
    private void HandleMeleeBehavior(float distanceToPlayer)
    {
        if (isRetreating)
        {
            if (attackTimer >= attackCooldown)
            {
                isRetreating = false;
            }
            else if (distanceToPlayer < retreatDistance && fleeCheckTimer > 0.25f)
            {
                fleeCheckTimer = 0;
                FindBestFleePoint(retreatDistance + 1f);
            }
            return;
        }

        if (distanceToPlayer <= attackRadius && attackTimer >= attackCooldown)
        {
            attackTimer = 0f;
            playerController.TakeDamage(attackDamage);
            isRetreating = true;
            FindBestFleePoint(retreatDistance);
        }
        else
        {
            agent.isStopped = false;
            agent.stoppingDistance = 0;
            agent.SetDestination(ClampPositionToRoom(playerTarget.position));
        }
    }

    private void HandleRangedBehavior(float distanceToPlayer)
    {
        Vector2 directionToPlayer = (playerTarget.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstacleLayerMask);
        bool hasLineOfSight = hit.collider == null;

        if (distanceToPlayer < stoppingDistance)
        {
            agent.isStopped = false;
            if (fleeCheckTimer > 0.25f)
            {
                fleeCheckTimer = 0;
                FindBestFleePoint(stoppingDistance + 2f);
            }
        }
        else if (!hasLineOfSight)
        {
            agent.isStopped = false;
            agent.stoppingDistance = 0;
            agent.SetDestination(ClampPositionToRoom(playerTarget.position));
        }
        else if (distanceToPlayer <= shootingRange)
        {
            agent.isStopped = true;
        }
        else
        {
            agent.isStopped = false;
            agent.stoppingDistance = stoppingDistance;
            agent.SetDestination(ClampPositionToRoom(playerTarget.position));
        }

        if (hasLineOfSight && projectilePrefab != null && attackTimer >= fireRate)
        {
            attackTimer = 0f;
            directionToPlayer = (playerTarget.position - transform.position).normalized;
            Vector3 spawnPosition = transform.position + (Vector3)directionToPlayer * 0.7f;
            GameObject projectileGO = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
            
            Projectile projectile = projectileGO.GetComponent<Projectile>();
            if (projectile != null) 
            {
                projectile.SetDamageTag("Player");
                projectile.damage = this.attackDamage; 
            }

            projectileGO.GetComponent<Rigidbody2D>().linearVelocity = directionToPlayer * projectileSpeed;
        }
    }

    private void FindBestFleePoint(float idealDistance)
    {
        Vector2 directionFromPlayer = (transform.position - playerTarget.position).normalized;
        float bestScore = -1;
        Vector3 bestPoint = transform.position;

        for (int i = 0; i < 8; i++)
        {
            Quaternion rotation = Quaternion.Euler(0, 0, i * 45f);
            Vector3 potentialDirection = rotation * directionFromPlayer;
            Vector3 potentialPoint = transform.position + potentialDirection * idealDistance;

            if (NavMesh.SamplePosition(potentialPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                if (!roomBounds.Contains(new Vector2Int(Mathf.FloorToInt(hit.position.x), Mathf.FloorToInt(hit.position.y))))
                {
                    continue;
                }

                float distanceFromPlayer = Vector2.Distance(hit.position, playerTarget.position);
                float dot = Vector2.Dot(directionFromPlayer, (hit.position - transform.position).normalized);
                float score = distanceFromPlayer + dot * 2f; 

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = hit.position;
                }
            }
        }

        if (bestScore > -1)
        {
            agent.SetDestination(bestPoint);
        }
    }
    
    private Vector3 ClampPositionToRoom(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, roomBounds.xMin, roomBounds.xMax);
        position.y = Mathf.Clamp(position.y, roomBounds.yMin, roomBounds.yMax);
        return position;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRadius);

        if (behaviorType == BehaviorType.Melee)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRadius);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, retreatDistance);
        }
        else if (behaviorType == BehaviorType.Ranged)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, shootingRange);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, stoppingDistance);
        }
    }
}