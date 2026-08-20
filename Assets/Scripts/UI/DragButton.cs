using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

/// <summary>
/// 可拖拽按钮：按住拖动跟随鼠标，松手时如果落在目标区域内就执行 onDrop，然后弹回原位。
/// 挂在主菜单每个按钮上。onDrop 在 Inspector 里拖 MainMenu 的对应方法（和 Button.onClick 一样拖）。
/// 注意：主菜单 Canvas 的 RenderMode 用 Screen Space - Overlay。
/// </summary>
public class DragButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("目标区域（把按钮拖到这就执行）")]
    public RectTransform target;

    [Header("拖到位后执行什么（拖 MainMenu 的方法进来）")]
    public UnityEvent onDrop;

    private RectTransform rect;
    private Vector2 homePos;   // 初始位置，松手弹回
    private Vector2 offset;    // 鼠标按在按钮上的位置，拖的时候不跳

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        homePos = rect.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        rect.SetAsLastSibling();                                 // 拖的时候盖在最上面
        offset = (Vector2)rect.position - eventData.position;    // 记下鼠标按在按钮哪个位置
    }

    public void OnDrag(PointerEventData eventData)
    {
        rect.position = eventData.position + offset;   // 跟随鼠标
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 松手：落在目标区域内就执行
        if (target != null && RectTransformUtility.RectangleContainsScreenPoint(target, eventData.position, eventData.pressEventCamera))
        {
            onDrop.Invoke();
        }

        rect.anchoredPosition = homePos;   // 弹回原位
    }
}
