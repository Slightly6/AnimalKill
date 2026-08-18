using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 一键用「点阵（RASTER）模式」从 16px 方舟像素字体生成锐利的 TMP 字体资产。
/// 和 12px 版的区别：16px 的字格更大、笔画更细，放大后不会像 12px 那样变成又粗又糙的大方块。
/// 适用：卡牌角标数字、筹码数字、花色符号（这些 16px 字里都全）。
/// 注意：16px 版中文字不全（只有 3171 个），所以中文卡牌名以后用 12px 版，这个只负责数字/字母/花色。
/// 用法：菜单栏 Tools → 生成16px点阵字体
/// </summary>
public class PixelFont16Generator
{
    // 源字体（.ttf，不是 .asset）
    private const string FontPath = "Assets/Font/ark-pixel-16px-monospaced-zh_cn.ttf";
    // 生成出来的字体资产（.asset）
    private const string OutputPath = "Assets/Font/ark-pixel-16px-monospaced-zh_cn RASTER.asset";

    [MenuItem("Tools/生成16px点阵字体")]
    public static void Run()
    {
        // 1. 找到源字体
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog("失败",
                "找不到源字体：\n" + FontPath +
                "\n\n请确认已经把这个 .ttf 拖进 Assets/Font 文件夹，并等 Unity 导入完成。",
                "好");
            return;
        }

        // 2. 收集要用到的字（16px 只收数字/字母/花色，中文以后用 12px）
        string characters = CollectCharacters();

        // 3. 用点阵模式创建字体资产（点大小 16 = 字体的原始像素尺寸，1:1 最锐利）
        //    padding 0，RASTER 点阵模式，2048x2048 大图，动态模式（方便批量加字）
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            font, 16, 0, GlyphRenderMode.RASTER, 2048, 2048, AtlasPopulationMode.Dynamic);

        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog("失败",
                "创建字体资产失败。请确认这个 .ttf 的导入设置里「Include Font Data」是勾上的。",
                "好");
            return;
        }

        // 4. 把所有字加进去（会自动把图集撑大到够用）
        string missing = "";
        bool ok = fontAsset.TryAddCharacters(characters, out missing);

        // 5. 像素字用「最近邻」采样，放大才不会糊（关键！默认的线性过滤会糊）
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            fontAsset.atlasTextures[0].filterMode = FilterMode.Point;

        // 6. 切回静态（锁定，运行时不再动态补字）
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        // 7. 保存成资产（主资产 + 图集贴图 + 材质三个一起存进去）
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);   // 已经生成过就删掉重来
        AssetDatabase.CreateAsset(fontAsset, OutputPath);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        EditorUtility.SetDirty(fontAsset);
        EditorUtility.SetDirty(fontAsset.atlasTextures[0]);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 8. 汇报结果
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
            result = "字体里缺这几个字（已跳过）：\n" + missing;
        }

        string msg = "16px 点阵字体生成完成！已收集 " + characters.Length + " 个字符。\n" + result +
            "\n\n生成文件：\n" + OutputPath;
        Debug.Log("[字体] " + msg);
        EditorUtility.DisplayDialog("完成", msg, "好");
    }

    // 收集要用到的字符（16px 只收数字/字母/花色，中文以后用 12px）
    private static string CollectCharacters()
    {
        HashSet<char> set = new HashSet<char>();

        // 1. 可打印 ASCII（含数字 0-9、字母 A-Z a-z、下划线、连字符、空格、符号）
        for (char c = ' '; c <= '~'; c++)
            set.Add(c);

        // 2. 花色符号（卡牌用的 ♠♥♦♣，16px 字里都有）
        string extra = "♠♥♦♣";
        for (int i = 0; i < extra.Length; i++)
            set.Add(extra[i]);

        // 拼成一个去重字符串
        StringBuilder sb = new StringBuilder();
        foreach (char c in set)
            sb.Append(c);
        return sb.ToString();
    }
}
