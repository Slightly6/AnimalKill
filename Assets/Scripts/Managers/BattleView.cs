using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 战斗演出层：所有动画和视觉效果（虚影/飞牌/飘字/计分板/飞撞/销毁）。
/// 挂在 BattleManager 同物体上。纯演出，不碰游戏逻辑（扣筹码/护盾等在 BattleManager）。
/// ShowHandBoard 接收 isPlay 参数，敌人回合后期可直接复用同一套演出。
/// </summary>
public class BattleView : Singleton<BattleView>
{
    [Header("桌面结算区（虚影位置公式：中心=锚点x, z+zOffset；牌高=ghostY）")]
    public Transform ghostAnchor;      // 桌面锚点（场景物体）
    public float ghostSpacing = 1.5f;  // 虚影横排间距
    public float ghostZOffset = 1.5f;  // 虚影中心相对锚点向玩家方向偏移
    public float ghostY = 2f;          // 虚影高度（锚点 y=0，牌立在 +2 的位置）
    [Range(0.1f, 1f)] public float ghostAlpha = 0.45f;   // 虚影透明度

    [Header("虚影阴影")]
    public bool realShadow = true;         // 真实阴影（方向光投影，需要桌面材质能接收阴影）
    public bool autoEnableLightShadow = true;  // 保底：方向光没开阴影时自动开 Hard Shadows
    public bool ghostShadow = false;       // 假阴影贴片（真实阴影不可用时的后备）
    public float shadowYOffset = 0.02f;    // 假阴影离桌面的高度（防穿模/闪烁）
    public float shadowWidth = 1.7f;       // 假阴影宽（比牌略窄）
    public float shadowDepth = 0.7f;       // 假阴影前后厚度（牌立着，脚下是窄条）
    [Range(0f, 1f)] public float shadowAlpha = 0.35f;    // 假阴影浓度

    [Header("虚影烟雾（在 Unity 里做好 ParticleSystem 预制体拖进来；留空=不冒烟）")]
    public ParticleSystem smokePrefab;   // 烟雾粒子预制体（每张虚影结算时在其位置播放一次）
    public float smokeDestroyDelay = 2f; // 播放后几秒销毁粒子对象（要大于粒子总寿命）

    [Header("演出引用")]
    public TextMeshPro Score;    // 得分文本
    public Transform EnemyPos;         // 敌人位置（Score 飞撞目标）
    public Transform PlayerFoldPos;         // 玩家位置（弃牌加盾时 Score 飞向这）
    public Transform PlayerPos;

    private readonly List<GameObject> ghosts = new List<GameObject>();   // 桌面虚影（牌自己的半透明克隆）
    private readonly List<GameObject> shadows = new List<GameObject>();  // 每个虚影脚下的阴影
    private static Shader ghostShader;   // 虚影专用透明 shader（懒加载）
    private static Material shadowMaterial;   // 阴影黑片材质（共用）
    private Camera mainCam;
    private Vector3 scoreHomePos;  // Score 归位用
    [Header("敌人手牌")]
    public Transform enemyHandRoot;
    public float enemyHandSpacing = 1.2f;
    public bool enemyCardsFaceDown = true;

    public readonly List<Card> enemyHandViews = new List<Card>();

    // BattleManager.Start 调用，初始化相机和阴影保底
    public void Init()
    {
        mainCam = Camera.main;
        if (autoEnableLightShadow) EnsureDirectionalShadow();
        EventBus.Subscribe<EnemyHandChangedEvent>(OnEnemyHandChanged);
        EventBus.Subscribe<EnemyIntentChangedEvent>(OnEnemyIntentChanged);
        EventBus.Subscribe<ChipsChangedEvent>(OnChipsChanged);
    }
    
