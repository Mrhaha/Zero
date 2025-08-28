using UnityEngine;

public class Dice3DRotator : MonoBehaviour
{
    public float speed = 180f;
    public bool isRotating = false;

    [Header("Axis")]
    public Vector3 axis = new Vector3(0.2f, 1f, 0.3f);
    public float smoothFactor = 12f;

    private float currentAngularSpeed = 0f;
    public float CurrentSpeedAbs { get; private set; } = 0f;

    // Align to target face when stopping
    private bool aligning = false;
    private Quaternion targetRotation;
    public float alignSpeed = 12f; // slerp speed

    [Header("Result")]
    public int faceValue = 1; // 1..6，记录当前对齐的结果点数

    void Awake()
    {
        if (axis == Vector3.zero)
            axis = new Vector3(0.3f, 1f, 0.2f);
        axis = axis.normalized;
        targetRotation = transform.rotation;
    }

    public void SetRotating(bool rotating)
    {
        isRotating = rotating;
        if (rotating)
        {
            aligning = false;
        }
    }

    public void SetAxis(Vector3 newAxis)
    {
        axis = newAxis == Vector3.zero ? Vector3.up : newAxis.normalized;
    }

    public void SetTargetFace(int face)
    {
        // face: 1..6 对应 1正面(+Z),2右(+X),3背(-Z),4左(-X),5上(+Y),6下(-Y)
        Quaternion q;
        switch (face)
        {
            case 1: q = Quaternion.identity; break;                  // +Z 朝前
            case 2: q = Quaternion.Euler(0f, -90f, 0f); break;       // +X 朝前
            case 3: q = Quaternion.Euler(0f, 180f, 0f); break;       // -Z 朝前
            case 4: q = Quaternion.Euler(0f, 90f, 0f); break;        // -X 朝前
            case 5: q = Quaternion.Euler(90f, 0f, 0f); break;        // +Y 朝前
            case 6: q = Quaternion.Euler(-90f, 0f, 0f); break;       // -Y 朝前
            default: q = Quaternion.identity; break;
        }
        faceValue = Mathf.Clamp(face, 1, 6);
        targetRotation = q;
        aligning = true;
        isRotating = false;
    }

    void Update()
    {
        float target = isRotating ? speed : 0f;
        float t = 1f - Mathf.Exp(-smoothFactor * Time.deltaTime);
        currentAngularSpeed = Mathf.Lerp(currentAngularSpeed, target, t);

        if (isRotating && currentAngularSpeed > 0.01f)
        {
            transform.Rotate(axis, currentAngularSpeed * Time.deltaTime, Space.World);
        }
        else if (aligning)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-alignSpeed * Time.deltaTime));
            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
            {
                transform.rotation = targetRotation;
                aligning = false;
            }
        }
        CurrentSpeedAbs = Mathf.Abs(currentAngularSpeed);
    }
}
