using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 一键生成卷轴地图 UI（整条长卷轴版）。
/// 菜单：Tools → Setup Map Scroll UI
///
/// 生成层级：
///   Canvas（已有则复用；没有就建 ScreenSpace-Overlay）
///   └── MapScrollRoot  （铺满全屏：深色背景 + CanvasGroup + RectMask2D + MapScrollUI，本身不动）
///       └── ScrollPanel（宽 600，高=地图全长，锚在 Root 底部中心；掉落/滚动/飞走都移动它）
///           ├── TopRod    （金色杆，宽 600 高 30，地图最上端 = Boss 端）
///           ├── MapPaper  （牛皮纸，宽 600-2*inset，高运行时按节点排数撑）
///           └── BottomRod （金色杆，宽 600 高 30，地图最下端 = 第一关 A 端）
///
/// 按 Tab：整条卷轴从屏幕上方掉落，BottomRod 砸到屏幕底部 EaseOutBounce 弹一下；
/// 滚轮：直接上下移动整条 ScrollPanel（纸/纹路/节点/杆一体移动），滚到顶才看见 TopRod；
/// 再按 Tab：整条向上飞出屏幕。
/// </summary>
public class MapScrollSetup
{
    // ===== 尺寸（宽度固定；高度=杆+纸+杆，纸高运行时按节点排数算）=====
    public const float PANEL_W     = 600f;
    public const float ROD_HEIGHT  = 30f;
    public const float PAPER_INSET = 24f;   // 纸每侧比杆窄多少（杆 600，纸 552）
    static readonly Color RodColor  = new Color(0.85f, 0.70f, 0.40f, 1f);
    static readonly Color PaperFallback = new Color(0.72f, 0.58f, 0.38f, 1f); // 找不到 ScrollParchment 材质时的兜底牛皮纸色

    [MenuItem("Tools/Setup Map Scroll UI")]
    public static void Setup()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
            canvas = CreateCanvas();

