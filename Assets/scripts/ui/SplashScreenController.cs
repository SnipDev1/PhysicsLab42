using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen startup splash overlay shown once per session in mainMenu,
/// matching the project's translucent card design and embedding the official NSTU logo
/// per NSTU Brandbook v3.0 (09.25).
/// </summary>
public class SplashScreenController : MonoBehaviour
{
    private const string MainMenuSceneName = "mainMenu";
    private const string LogoResourcePath = "NSTU_Logo";
    private const float LogoAspectRatio = 1972f / 803f;
    private const float LogoHeightPx = 76f;

    public float autoTransitionTime = 10f;
    public float fadeDuration = 0.35f;

    public static bool IsVisible { get; private set; }

    private static bool _hasShownThisSession;

    private GameObject _overlayRoot;
    private CanvasGroup _canvasGroup;
    private TextMeshProUGUI _promptTmp;
    private float _elapsed;
    private float _fadeElapsed;
    private bool _isFadingOut;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitOnMainMenuLoad()
    {
        if (_hasShownThisSession) return;
        if (SceneManager.GetActiveScene().name != MainMenuSceneName) return;

        _hasShownThisSession = true;
        Canvas canvas = MainMenuUiFactory.FindOrCreateCanvas();
        if (canvas.GetComponent<SplashScreenController>() == null)
            canvas.gameObject.AddComponent<SplashScreenController>();
    }

    public static void ResetSessionFlag()
    {
        _hasShownThisSession = false;
    }

    public void Start()
    {
        SelfCheck();
        BuildSplashUi();
    }

    public void Update()
    {
        if (!IsVisible || _overlayRoot == null) return;

        if (_isFadingOut)
        {
            _fadeElapsed += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(_fadeElapsed / fadeDuration) : 1f;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f - t;

            if (t >= 1f)
            {
                IsVisible = false;
                Destroy(_overlayRoot);
                _overlayRoot = null;
                Destroy(this);
            }
            return;
        }

        _elapsed += Time.unscaledDeltaTime;

        if (_promptTmp != null)
        {
            Color c = MainMenuUiFactory.AccentColor;
            c.a = 0.70f + 0.30f * (0.5f + 0.5f * Mathf.Sin(_elapsed * 3.5f));
            _promptTmp.color = c;
        }

        // Ignore the very first frame click/key residue on scene startup
        if (_elapsed > 0.25f && (Input.anyKeyDown || _elapsed >= autoTransitionTime))
            Dismiss();
    }

    public void Dismiss()
    {
        if (!IsVisible || _isFadingOut) return;
        _isFadingOut = true;
        _fadeElapsed = 0f;
        if (_canvasGroup != null)
            _canvasGroup.blocksRaycasts = false;
    }

    void OnDestroy()
    {
        if (_overlayRoot != null)
        {
            Destroy(_overlayRoot);
            _overlayRoot = null;
        }
        IsVisible = false;
    }

    private void BuildSplashUi()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? MainMenuUiFactory.FindOrCreateCanvas();

        RectTransform scrim = MainMenuUiFactory.CreatePanel(
            canvas.transform, "SplashScreenOverlay", new Color(0.05f, 0.08f, 0.14f, 0.42f), false, true);
        scrim.anchorMin = Vector2.zero;
        scrim.anchorMax = Vector2.one;
        scrim.offsetMin = Vector2.zero;
        scrim.offsetMax = Vector2.zero;
        scrim.SetAsLastSibling();

        _overlayRoot = scrim.gameObject;
        _canvasGroup = _overlayRoot.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        IsVisible = true;

        // Pure #FFFFFF card complies with NSTU Brandbook v3.0 p. 21 for the full-color logo
        RectTransform card = MainMenuUiFactory.CreatePanel(scrim, "SplashCard", Color.white, true, true);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(1340f, 760f);
        card.anchoredPosition = Vector2.zero;

        var vl = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(88, 88, 64, 52);
        vl.spacing = 0f;
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        BuildLogoSection(card);
        MainMenuUiFactory.CreateDivider(card, 2f);
        AddSpacer(card, 28f);

