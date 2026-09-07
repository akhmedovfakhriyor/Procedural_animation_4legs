using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// SpiderAgent — obstacle avoidance + dome climbing version
/// Ray sensors are added as components in the Inspector (RayPerceptionSensorComponent3D)
/// This script handles: movement, rewards, height control
public class SpiderAgent : Agent
{
    [Header("References")]
    public Transform body;
    public LegStepper[] legs;
    public Transform foodTarget;

    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float turnSpeed = 90f;
    public float maxEpisodeDistance = 25f;

    [Header("Body Height")]
    public float bodyHeightOffset = 0.5f;
    public float minGroundOffset = 0.2f;
    public float maxGroundOffset = 1.5f;
    public float maxRiseSpeed = 3f;
    public LayerMask groundLayer;

    [Header("Rewards")]
    public float reachFoodReward = 5f;
    public float fallPenalty = -2f;
    public float progressRewardScale = 0.12f;
    public float timePenaltyPerStep = -0.002f;
    public float facingRewardScale = 0.01f;
    public float elevationRewardScale = 0.05f;
    public float wallStuckPenalty = -0.005f;
    public float obstacleProximityScale = 0.003f;

    [Header("Episode")]
    public float spawnRadius = 8f;
    public float successRadius = 0.8f;
    public float fallTiltLimit = 55f;

    [Header("Foods Per Episode")]
    public int foodsPerEpisode = 5; // episode ends after collecting this many

    [Header("Obstacle Detection")]
    public float obstacleCheckDistance = 1.2f;
    public LayerMask obstacleLayer;

    // ── private ────────────────────────────────────────────────────
    Rigidbody rb;
    Vector3 startPosition;
    float prevDistToFood;
    float prevBodyY;
    int foodsCollected; // tracks how many foods eaten this episode

    // ───────────────────────────────────────────────────────────────
    public override void Initialize()
    {
        rb = body.GetComponent<Rigidbody>();
        startPosition = body.position;
    }

    public override void OnEpisodeBegin()
    {
        foodsCollected = 0;

        body.position = new Vector3(startPosition.x, body.position.y, startPosition.z);
        body.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        PlaceFoodTarget();

        prevDistToFood = Vector3.Distance(body.position, foodTarget.position);
        prevBodyY = body.position.y;    }

    // ── OBSERVATIONS ────────────────────────────────────────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toFood = foodTarget.position - body.position;
        Vector3 toFoodFlat = new Vector3(toFood.x, 0f, toFood.z);
        Vector3 localToFood = body.InverseTransformDirection(toFoodFlat.normalized);

        sensor.AddObservation(localToFood);                             // 3
        sensor.AddObservation(toFood.magnitude / maxEpisodeDistance);   // 1

        float elevationDiff = Mathf.Clamp(
            (foodTarget.position.y - body.position.y) / 5f, -1f, 1f);
        sensor.AddObservation(elevationDiff);                           // 1

        Vector3 localVel = rb != null
            ? body.InverseTransformDirection(rb.velocity) / moveSpeed
            : Vector3.zero;
        sensor.AddObservation(localVel);                                // 3

        sensor.AddObservation(body.up);                                 // 3