        // 已存在旧的 MapScrollRoot → 询问后重建
        var old = canvas.transform.Find("MapScrollRoot");
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "重建卷轴地图",
                    "场景里已经有 MapScrollRoot，是否删除并按新结构重建？",
                    "重建", "取消"))
                return;
            Object.DestroyImmediate(old.gameObject);
        }

        // EventSystem：UI 节点点击（UIMapNode）必须有，没有就补
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // 1. MapScrollRoot：铺满全屏，负责背景遮罩 + 裁剪 + 显隐 + 输入（自己不动）
        var rootGo = new GameObject(
            "MapScrollRoot",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(RectMask2D),
            typeof(MapScrollUI));
        Undo.RegisterCreatedObjectUndo(rootGo, "Create MapScrollRoot");

        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.SetParent(canvas.transform, false);
        Stretch(rootRt);

        var rootImg = rootGo.GetComponent<Image>();
        rootImg.color = new Color(0.04f, 0.03f, 0.025f, 0.92f);  // 深棕近黑，衬金色纸边
        rootImg.raycastTarget = true;   // 打开时挡住后面 3D 物体的 OnMouseDown

        var rootCg = rootGo.GetComponent<CanvasGroup>();
        rootCg.alpha = 0f;
        rootCg.interactable = false;
        rootCg.blocksRaycasts = false;

        // 2. ScrollPanel：整条长卷轴，锚在 Root 底部中心（pivot 也在底部中心）。
        //    初始高 0，运行时 BuildMap 后由 ContentSizeFitter 撑成 上杆+纸+下杆。
        var panelGo = new GameObject("ScrollPanel",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        Undo.RegisterCreatedObjectUndo(panelGo, "Create ScrollPanel");
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.SetParent(rootRt, false);
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0f);  // 底部中心
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.sizeDelta = new Vector2(PANEL_W, 0f);
        panelRt.anchoredPosition = Vector2.zero;

        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;     // 把 preferredHeight 写回杆/纸的 RectTransform
        vlg.childForceExpandWidth = false; // 宽度走各自 preferredWidth（杆 600、纸内缩）
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        var csf = panelGo.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 3. 上杆（VLG 第一个子 = 最上 = Boss 端）
        MakeRod("TopRod", panelRt, PANEL_W);

        // 4. 牛皮纸（节点/连线都挂它下面；高度初始 0，运行时按节点排数撑高）
        var paperGo = new GameObject("MapPaper", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(paperGo, "Create MapPaper");
        var paperRt = paperGo.GetComponent<RectTransform>();
        paperRt.SetParent(panelRt, false);

        var paperImg = paperGo.GetComponent<Image>();
        paperImg.color = Color.white;                 // 颜色交给材质；没材质时下面兜底色
        Material parchment = FindParchmentMaterial();
        if (parchment != null) paperImg.material = parchment;
        else paperImg.color = PaperFallback;

        var paperLe = paperGo.GetComponent<LayoutElement>();
        paperLe.preferredWidth = PANEL_W - PAPER_INSET * 2f;
        paperLe.preferredHeight = 0f;
        paperLe.flexibleWidth = 0f;
        paperLe.flexibleHeight = 0f;

        // 5. 下杆（VLG 最后一个子 = 最下 = 第一关 A 端，掉落时它砸屏幕底）
        MakeRod("BottomRod", panelRt, PANEL_W);

        // 6. 赋引用
        var ui = rootGo.GetComponent<MapScrollUI>();
        ui.scrollPanel = panelRt;
        ui.paper = paperRt;
        ui.paperLayout = paperLe;
        ui.panelWidth = PANEL_W;
        ui.paperInset = PAPER_INSET;

        EditorUtility.SetDirty(ui);
        Selection.activeGameObject = rootGo;

        // 杆材质状态（FindOrCreateMetalRodMaterial 已在 MakeRod 里调过，这里仅用于提示）
        bool rodMatReady = Shader.Find("UI/MetalRod") != null;
        Debug.Log((
            parchment != null
                ? "[MapScroll] 纸：已挂 ScrollParchment 材质"
                : "[MapScroll] 纸：未找到 UI/ScrollParchment 材质，用兜底色（请把材质拖到 MapPaper 的 Image.Material）")
            + " | " +
            (rodMatReady
                ? "杆：已挂 MetalRod 材质（圆柱+端帽+颗粒）"
                : "杆：未找到 UI/MetalRod shader，用兜底纯色（请确认 Assets/Shader/MetalRod.shader 已导入）")
            + "，运行后按 Tab。");
    }

    static Canvas CreateCanvas()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // 四角锚定铺满父物体
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static RectTransform MakeRod(string name, Transform parent, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var rodImg = go.GetComponent<Image>();
        // 杆用 UI/MetalRod shader 材质：圆柱面渐变高光 + 两端圆头端帽 + 表面颗粒
        Material rodMat = FindOrCreateMetalRodMaterial();
        if (rodMat != null)
        {
            rodImg.material = rodMat;
            rodImg.color = Color.white;   // 颜色交给材质；Image 自身保持白色
        }
        else
        {
            rodImg.color = RodColor;       // 兜底：没材质就纯色
        }

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = ROD_HEIGHT;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
        return rt;
    }

    // 在项目里找用 UI/ScrollParchment shader 的材质（你手动建的 ScrollParchment.mat）
    static Material FindParchmentMaterial()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && m.shader != null && m.shader.name == "UI/ScrollParchment")
                return m;
        }
        return null;
    }

    // 找用 UI/MetalRod shader 的材质；找不到就用 shader 在磁盘上新建一个 MetalRod.mat 资产
    static Material FindOrCreateMetalRodMaterial()
    {
        // 1) 先找已有的
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && m.shader != null && m.shader.name == "UI/MetalRod")
                return m;
        }

        // 2) 找不到就新建：和 MetalRod.shader 放同一目录，命名 MetalRod.mat
        Shader rodShader = Shader.Find("UI/MetalRod");
        if (rodShader == null)
            rodShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shader/MetalRod.shader");
        if (rodShader == null)
        {
            Debug.LogWarning("[MapScroll] 找不到 UI/MetalRod shader，杆用兜底纯色。请确认 Assets/Shader/MetalRod.shader 已被 Unity 导入。");
            return null;
        }

        // shader 资产路径：Assets/Shader/MetalRod.shader → 同目录 MetalRod.mat
        string shaderPath = AssetDatabase.GetAssetPath(rodShader);
        string dir = System.IO.Path.GetDirectoryName(shaderPath);
        if (string.IsNullOrEmpty(dir)) dir = "Assets";
        string matPath = dir + "/MetalRod.mat";
        matPath = AssetDatabase.GenerateUniqueAssetPath(matPath);

        var mat = new Material(rodShader);
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[MapScroll] 自动创建 MetalRod 材质：" + matPath);
        return mat;
    }
}
