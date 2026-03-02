using UnityEngine;
using UnityEngine.UI;

public class UI_Formation : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject panelRoot;
    public Button closeButton;
    
    // 👇 新增：下阵按钮
    public Button btnRemove; 

    [Header("战术沙盘 (左侧 6 宫格)")]
    public UI_RosterAvatar[] formationSlots = new UI_RosterAvatar[6];
    
    [Header("待命席 (右侧列表)")]
    public Transform reserveContainer;
    public GameObject reserveAvatarPrefab; 

    private int selectedSlotIndex = -1; 

    private void Start()
    {
        ClosePanel();
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (btnRemove != null) btnRemove.onClick.AddListener(OnRemoveClicked);
        
        // 注意：我们不再在 Start 里绑定沙盘点击事件，全部移到 RefreshUI 里防止被清洗！
    }

    public void OpenPanel()
    {
        if (UIManager.Instance != null) UIManager.Instance.OnOpenPanel();
        panelRoot.SetActive(true);
        selectedSlotIndex = -1; 
        RefreshUI();
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
    }

    private void RefreshUI()
    {
        // 1. 刷新左侧 6 宫格
        for (int i = 0; i < 6; i++)
        {
            RuntimeCharacter member = GameManager.Instance.activeFormation[i];
            UI_RosterAvatar avatarUI = formationSlots[i];
            Button btn = avatarUI.GetComponent<Button>();
            
            int index = i; // 闭包陷阱防御
            
            if (member != null)
            {
                avatarUI.gameObject.SetActive(true);
                // 这里会执行 RemoveAllListeners
                avatarUI.Setup(member, AvatarDisplayMode.Minimal, null); 
                if (avatarUI.portraitImage != null) avatarUI.portraitImage.color = Color.white;
            }
            else
            {
                if (avatarUI.portraitImage != null) { avatarUI.portraitImage.sprite = null; avatarUI.portraitImage.color = new Color(1, 1, 1, 0.1f); }
                if (avatarUI.nameContainer != null) avatarUI.nameContainer.SetActive(false);
                if (avatarUI.statsRoot != null) avatarUI.statsRoot.SetActive(false);
            }

            // 👇 核心修复：不管 Setup 怎么清洗，我们在最后强行重新绑定“换位监听器”！
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnFormationSlotClicked(index));

            // 高亮选中状态
            Image img = avatarUI.GetComponent<Image>();
            if (img != null) img.color = (i == selectedSlotIndex) ? Color.yellow : Color.white;
        }

        // 2. 刷新右侧板凳席
        foreach (Transform child in reserveContainer) Destroy(child.gameObject);
        
        foreach (var member in GameManager.Instance.reserveParty)
        {
            GameObject go = Instantiate(reserveAvatarPrefab, reserveContainer);
            UI_RosterAvatar avatarUI = go.GetComponent<UI_RosterAvatar>();
            if (avatarUI != null)
            {
                avatarUI.Setup(member, AvatarDisplayMode.NameOnly, OnReserveMemberClicked);
            }
        }

        // 3. 👇 新增：动态更新“下阵”按钮的可点击状态
        if (btnRemove != null)
        {
            bool canRemove = false;
            // 只有选中了格子，格子里有人，且这个人不是主角时，才能下阵！
            if (selectedSlotIndex != -1)
            {
                RuntimeCharacter selectedChar = GameManager.Instance.activeFormation[selectedSlotIndex];
                if (selectedChar != null)
                {
                    if (GameManager.Instance.playerTemplate == null || selectedChar.data.characterID != GameManager.Instance.playerTemplate.characterID)
                    {
                        canRemove = true;
                    }
                }
            }
            btnRemove.interactable = canRemove;
        }
    }

    // --- 核心交互法则 ---
    
    private void OnFormationSlotClicked(int clickedIndex)
    {
        if (selectedSlotIndex == -1)
        {
            // 第一次点击：选中
            selectedSlotIndex = clickedIndex;
            RefreshUI();
        }
        else if (selectedSlotIndex == clickedIndex)
        {
            // 点击同一个格子：取消选中
            selectedSlotIndex = -1;
            RefreshUI();
        }
        else
        {
            // 选中 A，再点 B：两人互换位置！
            SwapFormationSlots(selectedSlotIndex, clickedIndex);
            selectedSlotIndex = -1;
            RefreshUI();
        }
    }

    private void OnReserveMemberClicked(RuntimeCharacter reserveMember)
    {
        if (selectedSlotIndex == -1)
        {
            if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show("Sys", "请先在左侧选择一个上阵位置！", 0, null);
            return;
        }

        RuntimeCharacter memberOnBoard = GameManager.Instance.activeFormation[selectedSlotIndex];

        // 🛑 防线：试图用替补替换主角，直接拦截！
        if (memberOnBoard != null && memberOnBoard.data.characterID == GameManager.Instance.playerTemplate.characterID)
        {
            if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show("Sys", "主角无法被替换！", 0, null);
            return;
        }

        // 换人：场上的人去板凳，板凳的人上场
        GameManager.Instance.reserveParty.Remove(reserveMember);
        if (memberOnBoard != null) GameManager.Instance.reserveParty.Add(memberOnBoard);
        
        GameManager.Instance.activeFormation[selectedSlotIndex] = reserveMember;

        selectedSlotIndex = -1;
        RefreshUI();
    }

    // 👇 新增：下阵功能执行逻辑
    private void OnRemoveClicked()
    {
        if (selectedSlotIndex == -1) return;
        RuntimeCharacter member = GameManager.Instance.activeFormation[selectedSlotIndex];
        if (member == null) return;
        
        // 终极物理防线
        if (GameManager.Instance.playerTemplate != null && member.data.characterID == GameManager.Instance.playerTemplate.characterID) return;

        // 拔出萝卜，扔回后备箱
        GameManager.Instance.activeFormation[selectedSlotIndex] = null;
        GameManager.Instance.reserveParty.Add(member);
        
        selectedSlotIndex = -1; // 清空选择
        RefreshUI(); // 刷新画面
    }

    private void SwapFormationSlots(int indexA, int indexB)
    {
        if (indexA == indexB) return;
        RuntimeCharacter temp = GameManager.Instance.activeFormation[indexA];
        GameManager.Instance.activeFormation[indexA] = GameManager.Instance.activeFormation[indexB];
        GameManager.Instance.activeFormation[indexB] = temp;
    }
}