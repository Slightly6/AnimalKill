using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键把当前场景改成"黑暗草原"风格。
/// 菜单：Tools → Setup Dark Grassland Scene
///
/// 做的事：
///   1. 相机背景 → 深青黑
///   2. 方向光 → 冷色月光
///   3. 环境光 → 冷青灰
///   4. 雾 → 深青雾
///   5. 天空盒 → 黑暗草原渐变天空盒
///   6. 桌面材质 → 压暗（深棕木色）
///   7. 墙 → 禁用（草原不要墙）
///   8. 地面 → 新建大 Plane，深绿草色材质
///   9. 远景 → 一圈树影剪影
///  10. 萤火虫 → ParticleSystem
/// </summary>
public class DarkGrasslandSceneSetup
{
    [MenuItem("Tools/Setup Dark Grassland Scene")]
    public static void Setup()
    {
        var scene = SceneManager.GetActiveScene();
        Debug.Log("[Setup] 开始配置黑暗草原场景：" + scene.name);

        SetupCamera();
        SetupMoonlight();
        SetupAmbientAndFog();
        SetupSkybox();
        SetupTable();
        // DisableWalls();
        SetupGround();
        SetupTreeSilhouettes();
        SetupFireflies();

        Debug.Log("[Setup] 黑暗草原场景配置完成！运行游戏看看效果。");
    }

    // 只生成桌子（桌子被误删时点这个）
    [MenuItem("Tools/Setup Table Only")]
    public static void SetupTableOnly()
    {
        SetupTable();
        Debug.Log("[Setup] 桌子已生成");
    }

    // 一键把卡牌材质改成暗金色调（匹配黑暗草原主题）
    [MenuItem("Tools/Style Cards to Dark Grassland")]
    public static void StyleCards()
    {
        // 卡正面底：深墨绿（呼应赌桌绒布）
        SetMaterialColor("Assets/Materials/CardFront.mat", new Color(0.08f, 0.22f, 0.12f, 1f), 0.3f, 0f);
        // 卡背面：深绿 + 金边感
        SetMaterialColor("Assets/Materials/CardBack.mat", new Color(0.05f, 0.18f, 0.10f, 1f), 0.4f, 0.1f);
        // 动物图材质：轻微压暗，不抢主色
        SetMaterialColor("Assets/Materials/CardArt.mat", new Color(0.9f, 0.9f, 0.85f, 1f), 0.5f, 0f);

        Debug.Log("[Setup] 卡牌材质已改成暗金色调");
    }