        foreach (LegStepper leg in legs)
        {
            Vector3 offset = leg.FootPosition - leg.homeTransform.position;
            Vector3 localOff = body.InverseTransformDirection(offset);
            sensor.AddObservation(localOff / leg.stepDistance);         // 3
            sensor.AddObservation(leg.isMoving ? 1f : 0f);             // 1
        }
    }

    // ── ACTIONS ─────────────────────────────────────────────────────
    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turnInput = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        body.Rotate(0f, turnInput * turnSpeed * Time.fixedDeltaTime, 0f, Space.World);

        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        Vector3 delta = forward * moveInput * moveSpeed * Time.fixedDeltaTime;
        body.position += new Vector3(delta.x, 0f, delta.z);

        // 1. 3D progress toward food
        float distNow = Vector3.Distance(body.position, foodTarget.position);
        AddReward((prevDistToFood - distNow) * progressRewardScale);
        prevDistToFood = distNow;

        // 2. Elevation reward
        float foodElev = foodTarget.position.y - body.position.y;
        if (foodElev > 0.5f)
        {
            float upwardProgress = body.position.y - prevBodyY;
            AddReward(upwardProgress * elevationRewardScale);
        }
        prevBodyY = body.position.y;

        // 3. Facing reward
        Vector3 toFoodFlat = Vector3.ProjectOnPlane(
            foodTarget.position - body.position, Vector3.up).normalized;
        Vector3 bodyForward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        AddReward(Vector3.Dot(bodyForward, toFoodFlat) * facingRewardScale);

        // 4. Obstacle proximity penalty
        AddReward(GetObstaclePenalty());

        // 5. Time penalty
        AddReward(timePenaltyPerStep);

        // ── Terminal conditions ───────────────────────────────────

        float tilt = Vector3.Angle(body.up, Vector3.up);
        if (tilt > fallTiltLimit)
        {
            AddReward(fallPenalty);
            EndEpisode();
            return;
        }

        if (Vector3.Distance(body.position, startPosition) > maxEpisodeDistance)
        {
            EndEpisode();
            return;
        }

        // Food collected
        if (distNow < successRadius)
        {
            AddReward(reachFoodReward);
            foodsCollected++;

            if (foodsCollected >= foodsPerEpisode)
            {
                // Collected all foods for this episode — reset
                EndEpisode();
            }
            else
            {
                // Just move food to a new spot and keep going
                PlaceFoodTarget();
                prevDistToFood = Vector3.Distance(body.position, foodTarget.position);
            }
        }
    }

    // ── Obstacle penalty ────────────────────────────────────────────
    float GetObstaclePenalty()
    {
        float penalty = 0f;
        bool isRising = body.position.y > prevBodyY + 0.001f;

        Vector3[] dirs = {
            Quaternion.Euler(0, -40, 0) * body.forward,
            body.forward,
            Quaternion.Euler(0,  40, 0) * body.forward,
        };

        foreach (Vector3 dir in dirs)
        {
            if (Physics.Raycast(
                body.position + Vector3.up * 0.2f,
                dir,
                out RaycastHit hit,
                obstacleCheckDistance,
                obstacleLayer))
            {
                if (isRising) continue;
                float closeness = 1f - (hit.distance / obstacleCheckDistance);
                penalty -= closeness * obstacleProximityScale;
            }
        }

        return penalty;
    }

    // ── Height control ──────────────────────────────────────────────
    void FixedUpdate()
    {
        HandleBodyHeight();
    }

    void HandleBodyHeight()
    {
        float sum = 0f;
        int valid = 0;

        foreach (LegStepper leg in legs)
        {
            if (leg.isMoving) continue;
            float y = leg.FootPosition.y;
            if (float.IsNaN(y)) continue;
            sum += y;
            valid++;
        }

        if (valid == 0) return;

        float avg = sum / valid;
        float desiredY = avg + bodyHeightOffset;

        if (Physics.Raycast(body.position + Vector3.up, Vector3.down, out RaycastHit hit, 5f, groundLayer))
        {
            float groundY = hit.point.y;
            desiredY = Mathf.Clamp(desiredY,
                groundY + minGroundOffset,
                groundY + maxGroundOffset);
        }

        Vector3 pos = body.position;
        float delta = Mathf.Clamp(
            desiredY - pos.y,
            -maxRiseSpeed * Time.fixedDeltaTime,
             maxRiseSpeed * Time.fixedDeltaTime);

        pos.y += delta;
        pos.y -= 2f * Time.fixedDeltaTime;

        if (rb != null)
            rb.MovePosition(pos);
        else
            body.position = pos;
    }

    // ── Heuristic (WASD) ────────────────────────────────────────────
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        c[0] = Input.GetAxis("Vertical");
        c[1] = Input.GetAxis("Horizontal");
    }

    // ── Food placement ──────────────────────────────────────────────
    void PlaceFoodTarget()
    {
        for (int i = 0; i < 15; i++)
        {
            Vector2 flat = Random.insideUnitCircle.normalized * Random.Range(2f, spawnRadius);
            Vector3 origin = startPosition + new Vector3(flat.x, 8f, flat.y);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, groundLayer))
            {
                foodTarget.position = hit.point + hit.normal * 0.15f;
                return;
            }
        }
        Vector2 f2 = Random.insideUnitCircle.normalized * spawnRadius;
        foodTarget.position = startPosition + new Vector3(f2.x, 0.15f, f2.y);
    }

    void OnDrawGizmosSelected()
    {
        if (foodTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(foodTarget.position, successRadius);
        }
        if (body != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(body.position, spawnRadius);

            Gizmos.color = Color.red;
            Vector3[] dirs = {
                Quaternion.Euler(0, -40, 0) * body.forward,
                body.forward,
                Quaternion.Euler(0,  40, 0) * body.forward,
            };
            foreach (var d in dirs)
                Gizmos.DrawRay(body.position + Vector3.up * 0.2f, d * obstacleCheckDistance);
        }
    }
}