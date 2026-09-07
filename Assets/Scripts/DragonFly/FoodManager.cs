using UnityEngine;

/// <summary>
/// FoodManager.cs
/// Spawns and relocates food. Tells DragonflyAgent about the new food
/// transform so its observations stay accurate.
/// </summary>
public class FoodManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform dragonfly;
    [SerializeField] DragonflyAgent dragonflyAgent; // FIX: was DragonflyMovement (doesn't exist anymore)

    [Header("Food Settings")]
    [SerializeField] float spawnRadius = 8f;
    [SerializeField] float minSpawnDist = 3f;
    [SerializeField] float catchRadius = 0.8f;
    [SerializeField] float foodSize = 0.3f;
    [SerializeField] float foodHeight = 0f;
    [SerializeField] Color foodColor = Color.green;

    [Header("Food Bob")]
    [SerializeField] float bobAmplitude = 0.15f;
    [SerializeField] float bobFrequency = 2f;

    [Header("Debug")]
    [SerializeField] bool showDistanceLog = false;

    GameObject currentFood;
    Vector3 foodBasePosition;
    int foodsEaten = 0;

    void Start() => SpawnFood();

    void Update()
    {
        if (currentFood == null || dragonfly == null) { SpawnFood(); return; }

        float bobY = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        currentFood.transform.position = foodBasePosition + Vector3.up * bobY;

        float dist = Vector3.Distance(dragonfly.position, currentFood.transform.position);
        if (showDistanceLog) Debug.Log($"[FoodManager] Distance: {dist:F2}");
        if (dist <= catchRadius) CatchFood();
    }

    void SpawnFood()
    {
        if (currentFood != null) Destroy(currentFood);

        Vector2 randFlat = Random.insideUnitCircle.normalized
                         * Random.Range(minSpawnDist, spawnRadius);

        Vector3 spawnPos = dragonfly != null
            ? new Vector3(dragonfly.position.x + randFlat.x,
                          dragonfly.position.y + foodHeight,
                          dragonfly.position.z + randFlat.y)
            : new Vector3(randFlat.x, foodHeight, randFlat.y);

        foodBasePosition = spawnPos;

        currentFood = GameObject.CreatePrimitive(PrimitiveType.Cube);
        currentFood.name = "Food";
        currentFood.transform.position = spawnPos;
        currentFood.transform.localScale = Vector3.one * foodSize;
        Destroy(currentFood.GetComponent<Collider>());

        Renderer rend = currentFood.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = foodColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", foodColor * 0.6f);
            rend.material = mat;
        }

        if (dragonflyAgent != null)
            dragonflyAgent.SetFoodTarget(currentFood.transform);
    }

    void CatchFood()
    {
        foodsEaten++;
        Debug.Log($"[FoodManager] Food caught! Total: {foodsEaten}");
        Destroy(currentFood);
        currentFood = null;
        SpawnFood();
    }

    public int FoodsEaten => foodsEaten;
    public float DistanceToFood => currentFood != null && dragonfly != null
        ? Vector3.Distance(dragonfly.position, currentFood.transform.position) : -1f;
}