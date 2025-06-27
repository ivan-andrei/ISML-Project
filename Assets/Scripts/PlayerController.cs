using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed;
    public float moveSmoothness;

    [Header("Visuals")]
    public Color movingColor = new Color(0.62f, 0.37f, 0.37f);
    public Color idleColor = new Color(0.46f, 0.3f, 0.18f);
    public float velocityThreshold = 0.01f;

    [Header("Health")]
    public Slider healthSlider;
    public float maxHealth = 100f;
    public float healthBarSmoothTime = 0.1f;
    
    public float currentHealth { get; private set; }
    public bool isDead { get; private set; }

    [Header("Combat")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 12f;
    public float fireRate = 0.5f;

    private float fireTimer;

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;

    Vector3 moveDir, moveAmount, moveVelocity;
    public bool isCamouflaged { get; private set; }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        currentHealth = maxHealth;
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = maxHealth;
        }
        else
        {
            Debug.LogWarning("Health Slider is not assigned in the PlayerController!", this);
        }
        isDead = false; 
        
        UpdateColor(); 
    }

    void Update()
    {
        fireTimer += Time.deltaTime;

        MovementHandler();
        CombatHandler();
        UpdateColor();
        UpdateHealthUI();

        // if (Input.GetKeyDown(KeyCode.H))
        // {
        //     TakeDamage(10);
        // }
        // if (Input.GetKeyDown(KeyCode.J))
        // {
        //     Heal(10);
        // }

        if (healthSlider.value <= 0.01f)
            Die();
    }

    void CombatHandler()
    {
        if (!isCamouflaged && Input.GetMouseButton(0) && fireTimer >= fireRate)
        {
            Shoot();
        }
    }

    void Shoot()
    {
        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogWarning("Player shooting is not set up correctly (missing projectile or fire point).");
            return;
        }

        fireTimer = 0f;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0; 
        Vector2 shootDirection = (mousePosition - firePoint.position).normalized;

        GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        
        Projectile projectile = projectileGO.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.SetDamageTag("Enemy");
        }

        projectileGO.GetComponent<Rigidbody2D>().linearVelocity = shootDirection * projectileSpeed;
    }

    private void UpdateHealthUI()
    {
        if (healthSlider == null) return;

        float targetValue = currentHealth;
        healthSlider.value = Mathf.Lerp(healthSlider.value, targetValue, healthBarSmoothTime);
        if (healthSlider.value <= 0.01f)
            healthSlider.value = 0f;
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"Player took {damageAmount} damage. Current Health: {currentHealth}");
    }

    public void Heal(float healAmount)
    {
        if (isDead) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"Player healed {healAmount}. Current Health: {currentHealth}");
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("Player has died.");
        this.enabled = false; 
        rb.linearVelocity = Vector2.zero;
        spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f);
    }

    void FixedUpdate()
    {
        rb.linearVelocity = moveAmount * moveSpeed * 100f * Time.deltaTime;
    }

    void MovementHandler()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveDir = (transform.right * horizontal + transform.up * vertical).normalized;
        moveAmount = Vector3.SmoothDamp(moveAmount, moveDir, ref moveVelocity, moveSmoothness);
    }

    void UpdateColor()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && !isCamouflaged && rb.linearVelocity.magnitude < velocityThreshold)
        {
            isCamouflaged = true;
        }
        else if (rb.linearVelocity.magnitude >= velocityThreshold)
        {
            isCamouflaged = false;
        }

        if (isCamouflaged)
        {
            spriteRenderer.color = idleColor;
        }
        else
        {
            spriteRenderer.color = movingColor;
        }
    }
}