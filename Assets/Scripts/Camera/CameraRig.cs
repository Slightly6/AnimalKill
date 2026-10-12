using UnityEngine;

/// <summary>
/// 桌面摄像机：透视 + 滚轮在多个预设机位之间循环切换。
/// 每个机位存完整的位置、旋转、视野（缩放），不依赖 LookAt 原点。
/// 挂在 Main Camera 上。
/// 滚轮向上 → 下一个机位；向下 → 上一个机位（循环）。
/// </summary>
public class CameraRig : Singleton<CameraRig>
{
    [System.Serializable]
    public class CameraShot
    {
        public Vector3 position;
        public Vector3 eulerAngles;
        public float fieldOfView = 55f;
    }

    [Header("预设机位列表（在 Inspector 里手动编辑位置/旋转/视野）")]
    public CameraShot[] shots = new CameraShot[]
    {
        new CameraShot { position = new Vector3(0f, 13.2f, 5.5f), eulerAngles = new Vector3(65f, 0f, 0f), fieldOfView = 55f },
        new CameraShot { position = new Vector3(0f, 18f, 2f),  eulerAngles = new Vector3(80f, 0f, 0f), fieldOfView = 55f }
    };

    [Header("镜头参数")]
    public float lerpSpeed = 6f;      // 机位切换速度（越大越快）

    [Header("地图滚动（地图打开时滚轮上下移动相机）")]
    public float mapScrollSpeed = 1.5f;
    public float mapScrollMinY = -8f;
    public float mapScrollMaxY = 8f;

    [Header("桌面高度（和 BoardManager 保持一致）")]
    public float boardHeight = 10f;

    [Header("震屏")]
    public float shakeMaxOffset = 0.12f;   // 满 trauma 时的最大位移
    public float shakeMaxRoll = 1.5f;      // 满 trauma 时的最大 Z 旋转（度）
    public float shakeDecay = 1.8f;       // trauma 衰减速度（越大停得越快）

    private Camera cam;
    private int currentIndex = 0;     // 当前是第几个机位
    private float trauma;             // 震屏强度 0~1，命中时叠加，自动衰减
    private float mapScrollY = 0f;    // 地图打开时的上下滚动偏移

    /// <summary>命中时调用：0.3 小震，0.7 击杀大震</summary>
    public void AddShake(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    /// <summary>外部调：直接跳到第 index 个机位（不滚轮）</summary>
    public void JumpToIndex(int index)
    {
        if (shots == null || shots.Length == 0) return;
        currentIndex = Mathf.Clamp(index, 0, shots.Length - 1);
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = false;     // 强制透视
        if (shots != null && shots.Length > 0)
        {
            var s = shots[0];
            transform.position = s.position + Vector3.up * boardHeight;
            transform.eulerAngles = s.eulerAngles;
            cam.fieldOfView = s.fieldOfView;
        }
    }

    void Update()
    {
        if (shots == null || shots.Length == 0) return;

        float wheel = Input.mouseScrollDelta.y;
        if (wheel == 0f) return;

        if (GameProgress.mapOpen)
        {
            // 地图打开时：滚轮上下移动相机，看长地图
            mapScrollY -= wheel * mapScrollSpeed;
            mapScrollY = Mathf.Clamp(mapScrollY, mapScrollMinY, mapScrollMaxY);
        }
        else if (!GameProgress.InputLocked)
        {
            // 滚轮向上 → 下一个机位；向下 → 上一个机位（循环）
            if (wheel > 0f)
            {
                currentIndex++;
                if (currentIndex >= shots.Length) currentIndex = 0;
            }
            else
            {
                currentIndex--;
                if (currentIndex < 0) currentIndex = shots.Length - 1;
            }
        }
    }

    void LateUpdate()
    {
        // trauma 用真实时间衰减，这样命中停顿（timeScale≈0）期间震动不会卡住
        if (trauma > 0f)
            trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * shakeDecay);

        if (shots == null || shots.Length == 0) return;

        CameraShot target;
        float zOffset = 0f;

        if (GameProgress.mapOpen && shots.Length > 1)
        {
            // 卷轴地图打开：切到第 2 个机位（高位俯瞰全图），
            // currentIndex 不动 → 关图后镜头自然 lerp 回原机位
            target = shots[1];
            zOffset = mapScrollY;   // 滚轮沿世界 Z 轴滚动
        }
        else
        {
            target = shots[currentIndex];
        }

        Vector3 targetPos = target.position + new Vector3(0f, boardHeight, zOffset);
        Quaternion targetRot = Quaternion.Euler(target.eulerAngles);

        // 位置、旋转、视野都往目标靠
        transform.position = Vector3.Lerp(transform.position, targetPos, lerpSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, lerpSpeed * Time.deltaTime);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target.fieldOfView, lerpSpeed * Time.deltaTime);

        // 震屏：trauma² 让小震动克制、大震动猛烈。Perlin 噪声比随机抖动平滑。
        // 地图打开时不叠震屏，保持看地图时镜头稳定。
        if (trauma > 0.001f && !GameProgress.mapOpen)
        {
            float t = Time.unscaledTime * 25f;
            float mag = trauma * trauma;
            float ox = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * mag * shakeMaxOffset;
            float oy = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * mag * shakeMaxOffset;
            float roll = (Mathf.PerlinNoise(t, 10f) - 0.5f) * 2f * mag * shakeMaxRoll;
            transform.position += new Vector3(ox, oy, 0f);
            transform.Rotate(Vector3.forward, roll);
        }
    }
}
