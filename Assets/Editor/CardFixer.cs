using UnityEngine;
using UnityEditor;

/// <summary>
/// 一键把 Card.prefab 改成真 3D 卡：正面白卡 + 动物图 + 背面，靠背面剔除自动翻面。
/// 用法：菜单 Tools → 修卡牌 3D。跑一次就能删掉这个文件。
/// </summary>
public class CardFixer
{
    [MenuItem("Tools/修卡牌 3D")]
    public static void Fix()
    {
        // 1. 三个材质（Unlit/Transparent = 透明 + 背面剔除，正好翻面用）
        Material frontMat = MakeMat("CardFront", "Assets/Picture/Front.png");
        Material backMat = MakeMat("CardBack", "Assets/Picture/PG.png");
        Material artMat = MakeMat("CardArt", "Assets/Picture/Animals/壁虎.png");

        // 2. 打开 prefab 改（临时副本，改完存回去）
        string prefabPath = "Assets/Prefab/Card.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError("[修卡] 找不到 " + prefabPath);
            return;
        }

        // 根只当容器，不要渲染器。
        // 因为代码会把根缩放设成统一的 1（放牌）/0.9（拖牌），根上放 Quad 会变成正方形。
        RemoveComponent<MeshRenderer>(root);
        RemoveComponent<MeshFilter>(root);

        // 3. 背面（PG 花纹）：朝 -Z，翻面时露出来
        Transform back = root.transform.Find("Back");
        if (back != null)
        {
            MakeQuad(back, backMat, new Vector3(1.8f, 2.6f, 1f), Quaternion.Euler(0f, 180f, 0f), 0);
            back.localPosition = new Vector3(0f, 0f, -0.02f);
            back.gameObject.SetActive(true);
        }

        // 4. 动物图（正面中间）：朝 +Z，比白卡靠前一点
        Transform art = root.transform.Find("Front");
        if (art != null)
        {
            MakeQuad(art, artMat, new Vector3(1.25f, 1.25f, 1f), Quaternion.identity, 1);
            art.localPosition = new Vector3(0f, 0f, 0.02f);

            Card card = root.GetComponent<Card>();
            if (card != null)
            {
                card.frontRenderer = art.GetComponent<MeshRenderer>();
            }
        }

        // 5. 正面白卡：新加一个子物体（Quad 大小 = 卡面 1.8x2.6）
        MakeQuad(root.transform, "CardFront", frontMat, new Vector3(1.8f, 2.6f, 1f), Quaternion.identity, 0);

        // 点数文字放到最上面
        BumpTextOrder(root, "AttackText");
        BumpTextOrder(root, "HealthText");

        // 存回去
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[修卡] 完成：正面白卡 / 动物图 / 背面都换成 3D Quad，背面剔除自动翻面。");
    }

    // 新建（或复用）材质：Unlit/Transparent + 贴图
    static Material MakeMat(string name, string texPath)
    {
        string path = "Assets/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Unlit/Transparent"));
            AssetDatabase.CreateAsset(mat, path);
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
            if (sp != null) tex = sp.texture;
        }
        mat.mainTexture = tex;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 把某个子物体改成 Quad：删掉旧渲染器，加 MeshFilter(Quad) + MeshRenderer
    static void MakeQuad(Transform t, Material mat, Vector3 scale, Quaternion rot, int sortOrder)
    {
        RemoveComponent<SpriteRenderer>(t.gameObject);
        RemoveComponent<MeshRenderer>(t.gameObject);
        RemoveComponent<MeshFilter>(t.gameObject);

        MeshFilter mf = t.gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        MeshRenderer mr = t.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder = sortOrder;

        t.localScale = scale;
        t.localRotation = rot;
    }

    // 新建一个 Quad 子物体
    static GameObject MakeQuad(Transform parent, string name, Material mat, Vector3 scale, Quaternion rot, int sortOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder = sortOrder;

        go.transform.localScale = scale;
        go.transform.localRotation = rot;
        return go;
    }

    static void BumpTextOrder(GameObject root, string childName)
    {
        Transform t = root.transform.Find(childName);
        if (t == null) return;
        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 2;
    }

    static void RemoveComponent<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c != null) Object.DestroyImmediate(c);
    }
}
