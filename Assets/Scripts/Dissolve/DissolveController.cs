using System.Collections;
using UnityEngine;

/// <summary>
/// 溶解控制器：挂在"要溶解消失"的物体上（2D Sprite 和 3D 模型都行）。
/// 它会自动保留这个物体自己的主贴图，只在运行时把 shader 换成溶解版。
/// 所以不用提前换材质，每个物体的贴图都不会丢。
///
/// 用法：
/// 1. 挂上这个脚本
/// 2. 把 DissolveNoise.png 拖到「溶解噪声图」字段
/// 3. 别的脚本调用 Dissolve()，它就慢慢溶解消失
/// </summary>
public class DissolveController : MonoBehaviour
{
    [Header("溶解设置")]
    private float dissolveTime = 2f;        // 溶解总共花几秒
    public bool destroyWhenDone = true;      // 溶解完要不要删掉这个物体
    public Texture dissolveNoise;            // 溶解噪声图（拖 DissolveNoise.png 进来）

    // 全局共享的噪声图：如果 DissolveManager 设置了它，单个物体不拖噪声图也能用
    public static Texture defaultNoise;

    private Material mat;            // 这个物体自己的材质实例
    private bool dissolving = false; // 正在溶解中吗

    void Start()
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null)
        {
            Debug.LogWarning("DissolveController 挂在没有 Renderer 的物体上：" + gameObject.name);
            return;
        }

        // 生成这个物体自己的材质实例（互不影响）
        mat = r.material;

        // 判断是 2D Sprite 还是 3D，选对应的溶解 shader
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        bool isSprite = (sr != null);
        string shaderName = isSprite ? "Custom/SpriteDissolve" : "Custom/Dissolve";

        // 先记下原来的主贴图（每个物体贴图不一样，必须保留）
        // 注意：2D 的贴图在 Sprite 身上，3D 的在材质上，所以要分开拿
        Texture mainTex = null;
        if (isSprite)
        {
            if (sr.sprite != null)
            {
                mainTex = sr.sprite.texture;
            }
        }
        else
        {
            mainTex = mat.GetTexture("_MainTex");
        }
        Shader shader = Shader.Find(shaderName);

        if (shader == null)
        {
            Debug.LogWarning("找不到溶解 shader：" + shaderName);
            return;
        }

        // 换成溶解 shader
        mat.shader = shader;

        // 把原来的主贴图填回去（关键：贴图不丢）
        if (mainTex != null)
        {
            mat.SetTexture("_MainTex", mainTex);
        }

        // 设置噪声图（优先用自己拖的，没有就用全局默认的）
        Texture noise = dissolveNoise;
        if (noise == null)
        {
            noise = defaultNoise;
        }
        if (noise != null)
        {
            mat.SetTexture("_DissolveTex", noise);
        }
    }

    /// <summary>
    /// 开始溶解。右键组件标题栏就能看到「开始溶解」（方便测试）。
    /// </summary>
    [ContextMenu("开始溶解")]
    public void Dissolve()
    {
        if (dissolving)
        {
            return; // 已经在溶解了，不重复触发
        }
        StartCoroutine(DissolveRoutine());
    }

    /// <summary>
    /// 恢复成完整样子（溶解进度归零）。
    /// </summary>
    public void ResetDissolve()
    {
        if (mat != null)
        {
            mat.SetFloat("_DissolveAmount", 0f);
        }
        dissolving = false;
    }
    [ContextMenu("全部还原")]
    public void ResetAll()
    {
        DissolveManager.Instance.isDissolving = false;   // 你加的标志顺便复位

        DissolveController[] all =
    FindObjectsByType<DissolveController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].ResetDissolve();
        }
    }
    // 真正的溶解过程：进度从 0 慢慢涨到 1
    IEnumerator DissolveRoutine()
    {
        dissolving = true;
        float t = 0f;

        while (t < dissolveTime)
        {
            t = t + Time.deltaTime;
            float progress = t / dissolveTime;   // 算出当前进度 0~1

            if (mat != null)
            {
                mat.SetFloat("_DissolveAmount", progress);
            }
            yield return null;   // 等下一帧
        }

        // 最后确保完全消失
        if (mat != null)
        {
            mat.SetFloat("_DissolveAmount", 1f);
        }

        // 要不要删掉物体
        if (destroyWhenDone)
        {
            Destroy(gameObject);
        }
    }
}
