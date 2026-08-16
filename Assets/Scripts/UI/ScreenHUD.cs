using UnityEngine;

/// <summary>
/// 屏幕 HUD：动态生成牌堆和铃铛，固定在屏幕角落（相机相对，像手牌那样始终面向相机）。
/// 挂在 Manager 上，Start 生成，LateUpdate 每帧锚定到相机。
/// </summary>
public class ScreenHUD : MonoBehaviour
{
    [Header("预制体（拖入）")]
    public GameObject deckPilePrefab;   // 牌堆
    public GameObject bellPrefab;       // 铃铛

    [Header("屏幕位置（viewport 0~1，左下角是 0,0）")]
    public Vector2 deckPileAnchor = new Vector2(0.88f, 0.15f);   // 牌堆：右下
    public Vector2 bellAnchor = new Vector2(0.12f, 0.15f);       // 铃铛：左下

    [Header("设置")]
    public float hudDistance = 9f;      // 离相机距离（和手牌 handDist 一致）
    public float hudScale = 1f;         // 大小缩放
    public int hudSortOrder = 20;       // 渲染顺序（盖在手牌上面）

    private Transform deckPile;
    private Transform bell;

    void Start()
    {
        deckPile = SpawnHud(deckPilePrefab, "DeckPile");
        bell = SpawnHud(bellPrefab, "Bell");

        // 把牌堆位置告诉 DeckManager（抽牌时卡从这里出生）
        if (deckPile != null && DeckManager.Instance != null)
            DeckManager.Instance.deckPile = deckPile;
    }

    void LateUpdate()
    {
        if (Camera.main == null) return;
        Camera cam = Camera.main;

        if (deckPile != null) AnchorToViewport(deckPile, deckPileAnchor, cam);
        if (bell != null) AnchorToViewport(bell, bellAnchor, cam);
    }

    // 实例化一个 HUD 对象，挂在 ScreenHUD 下面
    Transform SpawnHud(GameObject prefab, string name)
    {
        if (prefab == null)
        {
            Debug.LogWarning("ScreenHUD 缺预制体：" + name);
            return null;
        }

        GameObject go = Instantiate(prefab, transform);
        go.name = name;
        go.transform.localScale = Vector3.one * hudScale;

        // 提到手牌上面，保证看得见
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = hudSortOrder;

        return go.transform;
    }

    // 把对象锚定到屏幕 viewport 位置，面向相机
    void AnchorToViewport(Transform t, Vector2 viewport, Camera cam)
    {
        Vector3 pos = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, hudDistance));
        t.position = pos;
        t.rotation = cam.transform.rotation;   // 面向相机
    }
}
