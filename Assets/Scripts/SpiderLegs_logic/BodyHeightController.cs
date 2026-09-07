using UnityEngine;

public class BodyHeightController : MonoBehaviour
{
    public Transform body;
    public LegStepper[] legs;

    [Header("Height Settings")]
    public float bodyHeightOffset = 0.5f;
    public float heightLerpSpeed = 5f;

    [Header("Limits")]
    public float maxUpOffset = 0.3f;
    public float maxDownOffset = 0.4f;

    float initialBodyY;
    Rigidbody rb;

    void Start()
    {
        initialBodyY = body.position.y;
        rb = body.GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Runs in FixedUpdate so rb.MovePosition plays nicely with physics.
        // Previously this was LateUpdate + direct transform write, which
        // caused the Rigidbody to snap back every physics step and created
        // the stiff/frozen-height behaviour when the agent was running.

        float sum = 0f;
        int valid = 0;

        foreach (LegStepper leg in legs)
        {
            float y = leg.FootPosition.y;
            if (float.IsNaN(y)) continue;
            sum += y;
            valid++;
        }

        if (valid == 0) return;

        float avg = sum / valid;
        float desiredY = avg + bodyHeightOffset;

        desiredY = Mathf.Clamp(
            desiredY,
            initialBodyY - maxDownOffset,
            initialBodyY + maxUpOffset
        );

        float newY = Mathf.Lerp(body.position.y, desiredY, Time.fixedDeltaTime * heightLerpSpeed);

        if (rb != null)
        {
            // Use MovePosition so Y is set through the physics system,
            // not by poking the transform directly.
            // X/Z are left unchanged — SpiderAgent owns those axes.
            Vector3 pos = body.position;
            pos.y = newY;
            rb.MovePosition(pos);
        }
        else
        {
            Vector3 pos = body.position;
            pos.y = newY;
            body.position = pos;
        }
    }
}