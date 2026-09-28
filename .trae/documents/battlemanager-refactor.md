# BattleManager 重构方案：拆分为 4 个可维护文件

## 概要

将当前 709 行的 `BattleManager.cs` 按职责拆成 4 个文件，**保证演出效果和游戏逻辑完全不变**，为后期类杀戮尖塔地图（动态生成敌人/多关卡）提供高维护性。

| 文件 | 职责 | 挂载位置 | 基类 |
|---|---|---|---|
| `BattleManager.cs`（瘦身） | 战斗流程编排 + 游戏逻辑 | 场景现有物体 | `Singleton<BattleManager>` |
| `BattleView.cs`（新建） | 所有动画演出（虚影/飞牌/飘字/计分板/飞撞/销毁） | **同 BattleManager 物体** | `Singleton<BattleView>` |
| `EnemyController.cs`（新建） | 敌人手牌和 AI（当前只建骨架，后期填） | **独立场景物体** | `Singleton<EnemyController>` |
| `PokerResolver.cs`（新建） | 纯牌型/分数/伤害计算（静态工具类） | 无（静态类） | `static class` |

---

## 现状分析

当前 `BattleManager.cs`（`Assets/Scripts/Managers/BattleManager.cs`，709 行）混合了 4 类职责：

1. **流程编排**：`StartLevel` / `GameLoop` / `SetPhase` / `OnEndPlayPhase` / `OnLevelCleared`
2. **虚影演出**：`RefreshGhosts` / `ClearGhosts` / `RemoveGhostAt` / `PlaySmoke` / `CreateShadow` / `CreateGhost` / `GetPlayPositions`（L144-310）
3. **计分演出**：`FlyToTable` / `FloatingText` / `ShowHandBoard` / `PunchScore` / `CreateBoardText` / `PopIn` / `PopOut` / `ScoreSlamEnemy` / `Bezier` / `ShrinkOutCards`（L435-698）
4. **牌型计算**：手动调 `PokerHandEvaluator` + 内联 `RankDisplay` / `HandName` / `cardBonus` 计算（L383-397, L605-666）

关键现状：
- `isPlay` 是**字段**（L22），由 `OnEndPlayPhase` 设置（L96），`ResolvePlayerHand` 读取（L400）。语义：`isPlay=true`→弃牌加盾，`isPlay=false`→出牌打人。**保持不变**。
- `ShowHandBoard`（L485-532）当前只做阶段 1-3（逐张飘字+牌型底分+倍率），**最终伤害演出**（"×0.8"/飞撞）在 `ResolvePlayerHand` 的 L400-415 里，且 `GameManager.EnemyLoseChips(damage)` 游戏逻辑混在 L413。
- `currentCheck` 字段（L60）已定义但**当前未使用**（护盾加成逻辑后期填）。
- 外部调用点：`CardDisplay` L166/175/184 调 `RefreshGhosts`；`MapManager` L171 调 `StartLevel`；`SkipEnemyTurnEffectSO` L12 调 `SkipEnemyAttack`。

---

## 具体改动

### 4.1 新建 `Assets/Scripts/Core/PokerResolver.cs`

**职责**：纯计算，不碰 Unity 对象。包装 `PokerHandEvaluator`，集中伤害/文本格式化。

**静态方法**：
```csharp
public static class PokerResolver
{
    // 转发 PokerHandEvaluator
    public static HandType Evaluate(List<CardDataSO> cards)
        => PokerHandEvaluator.Evaluate(cards);
    public static int GetBaseChips(HandType type)
        => PokerHandEvaluator.GetBaseChips(type);
    public static int GetMultiplier(HandType type)
        => PokerHandEvaluator.GetMultiplier(type);
    public static HashSet<int> GetCoreCardIndices(List<CardDataSO> cards, HandType type)
        => PokerHandEvaluator.GetCoreCardIndices(cards, type);

    // 点数→显示文本（从 BattleManager L605-610 搬来，原样）
    // A(1) 当 14，其余按点数；返回 "+14" 形式
    public static string RankDisplay(CardRank r)
    {
        int v = (int)r;
        if (v == 1) v = 14;
        return "+" + v.ToString();
    }

    // 牌型→中文名（从 BattleManager L652-666 搬来，原样）
    public static string HandName(HandType t) { /* 原 switch 原样搬 */ }

    // 主牌加成总和（从 ResolvePlayerHand L390-395 抽出）
    // 遍历 coreIndices 中的牌，累加其点数（等价于原 RankDisplay+TrimStart+Parse）
    public static int CalcCardBonus(List<Card> cards, HashSet<int> coreIndices)
    {
        int bonus = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (coreIndices.Contains(i))
            {
                int v = (int)cards[i].Data.rank;
                if (v == 1) v = 14;   // 和 RankDisplay 等价
                bonus += v;
            }
        }
        return bonus;
    }

    // 最终伤害 = (底分 + 主牌加成) × 倍率（从 ResolvePlayerHand L397 原样）
    public static int CalcDamage(int baseChips, int cardBonus, int mult)
        => (baseChips + cardBonus) * mult;
}
```

