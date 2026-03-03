using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_Blacksmith : MonoBehaviour
{
    public static UI_Blacksmith Instance { get; private set; }

    [Header("数据库 (Database)")]
    public List<RecipeData> allRecipes; // 在 Inspector 里把您做好的所有图纸拖进来

    [Header("全局 UI 引用 (Global UI)")]
    public GameObject panelRoot;
    public Button closeButton;
    public TextMeshProUGUI playerGoldText; // 显示玩家当前金币

    [Header("左侧：配方列表 (Left: Recipe List)")]
    public Transform recipeListContainer;
    public GameObject recipeSlotPrefab;

    [Header("右侧：产出预览 (Right: Output Preview)")]
    public GameObject previewPanel; // 右侧整体面板（没选配方时隐藏）
    public Image outputIcon;
    public TextMeshProUGUI outputName;
    public TextMeshProUGUI outputDesc;
    public TextMeshProUGUI outputBaseStats;

    [Header("右侧：锻造消耗 (Right: Cost & Ingredients)")]
    public TextMeshProUGUI costText;
    public Transform ingredientListContainer;
    public GameObject ingredientSlotPrefab;
    public Button craftButton;

    private RecipeData currentRecipe;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        ClosePanel();
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (craftButton != null) craftButton.onClick.AddListener(OnCraftClicked);
    }

    // --- 调试热键 (按 B 打开铁匠铺) ---
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (panelRoot.activeSelf) ClosePanel();
            else OpenPanel();
        }
    }

    public void OpenPanel()
    {
        if (UIManager.Instance != null) UIManager.Instance.OnOpenPanel();
        panelRoot.SetActive(true);
        currentRecipe = null;
        if (previewPanel != null) previewPanel.SetActive(false); // 刚打开时右侧为空

        RefreshRecipeList();
        UpdatePlayerGold();
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
    }

    private void UpdatePlayerGold()
    {
        if (playerGoldText != null && GameManager.Instance != null)
        {
            playerGoldText.text = $"拥有金币: {GameManager.Instance.Player.Gold}";
        }
    }

    // 1. 刷新左侧图纸列表
    private void RefreshRecipeList()
    {
        foreach (Transform child in recipeListContainer) Destroy(child.gameObject);

        foreach (var recipe in allRecipes)
        {
            if (recipe == null) continue;

            // 过滤未解锁的配方
            if (!recipe.isUnlockedByDefault) continue; 

            GameObject go = Instantiate(recipeSlotPrefab, recipeListContainer);
            UI_RecipeSlot slotUI = go.GetComponent<UI_RecipeSlot>();
            
            if (slotUI == null) slotUI = go.AddComponent<UI_RecipeSlot>();
            slotUI.Setup(recipe, SelectRecipe);
        }
    }

    // 2. 点击左侧图纸，刷新右侧详情
    public void SelectRecipe(RecipeData recipe)
    {
        currentRecipe = recipe;
        if (previewPanel != null) previewPanel.SetActive(true);

        // A. 刷新产出物基本信息 (白值展示)
        EquipmentData equip = recipe.outputEquipment;
        if (outputIcon != null) outputIcon.sprite = equip.icon;
        if (outputName != null) outputName.text = equip.itemName;
        if (outputDesc != null) outputDesc.text = equip.description;

        if (outputBaseStats != null)
        {
            string stats = "";
            if (equip.baseDamage > 0) stats += $"基础攻击: {equip.baseDamage}\n";
            if (equip.baseDefense > 0) stats += $"基础防御: {equip.baseDefense}\n";
            if (equip.baseMaxHP > 0) stats += $"基础生命: {equip.baseMaxHP}\n";
            stats += "<color=#FFD700>锻造时将赋予随机词条与品质！</color>";
            outputBaseStats.text = stats;
        }

        RefreshIngredients();
    }

    // 3. 刷新材料消耗与按钮状态
    private void RefreshIngredients()
    {
        if (currentRecipe == null) return;
        UpdatePlayerGold();

        bool canCraft = true;

        // 检查金币
        if (costText != null)
        {
            bool hasEnoughGold = GameManager.Instance.Player.Gold >= currentRecipe.craftingCost;
            string color = hasEnoughGold ? "#FFFFFF" : "#FF0000";
            costText.text = $"锻造费用: <color={color}>{currentRecipe.craftingCost}</color> 金币";
            if (!hasEnoughGold) canCraft = false;
        }

        // 检查并生成材料列表
        foreach (Transform child in ingredientListContainer) Destroy(child.gameObject);

        foreach (var ing in currentRecipe.ingredients)
        {
            GameObject go = Instantiate(ingredientSlotPrefab, ingredientListContainer);
            UI_IngredientSlot slotUI = go.GetComponent<UI_IngredientSlot>();
            if (slotUI == null) slotUI = go.AddComponent<UI_IngredientSlot>();

            // 统计背包里有多少个这个材料
            int haveAmount = 0;
            foreach (var invSlot in InventoryManager.Instance.inventory)
            {
                if (invSlot.itemData == ing.item) haveAmount += invSlot.amount;
            }

            bool hasEnough = haveAmount >= ing.amount;
            if (!hasEnough) canCraft = false;

            slotUI.Setup(ing.item, haveAmount, ing.amount);
        }

        // 决定大锤按钮是否亮起
        if (craftButton != null) craftButton.interactable = canCraft;
    }

    // 4. 真正执行锻造！
    private void OnCraftClicked()
    {
        if (currentRecipe == null || ForgeManager.Instance == null) return;

        bool success = ForgeManager.Instance.TryCraftEquipment(currentRecipe);
        
        if (success)
        {
            // 锻造成功后，刷新材料数量和金币显示！
            RefreshIngredients();
        }
    }
}
