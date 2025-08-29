using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class DiceManager : MonoBehaviour
{
    [Header("Dice Settings")]
    public int diceCount = 6;
    public float spacing = 1.4f;
    public float diceSize = 1.0f;
    public float rotationSpeed = 240f;
    public bool use3D = true; // 使用3D骰子
    public DefaultSkin defaultSkin = DefaultSkin.Dark; // 下拉选择内置皮肤
    public DiceSkin skinAsset;               // 可直接拖入自定义 ScriptableObject 皮肤
    public Material faceMaterial;            // URP 材质模板（建议：URP/Unlit 或 URP/Lit），用于3D面片

    private DiceRotator[] rotators;              // 2D用
    private SpriteRenderer[] renderers;          // 2D用
    private Sprite[] faceSprites; // 0..5 => 1..6 pips（用于2D/3D）
    private Dice3DRotator[] rotators3D;          // 3D用
    private GameObject[] dice3D;                 // 3D根节点
    
    [Header("3D Axis Settings")]
    public float axisJitter = 0.15f;             // 轴抖动幅度（越小方向越接近）
    public float axisMinY = 0.2f;                // 保证轴的Y分量不太低，避免纯水平导致观感怪
    public bool lockSimilarDirection = true;     // 统一朝向（负向轴会被翻转）
    private Vector3 baseAxis3D = Vector3.up;     // 整排骰子的基准轴
    public float cameraDotLimit = 0.5f;          // 与相机forward的|dot|上限，越小越避免垂直屏幕
    private bool isRotating = false;
    
    [Header("Spacebar Control")]
    public float energyPerPress = 1.0f;      // 每次按键增加的能量
    public float energyDecayPerSecond = 0.6f; // 基础衰减（会被动态调整）
    public float maxEnergy = 5f;              // 能量上限
    private float spinEnergy = 0f;            // 当前能量
    private float lastPressTime = -999f;      // 上次按键时间  

    [Header("Energy Decay Advanced")]
    public float nonLinearA = 2.0f;          // 非线性速度上限系数A
    public float nonLinearB = 1.0f;          // 非线性速度曲线B
    public float decayBase = 0.8f;           // 基础衰减率
    public float decayHigh = 3.2f;           // 空闲时提升后的高衰减率
    public float idleThreshold = 0.3f;       // 空闲阈值（多久未按键开始加速衰减）
    public float rampDuration = 0.4f;        // 衰减从基础到高值的过渡时长
    public float energyThresholdE0 = 1.8f;   // 能量阈值（超过后增加能量相关衰减）
    public float decayEnergyK = 0.6f;        // 能量相关衰减系数
    public float speedFrictionK = 0.005f;    // 速度相关摩擦（按角速度计）
    public float maxStopTime = 1.0f;         // 预计停下最大时长限制
    public float stopSpeedThresholdDeg = 8.0f; // 自然停判断角速度阈值（3D）
    // 按钮已删除，仅保留状态文本与总数覆盖层
    private Text statusText;
    private Image overlayImage;   // 半透明背景
    private Text totalText;       // 总数显示
    private float overlayAlpha = 0f;
    public float overlayTargetAlpha = 0.6f;
    public float overlayFadeDuration = 0.4f;
    private bool overlayFading = false;

    [Header("FX Settings")]
    public bool enableGhostTrail = true;
    public bool enableFlip = true;
    public bool exaggerationCartoon = true;

    public enum DefaultSkin { Classic, Dark, Candy, Neon }

    [Header("Audio SFX")]
    public bool enableSfx = true;
    public bool enableLoop = true;
    public bool enableTick = true;
    public bool enablePressSfx = false;
    public AudioClip clipStart;
    public AudioClip clipLoop;
    public AudioClip[] clipTick;
    public AudioClip clipStop;
    public AudioClip clipReveal;
    public AudioClip clipPress;
    public AudioMixerGroup sfxMixer;
    [Range(0f,1f)] public float volumeMaster = 1f;
    [Range(0f,1f)] public float volumeLoop = 0.25f;
    [Range(0f,1f)] public float volumeTick = 0.35f;
    [Range(0f,1f)] public float volumeOneShot = 0.6f;
    public float loopPitchMin = 0.9f;
    public float loopPitchMax = 1.2f;
    [Range(0f,1f)] public float spatialBlend = 0f; // 0=2D,1=3D
    public float tickBaseInterval = 0.25f;
    public float tickMinInterval = 0.08f;
    public float tickSpeedScale = 1.0f;
    public float tickPitchJitter = 0.08f;
    public float loopFadeOutTime = 0.3f;

    private AudioSource oneShotSource;
    private AudioSource[] diceAudio; // per-die source for loop/tick
    private float tickTimer = 0f;
    private bool loopFading = false;

    // Note: Bootstrap removed. Attach DiceManager manually to a scene object.

    void Start()
    {
        if (use3D) CreateDiceRow3D(); else CreateDiceRow2D();
        CreateUI();
        UpdateStatusText();
        // Prepare one-shot audio source
        if (enableSfx)
        {
            oneShotSource = gameObject.GetComponent<AudioSource>();
            if (oneShotSource == null) oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f;
            oneShotSource.dopplerLevel = 0f;
            if (sfxMixer != null) oneShotSource.outputAudioMixerGroup = sfxMixer;
        }
    }

    void Update()
    {
        // 阶段推进与加速：
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentStage == Stage.Shaking)
            {
                // 摇动阶段：空格增加能量，加速
                spinEnergy = Mathf.Min(maxEnergy, spinEnergy + energyPerPress);
                lastPressTime = Time.time;
                // Press SFX
                if (enableSfx && enablePressSfx && clipPress != null && oneShotSource != null)
                {
                    oneShotSource.pitch = 1f;
                    oneShotSource.PlayOneShot(clipPress, volumeMaster * Mathf.Min(0.6f, volumeOneShot * 0.6f));
                }
            }
            else
            {
                // 其他阶段：推进到下一阶段
                Stage prev = currentStage;
                NextStage();
                // 刚进入摇动时，给予一次初始能量
                if (prev == Stage.Ready && currentStage == Stage.Shaking)
                {
                    spinEnergy = Mathf.Min(maxEnergy, spinEnergy + energyPerPress);
                    lastPressTime = Time.time;
                    if (enableSfx && enablePressSfx && clipPress != null && oneShotSource != null)
                    {
                        oneShotSource.pitch = 1f;
                        oneShotSource.PlayOneShot(clipPress, volumeMaster * Mathf.Min(0.6f, volumeOneShot * 0.6f));
                    }
                }
            }
        }

        // 能量动态衰减
        if (currentStage == Stage.Shaking && spinEnergy > 0f)
        {
            float avgSpeed = 0f;
            int n = 0;
            if (use3D && rotators3D != null)
            {
                foreach (var r3 in rotators3D) { if (r3 != null) { avgSpeed += r3.CurrentSpeedAbs; n++; } }
            }
            else if (!use3D && rotators != null)
            {
                // 2D近似：使用基于能量的当前目标速度
                float eff = EffectiveSpeedFactor(spinEnergy);
                avgSpeed = rotationSpeed * eff;
                n = 1;
            }
            avgSpeed = n > 0 ? avgSpeed / n : 0f;

            float decay = DynamicDecayRate(spinEnergy, avgSpeed);
            // 如果预计停止时间过长，则强制提升衰减
            if (decay > 1e-4f)
            {
                float tEst = spinEnergy / decay;
                if (tEst > maxStopTime)
                {
                    decay = spinEnergy / Mathf.Max(0.1f, maxStopTime);
                }
            }
            spinEnergy = Mathf.Max(0f, spinEnergy - decay * Time.deltaTime);
        }

        // 基于能量的速度（能量随时间衰减，不按则自然减速）
        if (!use3D)
        {
            if (rotators != null)
            {
                float eff = EffectiveSpeedFactor(spinEnergy);
                foreach (var r in rotators) if (r != null) r.speed = rotationSpeed * eff;
            }
        }
        else
        {
            if (rotators3D != null)
            {
                float eff = EffectiveSpeedFactor(spinEnergy);
                foreach (var r3 in rotators3D) if (r3 != null) r3.speed = rotationSpeed * eff;
            }
        }

        // 摇动阶段：自然停下（能量耗尽且角速度接近0）后切换到显示结果
        if (currentStage == Stage.Shaking)
        {
            // Loop SFX gain/pitch follow speed factor
            if (enableSfx && enableLoop && clipLoop != null && diceAudio != null)
            {
                float eff = EffectiveSpeedFactor(spinEnergy);
                float tPitch = Mathf.InverseLerp(0f, Mathf.Max(0.01f, nonLinearA), eff);
                float pitch = Mathf.Lerp(loopPitchMin, loopPitchMax, tPitch);
                float vol = volumeMaster * volumeLoop * Mathf.Clamp01(eff / Mathf.Max(0.01f, nonLinearA));
                for (int i = 0; i < diceAudio.Length; i++)
                {
                    var src = diceAudio[i];
                    if (src == null) continue;
                    if (!src.isPlaying)
                    {
                        src.clip = clipLoop;
                        src.loop = true;
                        src.volume = 0f;
                        src.pitch = pitch;
                        src.Play();
                    }
                    src.pitch = pitch;
                    src.volume = vol;
                }
                loopFading = false; // actively driving loop
            }

            // Tick SFX (tempo scales with speed)
            if (enableSfx && enableTick && clipTick != null && clipTick.Length > 0)
            {
                float eff = EffectiveSpeedFactor(spinEnergy);
                float interval = tickBaseInterval / (1f + eff * Mathf.Max(0f, tickSpeedScale));
                interval = Mathf.Max(tickMinInterval, interval);
                tickTimer -= Time.deltaTime;
                if (tickTimer <= 0f)
                {
                    tickTimer = interval;
                    // choose source & clip
                    AudioSource src = (diceAudio != null && diceAudio.Length > 0) ? diceAudio[Random.Range(0, diceAudio.Length)] : oneShotSource;
                    if (src != null)
                    {
                        var clip = clipTick[Random.Range(0, clipTick.Length)];
                        float jitter = 1f + Random.Range(-tickPitchJitter, tickPitchJitter);
                        float vol = volumeMaster * volumeTick;
                        float oldPitch = src.pitch;
                        src.pitch = Mathf.Clamp(jitter, 0.5f, 2f);
                        src.PlayOneShot(clip, vol);
                        src.pitch = oldPitch;
                    }
                }
            }

            bool shouldStop = false;
            if (spinEnergy <= 0.0001f)
            {
                if (!use3D)
                {
                    // 2D：近似判断（没有公开当前角速度），给一个缓冲时间
                    shakingTimer -= Time.deltaTime;
                    if (shakingTimer <= 0f) shouldStop = true;
                }
                else
                {
                    // 3D：检查每个骰子的当前角速度
                    float threshold = stopSpeedThresholdDeg; // deg/s
                    shouldStop = true;
                    if (rotators3D != null)
                    {
                        foreach (var r3 in rotators3D)
                        {
                            if (r3 != null && r3.CurrentSpeedAbs > threshold)
                            {
                                shouldStop = false; break;
                            }
                        }
                    }
                }
            }
            else
            {
                // 若还有能量，刷新缓冲时间
                shakingTimer = 0.5f;
            }

            if (shouldStop)
            {
                StopAndReveal();
                currentStage = Stage.Revealed;
                revealTimer = Mathf.Max(0f, revealToTotalDelay);
                UpdateStatusText();
            }
            else
            {
                UpdateStatusText();
            }
        }

        // Loop fade-out when not in shaking
        if (enableSfx && (currentStage != Stage.Shaking) && diceAudio != null)
        {
            if (!loopFading)
            {
                loopFading = true;
            }
            float step = (loopFadeOutTime > 0f) ? Time.deltaTime / loopFadeOutTime : 1f;
            for (int i = 0; i < diceAudio.Length; i++)
            {
                var src = diceAudio[i];
                if (src == null) continue;
                if (src.isPlaying)
                {
                    src.volume = Mathf.Max(0f, src.volume - step * volumeMaster * volumeLoop);
                    if (src.volume <= 0.001f)
                    {
                        src.Stop();
                        src.clip = null;
                    }
                }
            }
        }

        // 结果阶段：延时自动进入总数显示
        if (currentStage == Stage.Revealed)
        {
            if (revealTimer > 0f)
            {
                revealTimer -= Time.deltaTime;
                if (revealTimer <= 0f)
                {
                    ShowTotal();
                    currentStage = Stage.TotalShown;
                    UpdateStatusText();
                }
            }
        }

        // 覆盖层淡入动画（总数显示阶段）
        if (currentStage == Stage.TotalShown && overlayFading && overlayImage != null)
        {
            overlayAlpha += Time.deltaTime / Mathf.Max(0.01f, overlayFadeDuration);
            float k = Mathf.Clamp01(overlayAlpha);
            // 背景
            overlayImage.color = new Color(0f, 0f, 0f, overlayTargetAlpha * k);
            // 文字
            if (totalText != null)
            {
                var c = totalText.color;
                totalText.color = new Color(c.r, c.g, c.b, k);
            }
            if (k >= 1f) overlayFading = false;
        }
    }

    // 阶段定义
    private enum Stage { Ready, Shaking, Revealed, TotalShown }
    private Stage currentStage = Stage.Ready;
    private int[] currentFaces; // 存当前各骰子的点数，用于求和
    private float shakingTimer = 0f; // 作为“自然停止”的缓冲计时（2D）
    private float revealTimer = 0f;  // 结果阶段到总数阶段的自动延迟
    [Header("Stage Timing")]
    public float revealToTotalDelay = 1.0f; // 停止后显示点数，延迟该时间后自动展示总数

    private void NextStage()
    {
        switch (currentStage)
        {
            case Stage.Ready:
                StartShaking();
                currentStage = Stage.Shaking;
                break;
            case Stage.Shaking:
                StopAndReveal();
                currentStage = Stage.Revealed;
                break;
            case Stage.Revealed:
                ShowTotal();
                currentStage = Stage.TotalShown;
                break;
            case Stage.TotalShown:
                ResetRound();
                currentStage = Stage.Ready;
                break;
        }
        UpdateStatusText();
    }

    private void StartShaking()
    {
        isRotating = true;
        shakingTimer = 0.5f; // 缓冲计时
        // 若能量为0，给极小初速以便开始旋转反馈
        if (spinEnergy <= 0f) spinEnergy = Mathf.Min(maxEnergy, energyPerPress * 0.5f);
        AllowDiceAudio();
        // Start SFX
        if (enableSfx && clipStart != null && oneShotSource != null)
        {
            oneShotSource.pitch = 1f;
            oneShotSource.PlayOneShot(clipStart, volumeMaster * volumeOneShot);
        }
        if (!use3D)
        {
            if (rotators != null)
                foreach (var r in rotators)
                {
                    if (r == null) continue;
                    r.SetRotating(true);
                    var gt = r.GetComponent<GhostTrail>();
                    if (gt != null) gt.active = enableGhostTrail;
                }
        }
        else
        {
            if (rotators3D != null)
                foreach (var r3 in rotators3D)
                    if (r3 != null) r3.SetRotating(true);
        }
    }

    private void StopAndReveal()
    {
        isRotating = false;
        // 强制静音骰子相关音效
        HardMuteDiceAudio();
        // Begin loop fade and play stop SFX
        if (enableSfx && clipStop != null && oneShotSource != null)
        {
            oneShotSource.pitch = 1f;
            oneShotSource.PlayOneShot(clipStop, volumeMaster * volumeOneShot);
        }
        if (!use3D)
        {
            if (rotators != null)
                foreach (var r in rotators)
                {
                    if (r == null) continue;
                    r.SetRotating(false);
                    var gt = r.GetComponent<GhostTrail>();
                    if (gt != null) gt.active = false;
                }
            // 随机结果
            currentFaces = new int[diceCount];
            for (int i = 0; i < diceCount; i++)
            {
                int face = Random.Range(1, 7);
                currentFaces[i] = face;
                if (renderers != null && renderers[i] != null)
                    renderers[i].sprite = faceSprites[face - 1];
            }
        }
        else
        {
            if (rotators3D != null)
            {
                currentFaces = new int[diceCount];
                for (int i = 0; i < rotators3D.Length; i++)
                {
                    var r3 = rotators3D[i];
                    if (r3 == null) continue;
                    // 订阅对齐事件
                    r3.OnAligned -= OnDieAligned;
                    r3.OnAligned += OnDieAligned;
                    r3.SetRotating(false);
                    int face = Random.Range(1, 7);
                    currentFaces[i] = face;
                    r3.SetTargetFace(face); // 平滑对齐
                }
            }
        }
    }

    private int alignedCount = 0;
    private void OnDieAligned(Dice3DRotator r)
    {
        alignedCount++;
        // 对齐点击音（可重用 stop/或 tick clip）
        if (enableSfx && oneShotSource != null)
        {
            AudioClip c = clipStop != null ? clipStop : (clipTick != null && clipTick.Length > 0 ? clipTick[0] : null);
            if (c != null)
            {
                float vol = volumeMaster * Mathf.Min(volumeOneShot, 0.5f);
                oneShotSource.pitch = 1f + Random.Range(-0.05f, 0.05f);
                oneShotSource.PlayOneShot(c, vol);
            }
        }
    }

    private void ShowTotal()
    {
        // 二次保险：显示总数前确保全部骰子音效关闭
        HardMuteDiceAudio();
        if (currentFaces == null || currentFaces.Length == 0) return;
        int sum = 0; foreach (var f in currentFaces) sum += f;
        string msg = $"本次结果：\n{string.Join(" ", currentFaces)}\n\n总数：{sum}\n\n按空格重新开始";
        // 显示覆盖层并淡入
        if (overlayImage != null)
        {
            overlayImage.gameObject.SetActive(true);
            overlayAlpha = 0f;
            overlayFading = true;
            overlayImage.color = new Color(0f, 0f, 0f, 0f);
        }
        if (totalText != null)
        {
            totalText.text = msg;
            var c = totalText.color; totalText.color = new Color(c.r, c.g, c.b, 0f);
        }
        if (statusText != null)
        {
            statusText.text = ""; // 避免与覆盖层文字重复
        }
        if (enableSfx && clipReveal != null && oneShotSource != null)
        {
            oneShotSource.pitch = 1f;
            oneShotSource.PlayOneShot(clipReveal, volumeMaster * volumeOneShot);
        }
    }

    private void ResetRound()
    {
        alignedCount = 0;
        currentFaces = null;
        // 隐藏覆盖层
        if (overlayImage != null)
        {
            overlayImage.gameObject.SetActive(false);
            overlayAlpha = 0f;
            overlayFading = false;
        }
        if (totalText != null)
        {
            var c = totalText.color; totalText.color = new Color(c.r, c.g, c.b, 0f);
            totalText.text = "";
        }
        if (!use3D)
        {
            // 统一重置到1点
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].sprite = faceSprites[0];
            if (rotators != null)
                foreach (var r in rotators)
                {
                    if (r == null) continue;
                    r.SetRotating(false);
                    var gt = r.GetComponent<GhostTrail>();
                    if (gt != null) gt.active = false;
                }
        }
        else
        {
            if (rotators3D != null)
                foreach (var r3 in rotators3D)
                {
                    if (r3 == null) continue;
                    r3.SetRotating(false);
                    r3.SetTargetFace(1); // 初始面朝前
                }
        }
        if (statusText != null) statusText.text = "按空格开始摇动";
    }

    private void HardMuteDiceAudio()
    {
        loopFading = false;
        tickTimer = 0f;
        if (diceAudio != null)
        {
            for (int i = 0; i < diceAudio.Length; i++)
            {
                var src = diceAudio[i];
                if (src == null) continue;
                if (src.isPlaying) src.Stop();
                src.clip = null;
                src.volume = 0f;
            }
        }
    }

    private void AllowDiceAudio()
    {
        loopFading = false;
        tickTimer = 0f;
    }

    private void CreateDiceRow()
    {
        rotators = new DiceRotator[diceCount];
        renderers = new SpriteRenderer[diceCount];

        // Prepare die face sprites (1..6)
        faceSprites = CreateDieSprites(64, 64, 3, 4);

        // Center the row around world origin
        float totalWidth = (diceCount - 1) * spacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < diceCount; i++)
        {
            var dice = new GameObject($"Dice_{i + 1}");
            var sr = dice.AddComponent<SpriteRenderer>();
            sr.sprite = faceSprites != null && faceSprites.Length >= 1 ? faceSprites[0] : null; // 初始都显示1点，整齐
            sr.sortingOrder = 0;

            dice.transform.localScale = Vector3.one * diceSize;
            dice.transform.position = new Vector3(startX + i * spacing, 0f, 0f);
            dice.transform.rotation = Quaternion.identity; // 初始不旋转，保持整齐

            var rot = dice.AddComponent<DiceRotator>();
            rot.speed = rotationSpeed;
            rot.isRotating = isRotating;
            rot.speedMultiplier = Random.Range(0.8f, 1.3f);
            rot.direction = Random.value < 0.5f ? -1 : 1;
            rot.enableFlip = enableFlip;
            rot.enableSquash = exaggerationCartoon;
            rot.AssignRendererAndFaces(sr, faceSprites, 0);
            rotators[i] = rot;
            renderers[i] = sr;

            if (enableGhostTrail)
            {
                var ghost = dice.AddComponent<GhostTrail>();
                ghost.source = sr;
                ghost.active = false;
            }
        }
    }

    private void CreateUI()
    {
        // Canvas
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // EventSystem (in case scene doesn't have one)
        EnsureEventSystem();

        // Button
        var buttonGO = new GameObject("ToggleButton", typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(canvasGO.transform, false);

        var img = buttonGO.GetComponent<Image>();
        img.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);

        // 按钮被删除：不再创建

        var rect = buttonGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 60);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -40);

        // 移除按钮整体（包括背景与交互），只保留状态文本
        GameObject.Destroy(buttonGO);

        // Status Text (center top)
        var statusGO = new GameObject("StatusText", typeof(Text));
        statusGO.transform.SetParent(canvasGO.transform, false);
        statusText = statusGO.GetComponent<Text>();
        statusText.alignment = TextAnchor.UpperCenter;
        statusText.color = Color.white;
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 28;
        var srect = statusGO.GetComponent<RectTransform>();
        srect.sizeDelta = new Vector2(600, 120);
        srect.anchorMin = new Vector2(0.5f, 1f);
        srect.anchorMax = new Vector2(0.5f, 1f);
        srect.pivot = new Vector2(0.5f, 1f);
        srect.anchoredPosition = new Vector2(0, -110);

        // Overlay Panel (full screen, initially hidden)
        var overlayGO = new GameObject("Overlay", typeof(Image));
        overlayGO.transform.SetParent(canvasGO.transform, false);
        overlayImage = overlayGO.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        var orect = overlayGO.GetComponent<RectTransform>();
        orect.anchorMin = Vector2.zero;
        orect.anchorMax = Vector2.one;
        orect.offsetMin = Vector2.zero;
        orect.offsetMax = Vector2.zero;
        overlayGO.SetActive(false);

        // Total Text (center)
        var totalGO = new GameObject("TotalText", typeof(Text));
        totalGO.transform.SetParent(overlayGO.transform, false);
        totalText = totalGO.GetComponent<Text>();
        totalText.alignment = TextAnchor.MiddleCenter;
        totalText.color = new Color(1f, 1f, 1f, 0f);
        totalText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        totalText.fontSize = 48;
        var trect = totalGO.GetComponent<RectTransform>();
        trect.sizeDelta = new Vector2(800, 320);
        trect.anchorMin = new Vector2(0.5f, 0.5f);
        trect.anchorMax = new Vector2(0.5f, 0.5f);
        trect.pivot = new Vector2(0.5f, 0.5f);
        trect.anchoredPosition = Vector2.zero;
    }

    // 旧的切换函数已被阶段逻辑替代

    private void SetRandomFaces2D()
    {
        if (renderers == null || faceSprites == null || faceSprites.Length < 6) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            var sr = renderers[i];
            if (sr == null) continue;
            int face = Random.Range(1, 7); // 1..6
            sr.sprite = faceSprites[face - 1];
        }
    }

    private void SetRandomFaces3D()
    {
        if (rotators3D == null) return;
        for (int i = 0; i < rotators3D.Length; i++)
        {
            var r3 = rotators3D[i];
            if (r3 == null) continue;
            int face = Random.Range(1, 7);
            r3.SetTargetFace(face);
        }
    }

    private void UpdateStatusText()
    {
        if (statusText == null) return;
        switch (currentStage)
        {
            case Stage.Ready:
                statusText.text = "阶段：准备中\n提示：按空格开始摇动";
                break;
            case Stage.Shaking:
                statusText.text = "阶段：摇动中（按空格加速，松开自然停）";
                break;
            case Stage.Revealed:
                statusText.text = $"阶段：结果已生成\n即将显示总数（{Mathf.CeilToInt(Mathf.Max(0f, revealTimer))}s）";
                break;
            case Stage.TotalShown:
                // ShowTotal 已经写入包含总数的文本
                break;
        }
    }

    private void EnsureEventSystem()
    {
        // If there's already an EventSystem, do nothing
        var existing = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (existing != null) return;

        var es = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        DontDestroyOnLoad(es);
    }

    private Sprite[] CreateDieSprites(int width, int height, int border, int dotRadius)
    {
        var sprites = new Sprite[6];
        for (int face = 1; face <= 6; face++)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var white = Color.white;
            var black = Color.black;
            var clear = new Color(0, 0, 0, 0);

            // Clear
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, clear);

            // Body + border
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = x >= border && x < width - border && y >= border && y < height - border;
                    if (inside)
                        tex.SetPixel(x, y, white);
                    else if (x >= border - 1 && x < width - (border - 1) && y >= border - 1 && y < height - (border - 1))
                        tex.SetPixel(x, y, black);
                }
            }

            // Pips positions
            int cx = width / 2, cy = height / 2;
            int inner = Mathf.Min(width, height) - border * 2;
            int off = inner / 4; // distance from center

            System.Collections.Generic.List<Vector2Int> points = new System.Collections.Generic.List<Vector2Int>();
            Vector2Int C = new Vector2Int(cx, cy);
            Vector2Int NW = new Vector2Int(cx - off, cy + off);
            Vector2Int NE = new Vector2Int(cx + off, cy + off);
            Vector2Int SW = new Vector2Int(cx - off, cy - off);
            Vector2Int SE = new Vector2Int(cx + off, cy - off);
            Vector2Int Wc = new Vector2Int(cx - off, cy);
            Vector2Int Ec = new Vector2Int(cx + off, cy);

            switch (face)
            {
                case 1: points.Add(C); break;
                case 2: points.Add(NW); points.Add(SE); break;
                case 3: points.Add(NW); points.Add(C); points.Add(SE); break;
                case 4: points.Add(NW); points.Add(NE); points.Add(SW); points.Add(SE); break;
                case 5: points.Add(NW); points.Add(NE); points.Add(C); points.Add(SW); points.Add(SE); break;
                case 6: points.Add(NW); points.Add(NE); points.Add(Wc); points.Add(Ec); points.Add(SW); points.Add(SE); break;
            }

            foreach (var p in points)
                DrawFilledCircle(tex, p.x, p.y, dotRadius, black);

            tex.Apply();
            sprites[face - 1] = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
        return sprites;
    }

    private Sprite[] SelectSkinSprites()
    {
        // 1) If skin asset provided, generate from it
        if (skinAsset != null)
        {
            var s = skinAsset.GenerateSprites();
            if (s != null && s.Length >= 6) return s;
        }
        // 2) Built-in presets (enum selection)
        DiceSkin preset = null;
        switch (defaultSkin)
        {
            case DefaultSkin.Dark: preset = DiceSkin.CreatePresetDark(); break;
            case DefaultSkin.Candy: preset = DiceSkin.CreatePresetCandy(); break;
            case DefaultSkin.Neon: preset = DiceSkin.CreatePresetNeon(); break;
            case DefaultSkin.Classic:
            default: preset = DiceSkin.CreatePresetClassic(); break;
        }
        var sprites = preset.GenerateSprites();
        if (sprites != null && sprites.Length >= 6) return sprites;
        // 3) Fallback to legacy procedural generator
        return CreateDieSprites(256, 256, 8, 14);
    }

    // 新增：分别构建2D/3D骰子
    private void CreateDiceRow2D()
    {
        faceSprites = SelectSkinSprites();
        rotators = new DiceRotator[diceCount];
        renderers = new SpriteRenderer[diceCount];
        diceAudio = new AudioSource[diceCount];

        float totalWidth = (diceCount - 1) * spacing;
        float startX = -totalWidth / 2f;
        for (int i = 0; i < diceCount; i++)
        {
            var dice = new GameObject($"Dice_{i + 1}");
            var sr = dice.AddComponent<SpriteRenderer>();
            sr.sprite = faceSprites[0];
            sr.sortingOrder = 0;
            dice.transform.localScale = Vector3.one * diceSize;
            dice.transform.position = new Vector3(startX + i * spacing, 0f, 0f);
            dice.transform.rotation = Quaternion.identity;

            var rot = dice.AddComponent<DiceRotator>();
            rot.speed = rotationSpeed;
            rot.isRotating = isRotating;
            rot.speedMultiplier = Random.Range(0.8f, 1.3f);
            rot.direction = Random.value < 0.5f ? -1 : 1;
            rot.enableFlip = enableFlip;
            rot.enableSquash = exaggerationCartoon;
            rot.AssignRendererAndFaces(sr, faceSprites, 0);
            rotators[i] = rot;
            renderers[i] = sr;

            if (enableGhostTrail)
            {
                var ghost = dice.AddComponent<GhostTrail>();
                ghost.source = sr;
                ghost.active = false;
            }

            // Per-die audio source (optional)
            if (enableSfx)
            {
                var src = dice.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false; // we'll control loop manually
                src.spatialBlend = spatialBlend;
                src.dopplerLevel = 0f;
                if (sfxMixer != null) src.outputAudioMixerGroup = sfxMixer;
                diceAudio[i] = src;
            }
        }
    }

    private void CreateDiceRow3D()
    {
        faceSprites = SelectSkinSprites();
        rotators3D = new Dice3DRotator[diceCount];
        dice3D = new GameObject[diceCount];
        diceAudio = new AudioSource[diceCount];

        float totalWidth = (diceCount - 1) * spacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < diceCount; i++)
        {
            var root = new GameObject($"Dice3D_{i + 1}");
            root.transform.position = new Vector3(startX + i * spacing, 0f, 0f);
            root.transform.localScale = Vector3.one * diceSize;
            root.transform.rotation = Quaternion.identity;

            BuildCubeWithFaces(root, faceSprites);

            var rot3 = root.AddComponent<Dice3DRotator>();
            rot3.speed = rotationSpeed;
            rot3.isRotating = isRotating;
            // 为每个骰子独立生成随机轴，但避免与相机forward近似（垂直屏幕）
            Vector3 axis = RandomAxisNotNearCamera(cameraDotLimit);
            rot3.SetAxis(axis);
            rotators3D[i] = rot3;
            dice3D[i] = root;

            // Per-die audio source
            if (enableSfx)
            {
                var src = root.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false; // start loop clip manually
                src.spatialBlend = spatialBlend;
                src.dopplerLevel = 0f;
                if (sfxMixer != null) src.outputAudioMixerGroup = sfxMixer;
                diceAudio[i] = src;
            }
        }
    }

    private Vector3 RandomAxisNotNearCamera(float maxAbsDot = 0.5f, int maxTries = 20)
    {
        Vector3 camF = Vector3.forward;
        if (Camera.main != null) camF = Camera.main.transform.forward;
        camF = camF.normalized;
        for (int i = 0; i < maxTries; i++)
        {
            Vector3 a = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            if (a == Vector3.zero) continue;
            a.Normalize();
            if (Mathf.Abs(Vector3.Dot(a, camF)) <= maxAbsDot)
                return a;
        }
        // 兜底：取与相机forward近似正交的方向
        Vector3 fb = Vector3.Cross(camF, Vector3.up);
        if (fb == Vector3.zero) fb = Vector3.Cross(camF, Vector3.right);
        return fb.normalized;
    }

    private void BuildCubeWithFaces(GameObject root, Sprite[] faces)
    {
        Vector3[] pos = new Vector3[]
        {
            new Vector3(0,0,0.5f),  // 1 front +Z
            new Vector3(0.5f,0,0),  // 2 right +X
            new Vector3(0,0,-0.5f), // 3 back -Z
            new Vector3(-0.5f,0,0), // 4 left -X
            new Vector3(0,0.5f,0),  // 5 top +Y
            new Vector3(0,-0.5f,0), // 6 bottom -Y
        };
        Quaternion[] rot = new Quaternion[]
        {
            Quaternion.identity,
            Quaternion.Euler(0,90,0),
            Quaternion.Euler(0,180,0),
            Quaternion.Euler(0,-90,0),
            Quaternion.Euler(-90,0,0),
            Quaternion.Euler(90,0,0),
        };

        for (int i = 0; i < 6; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"Face_{i+1}";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = pos[i];
            quad.transform.localRotation = rot[i];
            quad.transform.localScale = Vector3.one;

            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = quad.GetComponent<MeshRenderer>();
            // Assign a URP-compatible material template if provided to avoid pink in Player builds
            if (faceMaterial != null)
            {
                mr.sharedMaterial = faceMaterial;
            }
            // Use MaterialPropertyBlock to set face texture per quad
            var mpb = new MaterialPropertyBlock();
            if (faces != null && faces.Length >= 6 && faces[i] != null)
            {
                var tex = faces[i].texture;
                mpb.SetTexture("_BaseMap", tex);
                mpb.SetTexture("_MainTex", tex); // fallback for non-URP shaders
            }
            mr.SetPropertyBlock(mpb);
        }
    }

    // 非线性速度因子：0 at E=0, 接近 nonLinearA at 高能量
    private float EffectiveSpeedFactor(float energy)
    {
        if (energy <= 0f) return 0f;
        return nonLinearA * (1f - Mathf.Exp(-nonLinearB * energy));
    }

    // 动态衰减率：基础→空闲提升 + 能量相关 + 速度摩擦
    private float DynamicDecayRate(float energy, float avgSpeedDegPerSec)
    {
        float tIdle = Time.time - lastPressTime;
        float idleRampK = 0f;
        if (tIdle > idleThreshold)
        {
            idleRampK = Mathf.Clamp01((tIdle - idleThreshold) / Mathf.Max(0.01f, rampDuration));
        }
        float decay = Mathf.Lerp(decayBase, decayHigh, idleRampK);
        if (energy > energyThresholdE0)
        {
            decay += decayEnergyK * (energy - energyThresholdE0);
        }
        decay += speedFrictionK * Mathf.Max(0f, avgSpeedDegPerSec);
        return Mathf.Max(0f, decay);
    }

    private void DrawFilledCircle(Texture2D tex, int cx, int cy, int r, Color col)
    {
        int w = tex.width, h = tex.height;
        int r2 = r * r;
        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y <= r2)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < w && py >= 0 && py < h)
                        tex.SetPixel(px, py, col);
                }
            }
        }
    }
}
