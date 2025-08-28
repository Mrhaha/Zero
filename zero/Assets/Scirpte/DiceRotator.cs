using UnityEngine;

public class DiceRotator : MonoBehaviour
{
    public float speed = 180f;
    public bool isRotating = false;

    // Per-dice uniqueness
    public float speedMultiplier = 1f; // random per die
    public int direction = 1;          // +1 or -1

    [Header("Smoothing")]
    public float smoothFactor = 10f;   // larger = snappier
    private float currentAngularSpeed = 0f;

    [Header("Cartoon Exaggeration")]
    public bool enableSquash = true;
    public bool enableFlip = true;
    public float squashMax = 0.35f;          // 最大挤压比例（越大越夸张）
    public float squashSpeedRef = 720f;      // 达到该角速度时挤压接近上限
    public float flipThresholdAngle = 120f;  // 达到一定累计角度触发一次翻面
    public float flipDuration = 0.12f;       // 一次翻面时长
    public float flipMinScaleX = 0.15f;      // 翻面最小X缩放（越小越“贴纸翻转”）

    private float flipTimer = 0f;
    private float angleAccum = 0f;
    private Vector3 baseScale;
    private bool prevRotating = false;
    private float pulseScale = 0f;           // 过冲脉冲强度（叠加到缩放）

    // Sprite & faces
    private SpriteRenderer sr;
    private Sprite[] faces; // 0..5
    private int faceIndex = 0;
    private int nextFaceIndex = 0;

    public void AssignRendererAndFaces(SpriteRenderer renderer, Sprite[] faceSprites, int initialFace = 0)
    {
        sr = renderer;
        faces = faceSprites;
        faceIndex = Mathf.Clamp(initialFace, 0, (faces != null ? faces.Length - 1 : 0));
        if (sr != null && faces != null && faces.Length > 0)
        {
            sr.sprite = faces[faceIndex];
        }
    }

    public void SetRotating(bool rotating)
    {
        isRotating = rotating;
        // 开始/停止触发一次过冲脉冲
        pulseScale = rotating ? 0.25f : -0.18f;
    }

    void Update()
    {
        float target = isRotating ? speed * speedMultiplier * direction : 0f;
        // Exponential smoothing to approach target speed
        float t = 1f - Mathf.Exp(-smoothFactor * Time.deltaTime);
        currentAngularSpeed = Mathf.Lerp(currentAngularSpeed, target, t);

        // 旋转
        if (Mathf.Abs(currentAngularSpeed) > 0.001f)
            transform.Rotate(0f, 0f, currentAngularSpeed * Time.deltaTime);

        // 挤压/拉伸 & 过冲脉冲
        ApplySquashAndPulse(Time.deltaTime);

        // 假3D翻面
        HandleFlip(Time.deltaTime);

        prevRotating = isRotating;
    }

    private void ApplySquashAndPulse(float dt)
    {
        if (baseScale == Vector3.zero)
            baseScale = transform.localScale;

        float ax = Mathf.Abs(currentAngularSpeed);
        float squash = enableSquash ? Mathf.Clamp01(ax / Mathf.Max(1f, squashSpeedRef)) * squashMax : 0f;

        // 过冲脉冲渐消
        pulseScale = Mathf.MoveTowards(pulseScale, 0f, dt * 3.5f);

        float sx = (1f - squash) + pulseScale;
        float sy = (1f + squash) - pulseScale * 0.5f;
        sx = Mathf.Max(0.05f, sx);
        sy = Mathf.Max(0.05f, sy);

        transform.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);
    }

    private void HandleFlip(float dt)
    {
        if (!enableFlip || sr == null || faces == null || faces.Length < 2)
            return;

        float omega = Mathf.Abs(currentAngularSpeed);

        if (flipTimer > 0f)
        {
            flipTimer -= dt;
            float u = 1f - Mathf.Clamp01(flipTimer / flipDuration); // 0..1
            float phase = u * 2f; // 前半压到最小，后半回到1
            float sx = phase <= 1f ? Mathf.Lerp(1f, flipMinScaleX, phase) : Mathf.Lerp(flipMinScaleX, 1f, phase - 1f);
            float sy = 1f / Mathf.Max(0.2f, sx); // 反向拉伸，保持视觉面积

            // 叠加到当前缩放（与挤压共同作用）
            var s = transform.localScale;
            transform.localScale = new Vector3(s.x * sx, s.y * sy, s.z);

            // 中点切换贴图
            if (u >= 0.5f && sr.sprite != (faces != null ? faces[nextFaceIndex] : null))
            {
                faceIndex = nextFaceIndex;
                sr.sprite = faces[faceIndex];
            }
            return;
        }

        // 仅在旋转且速度达到阈值时积累角度触发翻面
        if (isRotating && omega > 90f) // 速度阈值，避免低速频繁翻面
        {
            angleAccum += omega * dt;
            if (angleAccum >= flipThresholdAngle)
            {
                angleAccum = 0f;
                // 选择下一个不同的面
                if (faces != null && faces.Length > 1)
                {
                    int nf = faceIndex;
                    for (int tries = 0; tries < 3 && nf == faceIndex; tries++)
                        nf = Random.Range(0, faces.Length);
                    nextFaceIndex = nf == faceIndex ? (faceIndex + 1) % faces.Length : nf;
                }
                flipTimer = flipDuration;
            }
        }
        else
        {
            angleAccum = 0f; // 停止或低速时不翻面
        }
    }
}
