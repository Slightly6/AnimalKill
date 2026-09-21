using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 觉醒商店面板（纯代码生成，不用手搭 UI）。
/// 商店节点由 MapManager 创建并 Open()：从玩家牌组随机挑 3 只未觉醒动物，
/// 花筹码觉醒；觉醒后该动物在本局所有战斗中带技能，死亡/重开清空。
/// </summary>
public class AwakeningShop : MonoBehaviour
{
    const int OFFER_COUNT = 3;        // 每次刷几个觉醒位
    const int BASE_PRICE = 10;        // 基础价
    const int PRICE_PER_RANK = 2;     // 每点力量加价

    private Transform root;           // 面板根（关闭时销毁）
    private readonly List<CardDataSO> offers = new List<CardDataSO>();
    private TextMeshProUGUI chipsText;
    private readonly List<TextMeshProUGUI> stateTexts = new List<TextMeshProUGUI>();
    private readonly List<Button> buyButtons = new List<Button>();

    public void Open()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[觉醒] 场景里没有 Canvas，无法打开觉醒面板");
            return;
        }

        RollOffers();
        BuildUI(canvas.transform);
        RefreshAll();
    }

    // 组件所在 GO（在 spawnedObjects 里）被清场时，连带销毁挂在 Canvas 下的视觉面板
    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    // 从玩家牌组随机挑 OFFER_COUNT 种未觉醒动物（同种动物去重）
    void RollOffers()
    {
        offers.Clear();

        var pool = new List<CardDataSO>();
        var seen = new HashSet<CardDataSO>();

        // 优先用 GameProgress 牌组；空则退到 DeckManager 的初始牌组
        List<CardDataSO> source = (GameProgress.playerDeck != null && GameProgress.playerDeck.Count > 0)
            ? GameProgress.playerDeck
            : (DeckManager.Instance != null ? DeckManager.Instance.deckCards : null);

        if (source != null)
        {
            foreach (CardDataSO d in source)
            {
                if (d == null || seen.Contains(d)) continue;
                if (GameProgress.IsCardAwakened(d)) continue;   // 已觉醒不刷
                if (d.awakenedAbility == null) continue;        // 没配觉醒技能也不刷
                seen.Add(d);
                pool.Add(d);
            }
        }

        // Fisher-Yates 洗牌取前 N
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardDataSO tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }
        for (int i = 0; i < OFFER_COUNT && i < pool.Count; i++)
            offers.Add(pool[i]);
    }

    int PriceOf(CardDataSO d)
    {
        return BASE_PRICE + (int)d.rank * PRICE_PER_RANK;
    }

    // ========== UI 搭建 ==========

    void BuildUI(Transform canvasRoot)
    {
        var panelGo = new GameObject("AwakeningShopPanel", typeof(RectTransform), typeof(Image));
        root = panelGo.transform;
        root.SetParent(canvasRoot, false);
        root.SetAsLastSibling();   // 盖在最上层

        var panelRt = (RectTransform)root;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(720, 420);
        panelRt.anchoredPosition = Vector2.zero;

        var panelImg = panelGo.GetComponent<Image>();
        panelImg.color = new Color(0.08f, 0.07f, 0.09f, 0.96f);

        // 标题
        var title = MakeText(root, "野兽觉醒祭坛", 30, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -28), new Vector2(600, 44));
        title.color = new Color(0.95f, 0.85f, 0.55f);

        // 筹码显示
        chipsText = MakeText(root, "", 22, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(30, -30), new Vector2(260, 34));
        chipsText.alignment = TextAlignmentOptions.Left;
        chipsText.color = new Color(0.8f, 0.95f, 0.75f);

        // 关闭按钮（离开 = 回卷轴选下一关）
        var closeBtn = MakeButton(root, "离开祭坛", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-30, -26), new Vector2(140, 42));
        closeBtn.onClick.AddListener(() =>
        {
            if (MapManager.Instance != null) MapManager.Instance.FinishNonBattleNode();
        });

        if (offers.Count == 0)
        {
            var empty = MakeText(root, "牌组里的野兽似乎都已经觉醒过了。", 22,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 40));
            empty.color = new Color(0.8f, 0.75f, 0.65f);
            return;
        }

        // 三栏
        float slotW = 200f;
        float gap = 20f;
        float totalW = offers.Count * slotW + (offers.Count - 1) * gap;
        for (int i = 0; i < offers.Count; i++)
        {
            float x = -totalW / 2f + slotW / 2f + i * (slotW + gap);
            BuildOfferColumn(i, x);
        }
    }

    void BuildOfferColumn(int index, float x)
    {
        var col = MakeImage(root, new Color(0.14f, 0.12f, 0.15f, 1f));
        var rt = (RectTransform)col.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200, 280);
        rt.anchoredPosition = new Vector2(x, -8);

        CardDataSO d = offers[index];
        string skillName = d.awakenedAbility != null ? d.awakenedAbility.abilityName : "？";
        string desc = d.awakenedAbility != null ? d.awakenedAbility.description : "";

        var nameText = MakeText(col.transform, "♠♥♦♣"[((int)d.suit) % 4] + " " + d.animalName, 24,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(180, 36));
        nameText.color = new Color(0.95f, 0.88f, 0.7f);

        var skillText = MakeText(col.transform, "【" + skillName + "】", 19,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(180, 30));
        skillText.color = new Color(0.75f, 0.85f, 1f);

        var descText = MakeText(col.transform, desc, 16,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(180, 120));
        descText.alignment = TextAlignmentOptions.Top;
        descText.color = new Color(0.8f, 0.78f, 0.72f);

        var btn = MakeButton(col.transform, "觉醒",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(160, 46));
        int captured = index;
        btn.onClick.AddListener(() => Buy(captured));
        buyButtons.Add(btn);

        var stateText = MakeText(col.transform, "", 15,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 72), new Vector2(180, 24));
        stateText.color = new Color(0.7f, 0.9f, 0.6f);
        stateTexts.Add(stateText);
    }

    void Buy(int index)
    {
        if (index < 0 || index >= offers.Count) return;
        CardDataSO d = offers[index];
        if (d == null || GameProgress.IsCardAwakened(d)) return;

        int price = PriceOf(d);
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.PlayerChips < price)
        {
            if (stateTexts[index] != null)
            {
                stateTexts[index].text = "筹码不够（需 " + price + "）";
                stateTexts[index].color = new Color(0.95f, 0.55f, 0.45f);
            }
            return;
        }

        gm.LoseChips(price);
        GameProgress.AwakenCard(d);
        if (SaveManager.Instance != null) SaveManager.Instance.Save();
        Debug.Log("[觉醒] " + d.animalName + " 觉醒了：" + d.abilityName);

        RefreshAll();
    }

    void RefreshAll()
    {
        if (chipsText != null && GameManager.Instance != null)
            chipsText.text = "筹码：" + GameManager.Instance.PlayerChips;

        for (int i = 0; i < offers.Count; i++)
        {
            CardDataSO d = offers[i];
            if (d == null) continue;
            bool awakened = GameProgress.IsCardAwakened(d);
            if (i < buyButtons.Count && buyButtons[i] != null)
            {
                buyButtons[i].interactable = !awakened;
                var label = buyButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = awakened ? "已觉醒" : ("觉醒 · " + PriceOf(d));
            }
            if (i < stateTexts.Count && stateTexts[i] != null && awakened)
                stateTexts[i].text = "本局已生效";
        }
    }

    // ========== uGUI 小工具 ==========

    Image MakeImage(Transform parent, Color color)
    {
        var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    TextMeshProUGUI MakeText(Transform parent, string content, int size,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 dims)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = dims;
        return tmp;
    }

    Button MakeButton(Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 dims)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.28f, 0.22f, 0.16f, 1f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.28f, 0.22f, 0.16f, 1f);
        colors.highlightedColor = new Color(0.42f, 0.33f, 0.22f, 1f);
        colors.pressedColor = new Color(0.2f, 0.16f, 0.12f, 1f);
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        btn.colors = colors;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = dims;

        var txt = MakeText(go.transform, label, 19,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, dims);
        txt.color = new Color(0.96f, 0.9f, 0.78f);
        return btn;
    }
}
