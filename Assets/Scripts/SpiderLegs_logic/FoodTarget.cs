using UnityEngine;

public class FoodTarget : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float spawnRadius = 8f;
    public float spawnHeight = 5f;
    public int maxAttempts = 25;

    [Header("Ground Identification")]
    public string groundTag = "Ground";

    [Header("Visual Pulse")]
    public float pulseSpeed = 2f;
    public float pulseAmplitude = 0.15f;

    Vector3 baseScale;
    Vector3 centerPoint;

    void Start()
    {
        baseScale = transform.localScale;
        centerPoint = transform.position;

        SpawnOnGround();
    }

    void Update()
    {
        float s = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        transform.localScale = baseScale * s;
    }

    public void SpawnOnGround()
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 rand = Random.insideUnitCircle * spawnRadius;
            Vector3 origin = centerPoint + new Vector3(rand.x, spawnHeight, rand.y);

            // 🔥 Raycast ALL layers (IMPORTANT)
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f))
            {
                // ❌ If first thing hit is NOT ground → reject
                if (!hit.collider.CompareTag(groundTag))
                    continue;

                // ✅ Valid spawn
                transform.position = hit.point + Vector3.up * 0.1f;
                return;
            }
        }

        // fallback
        transform.position = centerPoint + Vector3.up * 0.2f;
    }
}