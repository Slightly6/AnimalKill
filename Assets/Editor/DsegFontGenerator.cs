using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 一键生成「七段数码管」数字字体（DSEG7），电子万年历/计算器那种一段一段的 LED 数字。
/// DSEG7 是矢量字体，所以用 SDF 模式，任何字号都清晰、不糊也不糙。
/// 注意：七段数码管只有数字 0-9（和少量符号），画不了花色/字母。
/// 用法：菜单栏 Tools → 生成数码管数字字体
/// </summary>
public class DsegFontGenerator
{
    // 源字体（.ttf）
    private const string FontPath = "Assets/Font/DSEG7Classic-Regular.ttf";
    // 生成出来的字体资产（.asset）
    private const string OutputPath = "Assets/Font/DSEG7Classic-Regular SDF.asset";

    [MenuItem("Tools/生成数码管数字字体")]
    public static void Run()
    {
        // 1. 找到源字体
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog("失败",
                "找不到源字体：\n" + FontPath +
                "\n\n请确认已经把这个 .ttf 放进 Assets/Font 并等 Unity 导入完成。",
                "好");
            return;
        }

        // 2. 收集要用到的字
        string characters = CollectCharacters();

        // 3. 用 SDF 模式创建字体资产（采样点 90，TMP 默认，放大缩小都清晰；padding 9 给 SDF 留渐变）
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);

        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog("失败",
                "创建字体资产失败。请确认这个 .ttf 的导入设置里「Include Font Data」是勾上的。",
                "好");
            return;
        }

        // 4. 把字加进去
        string missing = "";
        bool ok = fontAsset.TryAddCharacters(characters, out missing);

        // 5. 切回静态（锁定，运行时不再动态补字）
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        // 6. 保存成资产（主资产 + 图集贴图 + 材质一起存进去）
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);   // 已经生成过就删掉重来
        AssetDatabase.CreateAsset(fontAsset, OutputPath);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        EditorUtility.SetDirty(fontAsset);
        EditorUtility.SetDirty(fontAsset.atlasTextures[0]);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 7. 汇报结果
        string result;
        if (ok)
        {
            result = "全部字形都加进去了！";
        }
        else if (string.IsNullOrEmpty(missing) || missing.Length >= characters.Length)
        {
            result = "这些字都已经在字体里了（可能是重复运行），无需再跑。";
        }
        else
        {
            result = "字体里缺这几个字（已跳过，正常，DSEG7 本来就没有）：\n" + missing;
        }

        string msg = "数码管字体生成完成！已收集 " + characters.Length + " 个字符。\n" + result +
            "\n\n生成文件：\n" + OutputPath;
        Debug.Log("[字体] " + msg);
        EditorUtility.DisplayDialog("完成", msg, "好");
    }

    // 收集要用到的字符：数字 + 常用符号 + 字母（缺的会自动跳过）
    private static string CollectCharacters()
    {
        HashSet<char> set = new HashSet<char>();

        // 数字 + 常用符号（万年历/计算器/日期会用到）
        string chars = "0123456789.:-+/ ";
        for (int i = 0; i < chars.Length; i++)
            set.Add(chars[i]);

        // 字母（DSEG7 支持一部分，缺的自动跳过，不影响数字）
        for (char c = 'A'; c <= 'Z'; c++)
            set.Add(c);
        for (char c = 'a'; c <= 'z'; c++)
            set.Add(c);

        // 拼成去重字符串
        StringBuilder sb = new StringBuilder();
        foreach (char c in set)
            sb.Append(c);
        return sb.ToString();
    }
}