**注意**：`CalcCardBonus` 用数值计算替代原代码的字符串解析（`RankDisplay().TrimStart('+')` + `Parse`），**数值结果完全等价**，更干净。

---

### 4.2 新建 `Assets/Scripts/Managers/BattleView.cs`

**职责**：所有动画演出。挂在 BattleManager 同物体上。`Singleton<BattleView>`。

**字段**（全部从 BattleManager 搬来，Inspector 拖引用）：
```csharp
public class BattleView : Singleton<BattleView>
{
    [Header("桌面结算区")]
    public Transform ghostAnchor;
    public float ghostSpacing = 1.5f;
    public float ghostZOffset = 1.5f;
    public float ghostY = 2f;
    [Range(0.1f, 1f)] public float ghostAlpha = 0.45f;

    [Header("虚影阴影")]
    public bool realShadow = true;
    public bool autoEnableLightShadow = true;
    public bool ghostShadow = false;
    public float shadowYOffset = 0.02f;
    public float shadowWidth = 1.7f;
    public float shadowDepth = 0.7f;
    [Range(0f, 1f)] public float shadowAlpha = 0.35f;

    [Header("虚影烟雾")]
    public ParticleSystem smokePrefab;
    public float smokeDestroyDelay = 2f;

    [Header("演出引用")]
    public TextMeshPro Score;
    public Transform EnemyPos;
    public Transform PlayerPos;

    // 内部
    private readonly List<GameObject> ghosts = new List<GameObject>();
    private readonly List<GameObject> shadows = new List<GameObject>();
    private static Shader ghostShader;
    private static Material shadowMaterial;
    private Camera mainCam;
    private Vector3 scoreHomePos;
}
```

**方法分配**（全部从 BattleManager 原样搬来，私有方法不变；标注公开接口）：

| 方法 | 来源行号 | 可见性 | 说明 |
|---|---|---|---|
| `Init()` | 新增 | public | 初始化 mainCam + EnsureDirectionalShadow，BattleManager.Start 调 |
| `EnsureDirectionalShadow()` | L71-85 | private | 原样 |
| `RefreshGhosts()` | L147-172 | **public** | CardDisplay 调用 |
| `ClearGhosts()` | L174-180 | public | StartLevel 调用 |
| `RemoveGhostAt(int)` | L183-187 | private | 原样 |
| `PlaySmoke(Vector3)` | L190-200 | private | 原样 |
| `GetAnchorY()` | L202-207 | private | 原样 |
| `CreateShadow()` | L210-231 | private | 原样 |
| `GetGhostRotation()` | L234-238 | private | 原样 |
| `CreateGhost(CardDataSO)` | L240-294 | private | 原样 |
| `GetPlayPositions(int)` | L296-310 | private | 原样 |
| `GetPlayCenter()` | L569-573 | private | 原样 |
| `GetSortedSelectedCards()` | L315-339 | **public** | BattleManager 调用 |
| `ClearSelection()` | L341-346 | **public** | BattleManager 调用 |
| `FlyToTable(...)` | L436-452 | private | 原样 |
| `FloatingText(...)` | L454-483 | private | 原样 |
| `ShowHandBoard(...)` | L485-532 + L400-415 整合 | private | **扩展**，见下 |
| `PunchScore()` | L534-548 | private | 原样 |
| `CreateBoardText(...)` | L550-566 | private | 原样 |
| `PopIn(...)` | L575-589 | private | 原样 |
| `PopOut(...)` | L592-604 | private | 原样 |
| `ScoreSlamEnemy(int, bool)` | L612-644 | private | **加 isPlay 参数**，见下 |
| `Bezier(...)` | L646-650 | private | 原样 |
| `ShrinkOutCards(List<Card>)` | L669-698 | private | 原样（含 RemoveFromHand+Destroy） |

