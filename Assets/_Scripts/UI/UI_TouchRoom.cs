using UnityEngine;
using UnityEngine.UI;

public class UI_TouchRoom : MonoBehaviour
{
    public static UI_TouchRoom Instance { get; private set; }

    [Header("Root")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Container")]
    [Tooltip("用来挂载角色触摸Prefab的空节点，建议居中拉伸或放于偏右位置")]
    public Transform modelContainer; 

    private CharacterData currentTarget;
    private GameObject currentModelInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if(closeButton) closeButton.onClick.AddListener(CloseMenu);
    }

    public void OpenMenu(CharacterData target)
    {
        if (target == null || !target.canBeTouched || target.touchInteractionPrefab == null) 
        {
            Debug.LogWarning("该角色无法被触摸或缺少 TouchPrefab。");
            return;
        }

        currentTarget = target;
        panelRoot.SetActive(true);

        // 1. 清理上一位角色的残骸
        if (currentModelInstance != null) Destroy(currentModelInstance);

        // 2. 实例化新的互动模型
        currentModelInstance = Instantiate(target.touchInteractionPrefab, modelContainer);
        // 确保坐标归零，完美贴合你的 Container 设计
        currentModelInstance.transform.localPosition = Vector3.zero; 
        currentModelInstance.transform.localScale = Vector3.one;

        // 3. 遍历并激活所有智能热点 (这就是你组件化思维的胜利！)
        UI_TouchHotspot[] hotspots = currentModelInstance.GetComponentsInChildren<UI_TouchHotspot>(true);
        foreach (var spot in hotspots)
        {
            spot.Setup(target);
        }

        // 4. 开启对话管理器的沉浸模式
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.IsImmersiveMode = true;
        }
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
        if (currentModelInstance != null) Destroy(currentModelInstance);
        currentTarget = null;

        // 关闭沉浸模式
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.IsImmersiveMode = false;
        }

        // 重新唤醒主交互面板 (UI_Interaction)
        UI_Interaction interactionUI = FindObjectOfType<UI_Interaction>(true);
        if (interactionUI != null) interactionUI.panelRoot.SetActive(true);
    }
}