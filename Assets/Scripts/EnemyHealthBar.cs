using UnityEngine;

public class EnemyHealthBar : MonoBehaviour
{
    public Transform healthFillTransform;
    public float smoothTime = 0.1f;

    private EnemyAI enemyAI;

    public void Initialize(EnemyAI enemy)
    {
        this.enemyAI = enemy;
    }

    void Update()
    {
        if (enemyAI == null) return;

        float targetXScale = enemyAI.currentHealth / enemyAI.maxHealth;
        targetXScale = Mathf.Clamp01(targetXScale);

        Vector3 currentScale = healthFillTransform.localScale;
        float smoothedXScale = Mathf.Lerp(currentScale.x, targetXScale, smoothTime);
        healthFillTransform.localScale = new Vector3(smoothedXScale, currentScale.y, currentScale.z);
    }
}