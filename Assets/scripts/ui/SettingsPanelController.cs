using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent game settings (FOV, mouse sensitivity) backed by PlayerPrefs.
/// </summary>
public static class GameSettings
{
    private const string KeyFov = "Settings_CameraFov";
    private const string KeySens = "Settings_CameraSensitivity";
    private const string KeyZoomFov = "Settings_CameraZoomFov";

    public const float MinFov = 40f;
    public const float MaxFov = 85f;
    public const float DefaultFov = 60f;

    public const float MinSens = 15f;
    public const float MaxSens = 250f;
    public const float DefaultSens = 75f;

    public const float MinZoomFov = 14f;
    public const float MaxZoomFov = 35f;
    public const float DefaultZoomFov = 24f;

    public static event System.Action OnChanged;

    public static float Fov
    {
        get => PlayerPrefs.GetFloat(KeyFov, DefaultFov);
        set
        {
            float clamped = Mathf.Clamp(value, MinFov, MaxFov);
            if (!Mathf.Approximately(clamped, PlayerPrefs.GetFloat(KeyFov, DefaultFov)))
            {
                PlayerPrefs.SetFloat(KeyFov, clamped);
                OnChanged?.Invoke();
            }
        }
    }

    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat(KeySens, DefaultSens);
        set
        {
            float clamped = Mathf.Clamp(value, MinSens, MaxSens);
            if (!Mathf.Approximately(clamped, PlayerPrefs.GetFloat(KeySens, DefaultSens)))
            {
                PlayerPrefs.SetFloat(KeySens, clamped);
                OnChanged?.Invoke();
            }
        }
    }

    public static float ZoomFov
    {
        get => PlayerPrefs.GetFloat(KeyZoomFov, DefaultZoomFov);
        set
        {
            float clamped = Mathf.Clamp(value, MinZoomFov, MaxZoomFov);
            if (!Mathf.Approximately(clamped, PlayerPrefs.GetFloat(KeyZoomFov, DefaultZoomFov)))
            {
                PlayerPrefs.SetFloat(KeyZoomFov, clamped);
                OnChanged?.Invoke();
            }
        }
    }

    public static void ResetToDefaults()
    {
        PlayerPrefs.SetFloat(KeyFov, DefaultFov);
        PlayerPrefs.SetFloat(KeySens, DefaultSens);
        PlayerPrefs.SetFloat(KeyZoomFov, DefaultZoomFov);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}

/// <summary>
/// Modal settings dialog for camera field-of-view and sensitivity adjustments.
/// Styled like the main menu and Help dialog.
/// </summary>
public class SettingsPanelController : MonoBehaviour
{
    public static SettingsPanelController ActiveInstance { get; private set; }
    public static bool IsOpen => ActiveInstance != null && ActiveInstance._root != null && ActiveInstance._root.activeSelf;

    private GameObject _root;
    private Slider _fovSlider;
    private TextMeshProUGUI _fovValueTmp;
    private Slider _sensSlider;
    private TextMeshProUGUI _sensValueTmp;
    private Slider _zoomSlider;
    private TextMeshProUGUI _zoomValueTmp;

    private void Awake()
    {
        if (ActiveInstance != null && ActiveInstance != this)
        {
            Destroy(this);
            return;
        }
        ActiveInstance = this;
    }

