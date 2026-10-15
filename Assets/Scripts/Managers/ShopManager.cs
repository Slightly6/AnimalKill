using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : Singleton<ShopManager>
{
    public List<JokerSO> jokerPool = new List<JokerSO>();
    public List<CardDataSO> CardsPool = new List<CardDataSO>();
    public GameObject cardPrefab;
    public GameObject hookCardPrefab; 
    // public GameObject hookCardPrefab;
    public int maxShopSize = 3;
    public float spacing = 1.5f;
    public Transform Anchor; 
    public List<Transform> shopCards=new List<Transform>();

    private Coroutine cantAffordRoutine;   // "买不起"临时台词的倒计时
    private Coroutine hideCupRoutine;      // 收骰子的延迟协程
    private int currentHoverPrice = 0;     // 当前悬浮卡价；>0 表示骰子已经在帽子外飘着

    // 商店常驻播报：敌人报出玩家当前持有的骰子数（进商店、买完都要调）
    public void ShowDiceLine()
    {
        if (GameProgress.cheatMode)   // 开挂：骰子无限
        {
            Narrator.SayStickyText("开挂中：骰子无限");
            return;
        }
        Narrator.SayStickyText("你持有的骰子数量为 " + GameProgress.dice);
    }

    // 买不起：报出还差多少，3 秒后自动变回持有骰子数
    public void HintCantAfford(int shortfall)
    {
        Narrator.SayStickyText("你无法支付该卡的费用，还差 " + shortfall + " 个骰子");
        if (cantAffordRoutine != null) StopCoroutine(cantAffordRoutine);
        cantAffordRoutine = StartCoroutine(RevertToDiceLine());
    }

    IEnumerator RevertToDiceLine()
    {
        yield return new WaitForSecondsRealtime(3f);
        cantAffordRoutine = null;
        ShowDiceLine();
    }

    // 悬浮商店卡：魔术帽里飘出所需数量的骰子（数量 = 卡价）
    public void ShowDiceCup(int price)
    {
        currentHoverPrice = price;
        if (hideCupRoutine != null) { StopCoroutine(hideCupRoutine); hideCupRoutine = null; }
        if (TOPhat.Instance != null) TOPhat.Instance.ShowHoverDice(price);
    }

    // 移开卡：骰子缩回帽子里。延迟一帧再收 —— 从 A 卡换到 B 卡时 Unity 会先触发 Exit 再触发 Enter
    public void HideDiceCup()
    {
        currentHoverPrice = 0;
        if (hideCupRoutine != null) StopCoroutine(hideCupRoutine);
        hideCupRoutine = StartCoroutine(HideCupNextFrame());
    }

    IEnumerator HideCupNextFrame()
    {
        yield return null;                    // 等一帧，看有没有新卡接手
        hideCupRoutine = null;
        if (currentHoverPrice > 0) yield break;   // 已被新卡接管，不收
        if (TOPhat.Instance != null) TOPhat.Instance.HideHoverDice();
    }

    public void CardPoolInit()
    {
        
        Shuffle(CardsPool);
        int shopSize = Mathf.Min(maxShopSize, CardsPool.Count);
        
        GetPlayPositions(cardPrefab,shopSize,0);
        ShowDiceLine();   // 进商店先报一次持有骰子数
    }
    public void JokerPoolInit()
    {
        Shuffle(jokerPool);
        int count = Mathf.Min(maxShopSize, jokerPool.Count);
        GetPlayPositions(hookCardPrefab,count,2.5f);
    }

    // 买小丑：成功（栏没满）返回 true，并把实体移出商店列表（不再被结算飞走）
    public bool BuyJoker(HookCard card)
    {
        bool ok = JokerManager.Instance.Add(card.Data, card.transform);   // 飞行/挂载动画由 HookDisplay 接管
        if (ok) shopCards.Remove(card.transform);
        return ok;
    }
    private void GetPlayPositions(GameObject cardPrefabrd, int count,float yPos=0f)
    {
        Vector3 anchorPos = Vector3.zero;
        if (Anchor != null) anchorPos = Anchor.position;
        else if (BoardManager.Instance != null) anchorPos = BoardManager.Instance.transform.position;
        Quaternion rotation = Quaternion.Euler(90, 0, 0);
        Vector3 center = new Vector3(anchorPos.x, 0f, anchorPos.z);
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * spacing;
            GameObject cardObj = Instantiate(cardPrefabrd, center + new Vector3(x, 2f, yPos), rotation);
            if(cardPrefabrd==cardPrefab)
            {
                Card card = cardObj.GetComponent<Card>();
                card.Init(CardsPool[i], true); 
                card.transform.rotation = Quaternion.Euler(90f, 90f, -90f);
            }else
            {
                HookCard card = cardObj.GetComponent<HookCard>();
                card.Init(jokerPool[i]); 
                card.transform.rotation = Quaternion.Euler(90f, 90f, -90f);
            }
            
            shopCards.Add(cardObj.transform); 
        }
        
    }

    //洗牌
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
