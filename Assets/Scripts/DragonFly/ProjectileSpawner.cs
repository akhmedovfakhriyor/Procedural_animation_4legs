using UnityEngine;

/// <summary>
/// ProjectileSpawner.cs
/// Fires spheres at the dragonfly at a set interval.
/// Projectiles must be on the "Projectile" layer so DragonflyAgent can detect them.
/// Add a Trigger Collider to projectiles so OnTriggerEnter fires on the dragonfly.
/// </summary>
public class ProjectileSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform dragonfly;

    [Header("Spawning")]
    [SerializeField] float spawnInterval = 2.5f;
    [SerializeField] float projectileRadius = 0.25f;
    [SerializeField] float projectileLifetime = 6f;

    [Header("Throwing")]
    [SerializeField] float throwForce = 14f;
    [SerializeField] float aimRandomness = 1.8f;  // random aim scatter radius
    [SerializeField] float leadFactor = 0.4f;  // how far ahead of dragonfly to aim

    [Header("Layer")]
    [SerializeField] string projectileLayerName = "Projectile";

    float spawnTimer;
    int projectileLayer;

    void Start()
    {
        spawnTimer = spawnInterval;
        projectileLayer = LayerMask.NameToLayer(projectileLayerName);

        if (projectileLayer == -1)
            Debug.LogWarning("ProjectileSpawner: Layer '" + projectileLayerName +
                "' not found. Add it in Project Settings > Tags & Layers.");
    }

    void Update()
    {
        if (dragonfly == null) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnProjectile();
            spawnTimer = spawnInterval;
        }
    }

    void SpawnProjectile()
    {
        GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        proj.name = "Projectile";
        proj.transform.position = transform.position;
        proj.transform.localScale = Vector3.one * projectileRadius * 2f;

        if (projectileLayer != -1)
            proj.layer = projectileLayer;

        // Make the sphere collider a Trigger so DragonflyAgent.OnTriggerEnter fires
        SphereCollider sc = proj.GetComponent<SphereCollider>();
        if (sc != null) sc.isTrigger = true;

        Rigidbody rb = proj.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Lead the target slightly ahead of its current position
        Vector3 leadOffset = dragonfly.forward * leadFactor;
        Vector3 aimTarget = dragonfly.position + leadOffset;
        Vector3 randomOffset = Random.insideUnitSphere * aimRandomness;
        Vector3 finalDir = (aimTarget + randomOffset - transform.position).normalized;

        rb.AddForce(finalDir * throwForce, ForceMode.Impulse);

        Destroy(proj, projectileLifetime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        if (dragonfly != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawLine(transform.position, dragonfly.position);
        }
    }
}