    private void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyHandChangedEvent>(OnEnemyHandChanged);
        EventBus.Unsubscribe<EnemyIntentChangedEvent>(OnEnemyIntentChanged);
        EventBus.Unsubscribe<ChipsChangedEvent>(OnChipsChanged);
    }
        private void OnEnemyHandChanged(EnemyHandChangedEvent e)
    {
        RefreshEnemyHand();
    }

    private void OnEnemyIntentChanged(EnemyIntentChangedEvent e)
    {
        if(e.intent == EnemyIntentType.Check)
        {
            Narrator.Say(SpeakTopic.Check);
        }
        else if(e.intent == EnemyIntentType.Call)
        {
            Narrator.Say(SpeakTopic.Call);
        }
        else if(e.intent == EnemyIntentType.Raise)
        {
            Narrator.Say(SpeakTopic.Raise);
        }
        Debug.Log("[敌人意图显示] " + e.intent);
    }

    private void OnChipsChanged(ChipsChangedEvent e)
    {
        Debug.Log("[敌人筹码] " + e.enemyChips );
    }

    // 保底：找到方向光，没开阴影就开 Hard Shadows；质量设置里关了阴影也一并打开
    private void EnsureDirectionalShadow()
    {
        if (QualitySettings.shadows == ShadowQuality.Disable)
            QualitySettings.shadows = ShadowQuality.All;

        Light[] lights = FindObjectsOfType<Light>();
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional && l.shadows == LightShadows.None)
            {
                l.shadows = LightShadows.Hard;
                Debug.Log("[虚影] 方向光「" + l.name + "」阴影已自动开启（Hard Shadows）");
            }
        }
    }

    // ========== 虚影 ==========

    // 选中牌变化时刷新：点一张出一张虚影（牌自己的半透明克隆），按点数→花色排序横排
    public void RefreshGhosts()
    {
        List<Card> picked = GetSortedSelectedCards();
        ClearGhosts();
        foreach (Card c in picked)
        {
            ghosts.Add(CreateGhost(c.Data));

            shadows.Add(ghostShadow ? CreateShadow() : null);
        }

        List<Vector3> positions = GetPlayPositions(picked.Count);
        Quaternion facePlayer = GetGhostRotation();
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].transform.position = positions[i];
            ghosts[i].transform.rotation = facePlayer;

            // 阴影贴在桌面：X/Z 和虚影一致，Y = 锚点高度 + 小偏移
            if (shadows[i] != null)
            {
                float groundY = GetAnchorY() + shadowYOffset;
                shadows[i].transform.position = new Vector3(positions[i].x, groundY, positions[i].z);
            }
        }
    }

    public void ClearGhosts()
    {
        foreach (GameObject g in ghosts) if (g != null) Destroy(g);
        ghosts.Clear();
        foreach (GameObject s in shadows) if (s != null) Destroy(s);
        shadows.Clear();
    }

    // 只移除某一张虚影+它的阴影（牌飞到它位置时调用）
    private void RemoveGhostAt(int index)
    {
        if (index >= 0 && index < ghosts.Count && ghosts[index] != null) Destroy(ghosts[index]);
        if (index >= 0 && index < shadows.Count && shadows[index] != null) Destroy(shadows[index]);
    }

    // 在虚影位置播放一次烟雾粒子（预制体在 Inspector 拖；"掉桌面"靠粒子自身重力参数）
    private void PlaySmoke(Vector3 pos)
    {
        if (smokePrefab == null)
        {
            Debug.LogWarning("[烟雾] Smoke Prefab 没拖到 BattleView，无法冒烟");
            return;
        }
        ParticleSystem smoke = Instantiate(smokePrefab, pos, Quaternion.identity);
        smoke.Play();
        Destroy(smoke.gameObject, smokeDestroyDelay);
    }

    private float GetAnchorY()
    {
        if (ghostAnchor != null) return ghostAnchor.position.y;
        if (BoardManager.Instance != null) return BoardManager.Instance.transform.position.y;
        return 0f;
    }

    // 脚下假阴影：平躺的半透明黑色片（Quad 绕 X 转 90°）
    private GameObject CreateShadow()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.transform.SetParent(transform);
        go.name = "PlayGhostShadow";
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);   // 阴影不挡点击

        if (shadowMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");   // 内置、支持透明
            shadowMaterial = new Material(sh != null ? sh : Shader.Find("Unlit/Color"));
            shadowMaterial.color = new Color(0f, 0f, 0f, shadowAlpha);
            if (shadowMaterial.HasProperty("_Color"))
                shadowMaterial.SetColor("_Color", new Color(0f, 0f, 0f, shadowAlpha));
        }
        go.GetComponent<Renderer>().sharedMaterial = shadowMaterial;

        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 平躺桌面
        go.transform.localScale = new Vector3(shadowWidth, shadowDepth, 1f);
        return go;
    }

    // 虚影立着朝向玩家的旋转（和手牌同一面向：直接用相机旋转，不带扇形倾斜）
    private Quaternion GetGhostRotation()
    {
        if (mainCam == null) mainCam = Camera.main;
        return mainCam != null ? mainCam.transform.rotation : Quaternion.identity;
    }

    private GameObject CreateGhost(CardDataSO data)
    {
        if (DeckManager.Instance == null || DeckManager.Instance.cardPrefab == null)
        {
            return new GameObject("PlayGhost(Empty)");
        }

        GameObject prefab = DeckManager.Instance.cardPrefab;
        GameObject go = Instantiate(prefab);
        go.transform.SetParent(transform);
        go.name = "PlayGhost_" + data.name;

        Card card = go.GetComponent<Card>();
        if (card != null)
        {
            card.Init(data, true);
            card.SetFaceDown(false);   // 虚影亮正面，让玩家看到将打出的牌
        }

        CardDisplay display = go.GetComponent<CardDisplay>();
        if (display != null) Destroy(display);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        if (ghostShader == null) ghostShader = Shader.Find("Custom/PlayingCardGhost");

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            // 真实阴影：虚影各部件都投影（ShadowCaster Pass 在 ghost shader 里）
            r.shadowCastingMode = realShadow
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            if (r.sharedMaterial == null) continue;
            if (ghostShader != null && r.sharedMaterial.shader.name == "Custom/PlayingCard")
            {
                r.material.shader = ghostShader;
                if (r.material.HasProperty("_GhostAlpha")) r.material.SetFloat("_GhostAlpha", ghostAlpha);
            }
        }

        // 点数花色文字（TMP）也跟着半透明；文字片不投影，避免碎影
        foreach (TMP_Text tmp in go.GetComponentsInChildren<TMP_Text>(true))
        {
            Color c = tmp.color;
            tmp.color = new Color(c.r, c.g, c.b, ghostAlpha);
            Renderer tr = tmp.GetComponent<Renderer>();
            if (tr != null) tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 保持预制体原始缩放（Instantiate 没父物体时是世界缩放）
        go.transform.localScale = prefab.transform.localScale;
        return go;
    }

    private List<Vector3> GetPlayPositions(int count)
    {
        List<Vector3> list = new List<Vector3>();
        Vector3 anchorPos = Vector3.zero;
        if (ghostAnchor != null) anchorPos = ghostAnchor.position;
        else if (BoardManager.Instance != null) anchorPos = BoardManager.Instance.transform.position;

        Vector3 center = new Vector3(anchorPos.x, 0f, anchorPos.z + ghostZOffset);
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * ghostSpacing;
            list.Add(center + new Vector3(x, ghostY, 0f));
        }
        return list;
    }

    // ========== 选中牌 ==========

    // 取当前选中的牌（过滤已销毁的），按 点数升序 → 花色(♠♥♦♣) 排序
    public List<Card> GetSortedSelectedCards()
    {
        List<Card> list = new List<Card>();

        // 1. 遍历选中的卡，过滤，加进列表
        for (int i = 0; i < CardDisplay.selectedCards.Count; i++)
        {
            CardDisplay d = CardDisplay.selectedCards[i];
            if (d == null) continue;
            if (d.Card == null) continue;
            if (d.Card.Data == null) continue;

            list.Add(d.Card);
        }

        // 2. 排序：先点数，再花色
        list.Sort((a, b) =>
        {
            int r = ((int)b.Data.rank).CompareTo((int)a.Data.rank);
            if (r != 0) return r;
            return ((int)b.Data.suit).CompareTo((int)a.Data.suit);
        });

        return list;
    }

    public void ClearSelection()
    {
        foreach (CardDisplay d in CardDisplay.selectedCards)
            if (d != null && d.Card != null) d.Card.IsSelected = false;
        CardDisplay.selectedCards.Clear();
    }

    // ========== 完整演出入口（BattleManager 调用） ==========

    // 完整演出流程：飞牌→计分→飞撞→销毁牌
    // BattleManager 先用 PokerResolver 算好 type/damage 等，传进来
    public IEnumerator PlayResolveSequence( List<Card> cards, HandType type, int baseChips, int mult,int damage, HashSet<int> coreIndices, bool isPlayfold)
    {
        // 1. 牌依次飞到虚影位，立着面向玩家
        List<Vector3> positions = GetPlayPositions(cards.Count);
        Quaternion facePlayer = GetGhostRotation();
        for (int i = 0; i < cards.Count; i++)
        {
            yield return FlyToTable(cards[i], positions[i], facePlayer, 0.18f);

            // 飞到瞬间：该位置冒烟 + 这张虚影消失（真牌顶替虚影停在桌上）
            PlaySmoke(positions[i]);
            RemoveGhostAt(i);
        }

        // 2. 计分演出（阶段1-3 + 最终伤害 + 飞撞 + Score归位）
        yield return ShowHandBoard(type, baseChips, mult, damage, positions, cards, coreIndices, isPlayfold);

        if (isPlayfold)
        {
            // 弃牌加盾：先显示 ×0.8，再显示 Check 值，Score 飞向玩家
            Score.text = damage.ToString() + "x0.8";
            yield return PunchScore();
            yield return new WaitForSeconds(0.5f);
            Score.text = Mathf.RoundToInt(damage * 0.8f).ToString();
            yield return PunchScore();
            yield return ScoreSlamEnemy(damage, true,false);
        }
        else
        {
            // 出牌打人：直接显示伤害，Score 飞向敌人
            Score.text = damage.ToString();
            yield return PunchScore();
            yield return ScoreSlamEnemy(damage, false,false);
        }

        Score.text = "";
        Score.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        Score.transform.position = scoreHomePos;
        // 3. 间隔
        yield return new WaitForSeconds(0.2f);

        // 4. 结算完了牌才离场：缩小消失
        yield return ShrinkOutCards(cards);
    }

    // ========== 飞牌 ==========

    // 牌从当前位置飞到桌面结算位（同时放平）
    private IEnumerator FlyToTable(Card card, Vector3 toPos, Quaternion toRot, float duration)
    {
        if (card == null) yield break;
        Vector3 fromPos = card.transform.position;
        Quaternion fromRot = card.transform.rotation;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            card.transform.position = Vector3.Lerp(fromPos, toPos, p);
            card.transform.rotation = Quaternion.Slerp(fromRot, toRot, p);
            yield return null;
        }
        card.transform.position = toPos;
        card.transform.rotation = toRot;
    }

    // ========== 飘字 ==========

    private IEnumerator FloatingText(Vector3 pos, string text, Color color, int jitterIndex = 0)
    {
        GameObject go = new GameObject("FloatText");
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null) go.transform.rotation = mainCam.transform.rotation;   // 面向相机

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 2.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 60;
        tmp.color = color;

        float jitterX = (jitterIndex - 0.5f) * 0.4f;
        float jitterY = Random.Range(-0.1f, 0.1f);
        float RotationZ = Random.Range(0, 9);
        go.transform.rotation = Quaternion.Euler(0, 180, RotationZ);
        Vector3 basePos = pos + new Vector3(0, jitterY, 0.8f);

        go.transform.position = basePos;
        go.transform.localScale = Vector3.one * 4;
        yield return PopIn(go.transform, 0.18f);

        yield return new WaitForSeconds(0.3f);


        yield return PopOut(go.transform, 0.3f);
        Destroy(go);
    }

    // ========== 计分演出 ==========

    // 结算全流程演出（含阶段1-3 + 最终伤害 + 飞撞 + Score归位）
    // isPlay: true=弃牌加盾(显示×0.8,飞向玩家)；false=出牌打人(显示伤害,飞向敌人)
    private IEnumerator ShowHandBoard(HandType type, int baseChips, int mult,
                                       int damage, List<Vector3> positions, List<Card> cards, HashSet<int> coreIndices, bool isPlay)
    {
        int running = 0;

        // 阶段1：逐张飘字 + Score 实时上涨
        for (int i = 0; i < cards.Count; i++)
        {
            if (coreIndices.Contains(i))
            {
                yield return FloatingText(positions[i], PokerResolver.RankDisplay(cards[i].Data.rank), Color.blue);
                running += int.Parse(PokerResolver.RankDisplay(cards[i].Data.rank).TrimStart('+'));
                Score.text = running.ToString();
                StartCoroutine(PunchScore());
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 阶段2：中间弹"牌型 +底分"，Score 加上底分
        GameObject board = CreateBoardText(PokerResolver.HandName(type) + " +" + baseChips, new Color(0.4f, 0.7f, 1f));
        float jitterY = Random.Range(-0.1f, 0.1f);
        float RotationZ = Random.Range(0, 9);
        board.transform.rotation = Quaternion.Euler(0, 180, RotationZ);
        Vector3 basePos = GetPlayCenter() + new Vector3(0, jitterY, 0.8f);

        board.transform.position = basePos;
        yield return PopIn(board.transform, 0.2f);
        running += baseChips;
        yield return new WaitForSeconds(0.2f);
        Score.text = running.ToString();
        yield return PunchScore();
        yield return new WaitForSeconds(0.5f);

        // "+底分"缩掉，只留牌型名
        TextMeshPro tmp = board.GetComponent<TextMeshPro>();
        tmp.text = PokerResolver.HandName(type);
        yield return new WaitForSeconds(0.2f);

        // 阶段3：弹"×倍率"
        tmp.text = PokerResolver.HandName(type) + " ×" + mult;
        tmp.color = new Color(1f, 0.85f, 0.3f);
        yield return PopIn(board.transform, 0.2f);
        yield return new WaitForSeconds(0.5f);
        yield return PopOut(board.transform, 0.15f);
        Destroy(board);

        
    }

    // Score 加分反馈
    private IEnumerator PunchScore()
    {
        Vector3 baseScale = new Vector3(0.2f, 0.2f, 0.2f);   // Score 的正常大小
        float dur = 0.18f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = p < 0.5f ? Mathf.Lerp(1f, 1.4f, p * 2f) : Mathf.Lerp(1.4f, 1f, (p - 0.5f) * 2f);
            Score.transform.localScale = baseScale * s;
            yield return null;
        }
        Score.transform.localScale = baseScale;
    }

    private GameObject CreateBoardText(string text, Color color)
    {
        GameObject go = new GameObject("CenterBoard");
        Vector3 center = GetPlayCenter();
        go.transform.position = center;
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null) go.transform.rotation = mainCam.transform.rotation;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 10f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 70;
        tmp.color = color;
        return go;
    }

    // 结算区中心
    private Vector3 GetPlayCenter()
    {
        List<Vector3> pos = GetPlayPositions(1);
        return pos[0];
    }

    // ========== 通用动画 ==========

    // 放大
    private IEnumerator PopIn(Transform t, float dur)
    {
        Vector3 baseScale = t.localScale;   // 记住调用前的初始大小（飘字=4，计分板=1）
        t.localScale = Vector3.zero;
        float time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / dur);
            float s = p < 0.5f ? Mathf.Lerp(0f, 1.3f, p * 2f) : Mathf.Lerp(1.3f, 1f, (p - 0.5f) * 2f);
            t.localScale = baseScale * s;
            yield return null;
        }
        t.localScale = baseScale;
    }

    // 缩到 0
    private IEnumerator PopOut(Transform t, float dur)
    {
        Vector3 startScale = t.localScale;
        float time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / dur);
            t.localScale = startScale * (1f - p);
            yield return null;
        }
        t.localScale = Vector3.zero;
    }

    // 攻击移动路径（Score 飞撞目标）
    // isPlay: true=飞向玩家(加盾)；false=飞向敌人(打人)
    private IEnumerator ScoreSlamEnemy(int damage, bool isPlayfold,bool enemy)
    {
        scoreHomePos = Score.transform.position;   // 记住原位，下次归位用

        Vector3 enemyPos = EnemyPos.position;
        Vector3 playerFoldPos = PlayerFoldPos.position;
        Vector3 playerPos = PlayerPos.position;
        Vector3 slamPos;

        if (isPlayfold)
        {      
            slamPos = playerFoldPos + Vector3.up * 1f;
        }
        else
        {
            if(enemy)
            {
                slamPos = playerPos + Vector3.up * 1f;
            }else
            {
                slamPos = enemyPos + Vector3.up * 1f;
            }
            
        }


        // 抛物线飞过去（放大）
        float t = 0f;
        float goDur = 0.7f;
        while (t < goDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / goDur);
            Vector3 mid = (scoreHomePos + slamPos) * 0.5f;
            Score.transform.position = Bezier(scoreHomePos, mid, slamPos, p);
            yield return null;
        }

        yield return new WaitForSeconds(0.15f);

        // Score 在目标位置缩掉
        yield return PopOut(Score.transform, 0.2f);
    }

    // 移动函数
    private Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // ========== 销毁牌 ==========

    // 结算完的牌整体缩小到 0 再销毁（有过渡，不再瞬间消失）
    private IEnumerator ShrinkOutCards(List<Card> cards)
    {
        List<Transform> ts = new List<Transform>();
        foreach (Card c in cards)
            if (c != null) ts.Add(c.transform);

        List<Vector3> baseScales = new List<Vector3>();
        foreach (Transform tr in ts) baseScales.Add(tr.localScale);

        float t = 0f;
        float dur = 0.25f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            for (int i = 0; i < ts.Count; i++)
            {
                if (ts[i] == null) continue;
                ts[i].localScale = baseScales[i] * (1f - p);   // 整体匀速缩小
            }
            yield return null;
        }

        foreach (Card c in cards)
        {
            if (c == null) continue;
            // DeckManager.Instance.RemoveFromHand(c);
            Destroy(c.gameObject);
        }
    }

        // ========== 敌人手牌显示 ==========

    // 刷新敌人手牌显示（BattleManager.StartLevel 和 EnemyController.RefillHand 后调）
     // 敌人手牌刷新：增量更新（已有的不动，只补缺的、删多余的）
    public void RefreshEnemyHand()   //手牌视图无限增加啊bug******
    {
        if (EnemyController.Instance == null) return;
        if (DeckManager.Instance == null || DeckManager.Instance.cardPrefab == null)
        {
            Debug.LogError("[BattleView] DeckManager.cardPrefab 为空");
            return;
        }
        if (enemyHandRoot == null)
        {
            Debug.LogError("[BattleView] enemyHandRoot 没拖引用");
            return;
        }

        IReadOnlyList<CardDataSO> hand = EnemyController.Instance.Hand;

        // 多余的视图销毁（hand 数据比视图少时）
        while (enemyHandViews.Count > hand.Count)
        {
            int last = enemyHandViews.Count - 1;
            if (enemyHandViews[last] != null) Destroy(enemyHandViews[last].gameObject);
            enemyHandViews.RemoveAt(last);
        }

        // 缺的视图补上（hand 数据比视图多时）
        while (enemyHandViews.Count < hand.Count)
        {
            int idx = enemyHandViews.Count;
            GameObject go = Instantiate(DeckManager.Instance.cardPrefab, enemyHandRoot);
            Card card = go.GetComponent<Card>();
            if (card == null) { Destroy(go); break; }

            card.Init(hand[idx], true);
            card.SetFaceDown(enemyCardsFaceDown);

            CardDisplay display = go.GetComponent<CardDisplay>();
            if (display != null) Destroy(display);

            enemyHandViews.Add(card);
        }

        LayoutEnemyHand();
    }

    public void RemoveFromHand(Card card)
    {
        enemyHandViews.Remove(card);
    }

    private void ClearEnemyHandViews()
    {
        foreach (Card card in enemyHandViews)
            if (card != null) Destroy(card.gameObject);
        enemyHandViews.Clear();
    }

    private void LayoutEnemyHand()
    {
        for (int i = 0; i < enemyHandViews.Count; i++)
        {
            float x = (i - (enemyHandViews.Count - 1) / 2f) * enemyHandSpacing;
            enemyHandViews[i].transform.localPosition = new Vector3(x, 0f, 0f);
        }
    }

    public void ClearEnemyHand()
    {
        ClearEnemyHandViews();
    }

    // ========== 敌人出牌演出 ==========

    // 敌人飞牌到桌面中心 + 显示伤害 + Score飞撞玩家 + 牌销毁
    // 不复用 PlayResolveSequence（那个调 DeckManager.RemoveFromHand，玩家专用）
        // 敌人出牌演出：复用玩家计分链路（飞牌→逐张飘字→牌型+底分→×倍率）+ Score飞撞玩家
    public IEnumerator PlayEnemyResolveSequence(EnemyPlayData playData,HandType type, int baseChips, int mult, int damage,
        HashSet<int> coreIndices, float attackMult = 1f,int preDamage = 0)
    {
        // 1. 取选中的敌人手牌视图
        List<Card> selectedViews = new List<Card>();
        foreach (int index in playData.handIndices)
        {
            if (index < 0 || index >= enemyHandViews.Count) continue;
            selectedViews.Add(enemyHandViews[index]);
        }
        foreach (Card c in selectedViews)
        {
            RemoveFromHand(c);
        }
        // 2. 飞到桌面结算位（和玩家同一位置）
        List<Vector3> positions = GetPlayPositions(selectedViews.Count);
        Quaternion facePlayer = GetGhostRotation();
        for (int i = 0; i < selectedViews.Count; i++)
        {
            yield return FlyToTable(selectedViews[i], positions[i], facePlayer, 0.18f);
        }

        // 3. 计分演出（和玩家完全一致）—— isPlay=false 走"直接显示伤害"分支
        yield return ShowHandBoard(type, baseChips, mult, damage, positions, selectedViews, coreIndices, isPlay: false);
        // 3.5 减半分步演出：先显示减半前伤害 → 弹红×0.5 → 数字当场变一半
        if (attackMult < 1f)
        {
            Score.text = preDamage.ToString();        // 减半前的数
            yield return PunchScore();
            yield return new WaitForSeconds(0.4f);

            GameObject debuff = CreateBoardText("×" + attackMult.ToString("0.##"), new Color(1f, 0.4f, 0.4f));
            yield return PopIn(debuff.transform, 0.2f);
            yield return new WaitForSeconds(0.5f);

            Score.text = damage.ToString();           // 当场变成一半
            StartCoroutine(PunchScore());
            yield return new WaitForSeconds(0.4f);
            yield return PopOut(debuff.transform, 0.15f);
            Destroy(debuff);
        }

        
        // 4. Score 显示伤害 + 飞撞玩家（敌人是打玩家，方向是 PlayerPos）
        Score.text = damage.ToString();
        yield return PunchScore();
        yield return ScoreSlamEnemy(damage, false,true);   // true=飞向 PlayerflodPos ，false=飞向 PlayerPos

        // 5. Score 归位
        Score.text = "";
        Score.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        Score.transform.position = scoreHomePos;

        yield return new WaitForSeconds(0.2f);

        // 6. 牌缩小销毁（敌人专用）
        yield return ShrinkOutEnemyCards(selectedViews);
    }

    // 敌人专用销毁（不碰 DeckManager）
    private IEnumerator ShrinkOutEnemyCards(List<Card> cards)
    {

        List<Transform> ts = new List<Transform>();
        foreach (Card c in cards)
            if (c != null) ts.Add(c.transform);

        List<Vector3> baseScales = new List<Vector3>();
        foreach (Transform tr in ts) baseScales.Add(tr.localScale);

        float t = 0f;
        float dur = 0.25f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            for (int i = 0; i < ts.Count; i++)
            {
                if (ts[i] == null) continue;
                ts[i].localScale = baseScales[i] * (1f - p);
            }
            yield return null;
        }

        foreach (Card c in cards)
        {    
            Destroy(c.gameObject);
        }
    }
}