    // 改材质的颜色 + 光滑度 + 金属度
    static void SetMaterialColor(string assetPath, Color color, float glossiness, float metallic)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            Debug.LogWarning("[Setup] 找不到材质：" + assetPath);
            return;
        }
        Undo.RecordObject(mat, "Style Card Material");
        mat.color = color;
        mat.SetFloat("_Glossiness", glossiness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
    }

    // 1. 相机背景
    static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            cam = Object.FindObjectOfType<Camera>();
        }
        if (cam == null)
        {
            Debug.LogWarning("[Setup] 没找到相机");
            return;
        }
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.04f, 0.1f, 0.1f, 1f);   // 兜底深青黑
        Undo.RecordObject(cam, "Setup Camera");
    }

    // 2. 方向光（月光）
    static void SetupMoonlight()
    {
        Light dirLight = null;
        foreach (var l in Object.FindObjectsOfType<Light>())
        {
            if (l.type == LightType.Directional) { dirLight = l; break; }
        }
        if (dirLight == null)
        {
            var go = new GameObject("Moonlight");
            dirLight = go.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            Undo.RegisterCreatedObjectUndo(go, "Create Moonlight");
        }
        Undo.RecordObject(dirLight, "Setup Moonlight");
        dirLight.gameObject.name = "Moonlight";   // 统一改名，Hierarchy 里好找
        dirLight.color = new Color(0.78f, 0.85f, 0.91f, 1f);   // #c8d8e8 冷白偏蓝
        dirLight.intensity = 0.8f;
        dirLight.shadows = LightShadows.Soft;
        dirLight.shadowStrength = 0.6f;
        dirLight.shadowBias = 0.05f;
        dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    // 3. 环境光 + 雾
    static void SetupAmbientAndFog()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.23f, 0.23f, 1f);   // #2a3a3a 冷青灰
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0.3f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.02f;
        RenderSettings.fogColor = new Color(0.08f, 0.13f, 0.13f, 1f);   // #152020 深青雾
    }

    // 4. 天空盒
    static void SetupSkybox()
    {
        // 找天空盒 shader
        var shader = Shader.Find("Custom/DarkGrasslandSkybox");
        if (shader == null)
        {
            Debug.LogWarning("[Setup] 找不到 DarkGrasslandSkybox shader，跳过天空盒");
            return;
        }

        var skyboxMat = new Material(shader);
        skyboxMat.name = "DarkGrasslandSkybox";

        RenderSettings.skybox = skyboxMat;
        RenderSettings.sun = Object.FindObjectOfType<Light>();
    }

    // 5. 桌子：保留你手动改好的 TableTop，只重算金边和桌腿
    static void SetupTable()
    {
        var table = GameObject.Find("Table_DarkGrass");
        if (table == null)
        {
            table = new GameObject("Table_DarkGrass");
            Undo.RegisterCreatedObjectUndo(table, "Create Table");
        }

        // 找现有的 TableTop（你手动改过大小的那个），没有就新建一个默认的
        var top = table.transform.Find("TableTop");
        if (top == null)
        {
            float boardY = 10f;
            var bm = Object.FindObjectOfType<BoardManager>();
            if (bm != null) boardY = bm.boardHeight;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TableTop";
            go.transform.SetParent(table.transform);
            go.transform.position = new Vector3(0, boardY - 0.1f, -1.25f);
            go.transform.localScale = new Vector3(13f, 0.2f, 9f);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var feltMat = new Material(Shader.Find("Standard"));
            feltMat.name = "TableFelt_DarkGreen";
            feltMat.color = new Color(0.06f, 0.18f, 0.1f, 1f);
            feltMat.SetFloat("_Glossiness", 0.75f);
            feltMat.SetFloat("_Metallic", 0f);
            go.GetComponent<Renderer>().sharedMaterial = feltMat;

            top = go.transform;
        }

        // 清掉旧的金边和桌腿（只删 Rim_ 和 Leg_ 开头的，保留 TableTop 本身）
        for (int i = top.childCount - 1; i >= 0; i--)
        {
            var child = top.GetChild(i);
            if (child.name.StartsWith("Rim_") || child.name.StartsWith("Leg_"))
                Object.DestroyImmediate(child.gameObject);
        }

        // 金边和桌腿作为 TableTop 的子物体，缩放自动联动
        Vector3 s = top.localScale;   // TableTop 当前实际尺寸

        var goldMat = new Material(Shader.Find("Standard"));
        goldMat.name = "TableRim_Gold";
        goldMat.color = new Color(0.75f, 0.6f, 0.2f, 1f);
        goldMat.SetFloat("_Glossiness", 0.7f);
        goldMat.SetFloat("_Metallic", 0.9f);

        const float rimW = 0.15f;
        const float rimH = 0.08f;
        float rimY = 0.5f + rimH * 0.5f / s.y;   // 顶面之上（局部坐标）

        // 前/后边：X 方向铺满，Z 方向是金边宽度
        CreateRim(top, new Vector3(0, rimY, 0.5f), new Vector3(1f, rimH / s.y, rimW / s.z), goldMat, "Rim_Front");
        CreateRim(top, new Vector3(0, rimY, -0.5f), new Vector3(1f, rimH / s.y, rimW / s.z), goldMat, "Rim_Back");
        // 左/右边：Z 方向铺满，X 方向是金边宽度
        CreateRim(top, new Vector3(-0.5f, rimY, 0), new Vector3(rimW / s.x, rimH / s.y, 1f), goldMat, "Rim_Left");
        CreateRim(top, new Vector3(0.5f, rimY, 0), new Vector3(rimW / s.x, rimH / s.y, 1f), goldMat, "Rim_Right");

        // 桌腿（4 根）
        var legMat = new Material(Shader.Find("Standard"));
        legMat.name = "TableLeg_Dark";
        legMat.color = new Color(0.08f, 0.06f, 0.04f, 1f);
        legMat.SetFloat("_Glossiness", 0.3f);
        legMat.SetFloat("_Metallic", 0.1f);

        float legWorldH = top.position.y - s.y * 0.5f;   // 桌面底到地面
        float legLocalH = legWorldH / s.y;
        float legLocalY = -0.5f - legLocalH * 0.5f;
        Vector3 legScale = new Vector3(0.3f / s.x, legLocalH, 0.3f / s.z);

        CreateLeg(top, new Vector3(-0.45f, legLocalY, 0.45f), legScale, legMat, "Leg_BL");
        CreateLeg(top, new Vector3(0.45f, legLocalY, 0.45f), legScale, legMat, "Leg_BR");
        CreateLeg(top, new Vector3(-0.45f, legLocalY, -0.45f), legScale, legMat, "Leg_FL");
        CreateLeg(top, new Vector3(0.45f, legLocalY, -0.45f), legScale, legMat, "Leg_FR");
    }

    // 生成一条金边（局部坐标）
    static void CreateRim(Transform parent, Vector3 localPos, Vector3 localScale, Material mat, string name)
    {
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rim.name = name;
        rim.transform.SetParent(parent, false);
        rim.transform.localPosition = localPos;
        rim.transform.localScale = localScale;
        Undo.RegisterCreatedObjectUndo(rim, "Create Rim");
        Object.DestroyImmediate(rim.GetComponent<Collider>());
        rim.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 生成一根桌腿（局部坐标）
    static void CreateLeg(Transform parent, Vector3 localPos, Vector3 localScale, Material mat, string name)
    {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = name;
        leg.transform.SetParent(parent, false);
        leg.transform.localPosition = localPos;
        leg.transform.localScale = localScale;
        Undo.RegisterCreatedObjectUndo(leg, "Create Leg");
        Object.DestroyImmediate(leg.GetComponent<Collider>());
        leg.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 6. 禁用墙（草原不要墙）
    // static void DisableWalls()
    // {
    //     var walls = Object.FindObjectsOfType<Wall>();
    //     foreach (var w in walls)
    //     {
    //         Undo.RecordObject(w.gameObject, "Disable Wall");
    //         w.gameObject.SetActive(false);
    //     }
    //     Debug.Log("[Setup] 禁用了 " + walls.Length + " 个墙对象");
    // }

    // 7. 地面（用素材里的 forest_ground_06 PBR 贴图）
    static void SetupGround()
    {
        var existing = GameObject.Find("Ground_DarkGrass");
        GameObject ground = existing;

        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_DarkGrass";
            Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
        }

        Undo.RecordObject(ground.transform, "Setup Ground");
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(15f, 1f, 15f);   // 150x150

        string texFolder = "Assets/素材/textures/";
        string diffPath  = texFolder + "forest_ground_06_diff_4k.jpg";
        string norPath   = texFolder + "forest_ground_06_nor_gl_4k.exr";
        string roughPath = texFolder + "forest_ground_06_rough_4k.exr";

        // 把法线贴图导入类型设为 Normal Map（否则不生效）
        SetNormalMapImport(norPath);
        // 4K 太大，限制最大尺寸 2048 省内存
        SetTextureMaxSize(diffPath, 2048);
        SetTextureMaxSize(norPath, 2048);
        SetTextureMaxSize(roughPath, 2048);

        var diffuse = AssetDatabase.LoadAssetAtPath<Texture>(diffPath);
        var normal  = AssetDatabase.LoadAssetAtPath<Texture>(norPath);

        var groundMat = new Material(Shader.Find("Standard"));
        groundMat.name = "DarkGrassGround";

        if (diffuse != null)
        {
            groundMat.SetTexture("_MainTex", diffuse);
            groundMat.SetTextureScale("_MainTex", new Vector2(8f, 8f));   // 平铺 8 次
        }
        if (normal != null)
        {
            groundMat.SetTexture("_BumpMap", normal);
            groundMat.SetFloat("_BumpScale", 6.0f);
        }
        groundMat.color = Color.white;
        groundMat.SetFloat("_Glossiness", 0.35f);   // 地表偏粗糙
        groundMat.SetFloat("_Metallic", 0f);

        var renderer = ground.GetComponent<Renderer>();
        Undo.RecordObject(renderer, "Setup Ground");
        renderer.sharedMaterial = groundMat;

        Debug.Log("[Setup] 地面贴图：diffuse=" + (diffuse != null) + ", normal=" + (normal != null));
    }

    // 把贴图导入类型设为 Normal Map
    static void SetNormalMapImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    // 限制贴图最大尺寸（省内存）
    static void SetTextureMaxSize(string assetPath, int maxSize)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        if (importer.maxTextureSize != maxSize)
        {
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }
    }

    // 8. 远景树影剪影（一圈圆锥+圆柱拼的简单树）
    static void SetupTreeSilhouettes()
    {
        var parent = GameObject.Find("TreeSilhouettes");
        if (parent == null)
        {
            parent = new GameObject("TreeSilhouettes");
            Undo.RegisterCreatedObjectUndo(parent, "Create Trees");
        }

        // 删掉旧的
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);
        }

        // 树影材质：近黑绿
        var treeMat = new Material(Shader.Find("Standard"));
        treeMat.name = "TreeSilhouette";
        treeMat.color = new Color(0.03f, 0.07f, 0.03f, 1f);   // #0a120a 近黑绿
        treeMat.SetFloat("_Glossiness", 0f);
        treeMat.SetFloat("_Metallic", 0f);

        int treeCount = 16;
        float radius = 18f;
        for (int i = 0; i < treeCount; i++)
        {
            float angle = (i / (float)treeCount) * Mathf.PI * 2f;
            float r = radius + Random.Range(-2f, 3f);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            var tree = new GameObject("Tree_" + i);
            tree.transform.SetParent(parent.transform);
            tree.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(tree, "Create Tree");

            // 树干（圆柱）
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform);
            trunk.transform.localPosition = new Vector3(0, 1.5f, 0);
            trunk.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
            trunk.GetComponent<Renderer>().sharedMaterial = treeMat;
            Object.DestroyImmediate(trunk.GetComponent<Collider>());

            // 树冠（用球拉长成椭圆，远看是树顶剪影）
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.SetParent(tree.transform);
            float h = Random.Range(2.5f, 4.5f);
            crown.transform.localPosition = new Vector3(0, 3f + h * 0.3f, 0);
            crown.transform.localScale = new Vector3(Random.Range(1.5f, 2.5f), h, Random.Range(1.5f, 2.5f));
            crown.GetComponent<Renderer>().sharedMaterial = treeMat;
            Object.DestroyImmediate(crown.GetComponent<Collider>());
        }
    }

    // 9. 萤火虫
    static void SetupFireflies()
    {
        var existing = GameObject.Find("Fireflies");
        GameObject fireflies = existing;

        if (fireflies == null)
        {
            fireflies = new GameObject("Fireflies");
            Undo.RegisterCreatedObjectUndo(fireflies, "Create Fireflies");
        }

        // 放在桌面上方一点
        fireflies.transform.position = new Vector3(0, 11f, 0);

        // 加组件（如果还没有）
        var swarm = fireflies.GetComponent<FireflySwarm>();
        if (swarm == null)
        {
            swarm = Undo.AddComponent<FireflySwarm>(fireflies);
        }
        swarm.count = 40;
        swarm.areaSize = new Vector3(14f, 5f, 10f);
    }
}