    private void OnDestroy()
    {
        if (ActiveInstance == this)
            ActiveInstance = null;
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseActive();
        }
    }

    public static void OpenOrCreate()
    {
        if (ActiveInstance == null)
        {
            Canvas canvas = MainMenuUiFactory.FindOrCreateCanvas();
            ActiveInstance = canvas.GetComponent<SettingsPanelController>() ?? canvas.gameObject.AddComponent<SettingsPanelController>();
        }
        ActiveInstance.Show();
    }

    public static void CloseActive()
    {
        if (ActiveInstance != null)
            ActiveInstance.Hide();
    }

    public void Show()
    {
        EnsureBuilt();
        SyncFromSettings();
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);

        GameSettings.Save();

        // If not in a paused or modal state, lock cursor back for game navigation
        if (!ExitButton.IsPaused && !HelpPanelController.IsOpen && !LabTutorialController.IsModalOpen && !DevMenuController.IsOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SyncFromSettings()
    {
        if (_fovSlider != null)
        {
            _fovSlider.value = GameSettings.Fov;
            _fovValueTmp.text = $"{GameSettings.Fov:0}°";
        }
        if (_sensSlider != null)
        {
            _sensSlider.value = GameSettings.Sensitivity;
            _sensValueTmp.text = $"{GameSettings.Sensitivity:0}";
        }
        if (_zoomSlider != null)
        {
            _zoomSlider.value = GameSettings.ZoomFov;
            _zoomValueTmp.text = $"{GameSettings.ZoomFov:0}°";
        }
    }

    private void EnsureBuilt()
    {
        if (_root != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? MainMenuUiFactory.FindOrCreateCanvas();
        RectTransform scrim = MainMenuUiFactory.CreatePanel(canvas.transform, "SettingsPanelOverlay", MainMenuUiFactory.ScrimColor, false, true);
        scrim.anchorMin = Vector2.zero;
        scrim.anchorMax = Vector2.one;
        scrim.offsetMin = Vector2.zero;
        scrim.offsetMax = Vector2.zero;
        _root = scrim.gameObject;

        RectTransform card = MainMenuUiFactory.CreatePanel(scrim, "SettingsCard", MainMenuUiFactory.ModalCardColor, true, true);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(800f, 640f);
        card.anchoredPosition = Vector2.zero;

        var cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(32, 32, 24, 24);
        cardLayout.spacing = 14f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        // Header
        BuildHeader(card);
        MainMenuUiFactory.CreateDivider(card, 2f);

        // Sliders
        (_fovSlider, _fovValueTmp) = MainMenuUiFactory.CreateSliderRow(
            card, "FovRow", "Угол обзора камеры (FOV)",
            GameSettings.MinFov, GameSettings.MaxFov, 2f, GameSettings.Fov, "{0:0}°",
            val => GameSettings.Fov = val);

        (_sensSlider, _sensValueTmp) = MainMenuUiFactory.CreateSliderRow(
            card, "SensRow", "Чувствительность мыши (скорость камеры)",
            GameSettings.MinSens, GameSettings.MaxSens, 5f, GameSettings.Sensitivity, "{0:0}",
            val => GameSettings.Sensitivity = val);

        (_zoomSlider, _zoomValueTmp) = MainMenuUiFactory.CreateSliderRow(
            card, "ZoomRow", "Приближение лупы [ПКМ] (FOV)",
            GameSettings.MinZoomFov, GameSettings.MaxZoomFov, 1f, GameSettings.ZoomFov, "{0:0}°",
            val => GameSettings.ZoomFov = val);

        MainMenuUiFactory.CreateDivider(card, 1f);

        // Explanatory note
        RectTransform infoPanel = MainMenuUiFactory.CreatePanel(card, "InfoPanel", new Color(0f, 0f, 0f, 0.04f), false, false);
        var ipVl = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        ipVl.padding = new RectOffset(16, 16, 10, 10);
        ipVl.childControlWidth = true;
        ipVl.childControlHeight = true;
        ipVl.childForceExpandWidth = true;
        ipVl.childForceExpandHeight = false;

        string note =
            "• Высота камеры регулируется клавишами [+] и [−] (без Ctrl).\n" +
            "• В браузере комбинация Ctrl + +/- меняет масштаб страницы, поэтому параметры FOV и чувствительности настраиваются здесь.\n" +
            "• Быстрый доступ к настройкам: клавиша [O] или пункт «Настройки» в меню паузы [Esc].";

        MainMenuUiFactory.CreateText(
            infoPanel, "NoteText", note,
            16f, FontStyles.Normal, MainMenuUiFactory.TextSecondary, TextAlignmentOptions.TopLeft);

        MainMenuUiFactory.CreateDivider(card, 1f);

        // Footer buttons
        BuildFooter(card);

        _root.SetActive(false);
    }

    private void BuildHeader(RectTransform card)
    {
        var headerGo = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var headerRt = headerGo.GetComponent<RectTransform>();
        headerRt.SetParent(card, false);

        var hl = headerGo.GetComponent<HorizontalLayoutGroup>();
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        var headerLe = headerGo.AddComponent<LayoutElement>();
        headerLe.preferredHeight = 44f;

        var title = MainMenuUiFactory.CreateText(
            headerRt, "Title", "Настройки камеры и управления",
            26f, FontStyles.Bold, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.MidlineLeft);
        var titleLe = title.gameObject.AddComponent<LayoutElement>();
        titleLe.flexibleWidth = 1f;

        Button closeBtn = MainMenuUiFactory.CreateButton(
            headerRt, "CloseButton", "Закрыть", 20f,
            CloseActive, true, MainMenuUiFactory.TextPrimary);
        var closeLe = closeBtn.gameObject.AddComponent<LayoutElement>();
        closeLe.preferredWidth = 140f;
        closeLe.preferredHeight = 40f;
    }

    private void BuildFooter(RectTransform card)
    {
        var footerGo = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var footerRt = footerGo.GetComponent<RectTransform>();
        footerRt.SetParent(card, false);

        var hl = footerGo.GetComponent<HorizontalLayoutGroup>();
        hl.spacing = 16f;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = false;

        var footerLe = footerGo.AddComponent<LayoutElement>();
        footerLe.preferredHeight = 52f;

        Button resetBtn = MainMenuUiFactory.CreateButton(
            footerRt, "ResetButton", "Сбросить по умолчанию", 20f,
            OnResetClicked, true, MainMenuUiFactory.TextSecondary);

        Button saveBtn = MainMenuUiFactory.CreateButton(
            footerRt, "SaveButton", "Применить и закрыть", 20f,
            CloseActive, true, MainMenuUiFactory.AccentColor);
    }

    private void OnResetClicked()
    {
        GameSettings.ResetToDefaults();
        SyncFromSettings();
    }
}
