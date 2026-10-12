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
        SetupAmbientFog();
        SetupDustMotes();
        SetupGodRays();
        SetupGroundDecorations();

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

    // 7. 地面（程序化草地 shader + 法线贴图）
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
        string norPath   = texFolder + "forest_ground_06_nor_gl_4k.exr";

        // 法线贴图导入类型设为 Normal Map
        SetNormalMapImport(norPath);
        SetTextureMaxSize(norPath, 2048);

        var normal = AssetDatabase.LoadAssetAtPath<Texture>(norPath);

        // 用程序化草地 shader：没有 diffuse 贴图，用噪声生成草地颜色
        var grassShader = Shader.Find("Custom/GrassGround");
        Material groundMat;
        if (grassShader != null)
        {
            groundMat = new Material(grassShader);
            groundMat.name = "DarkGrassGround";
            groundMat.SetColor("_GrassDark", new Color(0.05f, 0.12f, 0.05f, 1f));
            groundMat.SetColor("_GrassLight", new Color(0.10f, 0.22f, 0.09f, 1f));
            groundMat.SetColor("_GrassDry", new Color(0.16f, 0.14f, 0.07f, 1f));
            groundMat.SetFloat("_NoiseScale", 15f);
            groundMat.SetFloat("_NoiseAmp", 0.5f);
            groundMat.SetFloat("_DryAmount", 0.2f);
            groundMat.SetFloat("_NormalTiling", 10f);
            groundMat.SetFloat("_BumpScale", 4f);
            groundMat.SetFloat("_Glossiness", 0.25f);
            groundMat.SetFloat("_Metallic", 0f);
            if (normal != null) groundMat.SetTexture("_BumpMap", normal);
        }
        else
        {
            // 兜底 Standard
            groundMat = new Material(Shader.Find("Standard"));
            groundMat.name = "DarkGrassGround";
            groundMat.color = new Color(0.06f, 0.14f, 0.06f, 1f);
            groundMat.SetFloat("_Glossiness", 0.3f);
            if (normal != null)
            {
                groundMat.SetTexture("_BumpMap", normal);
                groundMat.SetFloat("_BumpScale", 4f);
            }
        }

        var renderer = ground.GetComponent<Renderer>();
        Undo.RecordObject(renderer, "Setup Ground");
        renderer.sharedMaterial = groundMat;

        Debug.Log("[Setup] 地面材质：GrassGround shader=" + (grassShader != null) + ", normal=" + (normal != null));
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

    // 8. 远景树（针叶松+阔叶树，多层堆叠+颜色层次）
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

        // 材质：树干深棕，树冠深绿（层次）
        var trunkMat = new Material(Shader.Find("Standard"));
        trunkMat.name = "TreeTrunk";
        trunkMat.color = new Color(0.08f, 0.05f, 0.03f, 1f);
        trunkMat.SetFloat("_Glossiness", 0.2f);
        trunkMat.SetFloat("_Metallic", 0f);

        var crownDarkMat = new Material(Shader.Find("Standard"));
        crownDarkMat.name = "TreeCrownDark";
        crownDarkMat.color = new Color(0.03f, 0.07f, 0.03f, 1f);
        crownDarkMat.SetFloat("_Glossiness", 0.1f);
        crownDarkMat.SetFloat("_Metallic", 0f);

        var crownMidMat = new Material(Shader.Find("Standard"));
        crownMidMat.name = "TreeCrownMid";
        crownMidMat.color = new Color(0.05f, 0.10f, 0.04f, 1f);
        crownMidMat.SetFloat("_Glossiness", 0.15f);
        crownMidMat.SetFloat("_Metallic", 0f);

        int treeCount = 20;
        float radius = 18f;
        for (int i = 0; i < treeCount; i++)
        {
            float angle = (i / (float)treeCount) * Mathf.PI * 2f;
            float r = radius + Random.Range(-2.5f, 3.5f);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            var tree = new GameObject("Tree_" + i);
            tree.transform.SetParent(parent.transform);
            tree.transform.position = pos;
            tree.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            Undo.RegisterCreatedObjectUndo(tree, "Create Tree");

            bool isPine = Random.value > 0.4f;   // 60% 松树，40% 阔叶树
            if (isPine)
                CreatePineTree(tree.transform, trunkMat, crownDarkMat, crownMidMat);
            else
                CreateBroadleafTree(tree.transform, trunkMat, crownDarkMat, crownMidMat);
        }
    }

    // 程序化生成圆锥网格（Unity 没有 PrimitiveType.Cone）
    static GameObject CreateCone(string name, int segments = 12)
    {
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        var mesh = new Mesh();
        var verts = new List<Vector3>();
        var tris = new List<int>();

        // 顶点：底面中心 + 底面圆周 + 顶点
        verts.Add(Vector3.zero);   // 底面中心 (index 0)
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
        }
        verts.Add(new Vector3(0f, 1f, 0f));   // 顶点 (index segments+1)

        // 底面三角形
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            tris.Add(0);
            tris.Add(next + 1);
            tris.Add(i + 1);
        }
        // 侧面三角形
        int tip = segments + 1;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            tris.Add(i + 1);
            tris.Add(tip);
            tris.Add(next + 1);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;

        return go;
    }

    // 针叶松：多层圆锥从下到上堆叠，越往上越小越密
    static void CreatePineTree(Transform parent, Material trunkMat, Material crownDark, Material crownMid)
    {
        float totalH = Random.Range(6f, 9f);
        float trunkH = totalH * 0.3f;

        // 树干
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(parent, false);
        trunk.transform.localPosition = new Vector3(0, trunkH * 0.5f, 0);
        trunk.transform.localScale = new Vector3(0.25f, trunkH * 0.5f, 0.25f);
        trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
        Object.DestroyImmediate(trunk.GetComponent<Collider>());

        // 树冠：4~6 层圆锥
        int layers = Random.Range(4, 7);
        float startY = trunkH;
        float crownH = totalH - trunkH;
        for (int i = 0; i < layers; i++)
        {
            float t = i / (float)(layers - 1);
            float coneH = crownH / layers * 1.3f;
            float y = startY + t * crownH * 0.7f;
            float radius = Mathf.Lerp(2.2f, 0.6f, t);

            var cone = CreateCone("CrownLayer_" + i);
            cone.transform.SetParent(parent, false);
            cone.transform.localPosition = new Vector3(
                Random.Range(-0.15f, 0.15f),
                y + coneH * 0.5f,
                Random.Range(-0.15f, 0.15f));
            cone.transform.localScale = new Vector3(radius, coneH, radius);
            cone.transform.rotation = Quaternion.Euler(
                Random.Range(-3f, 3f),
                Random.Range(0f, 360f),
                Random.Range(-3f, 3f));
            cone.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? crownDark : crownMid;
        }
    }

    // 阔叶树：多个球体/椭球体堆叠成不规则树冠
    static void CreateBroadleafTree(Transform parent, Material trunkMat, Material crownDark, Material crownMid)
    {
        float totalH = Random.Range(5f, 7f);
        float trunkH = totalH * 0.4f;

        // 树干
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(parent, false);
        trunk.transform.localPosition = new Vector3(0, trunkH * 0.5f, 0);
        trunk.transform.localScale = new Vector3(0.3f, trunkH * 0.5f, 0.3f);
        trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
        Object.DestroyImmediate(trunk.GetComponent<Collider>());

        // 树冠：5~8 个球体随机堆叠
        int blobs = Random.Range(5, 9);
        for (int i = 0; i < blobs; i++)
        {
            var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = "CrownBlob_" + i;
            blob.transform.SetParent(parent, false);

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(0f, 1.2f);
            float y = trunkH + Random.Range(0.5f, 2.5f);
            blob.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * dist,
                y,
                Mathf.Sin(angle) * dist);

            float s = Random.Range(1.2f, 2.2f);
            blob.transform.localScale = new Vector3(s, s * Random.Range(0.7f, 1.1f), s);
            blob.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? crownMid : crownDark;
            Object.DestroyImmediate(blob.GetComponent<Collider>());
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

    // 10. 环境雾气（低空柔和雾团）
    static void SetupAmbientFog()
    {
        var existing = GameObject.Find("AmbientFog");
        GameObject fog = existing;
        if (fog == null)
        {
            fog = new GameObject("AmbientFog");
            Undo.RegisterCreatedObjectUndo(fog, "Create AmbientFog");
        }
        Undo.RecordObject(fog.transform, "Setup AmbientFog");
        fog.transform.position = new Vector3(0, 1.5f, 0);

    }

    // 11. 空气灰尘微粒（增强丁达尔光柱可见度）
    static void SetupDustMotes()
    {
        var existing = GameObject.Find("DustMotes");
        GameObject dust = existing;
        if (dust == null)
        {
            dust = new GameObject("DustMotes");
            Undo.RegisterCreatedObjectUndo(dust, "Create DustMotes");
        }
        Undo.RecordObject(dust.transform, "Setup DustMotes");
        dust.transform.position = new Vector3(0, 8f, 0);

        var comp = dust.GetComponent<DustMotes>();
        if (comp == null) comp = Undo.AddComponent<DustMotes>(dust);
        comp.count = 80;
        comp.areaSize = new Vector3(20f, 10f, 16f);
        comp.color = new Color(0.65f, 0.78f, 0.88f, 0.5f);
    }

    // 12. 丁达尔光柱（月光穿过雾气）
    static void SetupGodRays()
    {
        var existing = GameObject.Find("GodRays");
        GameObject rays = existing;
        if (rays == null)
        {
            rays = new GameObject("GodRays");
            Undo.RegisterCreatedObjectUndo(rays, "Create GodRays");
        }
        Undo.RecordObject(rays.transform, "Setup GodRays");
        // 放在场景上方偏左，光柱朝右下方打（与月光方向一致）
        rays.transform.position = new Vector3(-8f, 18f, -4f);

        var comp = rays.GetComponent<GodRays>();
        if (comp == null) comp = Undo.AddComponent<GodRays>(rays);
        comp.count = 60;
        comp.lightDirection = new Vector3(0.5f, -0.7f, 0.3f);
        comp.color = new Color(0.7f, 0.82f, 0.9f, 0.12f);
    }

    // 13. 地面装饰（草丛+蘑菇剪影）
    static void SetupGroundDecorations()
    {
        var existing = GameObject.Find("GroundDecorations");
        GameObject deco = existing;
        if (deco == null)
        {
            deco = new GameObject("GroundDecorations");
            Undo.RegisterCreatedObjectUndo(deco, "Create GroundDecorations");
        }
        Undo.RecordObject(deco.transform, "Setup GroundDecorations");
        deco.transform.position = Vector3.zero;

        var comp = deco.GetComponent<GroundDecorations>();
        if (comp == null) comp = Undo.AddComponent<GroundDecorations>(deco);
        comp.innerRadius = 7f;
        comp.outerRadius = 16f;
        comp.grassCount = 50;
        comp.mushroomCount = 15;
    }
}