**公开演出入口**（BattleManager 调用）：
```csharp
// 完整演出流程：飞牌→计分→飞撞→销毁牌
// BattleManager 先用 PokerResolver 算好 type/damage 等，传进来
public IEnumerator PlayResolveSequence(
    List<Card> cards, HandType type, int baseChips, int mult,
    int damage, HashSet<int> coreIndices, bool isPlay)
{
    // 1. 飞牌到虚影位 + 冒烟 + 删虚影（原 L373-382）
    List<Vector3> positions = GetPlayPositions(cards.Count);
    Quaternion facePlayer = GetGhostRotation();
    for (int i = 0; i < cards.Count; i++)
    {
        yield return FlyToTable(cards[i], positions[i], facePlayer, 0.18f);
        PlaySmoke(positions[i]);
        RemoveGhostAt(i);
    }

    // 2. 计分演出（阶段1-3 + 最终伤害 + 飞撞 + Score归位）
    yield return ShowHandBoard(type, baseChips, mult, damage, positions, cards, coreIndices, isPlay);

    // 3. 等0.2s
    yield return new WaitForSeconds(0.2f);

    // 4. 牌缩小销毁（原 L426）
    yield return ShrinkOutCards(cards);
}
```

**ShowHandBoard 扩展**（整合原 L485-532 + L400-418）：
```csharp
private IEnumerator ShowHandBoard(HandType type, int baseChips, int mult,
    int damage, List<Vector3> positions, List<Card> cards,
    HashSet<int> coreIndices, bool isPlay)
{
    int running = 0;

    // 阶段1: 逐张飘字 + Score上涨（原 L490-500）
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

    // 阶段2: 牌型+底分 → +底分缩掉 → 牌型名（原 L504-522）
    GameObject board = CreateBoardText(PokerResolver.HandName(type) + " +" + baseChips, new Color(0.4f, 0.7f, 1f));
    // ... 原样 ...
    running += baseChips;
    yield return new WaitForSeconds(0.2f);
    Score.text = running.ToString();
    yield return PunchScore();
    yield return new WaitForSeconds(0.5f);
    TextMeshPro tmp = board.GetComponent<TextMeshPro>();
    tmp.text = PokerResolver.HandName(type);
    yield return new WaitForSeconds(0.2f);

    // 阶段3: 牌型×倍率 → 缩掉（原 L525-530）
    tmp.text = PokerResolver.HandName(type) + " ×" + mult;
    tmp.color = new Color(1f, 0.85f, 0.3f);
    yield return PopIn(board.transform, 0.2f);
    yield return new WaitForSeconds(0.5f);
    yield return PopOut(board.transform, 0.15f);
    Destroy(board);

    // 阶段4(整合自原 L400-415): 最终伤害演出
    if (isPlay)   // 弃牌加盾：显示 ×0.8
    {
        Score.text = damage.ToString() + "x0.8";
        yield return PunchScore();
        yield return new WaitForSeconds(0.5f);
        Score.text = Mathf.RoundToInt(damage * 0.8f).ToString();
        yield return PunchScore();
        yield return ScoreSlamEnemy(damage, isPlay);
    }
    else          // 出牌打人：显示伤害
    {
        Score.text = damage.ToString();
        yield return PunchScore();
        yield return ScoreSlamEnemy(damage, isPlay);
    }

    // 阶段5(整合自原 L416-418): Score归位
    Score.text = "";
    Score.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
    Score.transform.position = scoreHomePos;
}
```

**ScoreSlamEnemy 改参数**（原 L612-644，把字段 `isPlay` 改成参数）：
```csharp
private IEnumerator ScoreSlamEnemy(int damage, bool isPlay)
{
    scoreHomePos = Score.transform.position;
    Vector3 enemyPos = EnemyPos.position;
    Vector3 playerPos = PlayerPos.position;
    Vector3 slamPos = isPlay
        ? playerPos + Vector3.up * 1f   // 弃牌：飞向玩家
        : enemyPos + Vector3.up * 1f;   // 出牌：飞向敌人
    // ... 原抛物线飞行 + PopOut 原样 ...
}
```
**注意**：原代码 L619 `if(!isPlay)` 飞向 enemyPos，`else` 飞向 playerPos。改参数后逻辑等价（`isPlay` true→playerPos，false→enemyPos），**效果不变**。

