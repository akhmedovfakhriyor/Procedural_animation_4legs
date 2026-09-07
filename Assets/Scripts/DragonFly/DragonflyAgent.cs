using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(ProceduralFlightController))]
public class DragonflyAgent : Agent
{
    // ── References ───────────────────────────────────────────────────
    [Header("References")]
    public Transform foodTarget;
    public LayerMask projectileLayer;

    // ── Waypoint ──────────────────────────────────────────────────────
    [Header("Waypoint Settings")]
    [SerializeField] float waypointRange = 4f;

    // ── Rewards ───────────────────────────────────────────────────────
    [Header("Rewards")]
    [SerializeField] float reachFoodReward = 5f;
    [SerializeField] float progressScale = 0.08f;
    [SerializeField] float facingScale = 0.01f;
    [SerializeField] float timePenalty = -0.001f;
    [SerializeField] float hitByProjectilePenalty = -3f;

    // Reward for CHOOSING a waypoint that points toward food.
    // This is the key fix — it directly rewards the ML's decision quality
    // independent of whether the procedural system physically gets there.
    [SerializeField] float waypointDirectionScale = 0.05f;

    // ── Episode ───────────────────────────────────────────────────────
    [Header("Episode")]
    [SerializeField] float maxEpisodeDistance = 30f;
    [SerializeField] float catchRadius = 1f;
    [SerializeField] float spawnRadius = 10f;
    [SerializeField] int maxStepsPerEpisode = 3000; // FIX: episodes now always end

    // ── Obstacle Detection ────────────────────────────────────────────
    [Header("Obstacle Detection")]
    [SerializeField] float projectileScanRadius = 6f;

    // ── private ───────────────────────────────────────────────────────
    ProceduralFlightController flightController;
    Vector3 startPosition;
    float prevDistToFood;
    int stepCount;

    // ─────────────────────────────────────────────────────────────────
    public override void Initialize()
    {
        flightController = GetComponent<ProceduralFlightController>();
        startPosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        transform.position = startPosition;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        flightController.ResetState();

        PlaceFood();
        prevDistToFood = Vector3.Distance(transform.position, foodTarget.position);
        stepCount = 0;
    }

    // ── OBSERVATIONS (14 floats) ──────────────────────────────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toFood = foodTarget.position - transform.position;
        Vector3 localFood = transform.InverseTransformDirection(toFood.normalized);
        sensor.AddObservation(localFood);                               // 3
        sensor.AddObservation(toFood.magnitude / maxEpisodeDistance);   // 1

        Vector3 localProj = Vector3.zero;
        float projDist = 1f;
        Collider[] hits = Physics.OverlapSphere(
            transform.position, projectileScanRadius, projectileLayer);

        if (hits.Length > 0)
        {
            Collider closest = hits[0];
            float closestD = Vector3.Distance(transform.position, closest.transform.position);
            foreach (Collider c in hits)
            {
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < closestD) { closestD = d; closest = c; }
            }
            Vector3 toProj = closest.transform.position - transform.position;
            localProj = transform.InverseTransformDirection(toProj.normalized);
            projDist = closestD / projectileScanRadius;
        }

        sensor.AddObservation(localProj);                               // 3
        sensor.AddObservation(projDist);                                // 1
        sensor.AddObservation(flightController.NormalisedSpeed);        // 1
        sensor.AddObservation(transform.up);                            // 3

        float elevDiff = Mathf.Clamp(
            (foodTarget.position.y - transform.position.y) / 5f, -1f, 1f);
        sensor.AddObservation(elevDiff);                                // 1
        sensor.AddObservation(projDist < 0.3f ? 1f : 0f);             // 1
        // Total: 3+1+3+1+1+3+1+1 = 14
    }

    // ── ACTIONS ───────────────────────────────────────────────────────
    public override void OnActionReceived(ActionBuffers actions)
    {
        stepCount++;

        // The AI can output any direction now (-1 to 1 for all axes)
        float localX = actions.ContinuousActions[0];
        float localY = actions.ContinuousActions[1];
        float localZ = actions.ContinuousActions[2];

        Vector3 localDir = new Vector3(localX, localY, localZ);

        // Calculate the exact 3D point the AI wants to dart to
        Vector3 worldWaypoint = transform.position + transform.TransformDirection(localDir) * waypointRange;
        flightController.SetWaypoint(worldWaypoint);

        // ── Rewards ───────────────────────────────────────────────────
        float distNow = Vector3.Distance(transform.position, foodTarget.position);

        // Progress Reward: Only gets points for physically closing the distance.
        AddReward((prevDistToFood - distNow) * progressScale);
        prevDistToFood = distNow;

        // Time Penalty: -1 divided by max steps. Forces it to hurry to the food.
        AddReward(-1f / maxStepsPerEpisode);

        // ── Terminal conditions ───────────────────────────────────────
        if (stepCount >= maxStepsPerEpisode)
        {
            EndEpisode();
            return;
        }

        if (Vector3.Distance(transform.position, startPosition) > maxEpisodeDistance)
        {
            EndEpisode();
            return;
        }

        if (distNow < catchRadius)
        {
            AddReward(reachFoodReward);
            PlaceFood();
            prevDistToFood = Vector3.Distance(transform.position, foodTarget.position);
        }
    }



    // ── Projectile hit ────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if ((projectileLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            AddReward(hitByProjectilePenalty);
            EndEpisode();
        }
    }

    // ── Heuristic (keyboard testing) ─────────────────────────────────
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        // Allows you to test it like a drone
        c[0] = Input.GetAxis("Horizontal"); // A/D for Left/Right
        c[1] = 0f;
        c[2] = Input.GetAxis("Vertical");   // W/S for Forward/Backward
    }



    // ── Food placement ────────────────────────────────────────────────
    void PlaceFood()
    {
        if (foodTarget == null) return;
        Vector2 flat = Random.insideUnitCircle.normalized * Random.Range(3f, spawnRadius);
        float height = Random.Range(1f, 6f);
        foodTarget.position = startPosition + new Vector3(flat.x, height, flat.y);
    }

    public void SetFoodTarget(Transform t) => foodTarget = t;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (foodTarget != null)
            Gizmos.DrawWireSphere(foodTarget.position, catchRadius);

        Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, projectileScanRadius);
    }
}