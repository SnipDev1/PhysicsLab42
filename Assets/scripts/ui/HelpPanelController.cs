using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal help & illustrated manual overlay styled like the main menu, combining the Lab №42 PDF launcher
/// with the step-by-step text tutorial and real screenshots.
/// </summary>
public class HelpPanelController : MonoBehaviour
{
    public static HelpPanelController ActiveInstance { get; private set; }
    public static bool IsOpen => ActiveInstance != null && ActiveInstance._root != null && ActiveInstance._root.activeSelf;

    private GameObject _root;
    private ScrollRect _scrollRect;
    private RectTransform _contentRt;
    private readonly List<(RawImage image, string resourcePath)> _screenshots = new List<(RawImage, string)>();

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
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape) && Object.FindAnyObjectByType<ExitButton>() == null)
            CloseActive();
    }

    public static void OpenOrCreate()
    {
        if (ActiveInstance == null)
        {
            Canvas canvas = MainMenuUiFactory.FindOrCreateCanvas();
            ActiveInstance = canvas.GetComponent<HelpPanelController>() ?? canvas.gameObject.AddComponent<HelpPanelController>();
        }
        ActiveInstance.Show();
    }

    public static void CloseActive()
    {
        if (ActiveInstance != null && ActiveInstance._root != null)
            ActiveInstance._root.SetActive(false);
    }

    public void Show()
    {
        EnsureBuilt();
        RefreshScreenshots();
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRt);
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void EnsureBuilt()
    {
        if (_root != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? MainMenuUiFactory.FindOrCreateCanvas();
        RectTransform scrim = MainMenuUiFactory.CreatePanel(canvas.transform, "HelpPanelOverlay", MainMenuUiFactory.ScrimColor, false, true);
        scrim.anchorMin = Vector2.zero;
        scrim.anchorMax = Vector2.one;
        scrim.offsetMin = Vector2.zero;
        scrim.offsetMax = Vector2.zero;
        _root = scrim.gameObject;

        RectTransform card = MainMenuUiFactory.CreatePanel(scrim, "HelpCard", MainMenuUiFactory.ModalCardColor, true, true);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(1520f, 940f);
        card.anchoredPosition = Vector2.zero;

        var cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(0, 0, 0, 0);
        cardLayout.spacing = 0f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        BuildHeader(card);
        MainMenuUiFactory.CreateDivider(card);
        BuildScrollBody(card);

        _root.SetActive(false);
    }

    private void BuildHeader(RectTransform card)
    {
        RectTransform header = MainMenuUiFactory.CreatePanel(card, "Header", Color.clear, false, false);
        var headerLe = header.gameObject.AddComponent<LayoutElement>();
        headerLe.minHeight = 84f;
        headerLe.preferredHeight = 84f;

        var hl = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(32, 28, 14, 14);
        hl.spacing = 16f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        var title = MainMenuUiFactory.CreateText(
            header, "Title",
            "Справка — Работа с виртуальным стендом",
            28f, FontStyles.Bold, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.MidlineLeft);
        var titleLe = title.gameObject.AddComponent<LayoutElement>();
        titleLe.flexibleWidth = 1f;

        Button pdfBtn = MainMenuUiFactory.CreateButton(
            header, "OpenPdfButton", "Открыть PDF (ЛР № 42)", 23f,
            () => OpenPDF.OpenLab42Pdf(), true, MainMenuUiFactory.AccentColor);
        var pdfLe = pdfBtn.gameObject.AddComponent<LayoutElement>();
        pdfLe.preferredWidth = 350f;
        pdfLe.preferredHeight = 52f;

        Button closeBtn = MainMenuUiFactory.CreateButton(
            header, "CloseHelpButton", "Закрыть", 24f,
            CloseActive, true, MainMenuUiFactory.TextPrimary);
        var closeLe = closeBtn.gameObject.AddComponent<LayoutElement>();
        closeLe.preferredWidth = 160f;
        closeLe.preferredHeight = 52f;
    }

    private void BuildScrollBody(RectTransform card)
    {
        RectTransform body = MainMenuUiFactory.CreatePanel(card, "Body", Color.clear, false, false);
        var bodyLe = body.gameObject.AddComponent<LayoutElement>();
        bodyLe.flexibleHeight = 1f;
        bodyLe.flexibleWidth = 1f;

        _scrollRect = body.gameObject.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 35f;

        RectTransform viewport = MainMenuUiFactory.CreatePanel(body, "Viewport", new Color(1f, 1f, 1f, 0.01f), false, true);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(0f, 0f);
        viewport.offsetMax = new Vector2(-24f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        _contentRt = content;

        var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(36, 36, 24, 36);
        vl.spacing = 20f;
        vl.childAlignment = TextAnchor.UpperLeft;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Scrollbar
        RectTransform sbTrack = MainMenuUiFactory.CreatePanel(body, "Scrollbar", new Color(0f, 0f, 0f, 0.06f), false, true);
        sbTrack.anchorMin = new Vector2(1f, 0f);
        sbTrack.anchorMax = Vector2.one;
        sbTrack.pivot = new Vector2(1f, 0.5f);
        sbTrack.offsetMin = new Vector2(-18f, 12f);
        sbTrack.offsetMax = new Vector2(-6f, -12f);

        RectTransform sbArea = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
        sbArea.SetParent(sbTrack, false);
        sbArea.anchorMin = Vector2.zero;
        sbArea.anchorMax = Vector2.one;
        sbArea.offsetMin = Vector2.zero;
        sbArea.offsetMax = Vector2.zero;

        RectTransform sbHandle = MainMenuUiFactory.CreatePanel(sbArea, "Handle", new Color(0f, 0f, 0f, 0.28f), false, true);
        sbHandle.anchorMin = Vector2.zero;
        sbHandle.anchorMax = Vector2.one;
        sbHandle.offsetMin = Vector2.zero;
        sbHandle.offsetMax = Vector2.zero;

        var scrollbar = sbTrack.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = sbHandle;
        scrollbar.targetGraphic = sbHandle.GetComponent<Image>();

        _scrollRect.viewport = viewport;
        _scrollRect.content = content;
        _scrollRect.verticalScrollbar = scrollbar;
        _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        PopulateTutorialSections(content);
    }

    private void PopulateTutorialSections(RectTransform content)
    {
        MainMenuUiFactory.CreateText(
            content, "IntroText",
            "В данном разделе описаны органы управления и порядок работы с виртуальным лабораторным стендом. Теория и методика выполнения лабораторной работы № 42 открываются кнопкой «Открыть PDF (ЛР № 42)» в правом верхнем углу.",
            22f, FontStyles.Normal, MainMenuUiFactory.TextSecondary, TextAlignmentOptions.TopLeft);

        AddSection(
            content,
            "1. Устройство стенда и управление камерой",
            "• На столе расположены три блока: слева — стенд С3-ОК01 с клеммами схемы, справа сверху — источник питания ИПС-1, справа снизу — комбинированный измерительный прибор (амперметр и вольтметр).\n" +
            "• Поворот камеры — мышь; удержание [ПКМ] — лупа (приближение для точного наведения на клеммы и ручки).\n" +
            "• Настройка камеры: [+] / [−] — высота; раздел «Настройки» [O] или в меню паузы — FOV и чувствительность мыши; [ПКМ] — лупа (колёсико в лупе регулирует зум).\n" +
            "• Интерфейс: [I] — список клавиш управления; [Tab] / [H] — краткая памятка по подключению; [Esc] — меню паузы.",
            "TutorialScreenshots/step1_overview",
            "Рис. 1. Общий вид лабораторного стенда (слева — С3-ОК01, справа — ИПС-1 и измерительный блок).");

        AddSection(
            content,
            "2. Подключение проводов и сборка схемы",
            "• Чтобы соединить две клеммы, наведите прицел на первую и нажмите ЛКМ (появится зелёная метка выбора), затем нажмите ЛКМ по второй клемме. Повторный клик по той же паре снимает провод; повторный клик по уже выбранной клемме отменяет выбор.\n" +
            "• Для наглядности провода окрашены по стандартной цветовой маркировке:\n" +
            "  • Чёрный («−» / общий): ИПС-1 «0...20 В» (−) ↔ Фоторезистор, вывод 1\n" +
            "  • Красный («+» питания): ИПС-1 «0...20 В» (+) ↔ Плата С3-ОК01, вход (+)\n" +
            "  • Оранжевый (вход амперметра): Плата С3-ОК01, выход (I) ↔ Амперметр (I / +)\n" +
            "  • Жёлтый (выход амперметра): Амперметр (* / −) ↔ Фоторезистор, вывод 2\n" +
            "  • Чёрный (общий вольтметра): Вольтметр (* / −) ↔ Фоторезистор, вывод 1\n" +
            "  • Красный («+» вольтметра): Вольтметр (U / +) ↔ Фоторезистор, вывод 2",
            "TutorialScreenshots/step2_circuit",
            "Рис. 2. Собранная схема из 6 проводов между стендом С3-ОК01, ИПС-1 и измерительным блоком.");

        AddSection(
            content,
            "3. Включение приборов и настройка пределов измерений",
            "• Включите питание красными кнопками «Сеть» на левой части панелей ИПС-1 и измерительного блока (наведение прицела и клик ЛКМ).\n" +
            "• Регулировка ручек на панелях выполняется зажатием ЛКМ с движением мыши влево/вправо либо прокруткой колёсика мыши при наведённом прицеле.\n" +
            "• Вспомогательную ручку канала «0...6,3 В» на ИПС-1 оставьте на 0. Напряжение питания U подаётся ручкой «0...20 В».\n" +
            "• На измерительном приборе выбирайте кнопками предел измерения амперметра (20 / 200 / 2000) и вольтметра (2 / 20 / 200), а также единицы измерения кнопками мкА/мА и В/мВ. Если на табло горит индикация перегрузки «1   », выберите больший предел или переключите единицы.",
            "TutorialScreenshots/step3_controls",
            "Рис. 3. Включённые приборы ИПС-1 и измерительный блок, выбор пределов и вращение ручек.");

        AddSection(
            content,
            "4. Регулировка освещённости и переключение излучателей",
            "• Ручка «Уст. J/J0» на ИПС-1 задаёт относительную интенсивность от 0.000 до 1.000; текущее значение отображается на левом зелёном табло ИПС-1.\n" +
            "• Кнопка под индикатором «λ» по кругу переключает номер активного излучателя (от 0 до 7 на красном табло):\n" +
            "    λ0 = 430 нм   λ1 = 470 нм   λ2 = 520 нм   λ3 = 565 нм\n" +
            "    λ4 = 590 нм   λ5 = 660 нм   λ6 = 700 нм   λ7 = 860 нм\n" +
            "• Показания напряжения считываются с правого табло «Вольтметр», показания тока — с левого табло «Амперметр».",
            "TutorialScreenshots/step4_measurements",
            "Рис. 4. Снятие показаний с табло ИПС-1, амперметра и вольтметра.");
    }

    private void AddSection(RectTransform parent, string heading, string bodyText, string screenshotResource, string caption)
    {
        MainMenuUiFactory.CreateDivider(parent);

        MainMenuUiFactory.CreateText(
            parent, "SectionHeading", heading,
            26f, FontStyles.Bold, MainMenuUiFactory.AccentColor, TextAlignmentOptions.TopLeft);

        MainMenuUiFactory.CreateText(
            parent, "SectionBody", bodyText,
            21f, FontStyles.Normal, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.TopLeft);

        RectTransform frame = MainMenuUiFactory.CreatePanel(parent, "ScreenshotFrame", Color.clear, false, false);
        var frameLe = frame.gameObject.AddComponent<LayoutElement>();
        frameLe.preferredHeight = 500f;

        var rawGo = new GameObject("ScreenshotImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter), typeof(Outline));
        var rawRt = rawGo.GetComponent<RectTransform>();
        rawRt.SetParent(frame, false);
        rawRt.anchorMin = Vector2.zero;
        rawRt.anchorMax = Vector2.one;
        rawRt.offsetMin = new Vector2(4f, 4f);
        rawRt.offsetMax = new Vector2(-4f, -4f);

        var rawImg = rawGo.GetComponent<RawImage>();
        rawImg.raycastTarget = false;
        var outline = rawGo.GetComponent<Outline>();
        outline.effectColor = MainMenuUiFactory.BorderColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var arf = rawGo.GetComponent<AspectRatioFitter>();
        arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        arf.aspectRatio = 16f / 9f;

        _screenshots.Add((rawImg, screenshotResource));

        MainMenuUiFactory.CreateText(
            parent, "ScreenshotCaption", caption,
            18f, FontStyles.Italic, MainMenuUiFactory.TextSecondary, TextAlignmentOptions.Center);
    }

    private void RefreshScreenshots()
    {
        foreach (var (rawImg, path) in _screenshots)
        {
            if (rawImg == null) continue;
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                rawImg.texture = tex;
                rawImg.color = Color.white;
                if (rawImg.TryGetComponent<AspectRatioFitter>(out var arf) && tex.height > 0)
                    arf.aspectRatio = (float)tex.width / tex.height;
            }
            else
            {
                rawImg.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            }
        }
    }
}
