using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// 字体一键配置：
/// 默认字体用 卡通跳跳体（动态 SDF，卡通风），
/// 回退链用霞鹜文楷动态字体兜底生僻汉字，LiberationSans 兜底符号/拉丁。
/// 编译后自动执行；也可手动点 Tools → 字体 切换。
/// </summary>
public static class ChineseFontSetup
{
    // 卡通跳跳体（当前默认）
    const string CartoonTtfPath = "Assets/Font/KaTongTiaoTiaoTi.ttf";
    const string CartoonAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/KaTong TiaoTiao SDF.asset";

    // 霞鹜文楷（兜底/可手动切回）
    const string WenKaiTtfPath = "Assets/Font/LXGWWenKai-Regular.ttf";
    const string WenKaiAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LXGW WenKai SDF.asset";

    const string Pixel12Path = "Assets/Font/ark-pixel-12px-monospaced-zh_cn RASTER.asset";
    const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string LiberationFallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

    [InitializeOnLoadMethod]
    static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            // 默认字体已经是卡通跳跳体就不重复处理
            var cartoon = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CartoonAssetPath);
            if (cartoon != null && TMP_Settings.defaultFontAsset == cartoon)
                return;
            ApplyCartoon(false);
        };
    }

    [MenuItem("Tools/字体/应用卡通跳跳体（当前推荐）")]
    public static void MenuCartoon()
    {
        ApplyCartoon(true);
    }

    [MenuItem("Tools/字体/只用霞鹜文楷")]
    public static void MenuWenKai()
    {
        TMP_FontAsset wenkai = EnsureDynamicFont(WenKaiTtfPath, WenKaiAssetPath, "LXGW WenKai SDF", true);
        if (wenkai != null)
        {
            SetDefaultAndFallback(wenkai, false, true);
            Debug.Log("[字体] 默认字体已切换为霞鹜文楷。");
        }
    }

    [MenuItem("Tools/字体/应用12px像素字体（文楷兜底）")]
    public static void MenuPixel()
    {
        var pixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Pixel12Path);
        if (pixel == null)
        {
            Debug.LogWarning("[字体] 找不到 12px 像素字体资产：" + Pixel12Path);
            return;
        }
        EnsureDynamicFont(WenKaiTtfPath, WenKaiAssetPath, "LXGW WenKai SDF", false);
        SetDefaultAndFallback(pixel, true, true);
        Debug.Log("[字体] 完成：默认 12px 像素字，缺字自动回退霞鹜文楷/拉丁字体。像素字字号请用 12 的整数倍。");
    }

    static void ApplyCartoon(bool verbose)
    {
        // ttf 还没导入时先强制导入一次
        if (AssetDatabase.LoadAssetAtPath<Font>(CartoonTtfPath) == null)
            AssetDatabase.ImportAsset(CartoonTtfPath, ImportAssetOptions.ForceUpdate);

        TMP_FontAsset cartoon = EnsureDynamicFont(CartoonTtfPath, CartoonAssetPath, "KaTong TiaoTiao SDF", verbose);
        if (cartoon == null)
        {
            // 卡通字体生成失败就退回文楷，保证项目始终有中文字体
            TMP_FontAsset wenkai0 = EnsureDynamicFont(WenKaiTtfPath, WenKaiAssetPath, "LXGW WenKai SDF", verbose);
            if (wenkai0 != null) SetDefaultAndFallback(wenkai0, false, verbose);
            return;
        }

        // 文楷作为生僻字兜底（卡通字体若有缺字自动回退）
        EnsureDynamicFont(WenKaiTtfPath, WenKaiAssetPath, "LXGW WenKai SDF", false);

        SetDefaultAndFallback(cartoon, true, verbose);
        if (verbose)
            Debug.Log("[字体] 完成：默认卡通跳跳体，缺字自动回退霞鹜文楷/拉丁字体。");
    }

    // 默认字体 = defaultFont；addWenKai=true 时回退链：文楷 → LiberationSans → LiberationSans Fallback
    static void SetDefaultAndFallback(TMP_FontAsset defaultFont, bool addWenKai, bool verbose)
    {
        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null)
        {
            Debug.LogWarning("[字体] 找不到 TMP Settings（先 Window → TextMeshPro → Import TMP Essential Resources）");
            return;
        }

        var so = new SerializedObject(settings);
        so.FindProperty("m_defaultFontAsset").objectReferenceValue = defaultFont;

        var fallback = so.FindProperty("m_fallbackFontAssets");
        fallback.ClearArray();
        if (addWenKai)
            AppendRef(fallback, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(WenKaiAssetPath));
        AppendRef(fallback, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath));
        AppendRef(fallback, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFallbackPath));

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        if (verbose)
            Debug.Log("[字体] TMP Settings 已写入：默认=" + defaultFont.name);
    }

    static void AppendRef(SerializedProperty array, Object obj)
    {
        if (obj == null) return;
        int i = array.arraySize;
        array.InsertArrayElementAtIndex(i);
        array.GetArrayElementAtIndex(i).objectReferenceValue = obj;
    }

    /// <summary>
    /// 确保某个 ttf 对应的动态 TMP 字体资产存在，不存在则现场生成。
    /// 动态模式：运行时遇到任何汉字实时烘进图集，不用预烘焙字符表。
    /// </summary>
    static TMP_FontAsset EnsureDynamicFont(string ttfPath, string assetPath, string assetName, bool verbose)
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fontAsset != null) return fontAsset;

        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceUpdate);
            font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        }
        if (font == null)
        {
            if (verbose) Debug.LogWarning("[字体] 找不到 " + ttfPath);
            return null;
        }

        fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            90,                                  // 采样字号（SDF 清晰度）
            9,                                   // 图集内边距
            GlyphRenderMode.SDFAA,               // 抗锯齿 SDF
            1024, 1024,                          // 初始图集大小
            AtlasPopulationMode.Dynamic,         // 动态：缺什么字实时烘
            true);                               // 图集满了自动开新图集
        if (fontAsset == null)
        {
            Debug.LogError("[字体] TMP_FontAsset.CreateFontAsset 失败：" + ttfPath);
            return null;
        }

        fontAsset.name = assetName;
        AssetDatabase.CreateAsset(fontAsset, assetPath);
        if (fontAsset.material != null)
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture tex in fontAsset.atlasTextures)
                if (tex != null) AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[字体] 已生成动态字体资产：" + assetPath);
        return fontAsset;
    }
}
