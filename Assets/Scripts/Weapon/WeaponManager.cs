using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 武器管理器：挂在 Hero 上，管「拿刀/收刀/切换装备格」。
/// 3 个装备格固定：格0 = 近战刀类，格1 = 远程弓箭，格2 = 空着（以后用）。
/// 每个格装一把武器（WeaponData），切换格子时从数据实例化模型。
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("Hero 的动画器（拖 Hero 上的 Animator）")]
    public Animator animator;

    [Header("装备格0（近战刀类）")]
    public WeaponData slot0;

    [Header("装备格1（远程弓箭）")]
    public WeaponData slot1;

    [Header("装备格2（先空着）")]
    public WeaponData slot2;

    [Header("收纳节点（收刀挂哪）")]
    public Transform backNode;

    [Header("手上节点（拿刀挂哪）")]
    public Transform handNode;

    [Header("装备格 UI（3 个）")]
    public Image[] Slot;
    public Color normalColor = Color.white;     // 装备格平时颜色
    public Color highlightColor = Color.white;  // 装备格闪烁颜色

    [Header("当前用哪个装备格（0/1/2，按 1/2/3 切）")]
    public int currentSlot = 0;

    [Header("刀是否已拔出（true=能攻击）")]
    public bool isKnifeOut = false;

    [Header("武器伤害（挂在动画器所在模型物体上，不拖自动找）")]
    public WeaponDamage weaponDamage;

    [Header("装备格闪烁时长")]
    public float blinkDuration = 0.5f;

    private GameObject currentWeaponModel;   // 当前武器模型（从数据实例化）
    private WeaponData currentData;          // 当前武器数据
    private HeroController hero;             // 角色控制器（打断攻击动画前复位用）

    void Start()
    {
        // 动画器在子物体上，动画事件只会发给动画器所在的子物体，打不到父物体上的本脚本。
        // 所以在动画器所在物体上挂一个「转发器」，让 MoveToHand / MoveToBack 能传回来。
        if (animator != null)
        {
            AnimationEventRelay relay = animator.gameObject.GetComponent<AnimationEventRelay>();
            if (relay == null)
            {
                animator.gameObject.AddComponent<AnimationEventRelay>();
            }
        }

        // 武器伤害组件挂在动画器所在模型物体上（动画事件直接打到它），这里自动找一下
        if (weaponDamage == null && animator != null)
        {
            weaponDamage = animator.GetComponent<WeaponDamage>();
        }

        hero = GetComponent<HeroController>();   // 和 HeroController 挂同一物体（Hero 上）

        currentData = GetSlotData(currentSlot);
        SpawnWeaponModel();
        AttachModel(backNode);   // 一开始先收刀
        isKnifeOut = false;
        ApplyWeaponDamage();
    }

    // 拿第 slot 个槽位的武器数据
    public WeaponData GetSlotData(int slot)
    {
        if (slot == 0) return slot0;
        if (slot == 1) return slot1;
        return slot2;
    }

    // 给第 slot 个槽位装武器（背包装备时调用）
    public void SetSlotData(int slot, WeaponData data)
    {
        if (slot == 0) slot0 = data;
        else if (slot == 1) slot1 = data;
        else slot2 = data;
    }

    // 切换到第 slot 个槽位（按 1/2/3 或背包装备时调用）
    public void Equip(int slot)
    {
        if (slot < 0 || slot > 2) return;

        ResetAttackState();   // 换武器会打断攻击动画，先复位攻击状态，避免卡死

        currentSlot = slot;
        currentData = GetSlotData(slot);
        SpawnWeaponModel();       // 换成该槽位的武器模型
        AttachModel(backNode);    // 先收着，等拔刀动画再挂手上
        ApplyWeaponDamage();      // 更新伤害值

        // 告诉动画器现在用哪个装备格（Knife Type：0/1/2）
        if (animator != null) animator.SetInteger("Knife Type", slot);

        Blink(slot);
    }

    // 生成当前武器的模型：销毁旧的，从数据里的 weaponModel 实例化新的
    void SpawnWeaponModel()
    {
        if (currentWeaponModel != null)
        {
            Destroy(currentWeaponModel);
            currentWeaponModel = null;
        }

        if (currentData == null || currentData.weaponModel == null) return;

        currentWeaponModel = Instantiate(currentData.weaponModel);
        currentWeaponModel.name = currentData.weaponName;
    }

    // 把模型挂到某个节点下，套用武器数据里配的「挂载偏移」
    void AttachModel(Transform node)
    {
        if (currentWeaponModel == null || node == null) return;

        // 先记住模型实例化出来时的原始大小（prefab 里写死的缩放），后面缩放要乘它
        Vector3 originalScale = currentWeaponModel.transform.localScale;

        currentWeaponModel.transform.SetParent(node);

        if (currentData != null)
        {
            // 位置 = 挂载偏移（默认 0，等于以前归零）
            currentWeaponModel.transform.localPosition = currentData.mountOffset;
            // 旋转 = 挂载旋转（默认 0，正着拿）
            currentWeaponModel.transform.localRotation = Quaternion.Euler(currentData.mountRotation);
            // 缩放 = 原始大小 × 挂载缩放倍率（默认 1，保持原大小）
            currentWeaponModel.transform.localScale = originalScale * currentData.mountScale;
        }
        else
        {
            // 没有武器数据就退回老行为：位置、旋转归零
            currentWeaponModel.transform.localPosition = Vector3.zero;
            currentWeaponModel.transform.localRotation = Quaternion.identity;
        }
    }

    // 把当前武器的伤害值塞给武器伤害组件
    void ApplyWeaponDamage()
    {
        if (weaponDamage == null) return;
        if (currentData == null) return;
        weaponDamage.SetDamage(currentData.damage);
    }

    // 打断攻击动画前调用：把攻击状态复位，避免 isAttacking 卡死（换武器/收刀会打断攻击动画）
    void ResetAttackState()
    {
        if (hero == null) hero = GetComponent<HeroController>();
        if (hero != null && hero.isAttacking)
        {
            hero.EndAttack();   // EndAttack 会把 isAttacking 设回 false、关掉 Root Motion
        }
    }

    // 拿刀：触发拿刀动画
    public void DrawWeapon()
    {
        if (animator != null)
        {
            animator.SetBool("Hold Knife", true);
            animator.SetTrigger("IsKnife");
        }
        isKnifeOut = true;
    }

    // 收刀：触发收刀动画
    public void SheatheWeapon()
    {
        ResetAttackState();   // 收刀会打断攻击动画，先复位攻击状态，避免卡死

        if (animator != null)
        {
            animator.SetBool("Hold Knife", false);
            animator.SetTrigger("IsKnife");
        }
        isKnifeOut = false;
    }

    // 把武器挂到手上节点（拿刀动画事件调用）
    public void MoveToHand()
    {
        AttachModel(handNode);
    }

    // 把武器挂回收纳节点（收刀动画事件调用）
    public void MoveToBack()
    {
        AttachModel(backNode);
    }

    void Update()
    {
        // 只在第一人称能操作
        if (GameProgress.currentStage != GameStage.FirstPerson) return;

        // 按 1/2/3 切换装备格
        if (Input.GetKeyDown(KeyCode.Alpha1)) { if (isKnifeOut) { SheatheWeapon(); return; } Equip(0); DrawWeapon(); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { if (isKnifeOut) { SheatheWeapon(); return; } Equip(1); DrawWeapon(); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { if (isKnifeOut) { SheatheWeapon(); return; } Equip(2); DrawWeapon(); }

        // 按 F 收刀 / 拿刀
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (isKnifeOut) SheatheWeapon();
            else DrawWeapon();
        }
    }

    // 装备格闪烁
    public void Blink(int current)
    {
        StartCoroutine(BlinkRoutine(current));
    }

    IEnumerator BlinkRoutine(int current)
    {
        if (Slot == null || current < 0 || current >= Slot.Length) yield break;
        if (Slot[current] == null) yield break;

        Slot[current].color = highlightColor;   // 闪一下
        yield return new WaitForSeconds(blinkDuration);

        Slot[current].color = normalColor;      // 恢复
    }
}