---

### 4.3 新建 `Assets/Scripts/Managers/EnemyController.cs`

**职责**：敌人手牌和 AI。当前敌人不反击，**只建骨架**，后期填 AI。

```csharp
public class EnemyController : Singleton<EnemyController>
{
    // 敌人手牌（后期接入）
    // private List<Card> enemyHand;

    // 敌人回合（后期填：位掩码枚举 C(7,k) 贪心最优选牌 → 调 BattleView.PlayResolveSequence 演出）
    public IEnumerator ResolveEnemyTurn()
    {
        // TODO: 后期实现敌人 AI
        Debug.Log("[敌人] 回合未接入，跳过");
        yield break;
    }

    // 跳过敌方攻击（道具占位，从 BattleManager L103-106 搬来）
    public void SkipEnemyAttack()
    {
        Debug.Log("[道具] 跳过敌方攻击（敌方回合未接入，暂无效果）");
    }
}
```

**说明**：`EnemyPos` 引用留在 BattleView（Inspector 拖到场景敌人物体）。后期动态生成敌人时，由 MapManager 把新敌人 transform 赋给 `BattleView.EnemyPos`，EnemyController 独立管理敌人逻辑，二者解耦。

---

### 4.4 瘦身 `Assets/Scripts/Managers/BattleManager.cs`

**保留字段**：
```csharp
public class BattleManager : Singleton<BattleManager>
{
    public TurnPhase CurrentPhase { get; private set; }
    public bool IsPlayerTurn { get; private set; } = true;
    public bool IsInBattle { get; private set; }

    [Header("每关次数")]
    public int playsPerLevel = 8;
    [Header("不勾选为call")]
    public bool isPlay = false;
    public int actionsLeft { get; private set; }

    [Header("护盾")]
    private int currentCheck = 0;   // 当前未使用，后期填

    private bool playRequested = false;
    private bool levelEnded = false;
    private bool resolving = false;
    private Coroutine battleRoutine;
}
```

**保留方法**：
| 方法 | 说明 |
|---|---|
| `Start()` | 调 `BattleView.Instance.Init()` + 订阅 `EndPlayPhaseEvent` / `LevelClearedEvent` |
| `OnDestroy()` | 取消订阅（原样） |
| `OnEndPlayPhase(e)` | `playRequested=true; isPlay=e.isPlay;`（原样） |
| `OnLevelCleared(e)` | `levelEnded=true;`（原样） |
| `SkipEnemyAttack()` | 转发 `EnemyController.Instance.SkipEnemyAttack()`（外部不用改） |
| `StartLevel(cfg)` | 原样 + `BattleView.Instance.ClearGhosts()` 替代原 `ClearGhosts()` |
| `GameLoop()` | 原样 |
| `SetPhase(phase)` | 原样 |
| `ResolvePlayerHand()` | **瘦身**，见下 |

