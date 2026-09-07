using System.Collections;
using UnityEngine;

public class LegStepper : MonoBehaviour
{
    [Header("References")]
    public Transform ikTarget;
    public Transform homeTransform;
    // FIX: Added LayerMask so the raycast hits the floor, not the character
    public LayerMask groundLayer;

    [Header("Step Thresholds")]
    public float stepDistance = 0.6f;
    public float stepAngle = 30f;

    [Header("Step Motion")]
    public float stepHeight = 0.3f;
    public float stepDuration = 0.25f;
    public float stepForwardOffset = 0.25f;
    public float footHeightOffset = 0f;

    [Header("Limits")]
    public float velocityThreshold = 0.02f;

    public bool isMoving { get; private set; }
    public Vector3 FootPosition => currentFootPosition;

    Vector3 currentFootPosition;

    void Start()
    {
        currentFootPosition = GetGroundedPosition(homeTransform.position);
        ikTarget.position = currentFootPosition;
    }

    void Update()
    {
        ikTarget.position = currentFootPosition;
    }

    public bool CanStep(Vector3 velocity)
    {
        if (isMoving) return false;

        // FIX: Measure distance on the 2D plane (X and Z only)!
        // This prevents the height of the spider from mathematically triggering a step.
        Vector2 footXZ = new Vector2(currentFootPosition.x, currentFootPosition.z);
        Vector2 homeXZ = new Vector2(homeTransform.position.x, homeTransform.position.z);
        float dist = Vector2.Distance(footXZ, homeXZ);

        if (dist > stepDistance)
            return true;

        Vector3 footDir = Vector3.ProjectOnPlane(
            currentFootPosition - homeTransform.position,
            Vector3.up
        ).normalized;

        Vector3 homeDir = Vector3.ProjectOnPlane(
            homeTransform.forward,
            Vector3.up
        ).normalized;

        float angle = Vector3.Angle(footDir, homeDir);
        return angle > stepAngle;
    }

    public void Step(Vector3 velocity)
    {
        Vector3 direction =
            velocity.magnitude > velocityThreshold
            ? velocity.normalized
            : homeTransform.forward;

        Vector3 target =
            GetGroundedPosition(
                homeTransform.position + direction * stepForwardOffset
            );

        StartCoroutine(MoveLeg(target));
    }

    // FIX: Optimized raycast to ensure it finds the floor beneath the home transform
    Vector3 GetGroundedPosition(Vector3 origin)
    {
        RaycastHit hit;
        // Start the ray 2 units up to ensure it starts above the ground
        Vector3 rayOrigin = origin + Vector3.up * 2f;

        // Added groundLayer to the Raycast parameters
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 10f, groundLayer))
        {
            return hit.point + hit.normal * footHeightOffset;
        }

        // If it fails to hit the ground, return the origin point at Y=0 (or original position)
        return origin;
    }

    IEnumerator MoveLeg(Vector3 target)
    {
        isMoving = true;

        Vector3 start = currentFootPosition;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.fixedDeltaTime / stepDuration;

            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * stepHeight;

            currentFootPosition = pos;

            yield return new WaitForFixedUpdate();
        }

        currentFootPosition = target;
        isMoving = false;
    }
}