using UnityEngine;

/// <summary>
/// 溶解管理器：调用一次 DissolveAll()，让场景里所有挂了 DissolveController 的物体一起溶解。
/// 用法：
/// 1. 给所有"要溶解"的物体挂 DissolveController（hero 和 boss 不挂）
/// 2. 场景里放一个物体挂 DissolveManager
/// 3. 把 DissolveNoise.png 拖到「溶解噪声图」字段（拖这一次，所有物体共享）
/// 4. 调用 DissolveAll()，或者右键组件标题栏选「全部溶解」测试
/// </summary>
public class DissolveManager : Singleton<DissolveManager>
{
    [Header("溶解噪声图（拖一次，所有物体共享）")]
    public Texture dissolveNoise;
    public bool isDissolving = false; // 是否溶解
    void Start()
    {
        // 让所有 DissolveController 共享这张噪声图，就不用一个个拖了
        DissolveController.defaultNoise = dissolveNoise;
    }

    /// <summary>
    /// 让场景里所有 DissolveController 一起溶解。
    /// </summary>
    [ContextMenu("全部溶解")]
    public void DissolveAll()
    {
        isDissolving = true;
        DissolveController[] all = FindObjectsByType<DissolveController>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            all[i].Dissolve();
        }
    }
}
