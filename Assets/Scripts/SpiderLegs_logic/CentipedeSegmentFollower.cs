using UnityEngine;

public class CentipedeSegmentFollower : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform targetSegment;

    [Header("Follow Settings")]
    public float followDistance = 1.2f;
    public float positionLerpSpeed = 6f;
    public float rotationLerpSpeed = 5f;   // lower = smoother tail whip

    [Header("Ground Alignment")]
    public float maxSlopeAngle = 45f;

    Vector3 velocityRef;
    Vector3 prevPosition;

    void Start()
    {
        prevPosition = transform.position;
    }

    void LateUpdate()
    {
        if (!targetSegment) return;

        FollowPosition();
        FollowRotation();

        prevPosition = transform.position;
    }

    void FollowPosition()
    {
        Vector3 targetPos =
            targetSegment.position -
            targetSegment.forward * followDistance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref velocityRef,
            1f / positionLerpSpeed
        );
    }

    void FollowRotation()
    {
        // Derive rotation from how THIS segment actually moved this frame,
        // not from the leader's rotation. This decouples the follower from
        // ML's snappy per-decision rotation changes on the head segment.
        Vector3 moveDelta = transform.position - prevPosition;

        // Only rotate if we actually moved — avoids garbage direction when still
        if (moveDelta.sqrMagnitude > 0.00001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(moveDelta.normalized, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRot,
                Time.deltaTime * rotationLerpSpeed
            );
        }
        // If not moving: hold current rotation (no snapping to leader's rotation)
    }
}