**ResolvePlayerHand 瘦身后**（原 L350-433，搬走演出，保留编排+游戏逻辑）：
```csharp
private IEnumerator ResolvePlayerHand()
{
    List<Card> cards = BattleView.Instance.GetSortedSelectedCards();

    if (cards.Count == 0) { Debug.Log("[铃铛] 没选牌"); yield break; }
    if (actionsLeft <= 0) { Debug.Log("[铃铛] 次数用完"); yield break; }

    resolving = true;
    GameProgress.transitioning = true;

    // 1. 清选择 + 移手牌列表（牌对象存活能飞）
    BattleView.Instance.ClearSelection();
    foreach (Card c in cards)
        if (c != null) DeckManager.Instance.RemoveFromHand(c);

    // 2. 纯计算（PokerResolver）
    List<CardDataSO> datas = cards.ConvertAll(c => c.Data);
    HandType type = PokerResolver.Evaluate(datas);
    int baseChips = PokerResolver.GetBaseChips(type);
    int mult = PokerResolver.GetMultiplier(type);
    HashSet<int> coreIndices = PokerResolver.GetCoreCardIndices(datas, type);
    int cardBonus = PokerResolver.CalcCardBonus(cards, coreIndices);
    int damage = PokerResolver.CalcDamage(baseChips, cardBonus, mult);

    // 3. 纯演出（BattleView）— 飞牌+计分+飞撞+销毁牌
    yield return BattleView.Instance.PlayResolveSequence(
        cards, type, baseChips, mult, damage, coreIndices, isPlay);

    // 4. 游戏逻辑（原 L413，出牌打人；弃牌加盾后期填 currentCheck）
    if (!isPlay)
    {
        GameManager.Instance.EnemyLoseChips(damage);
    }
    // else { currentCheck += Mathf.RoundToInt(damage * 0.8f); }  // 后期填

    actionsLeft--;

    // 5. 补手牌
    if (DeckManager.Instance != null) yield return DeckManager.Instance.RefillHand();

    resolving = false;
    GameProgress.transitioning = false;
}
```

**注意**：原代码 L423 `yield return new WaitForSeconds(0.2f)` 已移入 `PlayResolveSequence` 步骤3，时序不变。

---

### 4.5 外部调用点修改

| 文件 | 行号 | 原 | 改 |
|---|---|---|---|
| `CardDisplay.cs` | 166, 175, 184 | `BattleManager.Instance.RefreshGhosts()` | `BattleView.Instance.RefreshGhosts()` |
| `MapManager.cs` | 171 | `BattleManager.Instance.StartLevel(cfg)` | 不变 |
| `SkipEnemyTurnEffectSO.cs` | 12 | `BattleManager.Instance.SkipEnemyAttack()` | 不变（BattleManager 转发） |

---

## Unity 场景操作

1. **选中 BattleManager 物体** → Add Component → `BattleView`（同物体挂两个组件）
2. 把原 BattleManager 上的字段值复制到 BattleView：
   - ghostAnchor / ghostSpacing / ghostZOffset / ghostY / ghostAlpha
   - realShadow / autoEnableLightShadow / ghostShadow / shadow* 字段
   - smokePrefab / smokeDestroyDelay
   - Score / EnemyPos / PlayerPos
3. **新建空 GameObject**（命名 `EnemyController`）→ Add Component → `EnemyController`
4. 把 `EnemyController` 物体的 transform 拖到 `BattleView.EnemyPos`（原 BattleManager.EnemyPos 指向的同一物体）
5. BattleManager 上搬走的字段可清空（演出字段已移到 BattleView）

---

## 验证步骤

1. **编译通过**：无报错（注意 `BattleView` / `EnemyController` 的 `Singleton` 基类）
2. **虚影**：点手牌，桌面出现半透明虚影（按点数→花色排序横排）—— 和之前一致
3. **出牌演出**（isPlay=false）：点铃铛 → 牌飞到虚影位+冒烟+虚影消失 → 逐张飘字+Score上涨 → 牌型+底分 → ×倍率 → 显示伤害 → Score飞撞敌人 → 敌人掉筹码 —— 和之前一致
4. **弃牌演出**（isPlay=true）：显示 ×0.8 → Check值 → Score飞向玩家 —— 和之前一致
5. **次数扣减**：每次出/弃 actionsLeft-- 正常
6. **补手牌**：演出后补到 7 张正常
7. **道具**：SkipEnemyTurn 效果正常（转发 EnemyController）

---

## 注意事项

- **效果不变**：所有动画参数（时长、缩放值、颜色、位置偏移）原样搬运，不调数值
- **isPlay 语义不变**：`true`=弃牌加盾，`false`=出牌打人（保持代码现状）
- **currentCheck 未实现**：字段保留在 BattleManager，护盾加成/衰减逻辑后期再填
- **敌人 AI 未实现**：EnemyController 只有骨架，后期填位掩码枚举贪心选牌
- **ShowHandBoard 扩展**：把原 L400-415 的最终伤害演出整合进来，接收 `isPlay` 参数，以便敌人回合后期复用同一套演出
- **ShrinkOutCards 依赖 DeckManager**：BattleView 调 `DeckManager.Instance.RemoveFromHand`（可接受，演出需要操作牌对象）
