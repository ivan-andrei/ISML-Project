using UnityEngine;
using UnityEngine.AI;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody2D))]
public class CompanionAI : MonoBehaviour
{
    public enum CompanionType { Fighter, Healer };
    private enum AgentState { Following, Attacking, Healing, Repositioning };

    [Header("Agent Core")]
    public CompanionType companionType = CompanionType.Fighter;
    private AgentState currentState;

    [Header("References")]
    private NavMeshAgent agent;
    private Transform playerTarget;
    private PlayerController playerController;
    private Rigidbody2D rb;
    private Transform currentEnemyTarget;

    [Header("Common Settings")]
    public float followDistance = 3f;
    public float moveSpeed = 4f;
    public float perceptionRate = 0.5f;

    [Header("Fighter Agent Settings")]
    public LayerMask obstacleLayerMask;
    public GameObject fighterProjectilePrefab;
    public Transform firePoint;
    public float attackRange = 12f;
    public float fireRate = 1f;
    public float projectileSpeed = 15f;

    [Header("Healer Agent Settings")]
    public GameObject healParticlePrefab;
    public float healAmount = 25f;
    public float healCooldown = 8f;
    public float healRange = 5f;
    [Range(0, 1)]
    public float healThreshold = 0.6f;

    // Internal timers
    private float perceptionTimer;
    private float actionTimer;
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

        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerTarget = playerController.transform;
        }
        else
        {
            Debug.LogError("CompanionAI: Could not find PlayerController! Disabling agent.", this);
            this.enabled = false;
            return;
        }

        currentState = AgentState.Following;
        actionTimer = (companionType == CompanionType.Healer) ? healCooldown : fireRate;
    }

    void OnEnable()
    {
        RoomCameraController.OnPlayerEnteredNewRoom += UpdateRoomBounds;
    }

    void OnDisable()
    {
        RoomCameraController.OnPlayerEnteredNewRoom -= UpdateRoomBounds;
    }

    private void UpdateRoomBounds(Vector2Int gridPosition, Vector2Int size)
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
        perceptionTimer += Time.deltaTime;
        actionTimer += Time.deltaTime;

        if (perceptionTimer >= perceptionRate)
        {
            perceptionTimer = 0f;
            UpdateAgentState();
        }

        ExecuteCurrentState();
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

    void UpdateAgentState()
    {
        if (playerTarget == null) return;

        switch (companionType)
        {
            case CompanionType.Fighter:
                FindClosestEnemy(); 
                break;

            case CompanionType.Healer:
                bool needsHealing = playerController.currentHealth < (playerController.maxHealth * healThreshold);
                bool canHeal = actionTimer >= healCooldown;
                float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);

                if (needsHealing && canHeal && distanceToPlayer <= healRange)
                {
                    currentState = AgentState.Healing;
                }
                else
                {
                    currentState = AgentState.Following;
                }
                break;
        }
    }
    void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case AgentState.Following:
                HandleFollowing();
                break;
            case AgentState.Attacking:
                HandleAttacking();
                break;
            case AgentState.Healing:
                HandleHealing();
                break;
        }
    }

    void HandleFollowing()
    {
        agent.stoppingDistance = followDistance;
        agent.SetDestination(ClampPositionToRoom(playerTarget.position));
    }
    void HandleAttacking()
    {
        if (currentEnemyTarget == null)
        {
            currentState = AgentState.Following;
            agent.isStopped = false;
            HandleFollowing();
            return;
        }

        Vector2 directionToEnemy = (currentEnemyTarget.position - transform.position).normalized;
        float distanceToEnemy = Vector2.Distance(transform.position, currentEnemyTarget.position);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToEnemy, distanceToEnemy, obstacleLayerMask);
        bool hasLineOfSight = hit.collider == null;

        if (!hasLineOfSight)
        {
            agent.isStopped = false;
            agent.stoppingDistance = 0;
            agent.SetDestination(ClampPositionToRoom(currentEnemyTarget.position));
        }
        else if (distanceToEnemy > attackRange)
        {
            agent.isStopped = false;
            agent.stoppingDistance = attackRange * 0.8f;
            agent.SetDestination(ClampPositionToRoom(currentEnemyTarget.position));
        }
        else
        {
            agent.isStopped = true;
            if (actionTimer >= fireRate)
            {
                actionTimer = 0f;
                Vector2 fireDirection = (currentEnemyTarget.position - firePoint.position).normalized;
                GameObject projGO = Instantiate(fighterProjectilePrefab, firePoint.position, Quaternion.identity);
                
                Projectile projectile = projGO.GetComponent<Projectile>();
                if (projectile != null)
                {
                    projectile.SetDamageTag("Enemy");
                }
                
                projGO.GetComponent<Rigidbody2D>().linearVelocity = fireDirection * projectileSpeed;
            }
        }
    }

    private Vector3 ClampPositionToRoom(Vector3 position)
    {
        if(roomBounds.size == Vector2.zero) return position;

        position.x = Mathf.Clamp(position.x, roomBounds.xMin, roomBounds.xMax);
        position.y = Mathf.Clamp(position.y, roomBounds.yMin, roomBounds.yMax);
        return position;
    }

    void HandleHealing()
    {
        Debug.Log("Healer Companion is healing the player.");
        playerController.Heal(healAmount);
        
        if (healParticlePrefab != null && playerTarget != null)
        {
            GameObject healParticles = Instantiate(healParticlePrefab, playerTarget.position, Quaternion.identity);
            Destroy(healParticles, 1.0f);
        }

        actionTimer = 0f;
        currentState = AgentState.Following;
    }

    void FindClosestEnemy()
    {
        currentEnemyTarget = null;
        float closestDistance = Mathf.Infinity;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange * 1.5f);

        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyAI enemy = hit.GetComponentInParent<EnemyAI>();
                if (enemy != null && enemy.currentHealth > 0)
                {
                    float distance = Vector2.Distance(transform.position, hit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        currentEnemyTarget = hit.transform;
                    }
                }
            }
        }
        if (currentEnemyTarget != null)
        {
            currentState = AgentState.Attacking;
        }
        else
        {
            currentState = AgentState.Following;
        }
    }
}