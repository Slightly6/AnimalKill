using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 把 OpenChest 预制体切成「盖子」+「身子」两半，盖子 pivot 放在铰链上，绕轴转就能开盖。
/// 用法：菜单 Tools → 切宝箱(盖子+身子)，点一下。跑完可以删掉本文件。
/// 想改切的位置 / 铰链方向，改下面两个常量再重跑。
/// </summary>
public class ChestSplitter
{
    static float cutRatio = 0.5f;      // 切在高度 50% 处（0=底，1=顶）
    static bool hingeAtMaxZ = false;   // 铰链放 -Z 那一边（true = 放 +Z 那边）

    [MenuItem("Tools/切宝箱(盖子+身子)")]
    public static void Split()
    {
        string prefabPath = "Assets/Chest/OpenChest.prefab";
        string lidPath = "Assets/Chest/OpenChest_Lid.asset";
        string bodyPath = "Assets/Chest/OpenChest_Body.asset";

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError("[切宝箱] 找不到 " + prefabPath);
            return;
        }

        MeshFilter mf = root.GetComponent<MeshFilter>();
        MeshRenderer mr = root.GetComponent<MeshRenderer>();
        if (mf == null)
        {
            Debug.LogError("[切宝箱] OpenChest 没有 MeshFilter");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // 每次从原始完整模型切，别用当前 prefab 的网格（否则重复跑会越切越少、箱顶没了）
        Object[] all = AssetDatabase.LoadAllAssetsAtPath("Assets/Chest/M00656.fbx");
        Mesh src = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is Mesh)
            {
                src = (Mesh)all[i];
                break;
            }
        }
        if (src == null)
        {
            Debug.LogError("[切宝箱] 在 Assets/Chest/M00656.fbx 里没找到网格");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }
        Vector3[] vs = src.vertices;
        Vector3[] ns = src.normals;
        Vector2[] uvs = src.uv;
        int[] tris = src.triangles;

        // 高度范围（原始网格坐标）
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < vs.Length; i++)
        {
            if (vs[i].y < minY) minY = vs[i].y;
            if (vs[i].y > maxY) maxY = vs[i].y;
        }
        float cutY = Mathf.Lerp(minY, maxY, cutRatio);

        // 收集两半的数据
        List<Vector3> lidP = new List<Vector3>(), bodyP = new List<Vector3>();
        List<Vector3> lidN = new List<Vector3>(), bodyN = new List<Vector3>();
        List<Vector2> lidU = new List<Vector2>(), bodyU = new List<Vector2>();
        List<int> lidT = new List<int>(), bodyT = new List<int>();

        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];

            Vector3 n0 = ns.Length > 0 ? ns[i0] : Vector3.up;
            Vector3 n1 = ns.Length > 0 ? ns[i1] : Vector3.up;
            Vector3 n2 = ns.Length > 0 ? ns[i2] : Vector3.up;
            Vector2 u0 = uvs.Length > 0 ? uvs[i0] : Vector2.zero;
            Vector2 u1 = uvs.Length > 0 ? uvs[i1] : Vector2.zero;
            Vector2 u2 = uvs.Length > 0 ? uvs[i2] : Vector2.zero;

            CutTriangle(
                vs[i0], vs[i1], vs[i2],
                n0, n1, n2,
                u0, u1, u2,
                cutY,
                lidP, lidN, lidU, lidT,
                bodyP, bodyN, bodyU, bodyT);
        }

        if (lidT.Count == 0 || bodyT.Count == 0)
        {
            Debug.LogError("[切宝箱] 有一半是空的，把 cutRatio 调到 0~1 之间再跑");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // 造两个网格
        Mesh lid = BuildMesh("OpenChest_Lid", lidP, lidN, lidU, lidT);
        Mesh body = BuildMesh("OpenChest_Body", bodyP, bodyN, bodyU, bodyT);

        // 盖子的 XZ 范围（原始坐标）
        float lminX = float.MaxValue, lmaxX = float.MinValue, lminZ = float.MaxValue, lmaxZ = float.MinValue;
        for (int i = 0; i < lidP.Count; i++)
        {
            if (lidP[i].x < lminX) lminX = lidP[i].x;
            if (lidP[i].x > lmaxX) lmaxX = lidP[i].x;
            if (lidP[i].z < lminZ) lminZ = lidP[i].z;
            if (lidP[i].z > lmaxZ) lmaxZ = lidP[i].z;
        }

        // 铰链点 = 切面高度 + 盖子 X 中心 + 后边沿（Z 最大那边）
        Vector3 hinge = new Vector3((lminX + lmaxX) * 0.5f, cutY, hingeAtMaxZ ? lmaxZ : lminZ);

        // 盖子所有顶点平移 -hinge，让 pivot 正好落在铰链上
        Vector3[] lidVerts = lid.vertices;
        for (int i = 0; i < lidVerts.Length; i++) lidVerts[i] -= hinge;
        lid.vertices = lidVerts;
        lid.RecalculateBounds();

        // 存两个网格（重复跑会覆盖）
        AssetDatabase.DeleteAsset(lidPath);
        AssetDatabase.DeleteAsset(bodyPath);
        AssetDatabase.CreateAsset(lid, lidPath);
        AssetDatabase.CreateAsset(body, bodyPath);

        // 根 = 身子
        mf.sharedMesh = body;

        // 加「Lid」子物体
        RemoveChild(root, "Lid");
        GameObject lidGo = new GameObject("Lid");
        lidGo.transform.SetParent(root.transform, false);
        lidGo.transform.localPosition = hinge;
        lidGo.transform.localRotation = Quaternion.identity;
        lidGo.transform.localScale = Vector3.one;

        MeshFilter lmf = lidGo.AddComponent<MeshFilter>();
        lmf.sharedMesh = lid;
        MeshRenderer lmr = lidGo.AddComponent<MeshRenderer>();
        if (mr != null) lmr.sharedMaterial = mr.sharedMaterial;

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[切宝箱] 完成。切在 Y=" + cutY.ToString("F2") + "，铰链在 " + hinge + "。选中 Lid 绕 X 轴转就能开盖。");
    }

    // 把一个三角形按高度切到盖子/身子两半，跨切线的三角形拆成两段
    static void CutTriangle(
        Vector3 a, Vector3 b, Vector3 c,
        Vector3 na, Vector3 nb, Vector3 nc,
        Vector2 ua, Vector2 ub, Vector2 uc,
        float cutY,
        List<Vector3> lidP, List<Vector3> lidN, List<Vector2> lidU, List<int> lidT,
        List<Vector3> bodyP, List<Vector3> bodyN, List<Vector2> bodyU, List<int> bodyT)
    {
        bool aa = a.y >= cutY;
        bool bb = b.y >= cutY;
        bool cc = c.y >= cutY;
        int above = (aa ? 1 : 0) + (bb ? 1 : 0) + (cc ? 1 : 0);

        // 全在上 / 全在下：原样塞进去
        if (above == 3)
        {
            PushTri(lidP, lidN, lidU, lidT, a, b, c, na, nb, nc, ua, ub, uc);
            return;
        }
        if (above == 0)
        {
            PushTri(bodyP, bodyN, bodyU, bodyT, a, b, c, na, nb, nc, ua, ub, uc);
            return;
        }

        if (above == 1)
        {
            // 唯一在上的顶点放 A，另外两个按环形顺序 B、C
            Vector3 A, B, C; Vector3 nA, nB, nC; Vector2 uA, uB, uC;
            if (aa) { A = a; B = b; C = c; nA = na; nB = nb; nC = nc; uA = ua; uB = ub; uC = uc; }
            else if (bb) { A = b; B = c; C = a; nA = nb; nB = nc; nC = na; uA = ub; uB = uc; uC = ua; }
            else { A = c; B = a; C = b; nA = nc; nB = na; nC = nb; uA = uc; uB = ua; uC = ub; }

            Vector3 p1, p2; Vector3 n1, n2; Vector2 u1, u2;
            CutPoint(A, B, nA, nB, uA, uB, cutY, out p1, out n1, out u1);
            CutPoint(A, C, nA, nC, uA, uC, cutY, out p2, out n2, out u2);

            // 上：A-p1-p2 一个三角；下：B-C-p2、B-p2-p1 两个三角
            PushTri(lidP, lidN, lidU, lidT, A, p1, p2, nA, n1, n2, uA, u1, u2);
            PushTri(bodyP, bodyN, bodyU, bodyT, B, C, p2, nB, nC, n2, uB, uC, u2);
            PushTri(bodyP, bodyN, bodyU, bodyT, B, p2, p1, nB, n2, n1, uB, u2, u1);
            return;
        }

        // above == 2（一个在下）
        Vector3 A2, B2, C2; Vector3 nA2, nB2, nC2; Vector2 uA2, uB2, uC2;
        if (!aa) { C2 = a; A2 = b; B2 = c; nC2 = na; nA2 = nb; nB2 = nc; uC2 = ua; uA2 = ub; uB2 = uc; }
        else if (!bb) { C2 = b; A2 = c; B2 = a; nC2 = nb; nA2 = nc; nB2 = na; uC2 = ub; uA2 = uc; uB2 = ua; }
        else { C2 = c; A2 = a; B2 = b; nC2 = nc; nA2 = na; nB2 = nb; uC2 = uc; uA2 = ua; uB2 = ub; }

        Vector3 q1, q2; Vector3 m1, m2; Vector2 v1, v2;
        CutPoint(A2, C2, nA2, nC2, uA2, uC2, cutY, out q1, out m1, out v1);
        CutPoint(B2, C2, nB2, nC2, uB2, uC2, cutY, out q2, out m2, out v2);

        // 上：A-B-q2、A-q2-q1 两个三角；下：C-q1-q2 一个三角
        PushTri(lidP, lidN, lidU, lidT, A2, B2, q2, nA2, nB2, m2, uA2, uB2, v2);
        PushTri(lidP, lidN, lidU, lidT, A2, q2, q1, nA2, m2, m1, uA2, v2, v1);
        PushTri(bodyP, bodyN, bodyU, bodyT, C2, q1, q2, nC2, m1, m2, uC2, v1, v2);
    }

    // 求线段 a-b 与切面（y=cutY）的交点，法线/UV 一起按比例插值
    static void CutPoint(Vector3 a, Vector3 b, Vector3 na, Vector3 nb, Vector2 ua, Vector2 ub, float cutY,
        out Vector3 p, out Vector3 n, out Vector2 u)
    {
        float t = (cutY - a.y) / (b.y - a.y);
        p = Vector3.Lerp(a, b, t);
        p.y = cutY;   // 钉死在切面上，防止浮点误差留缝
        n = Vector3.Lerp(na, nb, t).normalized;
        u = Vector2.Lerp(ua, ub, t);
    }

    // 把三个顶点塞进一个列表，返回起始索引
    static void PushTri(
        List<Vector3> P, List<Vector3> N, List<Vector2> U, List<int> T,
        Vector3 a, Vector3 b, Vector3 c,
        Vector3 na, Vector3 nb, Vector3 nc,
        Vector2 ua, Vector2 ub, Vector2 uc)
    {
        int i0 = P.Count;
        P.Add(a); P.Add(b); P.Add(c);
        N.Add(na); N.Add(nb); N.Add(nc);
        U.Add(ua); U.Add(ub); U.Add(uc);
        T.Add(i0); T.Add(i0 + 1); T.Add(i0 + 2);
    }

    static Mesh BuildMesh(string name, List<Vector3> P, List<Vector3> N, List<Vector2> U, List<int> T)
    {
        Mesh m = new Mesh();
        m.name = name;
        m.vertices = P.ToArray();
        m.normals = N.ToArray();
        m.uv = U.ToArray();
        m.triangles = T.ToArray();
        m.RecalculateBounds();
        return m;
    }

    static void RemoveChild(GameObject root, string name)
    {
        Transform t = root.transform.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }
}
