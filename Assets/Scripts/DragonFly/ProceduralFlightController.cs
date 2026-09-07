using UnityEngine;

public class ProceduralFlightController : MonoBehaviour
{
    [Header("Snappy Point-to-Point Movement")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float rotationSpeed = 25f; // Fast, insect-like snapping

    [Header("Perlin Jitter (Visual Polish)")]
    [SerializeField] float noiseFrequency = 5f;
    [SerializeField] float posNoiseAmplitude = 0.05f;

    public bool trainingMode = true;
    public float NormalisedSpeed => 1f; // Simplified for ML observations

    Vector3 currentWaypoint;
    Vector3 noiseOffset;

    void Awake()
    {
        currentWaypoint = transform.position;
        noiseOffset = new Vector3(Random.value, Random.value, Random.value) * 100f;
    }

    public void SetWaypoint(Vector3 worldPosition)
    {
        currentWaypoint = worldPosition;
    }

    public void ResetState()
    {
        currentWaypoint = transform.position;
    }

    void FixedUpdate()
    {
        ApplySnappyPointToPointMovement();
        ApplySnappyRotation();
        ApplyPerlinJitter();
    }

    void ApplySnappyPointToPointMovement()
    {
        // Move DIRECTLY to the waypoint, independent of where we are looking.
        // This gives you the strict, snappy, object-avoiding movement.
        transform.position = Vector3.Lerp(transform.position, currentWaypoint, moveSpeed * Time.fixedDeltaTime);
    }

    void ApplySnappyRotation()
    {
        // Visually snap rotation to face the waypoint (so it doesn't look like it's flying backwards)
        Vector3 toWaypoint = currentWaypoint - transform.position;
        if (toWaypoint.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toWaypoint.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void ApplyPerlinJitter()
    {
        if (trainingMode) return; // Keep it perfectly still for the AI to learn easily

        float t = Time.time * noiseFrequency;
        float nx = (Mathf.PerlinNoise(t + noiseOffset.x, 0f) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(t + noiseOffset.y, 1f) - 0.5f) * 2f;
        float nz = (Mathf.PerlinNoise(t + noiseOffset.z, 2f) - 0.5f) * 2f;

        transform.position += new Vector3(nx, ny, nz) * posNoiseAmplitude;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(currentWaypoint, 0.3f);
        Gizmos.DrawLine(transform.position, currentWaypoint);
    }
}