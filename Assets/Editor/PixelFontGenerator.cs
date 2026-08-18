using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// 一键重新生成像素字体：自动收集游戏里用到的所有字（数字、字母、下划线、花色、所有中文），
/// 重新填进 ark-pixel 字体资产里，解决「缺字形」的警告和豆腐块。
/// 用法：菜单栏 Tools → 重新生成像素字体
///
/// 原理：TMP 字体资产（.asset）只存了「生成时指定的那些字」。这里把项目里所有会用到的字
/// 扫描出来，一次性补进这个资产里（原地改，不改 GUID，卡牌预制体上的引用不会断）。
/// </summary>
public class PixelFontGenerator
{
    // 要重新生成的字体资产（.asset，不是 .ttf）。改字体就改这里。
    private const string FontAssetPath = "Assets/Font/ark-pixel-16px-monospaced-zh_cn SDF.asset";

    [MenuItem("Tools/重新生成像素字体")]
    public static void Run()
    {
        // 找到字体资产（必须已经用 Font Asset Creator 生成过一次）
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog("失败",
                "找不到字体资产：\n" + FontAssetPath +
                "\n\n请先用 Window → TextMeshPro → Font Asset Creator 生成一次这个资产。",
                "好");
            return;
        }

        // 1. 收集所有用到的字
        string characters = CollectCharacters();

        // 2. 切到动态模式（会自动关联源字体 .ttf），把缺的字全填进去
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        string missing = "";
        bool ok = fontAsset.TryAddCharacters(characters, out missing);

        // 3. 切回静态（锁定，运行时不再动态补字），并保存
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fontAsset);
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            EditorUtility.SetDirty(fontAsset.atlasTextures[0]);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 4. 汇报结果
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

        string msg = "重新生成完成！已收集 " + characters.Length + " 个字符。\n" + result;
        Debug.Log("[字体] " + msg);
        EditorUtility.DisplayDialog("完成", msg, "好");
    }

    // 收集所有用到的字符
    private static string CollectCharacters()
    {
        HashSet<char> set = new HashSet<char>();

        // 1. 固定必加：可打印 ASCII（含数字、字母、下划线 _、连字符 -、空格、符号）
        for (char c = ' '; c <= '~'; c++)
            set.Add(c);

        // 2. 花色符号 + 常用全角符号
        string extra = "♠♥♦♣【】　";
        for (int i = 0; i < extra.Length; i++)
            set.Add(extra[i]);

        // 3. 扫描 Assets 下所有文本文件里的非 ASCII 字符（中文等）
        string assetsDir = Application.dataPath;
        string[] exts = { ".cs", ".asset", ".prefab", ".unity", ".txt", ".json" };
        for (int e = 0; e < exts.Length; e++)
        {
            string[] files = Directory.GetFiles(assetsDir, "*" + exts[e], SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                CollectNonAscii(files[i], set);
        }

        // 拼成一个去重字符串
        StringBuilder sb = new StringBuilder();
        foreach (char c in set)
            sb.Append(c);
        return sb.ToString();
    }

    // 读一个文件，把里面所有非 ASCII 字符收进 set
    private static void CollectNonAscii(string path, HashSet<char> set)
    {
        string text = "";
        try
        {
            text = File.ReadAllText(path);
        }
        catch
        {
            return;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c > 127)
                set.Add(c);
        }
    }
}
