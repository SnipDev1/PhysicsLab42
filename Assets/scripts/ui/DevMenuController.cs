#if UNITY_EDITOR
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hidden developer menu toggled via [Ctrl + Alt + ]].
/// Active ONLY in Unity Editor; completely stripped out in builds.
/// </summary>
public class DevMenuController : MonoBehaviour
{
    private const string AutoConnectPrefKey = "Dev_AutoConnectOnStart";
    public static DevMenuController ActiveInstance { get; private set; }
    public static bool IsOpen => ActiveInstance != null && ActiveInstance._root != null && ActiveInstance._root.activeSelf;

    public static bool AutoConnectOnStart
    {
        get => PlayerPrefs.GetInt(AutoConnectPrefKey, 0) == 1;
        set => PlayerPrefs.SetInt(AutoConnectPrefKey, value ? 1 : 0);
    }

    private GameObject _root;
    private TextMeshProUGUI _autoStartLabelTmp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureDevMenuExists();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureDevMenuExists();
        if (AutoConnectOnStart && (scene.name == "SampleScene" || scene.name == "TutorialScene"))
        {
            var autoGo = new GameObject("DevAutoConnector");
            autoGo.AddComponent<DevAutoConnector>();
        }
    }

    public static void EnsureDevMenuExists()
    {
        if (ActiveInstance == null)
        {
            Canvas canvas = MainMenuUiFactory.FindOrCreateCanvas();
            ActiveInstance = canvas.GetComponent<DevMenuController>() ?? canvas.gameObject.AddComponent<DevMenuController>();
        }
    }

    public static void OpenOrCreate()
    {
        EnsureDevMenuExists();
        ActiveInstance.Show();
    }

    public static void CloseActive()
    {
        if (ActiveInstance != null)
            ActiveInstance.Hide();
    }

    public static void Toggle()
    {
        if (IsOpen)
            CloseActive();
        else
            OpenOrCreate();
    }

    void Awake()
    {
        if (ActiveInstance != null && ActiveInstance != this)
        {
            Destroy(this);
            return;
        }
        ActiveInstance = this;
    }

    void OnDestroy()
    {
        if (ActiveInstance == this)
            ActiveInstance = null;
    }

    void Update()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool bracket = Input.GetKeyDown(KeyCode.RightBracket);

        if (ctrl && alt && bracket)
        {
            Toggle();
            return;
        }

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
        }
    }

    public void Show()
    {
        EnsureBuilt();
        RefreshButtonLabels();
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);

        string activeScene = SceneManager.GetActiveScene().name;
        if ((activeScene == "SampleScene" || activeScene == "TutorialScene") &&
            !ExitButton.IsPaused && !HelpPanelController.IsOpen && !LabTutorialController.IsModalOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void EnsureBuilt()
    {
        if (_root != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? MainMenuUiFactory.FindOrCreateCanvas();
        RectTransform scrim = MainMenuUiFactory.CreatePanel(
            canvas.transform, "DevMenuOverlay", new Color(0.04f, 0.06f, 0.12f, 0.65f), false, true);
        scrim.anchorMin = Vector2.zero;
        scrim.anchorMax = Vector2.one;
        scrim.offsetMin = Vector2.zero;
        scrim.offsetMax = Vector2.zero;
        _root = scrim.gameObject;

        RectTransform card = MainMenuUiFactory.CreatePanel(scrim, "DevCard", MainMenuUiFactory.ModalCardColor, true, true);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(680f, 520f);
        card.anchoredPosition = Vector2.zero;

        var vl = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(36, 36, 28, 28);
        vl.spacing = 14f;
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        // Header
        MainMenuUiFactory.CreateText(
            card, "DevTitle",
            "DEV-MENU (Режим разработчика)",
            28f, FontStyles.Bold, MainMenuUiFactory.AccentColor, TextAlignmentOptions.Center);

        MainMenuUiFactory.CreateText(
            card, "DevSubtitle",
            "Горячая клавиша: [Ctrl + Alt + ]]",
            17f, FontStyles.Italic, MainMenuUiFactory.TextSecondary, TextAlignmentOptions.Center);

        MainMenuUiFactory.CreateDivider(card, 2f);

        // Buttons
        AddActionButton(card, "AutoConnectBtn", "Включить автосборку схемы (6 проводов)", MainMenuUiFactory.AccentColor, OnAutoConnectClicked);
        AddActionButton(card, "ClearWiresBtn", "Разобрать схему (снять все провода)", MainMenuUiFactory.DangerColor, OnClearWiresClicked);

        Button toggleBtn = AddActionButton(card, "ToggleAutoStartBtn", "", MainMenuUiFactory.TextPrimary, OnToggleAutoStartClicked);
        _autoStartLabelTmp = toggleBtn.GetComponentInChildren<TextMeshProUGUI>();

        MainMenuUiFactory.CreateDivider(card, 1f);

        AddActionButton(card, "SceneSwitchBtn", "Переключить сцену (Лабораторная / Меню)", MainMenuUiFactory.TextSecondary, OnSwitchSceneClicked);
        AddActionButton(card, "CloseBtn", "Закрыть [Esc]", MainMenuUiFactory.TextPrimary, Hide);

        _root.SetActive(false);
    }

    private Button AddActionButton(RectTransform parent, string name, string label, Color textColor, UnityEngine.Events.UnityAction action)
    {
        Button btn = MainMenuUiFactory.CreateButton(parent, name, label, 21f, action, true, textColor);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 52f;
        return btn;
    }

    private void RefreshButtonLabels()
    {
        if (_autoStartLabelTmp != null)
        {
            string state = AutoConnectOnStart ? "<color=#1F803D><b>ВКЛ</b></color>" : "<color=#D92626><b>ВЫКЛ</b></color>";
            _autoStartLabelTmp.text = $"Автосборка при старте сцены: {state}";
        }
    }

    private void OnAutoConnectClicked()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (LabSetupController.Instance != null)
        {
            LabSetupController.Instance.AutoConnectStandardCircuit();
            Hide();
        }
        else
        {
            AutoConnectOnStart = true;
            Hide();
            SceneManager.LoadScene("SampleScene");
        }
    }

    private void OnClearWiresClicked()
    {
        if (LabSetupController.Instance != null)
        {
            LabSetupController.Instance.ClearAllWires();
            Hide();
        }
    }

    private void OnToggleAutoStartClicked()
    {
        AutoConnectOnStart = !AutoConnectOnStart;
        RefreshButtonLabels();
    }

    private void OnSwitchSceneClicked()
    {
        Hide();
        string current = SceneManager.GetActiveScene().name;
        string target = current == "mainMenu" ? "SampleScene" : "mainMenu";
        SceneManager.LoadScene(target);
    }
}

/// <summary>
/// Helper component that waits for LabSetupController initialization on scene load to perform auto-connection.
/// </summary>
public class DevAutoConnector : MonoBehaviour
{
    private float _timer = 0f;

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (LabSetupController.Instance != null && LabSetupController.Instance.ArePortsInitialized)
        {
            LabSetupController.Instance.AutoConnectStandardCircuit();
            Destroy(gameObject);
            return;
        }

        if (_timer > 3f)
        {
            Destroy(gameObject);
        }
    }
}
#else
using UnityEngine;

/// <summary>
/// Empty stub for player builds (WebGL, Standalone, Mobile).
/// Excludes dev-menu completely from release builds.
/// </summary>
public class DevMenuController : MonoBehaviour
{
    public static bool IsOpen => false;
    public static void CloseActive() { }
}
#endif
