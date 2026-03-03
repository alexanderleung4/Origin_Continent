using UnityEngine;
using UnityEngine.UI;

public class UI_Formation : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject panelRoot;
    public Button closeButton;
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
            
            // 👇 核心修复：定义真正的“空位”(过滤掉Unity自动生成的僵尸对象)
            bool isEmptySlot = (member == null || member.data == null);

            UI_RosterAvatar avatarUI = formationSlots[i];
            Button btn = avatarUI.GetComponent<Button>();
            int index = i; 
            
            if (!isEmptySlot)
            {
                avatarUI.gameObject.SetActive(true);
                avatarUI.Setup(member, AvatarDisplayMode.Minimal, null); 
                if (avatarUI.portraitImage != null) avatarUI.portraitImage.color = Color.white;
            }
            else
            {
                if (avatarUI.portraitImage != null) { avatarUI.portraitImage.sprite = null; avatarUI.portraitImage.color = new Color(1, 1, 1, 0.1f); }
                if (avatarUI.nameContainer != null) avatarUI.nameContainer.SetActive(false);
                if (avatarUI.statsRoot != null) avatarUI.statsRoot.SetActive(false);
            }

            // 无论有没有人，格子必须能点
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnFormationSlotClicked(index));
            }

            Image img = avatarUI.GetComponent<Image>();
            if (img != null) img.color = (i == selectedSlotIndex) ? Color.yellow : Color.white;
        }

        // 2. 刷新右侧板凳席
        foreach (Transform child in reserveContainer) Destroy(child.gameObject);
        
        foreach (var member in GameManager.Instance.reserveParty)
        {
            if (member == null || member.data == null) continue; // 双重保险
            
            GameObject go = Instantiate(reserveAvatarPrefab, reserveContainer);
            UI_RosterAvatar avatarUI = go.GetComponent<UI_RosterAvatar>();
            if (avatarUI != null)
            {
                avatarUI.Setup(member, AvatarDisplayMode.NameOnly, OnReserveMemberClicked);
            }
        }

        // 3. 动态更新“下阵”按钮的可点击状态
        if (btnRemove != null)
        {
            bool canRemove = false;
            if (selectedSlotIndex != -1)
            {
                RuntimeCharacter selectedChar = GameManager.Instance.activeFormation[selectedSlotIndex];
                // 👇 加入 data != null 的防报错检查
                if (selectedChar != null && selectedChar.data != null)
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

    private void OnFormationSlotClicked(int clickedIndex)
    {
        if (selectedSlotIndex == -1)
        {
            selectedSlotIndex = clickedIndex;
            RefreshUI();
        }
        else if (selectedSlotIndex == clickedIndex)
        {
            selectedSlotIndex = -1;
            RefreshUI();
        }
        else
        {
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
        bool isBoardEmpty = (memberOnBoard == null || memberOnBoard.data == null);

        // 如果要替换的位置上有主角，拦截！
        if (!isBoardEmpty && GameManager.Instance.playerTemplate != null && memberOnBoard.data.characterID == GameManager.Instance.playerTemplate.characterID)
        {
            if (UI_SystemToast.Instance != null) UI_SystemToast.Instance.Show("Sys", "主角无法被替换！", 0, null);
            return;
        }

        // 换人
        GameManager.Instance.reserveParty.Remove(reserveMember);
        if (!isBoardEmpty) GameManager.Instance.reserveParty.Add(memberOnBoard);
        
        GameManager.Instance.activeFormation[selectedSlotIndex] = reserveMember;

        selectedSlotIndex = -1;
        RefreshUI();
    }

    private void OnRemoveClicked()
    {
        if (selectedSlotIndex == -1) return;
        RuntimeCharacter member = GameManager.Instance.activeFormation[selectedSlotIndex];
        
        // 👇 同样加入 data 的防御
        if (member == null || member.data == null) return; 
        
        if (GameManager.Instance.playerTemplate != null && member.data.characterID == GameManager.Instance.playerTemplate.characterID) return;

        // 由于 Unity 会自动塞空类，下阵时我们不能赋值 null，而是赋一个新的“空肉身”覆盖掉它
        GameManager.Instance.activeFormation[selectedSlotIndex] = new RuntimeCharacter(null);
        GameManager.Instance.reserveParty.Add(member);
        
        selectedSlotIndex = -1; 
        RefreshUI(); 
    }

    private void SwapFormationSlots(int indexA, int indexB)
    {
        if (indexA == indexB) return;
        RuntimeCharacter temp = GameManager.Instance.activeFormation[indexA];
        GameManager.Instance.activeFormation[indexA] = GameManager.Instance.activeFormation[indexB];
        GameManager.Instance.activeFormation[indexB] = temp;
    }
}