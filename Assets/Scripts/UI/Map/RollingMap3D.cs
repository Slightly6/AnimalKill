using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 卷轴式地图：纸张从螺旋卷状态逐渐铺平。
/// 纸张沿本地 Z 轴展开，卷轴沿本地 X 轴。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RollingMap3D : MonoBehaviour
{
    [Header("起点 / 终点")]
    public Transform enemyPos;
    public Transform tableCenter;
    public float startOffset = 1f;

    [Header("纸张")]
    [Min(0.1f)] public float paperLength = 6f;
    [Min(0.1f)] public float paperWidth = 4f;
    [Min(32)] public int lengthCuts = 256;

    [Header("入场升起 / 收起下沉（世界坐标 Y）")]
    [Tooltip("升起终点：卷轴展开时贴住的桌面高度")]
    public float upperY = 0.02f;
    [Tooltip("下沉终点：纸卷藏在桌面下的高度（要小于 upperY）")]
    public float lowerY = -0.4f;
    [Min(0.01f)] public float riseDuration = 0.45f;

    [Header("卷轴")]
    [Tooltip("纸卷中心的内半径")]
    [Min(0.01f)] public float coreRadius = 0.12f;

    [Tooltip("纸张厚度，决定螺旋层之间的间距")]
    [Min(0.0001f)] public float paperThickness = 0.0025f;

    [Header("动画")]
    [Min(0.01f)] public float rollDuration = 1.2f;

    [Header("材质")]
    public Material paperMaterial;

    [Header("调试")]
    [Range(0f, 1f)] public float debugProgress = 0f;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh paperMesh;

    private Vector3[] vertices;
    private Vector2[] uvs;
    private int[] triangles;

    private Coroutine activeAnimation;

    private void Awake()
    {
        BuildPaperMesh();
        SetProgress(debugProgress);

        // 卷着开局：先藏到桌面下，等 RollOut 升起，避免开局第一帧纸卷摆在桌上闪一下
        if (debugProgress <= 0.001f && lowerY < upperY)
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, lowerY, p.z);
        }
    }

    private void OnValidate()
    {
        paperLength = Mathf.Max(0.1f, paperLength);
        paperWidth = Mathf.Max(0.1f, paperWidth);
        lengthCuts = Mathf.Max(32, lengthCuts);
        coreRadius = Mathf.Max(0.01f, coreRadius);
        paperThickness = Mathf.Max(0.0001f, paperThickness);
        rollDuration = Mathf.Max(0.01f, rollDuration);

        // Editor 下也要建好 mesh 才能看见（OnValidate 期间 Unity 禁止 SendMessage，延迟一拍再建）
if (paperMesh == null || vertices == null || vertices.Length != (lengthCuts + 1) * 2)
{
#if UNITY_EDITOR
    UnityEditor.EditorApplication.delayCall += () => { if (this != null) BuildPaperMesh(); };
#else
    BuildPaperMesh();
#endif
}
        SetProgress(debugProgress);
    }

    private void OnDestroy()
    {
        if (paperMesh != null)
            Destroy(paperMesh);
    }

    /// <summary>
    /// 重建纸张网格。运行时修改 lengthCuts 后调用此方法。
    /// </summary>
    [ContextMenu("Rebuild Paper Mesh")]
    public void BuildPaperMesh()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (paperMesh != null)
        {
            if (Application.isPlaying) Destroy(paperMesh);
            else DestroyImmediate(paperMesh);
        }

        paperMesh = new Mesh
        {
            name = "Rolling Paper Mesh"
        };

        // 这是动态变形网格。
        paperMesh.MarkDynamic();

        int vertexCount = (lengthCuts + 1) * 2;

        vertices = new Vector3[vertexCount];
        uvs = new Vector2[vertexCount];
        triangles = new int[lengthCuts * 6];

        if (vertexCount > 65535)
            paperMesh.indexFormat = IndexFormat.UInt32;

        int triIndex = 0;

        for (int i = 0; i <= lengthCuts; i++)
        {
            float v = (float)i / lengthCuts;
            int left = i * 2;
            int right = left + 1;

            uvs[left] = new Vector2(0f, v);
            uvs[right] = new Vector2(1f, v);

            if (i == lengthCuts)
                continue;

            int nextLeft = left + 2;
            int nextRight = right + 2;

            // 三角形正面朝上。
            triangles[triIndex++] = left;
            triangles[triIndex++] = nextLeft;
            triangles[triIndex++] = right;

            triangles[triIndex++] = right;
            triangles[triIndex++] = nextLeft;
            triangles[triIndex++] = nextRight;
        }

        paperMesh.vertices = vertices;
        paperMesh.uv = uvs;
        paperMesh.triangles = triangles;
        paperMesh.RecalculateNormals();
        paperMesh.RecalculateBounds();

        meshFilter.sharedMesh = paperMesh;

        if (paperMaterial != null)
            meshRenderer.sharedMaterial = paperMaterial;

        meshRenderer.shadowCastingMode = ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
    }

    /// <summary>
    /// 设置展开进度：0 是卷起，1 是完全铺开。
    /// </summary>
    public void SetProgress(float progress)
    {
        debugProgress = Mathf.Clamp01(progress);

        if (paperMesh == null || vertices == null)
            return;

        AlignToPath();
        UpdatePaperGeometry(debugProgress);
    }
    /// <summary>
    /// 升起 → 铺开地图 → 生成节点。
    /// 可在协程中使用：yield return map.RollOut();
    /// </summary>
    public Coroutine RollOut()
    {
        GameProgress.mapOpen = true;   // 铺图期间锁住战斗输入

        if (activeAnimation != null) StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(RollOutRoutine());
        return activeAnimation;
    }

    /// <summary>
    /// 清除节点 → 卷起地图 → 沉回桌面下。
    /// 可在协程中使用：yield return map.RollUp();
    /// </summary>
    public Coroutine RollUp()
    {
        if (TableMapGenerator.Instance != null)
            TableMapGenerator.Instance.ClearGeneratedObjects();   // 节点和线必须在卷起前清

        if (activeAnimation != null) StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(RollUpRoutine());
        return activeAnimation;
    }

    // 升起（纸保持卷着，不能调 SetProgress，否则 y 会被 AlignToPath 拉回 upperY）→ 再展开
    private IEnumerator RollOutRoutine()
    {
        AlignToPath();   // 先定好 X/Z/朝向，此时 y = upperY
        Vector3 landedPos = transform.position;
        Vector3 hiddenPos = new Vector3(landedPos.x, lowerY, landedPos.z);

        if (lowerY < upperY - 0.0001f)
        {
            transform.position = hiddenPos;

            float elapsed = 0f;
            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);   // ease-out：出桌快、到位轻
                transform.position = Vector3.Lerp(hiddenPos, landedPos, eased);
                yield return null;
            }
            transform.position = landedPos;
        }

        yield return RunRollProgress(1f);
        TableMapGenerator.Instance.GenerateMap();
        activeAnimation = null;
    }

    // 原地卷成纸卷 → 再整体沉到桌面下
    private IEnumerator RollUpRoutine()
    {
        yield return RunRollProgress(0f);   // SetProgress 每帧把 y 钉在 upperY，卷完位置就是 landedPos

        if (lowerY < upperY - 0.0001f)
        {
            Vector3 landedPos = transform.position;
            Vector3 hiddenPos = new Vector3(landedPos.x, lowerY, landedPos.z);

            float elapsed = 0f;
            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseDuration);
                float eased = t * t * t;     // ease-in：开始慢、越沉越快，像被桌面吸进去
                transform.position = Vector3.Lerp(landedPos, hiddenPos, eased);
                yield return null;
            }
            transform.position = hiddenPos;
        }

        activeAnimation = null;
    }

    // 只做卷/展进度动画，不改 activeAnimation（由外层两段式协程持有）
    private IEnumerator RunRollProgress(float targetProgress)
    {
        float from = debugProgress;
        float elapsed = 0f;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rollDuration);
            float eased = t * t * (3f - 2f * t);   // SmoothStep 平滑起止
            SetProgress(Mathf.Lerp(from, targetProgress, eased));
            yield return null;
        }

        SetProgress(targetProgress);
    }

    private void AlignToPath()
    {
        if (enemyPos == null || tableCenter == null)
            return;

        Vector3 direction = tableCenter.position - enemyPos.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;
        else
            direction.Normalize();

        Vector3 origin = enemyPos.position + direction * startOffset;
        origin.y = upperY;

        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void UpdatePaperGeometry(float progress)
    {
        float unfoldedLength = progress * paperLength;
        float remainingLength = paperLength - unfoldedLength;
        float thickness = Mathf.Max(0.0001f, paperThickness);

        // 纸卷的外半径会随着纸张铺开逐渐缩小。
        float outerRadius = Mathf.Sqrt(
            coreRadius * coreRadius +
            thickness * remainingLength / Mathf.PI
        );

        for (int i = 0; i <= lengthCuts; i++)
        {
            float materialZ = paperLength * ((float)i / lengthCuts);

            float y;
            float z;

            if (materialZ <= unfoldedLength || remainingLength <= 0.0001f)
            {
                // 已铺开的部分平放在桌面上。
                y = 0f;
                z = materialZ;
            }
            else
            {
                // 尚未铺开的部分沿螺旋卷曲。
                float distanceOnRoll = materialZ - unfoldedLength;

                float radiusSquared =
                    outerRadius * outerRadius -
                    thickness * distanceOnRoll / Mathf.PI;

                float radius = Mathf.Sqrt(
                    Mathf.Max(coreRadius * coreRadius, radiusSquared)
                );

                // 让纸张从桌面切线处平滑进入卷轴。
                float angle =
                    -Mathf.PI * 0.5f +
                    (2f * Mathf.PI / thickness) * (outerRadius - radius);

                y = outerRadius + radius * Mathf.Sin(angle);
                z = unfoldedLength + radius * Mathf.Cos(angle);
            }

            int left = i * 2;
            int right = left + 1;

            vertices[left] = new Vector3(-paperWidth * 0.5f, y, z);
            vertices[right] = new Vector3(paperWidth * 0.5f, y, z);
        }

        paperMesh.vertices = vertices;
        paperMesh.RecalculateNormals();
        paperMesh.RecalculateBounds();
    }
    


}