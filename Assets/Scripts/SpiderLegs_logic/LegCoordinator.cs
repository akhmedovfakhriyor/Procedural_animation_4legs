using UnityEngine;

public class LegCoordinator : MonoBehaviour
{
    public LegStepper frontLeft;
    public LegStepper frontRight;
    public LegStepper backLeft;
    public LegStepper backRight;

    public Transform body;

    Vector3 lastBodyPos;
    Quaternion lastBodyRot;
    bool diagonal;

    void Start()
    {
        lastBodyPos = body.position;
        lastBodyRot = body.rotation;
    }

    void Update()
    {
        Vector3 velocity =
            (body.position - lastBodyPos) / Mathf.Max(Time.deltaTime, 0.0001f);

        lastBodyPos = body.position;
        lastBodyRot = body.rotation;

        if (AnyLegMoving())
            return;

        if (!diagonal)
            TryPair(frontLeft, backRight, velocity);
        else
            TryPair(frontRight, backLeft, velocity);
    }

    void TryPair(LegStepper a, LegStepper b, Vector3 velocity)
    {
        if (a.CanStep(velocity) || b.CanStep(velocity))
        {
            a.Step(velocity);
            b.Step(velocity);
            diagonal = !diagonal;
        }
    }

    bool AnyLegMoving()
    {
        return frontLeft.isMoving ||
               frontRight.isMoving ||
               backLeft.isMoving ||
               backRight.isMoving;
    }
}
