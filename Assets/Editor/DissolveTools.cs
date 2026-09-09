using UnityEditor;
using UnityEngine;

/// <summary>
/// 溶解编辑器工具（只在编辑器用，不会进游戏）。
/// 用法：在 Hierarchy 里选中要溶解的物体（可以多选，或选一个父物体一次搞定），
/// 然后点菜单 Tools → 溶解 → 给选中物体挂 DissolveController。
/// 会自动给它们（以及所有子物体）里有 Renderer 的物体挂上 DissolveController。
/// 注意：只挂脚本，不改材质（贴图由 DissolveController 运行时自己保留）。
/// </summary>
public class DissolveTools
{
    [MenuItem("Tools/溶解/给选中物体挂 DissolveController（含子物体）")]
    static void AddDissolveToSelected()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            Debug.LogWarning("先在 Hierarchy 里选中一些物体，再点这个菜单");
            return;
        }

        int count = 0;
        for (int i = 0; i < selected.Length; i++)
        {
            count += AddDissolveRecursive(selected[i]);
        }

        Debug.Log("完成：给 " + count + " 个物体挂上了 DissolveController");
    }

    // 递归：给这个物体和它所有子物体里，有 Renderer 的挂上 DissolveController
    static int AddDissolveRecursive(GameObject go)
    {
        int count = 0;

        // 只有能被渲染的物体（有 Renderer）才需要挂，而且别重复挂
        if (go.GetComponent<Renderer>() != null && go.GetComponent<DissolveController>() == null)
        {
            go.AddComponent<DissolveController>();
            count++;
        }

        // 继续处理所有子物体
        for (int i = 0; i < go.transform.childCount; i++)
        {
            count += AddDissolveRecursive(go.transform.GetChild(i).gameObject);
        }

        return count;
    }
}
