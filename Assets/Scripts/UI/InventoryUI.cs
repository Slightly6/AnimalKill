using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包界面：挂在 Canvas 下的背包面板（Panel）上。
/// 按 Tab 开关，打开时全显示拥有的武器（每把一个格子），点一把装到对应装备格。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("背包管理器（挂 Hero 上，拖进来）")]
    public InventoryManager inventoryManager;

    [Header("背包面板（本脚本挂的面板，默认隐藏）")]
    public GameObject panel;

    [Header("格子容器（放格子的空物体，加 GridLayoutGroup 自动排列）")]
    public Transform content;

    [Header("武器格子预制体（挂 WeaponSlot 的 Button 预制体）")]
    public GameObject slotPrefab;

    private bool isOpen = false;

    void Start()
    {
        if (panel == null) panel = gameObject;
        panel.SetActive(false);   // 一开始背包藏起来

        // 背包变化时刷新（比如 Boss 掉武器、换武器）
        EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
    }

    void Update()
    {
        // 按 Tab 开关背包（任何时候都能开，方便随时查看 / 换武器）
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Toggle();
        }
    }

    // 开 / 关背包
    void Toggle()
    {
        isOpen = !isOpen;
        panel.SetActive(isOpen);

        if (isOpen)
        {
            Refresh();   // 打开时重新生成格子

            // 打开背包：放出鼠标，让玩家能点武器
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 关背包：第一人称要锁回鼠标（转视角用），打牌阶段本来就显示鼠标，不用动
            if (GameProgress.currentStage == GameStage.FirstPerson)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    // 背包变化 → 如果开着就刷新
    void OnInventoryChanged(InventoryChangedEvent e)
    {
        if (isOpen) Refresh();
    }

    // 全显示：每把拥有的武器一个格子
    void Refresh()
    {
        if (inventoryManager == null || content == null || slotPrefab == null) return;

        // 清空旧格子
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        // 遍历拥有的武器，每把一个格子
        List<int> owned = inventoryManager.GetOwnedIndices();
        for (int i = 0; i < owned.Count; i++)
        {
            int index = owned[i];
            WeaponData data = inventoryManager.GetPoolWeapon(index);
            if (data == null) continue;

            GameObject go = Instantiate(slotPrefab, content);
            WeaponSlot slot = go.GetComponent<WeaponSlot>();
            if (slot != null)
            {
                slot.Setup(inventoryManager, data, index);
            }
        }
    }
}