        MainMenuUiFactory.CreateText(
            card, "SplashTitle",
            "ВИРТУАЛЬНАЯ ЛАБОРАТОРНАЯ РАБОТА",
            42f, FontStyles.Bold, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.Center);

        AddSpacer(card, 12f);

        MainMenuUiFactory.CreateText(
            card, "SplashSubtitle",
            "Лабораторная работа № 42. Исследование свойств фоторезистора",
            27f, FontStyles.Bold, MainMenuUiFactory.AccentColor, TextAlignmentOptions.Center);

        AddSpacer(card, 24f);
        BuildAuthorsSection(card);
        AddSpacer(card, 24f);
        MainMenuUiFactory.CreateDivider(card, 2f);

        var flexSpacer = new GameObject("FlexSpacer", typeof(RectTransform), typeof(LayoutElement));
        flexSpacer.GetComponent<RectTransform>().SetParent(card, false);
        flexSpacer.GetComponent<LayoutElement>().flexibleHeight = 1f;

        _promptTmp = MainMenuUiFactory.CreateText(
            card, "ContinuePrompt",
            "Нажмите любую клавишу или кликните мышью для продолжения...",
            24f, FontStyles.Bold, MainMenuUiFactory.AccentColor, TextAlignmentOptions.Center);
    }

    private static void BuildLogoSection(RectTransform card)
    {
        // Brandbook v3.0 p. 20: preserve clear space around the 76 px square mark (top 64+12=76 px, bottom 60 px)
        var zone = new GameObject("LogoClearSpaceZone", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        var zoneRt = zone.GetComponent<RectTransform>();
        zoneRt.SetParent(card, false);

        var zoneLe = zone.GetComponent<LayoutElement>();
        zoneLe.minHeight = 148f;
        zoneLe.preferredHeight = 148f;

        var hl = zone.GetComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(0, 0, 12, 60);
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        Texture2D logoTex = Resources.Load<Texture2D>(LogoResourcePath);
        float aspect = (logoTex != null && logoTex.height > 0) ? (float)logoTex.width / logoTex.height : LogoAspectRatio;

        var logoGo = new GameObject("NstuLogo", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
        var logoRt = logoGo.GetComponent<RectTransform>();
        logoRt.SetParent(zoneRt, false);

        var logoLe = logoGo.GetComponent<LayoutElement>();
        logoLe.minWidth = LogoHeightPx * aspect;
        logoLe.preferredWidth = LogoHeightPx * aspect;
        logoLe.minHeight = LogoHeightPx;
        logoLe.preferredHeight = LogoHeightPx;

        var raw = logoGo.GetComponent<RawImage>();
        raw.texture = logoTex;
        raw.color = logoTex != null ? Color.white : Color.clear;
        raw.raycastTarget = false;
    }

    private static void BuildAuthorsSection(RectTransform card)
    {
        RectTransform band = MainMenuUiFactory.CreatePanel(
            card, "AuthorsBand", new Color(0.95f, 0.96f, 0.99f, 1f), false, false);
        var bandLe = band.gameObject.AddComponent<LayoutElement>();
        bandLe.minHeight = 154f;
        bandLe.preferredHeight = 154f;

        var vl = band.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(28, 28, 14, 14);
        vl.spacing = 6f;
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = true;
        vl.childControlHeight = true;

        MainMenuUiFactory.CreateText(
            band, "StudentsText",
            "Смородников Дмитрий ПМ-53\nПриц Иван ПМ-53\nЧерных Егор ПМИ-53",
            21f, FontStyles.Bold, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.Center);

        MainMenuUiFactory.CreateText(
            band, "SupervisorText",
            "Руководитель Баранов А.В., кафедра ОФ НГТУ",
            19f, FontStyles.Normal, MainMenuUiFactory.AccentColor, TextAlignmentOptions.Center);
    }

    private static void AddSpacer(RectTransform parent, float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
    }

    private void SelfCheck()
    {
        Debug.Assert(autoTransitionTime > 0f, "SplashScreenController.autoTransitionTime must be > 0");
        Debug.Assert(fadeDuration >= 0f, "SplashScreenController.fadeDuration must be >= 0");
        Debug.Assert(LogoAspectRatio > 2.0f && LogoAspectRatio < 3.0f, "NSTU logo aspect ratio out of range");
    }
}
