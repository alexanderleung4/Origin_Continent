using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_GiftMenu : MonoBehaviour
{
    public static UI_GiftMenu Instance { get; private set; }

    [Header("Root")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Right: Character Display")]
    public Image bigPortrait; // 放大贴脸的立绘
    public TextMeshProUGUI characterNameText;
    
    // 羁绊数值显示
    public Slider trustSlider;
    public TextMeshProUGUI trustText;
    public Slider intimacySlider;
    public TextMeshProUGUI intimacyText;
    public Slider dependencySlider;
    public TextMeshProUGUI dependencyText;

    [Header("Left: Gift Inventory")]
    public Transform giftListContainer; // 挂着 Grid/Vertical Layout Group 的节点
    public GameObject giftSlotPrefab;   // 复用你背包物品格的预制体即可
    
    [Header("Middle: Item Detail")]
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemDescText;
    public Button giveButton;

    private CharacterData currentTarget;
    private ItemData selectedGift;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if(closeButton) closeButton.onClick.AddListener(CloseMenu);
        if(giveButton) giveButton.onClick.AddListener(OnConfirmGive);
    }

    public void OpenMenu(CharacterData target)
    {
        currentTarget = target;
        panelRoot.SetActive(true);
        selectedGift = null;
        
        // 1. 设置贴脸大图
        if (bigPortrait != null && target.portrait != null) bigPortrait.sprite = target.portrait;
        if (characterNameText != null) characterNameText.text = target.characterName;

        // 2. 刷新进度条
        RefreshAffinityUI();

        // 3. 清空描述面板
        ClearItemDetail();

        // 4. 从全局背包捞取可赠送物品
        PopulateGiftList();
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
        // 重新唤醒那个小的主交互面板
        UI_Interaction interactionUI = FindObjectOfType<UI_Interaction>(true);
        if (interactionUI != null) interactionUI.panelRoot.SetActive(true);
    }

    private void RefreshAffinityUI()
    {
        if (AffinityManager.Instance == null || currentTarget == null) return;
        
        string id = currentTarget.characterID;
        int t = AffinityManager.Instance.GetAffinity(id, AffinityType.Trust);
        int i = AffinityManager.Instance.GetAffinity(id, AffinityType.Intimacy);
        int d = AffinityManager.Instance.GetAffinity(id, AffinityType.Dependency);

        // 假设当前阶段进度条上限都是 100
        if (trustSlider) trustSlider.value = t / 100f;
        if (trustText) trustText.text = $"信任值: {t}/100";
        
        if (intimacySlider) intimacySlider.value = i / 100f;
        if (intimacyText) intimacyText.text = $"亲密值: {i}/100";

        if (dependencySlider) dependencySlider.value = d / 100f;
        if (dependencyText) dependencyText.text = $"依赖值: {d}/100";
    }

    private void PopulateGiftList()
    {
        foreach (Transform child in giftListContainer) Destroy(child.gameObject);

        if (InventoryManager.Instance == null) return;

        foreach (var slot in InventoryManager.Instance.inventory)
        {
            // 核心过滤：只有打勾了 isGiftable 且数量大于 0 的才显示
            if (slot.itemData != null && slot.itemData.isGiftable && slot.amount > 0)
            {
                GameObject obj = Instantiate(giftSlotPrefab, giftListContainer);
                
                Transform iconTrans = obj.transform.Find("Icon");
                if (iconTrans != null) iconTrans.GetComponent<Image>().sprite = slot.itemData.icon;

                Transform amountTrans = obj.transform.Find("Amount"); 
                if (amountTrans != null) amountTrans.GetComponent<TextMeshProUGUI>().text = slot.amount.ToString();
                
                // 绑定点击事件
                ItemData cachedData = slot.itemData;
                obj.GetComponent<Button>().onClick.AddListener(() => OnGiftSelected(cachedData));
            }
        }
    }

    private void OnGiftSelected(ItemData item)
    {
        selectedGift = item;
        if (itemNameText) itemNameText.text = item.itemName;
        if (itemDescText) itemDescText.text = item.description;
        if (giveButton) giveButton.interactable = true;
    }

    private void ClearItemDetail()
    {
        if (itemNameText) itemNameText.text = "选择要赠送的礼物";
        if (itemDescText) itemDescText.text = "";
        if (giveButton) giveButton.interactable = false;
    }

    private void OnConfirmGive()
    {
        if (selectedGift == null || currentTarget == null) return;

        // 1. 扣除行动点
        if (!AffinityManager.Instance.ConsumeInteractionPoint())
        {
            if (UI_SystemToast.Instance) UI_SystemToast.Instance.Show("No_AP", "精力耗尽！", 0, null);
            return;
        }

        // 2. 扣除背包物品
        InventoryManager.Instance.RemoveItem(selectedGift, 1);

        // 3. 增加对应属性
        AffinityManager.Instance.AddAffinity(currentTarget.characterID, selectedGift.giftAffinityType, selectedGift.giftAffinityValue);

        // 4. 演出反馈 (爆表/跳字/刷新UI)
        if (UI_SystemToast.Instance) 
            UI_SystemToast.Instance.Show("Gift", $"{currentTarget.characterName} 的 {selectedGift.giftAffinityType} 提升了 {selectedGift.giftAffinityValue}!", 0, null);

        // 刷新自己
        RefreshAffinityUI();
        PopulateGiftList(); 
        ClearItemDetail();
    }
}