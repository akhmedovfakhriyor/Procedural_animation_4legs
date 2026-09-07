using UnityEngine;

/// <summary>
/// DragonflyMovement.cs
/// Controls the dragonfly cube with insect-accurate procedural movement:
///   - Constant forward flight
///   - Food seeking with sharp snapping rotations
///   - Projectile avoidance (SphereCast)
///   - Perlin noise micro-jitter on position and rotation
///   - Random speed bursts / hover pauses
/// </summary>
public class DragonflyMovement : MonoBehaviour
{
    // ── References ───────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] Transform foodTarget;
    [SerializeField] LayerMask projectileLayer;

    // ── Base Movement ─────────────────────────────────────────────────
    [Header("Base Movement")]
    [SerializeField] float baseSpeed = 4f;
    [SerializeField] float rotationSpeed = 8f;
    [SerializeField] float minForwardSpeed = 1.5f;

    // ── Darting Bursts ────────────────────────────────────────────────
    [Header("Darting Bursts")]
    [SerializeField] float dartMultiplier = 2.8f;
    [SerializeField] float hoverMultiplier = 0.35f;
    [SerializeField] float dartDuration = 0.25f;
    [SerializeField] float hoverDuration = 0.4f;
    [SerializeField] float dartInterval = 1.2f;
    [SerializeField] float speedLerpRate = 12f;

    // ── Perlin Noise Jitter ───────────────────────────────────────────
    [Header("Perlin Noise Jitter")]
    [SerializeField] float noiseFrequency = 4f;
    [SerializeField] float posNoiseAmplitude = 0.06f;
    [SerializeField] float rotNoiseAmplitude = 6f;

    // ── Obstacle Avoidance ────────────────────────────────────────────
    // FIX: Removed evadeStrength field — it was assigned but never read.
    // Evasion direction is now purely geometry-based (reflect + up bias).
    [Header("Obstacle Avoidance")]
    [SerializeField] float detectionRadius = 1.2f;
    [SerializeField] float detectionRange = 5f;
    [SerializeField] float dangerZoneRadius = 2.5f;

    // ── private state ─────────────────────────────────────────────────
    float currentSpeed;
    float dartTimer;
    bool isDarting;
    float dartPhaseTimer;
    Vector3 noiseOffset;

    void Start()
    {
        currentSpeed = baseSpeed;
        dartTimer = Random.Range(0f, dartInterval);

        noiseOffset = new Vector3(
            Random.Range(0f, 100f),
            Random.Range(0f, 100f),
            Random.Range(0f, 100f)
        );
    }

    void Update()
    {
        Vector3 steerDir = ComputeSteerDirection();

        ApplyRotation(steerDir);
        ApplyDartingSpeed();
        ApplyForwardMovement();
        ApplyPerlinJitter();
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. STEERING — dodge takes priority over food
    // ─────────────────────────────────────────────────────────────────
    Vector3 ComputeSteerDirection()
    {
        if (Physics.SphereCast(
                transform.position,
                detectionRadius,
                transform.forward,
                out RaycastHit hit,
                detectionRange,
                projectileLayer))
        {
            // Reflect the incoming threat direction off its surface normal,
            // then bias upward — dragonflies dart upward when startled
            Vector3 incomingDir = (transform.position - hit.point).normalized;
            Vector3 evadeDir = Vector3.Reflect(-incomingDir, hit.normal);
            evadeDir += Vector3.up * 0.4f;

            if (hit.distance < dangerZoneRadius)
                return evadeDir.normalized;
        }

        if (foodTarget != null)
            return (foodTarget.position - transform.position).normalized;

        return transform.forward;
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. ROTATION — high-speed Slerp for sharp insect-like snapping
    // ─────────────────────────────────────────────────────────────────
    void ApplyRotation(Vector3 steerDir)
    {
        if (steerDir == Vector3.zero) return;

        Quaternion targetRot = Quaternion.LookRotation(steerDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. DARTING SPEED — burst → hover → repeat cycle
    // ─────────────────────────────────────────────────────────────────
    void ApplyDartingSpeed()
    {
        dartTimer -= Time.deltaTime;

        if (dartTimer <= 0f && !isDarting)
        {
            isDarting = true;
            dartPhaseTimer = dartDuration;
            dartTimer = dartInterval;
        }

        float targetSpeed;

        if (isDarting)
        {
            dartPhaseTimer -= Time.deltaTime;

            if (dartPhaseTimer > 0f)
                targetSpeed = baseSpeed * dartMultiplier;
            else if (dartPhaseTimer > -hoverDuration)
                targetSpeed = Mathf.Max(baseSpeed * hoverMultiplier, minForwardSpeed);
            else
            {
                isDarting = false;
                targetSpeed = baseSpeed;
            }
        }
        else
        {
            targetSpeed = baseSpeed;
        }

        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, speedLerpRate * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. FORWARD MOVEMENT — always moving, never stops
    // ─────────────────────────────────────────────────────────────────
    void ApplyForwardMovement()
    {
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────────
    // 5. PERLIN JITTER
    //    Perlin returns [0,1] — remapped to [-1,1] via (val - 0.5) * 2
    //    Three independent noise samples (different Y seed) for X, Y, Z
    //    so axes don't oscillate in sync with each other.
    // ─────────────────────────────────────────────────────────────────
    void ApplyPerlinJitter()
    {
        float t = Time.time * noiseFrequency;

        float nx = (Mathf.PerlinNoise(t + noiseOffset.x, 0f) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(t + noiseOffset.y, 1f) - 0.5f) * 2f;
        float nz = (Mathf.PerlinNoise(t + noiseOffset.z, 2f) - 0.5f) * 2f;

        transform.position += new Vector3(nx, ny, nz) * posNoiseAmplitude;

        float rollJitter = nx * rotNoiseAmplitude;
        float pitchJitter = ny * rotNoiseAmplitude * 0.5f;
        transform.rotation *= Quaternion.Euler(pitchJitter, 0f, rollJitter);
    }

    public void SetFoodTarget(Transform t) => foodTarget = t;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * detectionRange, detectionRadius);
        Gizmos.DrawRay(transform.position, transform.forward * detectionRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, dangerZoneRadius);
    }
}