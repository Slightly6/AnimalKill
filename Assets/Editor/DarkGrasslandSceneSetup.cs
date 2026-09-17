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
        DisableWalls();
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

    // 5. 生成实体桌面（卡牌正下方）
    static void SetupTable()
    {
        // 取 BoardManager 的 boardHeight 作为桌面高度（默认 10）
        float boardY = 10f;
        var bm = Object.FindObjectOfType<BoardManager>();
        if (bm != null) boardY = bm.boardHeight;

        // 已有就复用（先清掉旧的子物体，避免重复运行叠两层）
        var existing = GameObject.Find("Table_DarkGrass");
        GameObject table = existing;
        if (table == null)
        {
            table = new GameObject("Table_DarkGrass");
            Undo.RegisterCreatedObjectUndo(table, "Create Table");
        }
        else
        {
            for (int i = table.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(table.transform.GetChild(i).gameObject);
            }
        }

        // 桌面：深绿绒布面（薄盒子）
        // 范围覆盖 5 路（x=-4.4~4.4）+ 纵深（z=-3.5~1.0），四周留边
        float tableWidth = 13f;    // x 方向
        float tableDepth = 9f;     // z 方向
        float tableThick = 0.2f;

        var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "TableTop";
        top.transform.SetParent(table.transform);
        top.transform.position = new Vector3(0, boardY - tableThick * 0.5f, -1.25f);
        top.transform.localScale = new Vector3(tableWidth, tableThick, tableDepth);
        Undo.RegisterCreatedObjectUndo(top, "Create Table Top");
        Object.DestroyImmediate(top.GetComponent<Collider>());

        // 深绿绒布材质
        var feltMat = new Material(Shader.Find("Standard"));
        feltMat.name = "TableFelt_DarkGreen";
        feltMat.color = new Color(0.06f, 0.18f, 0.1f, 1f);   // #0f2e19 墨绿
        feltMat.SetFloat("_Glossiness", 0.75f);   // 绒布柔光
        feltMat.SetFloat("_Metallic", 0f);
        top.GetComponent<Renderer>().sharedMaterial = feltMat;

        // 金边：桌面边缘一圈细条（赌桌感）
        var goldMat = new Material(Shader.Find("Standard"));
        goldMat.name = "TableRim_Gold";
        goldMat.color = new Color(0.75f, 0.6f, 0.2f, 1f);   // 金色
        goldMat.SetFloat("_Glossiness", 0.7f);
        goldMat.SetFloat("_Metallic", 0.9f);

        float rimW = 0.15f;
        float rimH = 0.08f;
        // 四条边
        CreateRim(table.transform, new Vector3(0, boardY + rimH * 0.5f, -1.25f + tableDepth * 0.5f), new Vector3(tableWidth, rimH, rimW), goldMat, "Rim_Far");
        CreateRim(table.transform, new Vector3(0, boardY + rimH * 0.5f, -1.25f - tableDepth * 0.5f), new Vector3(tableWidth, rimH, rimW), goldMat, "Rim_Near");
        CreateRim(table.transform, new Vector3(-tableWidth * 0.5f, boardY + rimH * 0.5f, -1.25f), new Vector3(rimW, rimH, tableDepth), goldMat, "Rim_Left");
        CreateRim(table.transform, new Vector3(tableWidth * 0.5f, boardY + rimH * 0.5f, -1.25f), new Vector3(rimW, rimH, tableDepth), goldMat, "Rim_Right");

        // 桌腿（4 根）
        var legMat = new Material(Shader.Find("Standard"));
        legMat.name = "TableLeg_Dark";
        legMat.color = new Color(0.08f, 0.06f, 0.04f, 1f);   // 近黑棕
        legMat.SetFloat("_Glossiness", 0.3f);
        legMat.SetFloat("_Metallic", 0.1f);

        float legX = tableWidth * 0.45f;
        float legZ = tableDepth * 0.45f;
        float legTopY = boardY - tableThick;
        CreateLeg(table.transform, new Vector3(-legX, (legTopY + 0) * 0.5f, -1.25f + legZ), new Vector3(0.3f, legTopY, 0.3f), legMat, "Leg_BL");
        CreateLeg(table.transform, new Vector3(legX, (legTopY + 0) * 0.5f, -1.25f + legZ), new Vector3(0.3f, legTopY, 0.3f), legMat, "Leg_BR");
        CreateLeg(table.transform, new Vector3(-legX, (legTopY + 0) * 0.5f, -1.25f - legZ), new Vector3(0.3f, legTopY, 0.3f), legMat, "Leg_FL");
        CreateLeg(table.transform, new Vector3(legX, (legTopY + 0) * 0.5f, -1.25f - legZ), new Vector3(0.3f, legTopY, 0.3f), legMat, "Leg_FR");
    }

    // 生成一条金边
    static void CreateRim(Transform parent, Vector3 pos, Vector3 scale, Material mat, string name)
    {
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rim.name = name;
        rim.transform.SetParent(parent);
        rim.transform.position = pos;
        rim.transform.localScale = scale;
        Undo.RegisterCreatedObjectUndo(rim, "Create Rim");
        Object.DestroyImmediate(rim.GetComponent<Collider>());
        rim.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 生成一根桌腿
    static void CreateLeg(Transform parent, Vector3 pos, Vector3 scale, Material mat, string name)
    {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leg.name = name;
        leg.transform.SetParent(parent);
        leg.transform.position = pos;
        leg.transform.localScale = scale;
        Undo.RegisterCreatedObjectUndo(leg, "Create Leg");
        Object.DestroyImmediate(leg.GetComponent<Collider>());
        leg.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 6. 禁用墙（草原不要墙）
    static void DisableWalls()
    {
        var walls = Object.FindObjectsOfType<Wall>();
        foreach (var w in walls)
        {
            Undo.RecordObject(w.gameObject, "Disable Wall");
            w.gameObject.SetActive(false);
        }
        Debug.Log("[Setup] 禁用了 " + walls.Length + " 个墙对象");
    }

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
