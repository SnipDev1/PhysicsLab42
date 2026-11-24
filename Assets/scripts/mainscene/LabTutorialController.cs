using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Interactive 9-step guided tutorial for Lab №42 in TutorialScene,
/// highlighting target 3D controls/ports and rendering a main-menu-styled HUD & completion card.
/// </summary>
public class LabTutorialController : MonoBehaviour
{
    public static bool IsModalOpen { get; private set; }

    private const int TotalSteps = 9;

    private LabSetupController _lab;
    private Camera _cam;
    private float _lookHoldTime;
    private bool _lookedAtTarget;
    private float _camStartY;
    private float _camStartFov;
    private bool _heightAdjusted;
    private bool _zoomUsed;

    private int _currentStep;
    private int _step8LambdaBaseline;
    private bool _completed;
    private bool _dismissedAfterComplete;

    // UI
    private GameObject _panelRoot;
    private TextMeshProUGUI _stepBadgeTmp;
    private RectTransform _progressFillRt;
    private TextMeshProUGUI _titleTmp;
    private TextMeshProUGUI _descTmp;
    private TextMeshProUGUI _checklistTmp;
    private GameObject _completionOverlay;
    private string _lastChecklistText = "";

    // 3D Markers
    private readonly List<GameObject> _markerPool = new List<GameObject>();
    private readonly List<Renderer> _markerRenderers = new List<Renderer>();
    private Material _markerBaseMat;
    private readonly Dictionary<string, Vector3> _controlPosCache = new Dictionary<string, Vector3>();

    public int CurrentStep => _currentStep;
    public bool IsCompleted => _completed;

    void Awake()
    {
        IsModalOpen = false;
    }

    void Start()
    {
        _lab = LabSetupController.Instance != null ? LabSetupController.Instance : Object.FindAnyObjectByType<LabSetupController>();
        _cam = LabSetupController.GetMainCamera();
        BuildUi();
        EnterStep(0);
    }

    void OnDestroy()
    {
        IsModalOpen = false;
        for (int i = 0; i < _markerRenderers.Count; i++)
        {
            if (_markerRenderers[i] != null && _markerRenderers[i].sharedMaterial != null)
                Destroy(_markerRenderers[i].sharedMaterial);
        }
        for (int i = 0; i < _markerPool.Count; i++)
        {
            if (_markerPool[i] != null) Destroy(_markerPool[i]);
        }
        _markerPool.Clear();
        _markerRenderers.Clear();
        if (_markerBaseMat != null)
        {
            Destroy(_markerBaseMat);
            _markerBaseMat = null;
        }
    }

    void Update()
    {
        if (_lab == null)
            _lab = LabSetupController.Instance;
        if (_cam == null)
            _cam = LabSetupController.GetMainCamera();

        bool pausedOrHelp = ExitButton.IsPaused || HelpPanelController.IsOpen;

        if (_panelRoot != null)
        {
            bool showPanel = !pausedOrHelp && !IsModalOpen && !_dismissedAfterComplete;
            if (_panelRoot.activeSelf != showPanel)
                _panelRoot.SetActive(showPanel);
        }

        if (IsModalOpen)
        {
            HideAllMarkers();
            if (!pausedOrHelp && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                GoToLabScene();
            return;
        }

        if (pausedOrHelp || _dismissedAfterComplete || _lab == null)
        {
            HideAllMarkers();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            AdvanceStep();
            return;
        }

        if (Input.GetKeyDown(KeyCode.B) && _currentStep > 0)
        {
            EnterStep(_currentStep - 1);
            return;
        }

        EvaluateCurrentStep();
        UpdateMarkers();
    }

    public void AdvanceStep()
    {
        if (_currentStep + 1 < TotalSteps)
            EnterStep(_currentStep + 1);
        else
            ShowCompletionModal();
    }

    private void EnterStep(int stepIndex)
    {
        _currentStep = Mathf.Clamp(stepIndex, 0, TotalSteps - 1);
        _lastChecklistText = "";

        if (_currentStep == 0)
        {
            _lookHoldTime = 0f;
            _lookedAtTarget = false;
        }
        else if (_currentStep == 1)
        {
            _camStartY = _cam != null ? _cam.transform.position.y : 0f;
            _camStartFov = _cam != null ? _cam.fieldOfView : 60f;
            _heightAdjusted = false;
            _zoomUsed = false;
        }
        else if (_currentStep == 8 && _lab != null)
        {
            _step8LambdaBaseline = _lab.LambdaSwitchCount;
        }

        RefreshStepStaticUi();
        EvaluateCurrentStep();
        UpdateMarkers();
    }

    private void RefreshStepStaticUi()
    {
        if (_stepBadgeTmp == null) return;

        _stepBadgeTmp.text = $"ОБУЧЕНИЕ  •  ШАГ {_currentStep + 1} ИЗ {TotalSteps}";
        if (_progressFillRt != null)
            _progressFillRt.anchorMax = new Vector2((_currentStep + 1) / (float)TotalSteps, 1f);

        GetStepCopy(_currentStep, out string title, out string desc);
        _titleTmp.text = title;
        _descTmp.text = desc;
    }

    private static void GetStepCopy(int step, out string title, out string desc)
    {
        switch (step)
        {
            case 0:
                title = "Знакомство со стендом и наведение камеры";
                desc = "Поверните камеру мышью и наведите прицел на подсвеченную клемму фоторезистора на стенде С3-ОК01 (удерживайте взгляд до отметки [ОК]).";
                break;
            case 1:
                title = "Регулировка положения камеры и приближение";
                desc = "Отрегулируйте высоту камеры клавишами [−] / [+] и приблизьте вид удержанием [ПКМ] (также доступны [Shift + +/-] для угла обзора и [Ctrl + +/-] для чувствительности).";
                break;
            case 2:
                title = "Цепь питания ИПС-1 (чёрный и красный)";
                desc = "Соедините питание постоянного тока по конвенции: чёрный провод на «−», красный на «+». Кликайте ЛКМ по паре подсвеченных клемм.";
                break;
            case 3:
                title = "Последовательное включение амперметра";
                desc = "Включите амперметр в разрыв цепи фоторезистора: оранжевый провод соединяет плату с клеммой (I/+), жёлтый — клемму (* / −) со вторым выводом фоторезистора.";
                break;
            case 4:
                title = "Параллельное включение вольтметра";
                desc = "Подключите вольтметр параллельно фоторезистору: чёрный провод на общий (* / −), красный — на измерительный вход (U / +).";
                break;
            case 5:
                title = "Включение питания приборов";
                desc = "Нажмите красные кнопки «Сеть» на обоих приборах — верхнем ИПС-1 и нижнем комбинированном вольтметре/амперметре.";
                break;
            case 6:
                title = "Снятие вольт-амперной характеристики";
                desc = "Наведите прицел на ручку «0...20 В» на ИПС-1 и плавно установите напряжение U не менее 4.5 В (тяните мышью с ЛКМ или крутите колёсико). Ручку «0...6,3 В» держите на 0.";
                break;
            case 7:
                title = "Световая характеристика и предел тока";
                desc = "Увеличьте относительную интенсивность освещения J/J0 до >= 0.35. При перегрузке амперметра («1   ») переключите предел на 200 кнопкой на передней панели.";
                break;
            default:
                title = "Спектральная характеристика (выбор λ)";
                desc = "Переключите светодиод стенда С3-ОК01 кнопкой λ не менее 2 раз, наблюдая за изменением фототока на разных длинах волн (430–860 нм).";
                break;
        }
    }

    private void EvaluateCurrentStep()
    {
        if (_lab == null) return;

        bool stepDone = false;
        string checklist = "";

        switch (_currentStep)
        {
            case 0:
            {
                bool lookingAtPort = false;
                if (_cam != null && _lab.TryGetPortWorldPos(6, out Vector3 targetPos))
                {
                    Vector3 toTarget = (targetPos - _cam.transform.position).normalized;
                    if (Vector3.Angle(_cam.transform.forward, toTarget) < 5.5f)
                        lookingAtPort = true;
                }
                if (LabSetupController.HoveredObject != null &&
                    (LabSetupController.HoveredObject.name == "Cylinder.026 (2)" ||
                     LabSetupController.HoveredObject.name == "Cylinder.026 (3)"))
                {
                    lookingAtPort = true;
                }

                if (lookingAtPort)
                    _lookHoldTime += Time.deltaTime;
                else
                    _lookHoldTime = Mathf.Max(0f, _lookHoldTime - Time.deltaTime);

                if (_lookHoldTime >= 0.4f)
                    _lookedAtTarget = true;

                checklist = $"{FmtCheck(_lookedAtTarget)} Посмотреть на подсвеченную клемму С3-ОК01";
                stepDone = _lookedAtTarget;
                break;
            }
            case 1:
            {
                if (_cam != null)
                {
                    if (Mathf.Abs(_cam.transform.position.y - _camStartY) >= 0.15f)
                        _heightAdjusted = true;
                    if (Input.GetMouseButton(1) || Mathf.Abs(_cam.fieldOfView - _camStartFov) >= 4f)
                        _zoomUsed = true;
                }

                float currentY = _cam != null ? _cam.transform.position.y : 0f;
                checklist = $"{FmtCheck(_heightAdjusted)} Изменить высоту камеры [−] / [+] ({currentY:0.00} м)\n" +
                            $"{FmtCheck(_zoomUsed)} Приблизить вид [ПКМ] или [Shift + +/-]";
                stepDone = _heightAdjusted && _zoomUsed;
                break;
            }
            case 2:
            {
                bool negWire = _lab.HasWireBetweenNodes(0, 6);
                bool posWire = _lab.HasWireBetweenNodes(1, 8);
                checklist = $"{FmtCheck(negWire)} Чёрный: ИПС (−) ↔ ФР вывод 1\n" +
                            $"{FmtCheck(posWire)} Красный: ИПС (+) ↔ Плата (+)";
                stepDone = (negWire && posWire) || _lab.IsPhotoCircuitConnected;
                break;
            }
            case 3:
            {
                bool orangeWire = _lab.HasWireBetweenNodes(8, 5);
                bool yellowWire = _lab.HasWireBetweenNodes(4, 7);
                checklist = $"{FmtCheck(orangeWire)} Оранжевый: Плата (I) ↔ Амперметр (I/+)\n" +
                            $"{FmtCheck(yellowWire)} Жёлтый: Амперметр (*) ↔ ФР вывод 2";
                stepDone = _lab.IsPhotoCircuitConnected;
                break;
            }
            case 4:
            {
                bool vNeg = _lab.HasWireBetweenNodes(2, 6);
                bool vPos = _lab.HasWireBetweenNodes(3, 7);
                bool fullOk = _lab.IsVoltmeterConnected && _lab.IsPhotoCircuitConnected;
                checklist = $"{FmtCheck(vNeg || fullOk)} Чёрный: Вольтметр (*) ↔ ФР вывод 1\n" +
                            $"{FmtCheck(vPos || fullOk)} Красный: Вольтметр (U/+) ↔ ФР вывод 2";
                stepDone = fullOk;
                break;
            }
            case 5:
            {
                checklist = $"{FmtCheck(_lab.isIpsPowered)} Включить «Сеть» на ИПС-1\n" +
                            $"{FmtCheck(_lab.isMeterPowered)} Включить «Сеть» на мультиметре";
                stepDone = _lab.isIpsPowered && _lab.isMeterPowered;
                break;
            }
            case 6:
            {
                bool uOk = _lab.voltageU >= 4.5f;
                checklist = $"{FmtCheck(uOk)} Установить U >= 4.5 В (сейчас {_lab.voltageU:0.00} В)";
                stepDone = uOk;
                break;
            }
            case 7:
            {
                bool jOk = _lab.baseCurrentIo >= 0.35f;
                bool limit200 = Mathf.Approximately(_lab.ampRange, 200f);
                checklist = $"{FmtCheck(jOk)} Задать J/J0 >= 0.35 (сейчас {_lab.baseCurrentIo:0.000})\n" +
                            $"{FmtCheck(limit200)} Выбрать предел амперметра 200 ({_lab.ampRange:0})";
                stepDone = jOk && limit200;
                break;
            }
            case 8:
            {
                int switches = Mathf.Max(0, _lab.LambdaSwitchCount - _step8LambdaBaseline);
                bool lOk = switches >= 2;
                checklist = $"{FmtCheck(lOk)} Переключить λ >= 2 раз ({Mathf.Min(switches, 2)}/2, сейчас {_lab.GetCurrentWavelengthNm()} нм)";
                stepDone = lOk;
                break;
            }
        }

        if (_checklistTmp != null && checklist != _lastChecklistText)
        {
            _lastChecklistText = checklist;
            _checklistTmp.text = checklist;
        }

        if (stepDone)
            AdvanceStep();
    }

    private static string FmtCheck(bool ok) =>
        ok ? "<color=#1F803D><b>[ОК]</b></color>" : "<color=#424756>[ ]</color>";

    private void ShowCompletionModal()
    {
        _completed = true;
        IsModalOpen = true;
        HideAllMarkers();
        if (_panelRoot != null) _panelRoot.SetActive(false);

        if (_completionOverlay != null)
        {
            _completionOverlay.SetActive(true);
            _completionOverlay.transform.SetAsLastSibling();
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void GoToLabScene()
    {
        IsModalOpen = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("SampleScene");
    }

    public void ContinueInTutorialScene()
    {
        IsModalOpen = false;
        _dismissedAfterComplete = true;
        if (_completionOverlay != null)
            _completionOverlay.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void GoToMainMenu()
    {
        IsModalOpen = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("mainMenu");
    }

    private void BuildUi()
    {
        Canvas canvas = MainMenuUiFactory.FindOrCreateCanvas();

        // Карточка шага обучения справа сверху
        RectTransform card = MainMenuUiFactory.CreatePanel(canvas.transform, "TutorialStepCard", MainMenuUiFactory.ModalCardColor, true, false);
        card.anchorMin = new Vector2(1f, 1f);
        card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(1f, 1f);
        card.anchoredPosition = new Vector2(-16f, -16f);
        card.sizeDelta = new Vector2(520f, 300f);

        var vl = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(22, 22, 18, 18);
        vl.spacing = 10f;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        var csf = card.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _stepBadgeTmp = MainMenuUiFactory.CreateText(
            card, "StepBadge", "ОБУЧЕНИЕ  •  ШАГ 1 ИЗ 9",
            17f, FontStyles.Bold, MainMenuUiFactory.AccentColor, TextAlignmentOptions.Left, false);

        RectTransform track = MainMenuUiFactory.CreatePanel(card, "ProgressTrack", new Color(0f, 0f, 0f, 0.08f), false, false);
        var trackLe = track.gameObject.AddComponent<LayoutElement>();
        trackLe.minHeight = 6f;
        trackLe.preferredHeight = 6f;

        _progressFillRt = MainMenuUiFactory.CreatePanel(track, "ProgressFill", MainMenuUiFactory.AccentColor, false, false);
        _progressFillRt.anchorMin = Vector2.zero;
        _progressFillRt.anchorMax = new Vector2(1f / TotalSteps, 1f);
        _progressFillRt.offsetMin = Vector2.zero;
        _progressFillRt.offsetMax = Vector2.zero;

        _titleTmp = MainMenuUiFactory.CreateText(
            card, "StepTitle", "",
            23f, FontStyles.Bold, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.TopLeft, false);

        _descTmp = MainMenuUiFactory.CreateText(
            card, "StepDesc", "",
            18f, FontStyles.Normal, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.TopLeft, false);

        MainMenuUiFactory.CreateDivider(card);

        _checklistTmp = MainMenuUiFactory.CreateText(
            card, "StepChecklist", "",
            19f, FontStyles.Normal, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.TopLeft, false);

        MainMenuUiFactory.CreateText(
            card, "StepFooter", "[Enter] Далее / Пропустить   [B] Назад",
            15f, FontStyles.Italic, MainMenuUiFactory.TextSecondary, TextAlignmentOptions.TopLeft, false);

        _panelRoot = card.gameObject;

        // Поставим сразу после LabHud, но под PauseMenu / HelpPanel
        var hud = canvas.transform.Find("LabHud");
        int targetIdx = hud != null ? hud.GetSiblingIndex() + 1 : 0;
        card.SetSiblingIndex(targetIdx);

        BuildCompletionModal(canvas.transform);
    }

    private void BuildCompletionModal(Transform canvasTransform)
    {
        RectTransform scrim = MainMenuUiFactory.CreatePanel(canvasTransform, "TutorialCompletedOverlay", MainMenuUiFactory.ScrimColor, false, true);
        scrim.anchorMin = Vector2.zero;
        scrim.anchorMax = Vector2.one;
        scrim.offsetMin = Vector2.zero;
        scrim.offsetMax = Vector2.zero;
        _completionOverlay = scrim.gameObject;

        RectTransform card = MainMenuUiFactory.CreatePanel(scrim, "CompletionCard", MainMenuUiFactory.ModalCardColor, true, true);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(760f, 430f);
        card.anchoredPosition = Vector2.zero;

        var vl = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(40, 40, 36, 36);
        vl.spacing = 18f;
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        MainMenuUiFactory.CreateText(
            card, "CompletionTitle", "Обучение завершено!",
            38f, FontStyles.Bold, MainMenuUiFactory.SuccessColor, TextAlignmentOptions.Center, false);

        MainMenuUiFactory.CreateText(
            card, "CompletionDesc",
            "Вы освоили базовые принципы работы с симулятором и приборами",
            22f, FontStyles.Normal, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.Center, false);

        MainMenuUiFactory.CreateDivider(card);

        AddModalButton(card, "GoLabButton", "Перейти к лабораторной работе", MainMenuUiFactory.AccentColor, GoToLabScene);
        AddModalButton(card, "ContinueButton", "Продолжить здесь", MainMenuUiFactory.TextPrimary, ContinueInTutorialScene);
        AddModalButton(card, "MainMenuButton", "Главное меню", MainMenuUiFactory.TextPrimary, GoToMainMenu);

        _completionOverlay.SetActive(false);
    }

    private static void AddModalButton(RectTransform parent, string name, string label, Color textColor, UnityEngine.Events.UnityAction onClick)
    {
        Button btn = MainMenuUiFactory.CreateButton(parent, name, label, 26f, onClick, true, textColor);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 58f;
    }

    private void UpdateMarkers()
    {
        if (_lab == null || !_lab.ArePortsInitialized)
        {
            HideAllMarkers();
            return;
        }

        int idx = 0;
        float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 5f);

        switch (_currentStep)
        {
            case 0:
                PlacePortMarker(ref idx, 6, new Color(0.18f, 0.45f, 0.95f, 0.92f), pulse);
                break;
            case 1:
                break;
            case 2:
                if (!_lab.HasWireBetweenNodes(0, 6))
                {
                    PlacePortMarker(ref idx, 0, new Color(0.22f, 0.24f, 0.28f, 0.90f), pulse);
                    PlacePortMarker(ref idx, 6, new Color(0.22f, 0.24f, 0.28f, 0.90f), pulse);
                }
                if (!_lab.HasWireBetweenNodes(1, 8))
                {
                    PlacePortMarker(ref idx, 1, new Color(0.92f, 0.20f, 0.20f, 0.90f), pulse);
                    PlacePortMarker(ref idx, 10, new Color(0.92f, 0.20f, 0.20f, 0.90f), pulse);
                }
                break;
            case 3:
                if (!_lab.HasWireBetweenNodes(8, 5))
                {
                    PlacePortMarker(ref idx, 11, new Color(0.95f, 0.48f, 0.12f, 0.90f), pulse);
                    PlacePortMarker(ref idx, 5, new Color(0.95f, 0.48f, 0.12f, 0.90f), pulse);
                }
                if (!_lab.HasWireBetweenNodes(4, 7))
                {
                    PlacePortMarker(ref idx, 4, new Color(0.95f, 0.78f, 0.15f, 0.90f), pulse);
                    PlacePortMarker(ref idx, 8, new Color(0.95f, 0.78f, 0.15f, 0.90f), pulse);
                }
                break;
            case 4:
                if (!_lab.HasWireBetweenNodes(2, 6))
                {
                    PlacePortMarker(ref idx, 2, new Color(0.22f, 0.24f, 0.28f, 0.90f), pulse);
                    PlacePortMarker(ref idx, 7, new Color(0.22f, 0.24f, 0.28f, 0.90f), pulse);
                }
                if (!_lab.HasWireBetweenNodes(3, 7))
                {
                    PlacePortMarker(ref idx, 3, new Color(0.92f, 0.20f, 0.20f, 0.90f), pulse);
                    PlacePortMarker(ref idx, 9, new Color(0.92f, 0.20f, 0.20f, 0.90f), pulse);
                }
                break;
            case 5:
                if (!_lab.isIpsPowered)
                    PlaceControlMarker(ref idx, "Power_Button2", new Color(0.92f, 0.20f, 0.20f, 0.90f), pulse);
                if (!_lab.isMeterPowered)
                    PlaceControlMarker(ref idx, "Power_Button", new Color(0.92f, 0.20f, 0.20f, 0.90f), pulse);
                break;
            case 6:
                PlaceControlMarker(ref idx, "0_20_volt", new Color(0.18f, 0.45f, 0.95f, 0.90f), pulse);
                break;
            case 7:
                if (_lab.baseCurrentIo < 0.35f)
                    PlaceControlMarker(ref idx, "J0rotate", new Color(0.15f, 0.72f, 0.35f, 0.90f), pulse);
                if (_lab.IsAmpOverloaded)
                    PlaceControlMarker(ref idx, "Limit_Button200amp", new Color(0.95f, 0.50f, 0.10f, 0.90f), pulse);
                break;
            case 8:
                PlaceControlMarker(ref idx, "volna_button", new Color(0.18f, 0.45f, 0.95f, 0.90f), pulse);
                break;
        }

        for (int i = idx; i < _markerPool.Count; i++)
        {
            if (_markerPool[i] != null && _markerPool[i].activeSelf)
                _markerPool[i].SetActive(false);
        }
    }

    private void PlacePortMarker(ref int idx, int portIndex, Color color, float pulse)
    {
        if (!_lab.TryGetPortWorldPos(portIndex, out Vector3 pos)) return;
        PlaceMarkerAt(ref idx, pos + Vector3.up * 0.02f, color, pulse);
    }

    private void PlaceControlMarker(ref int idx, string objName, Color color, float pulse)
    {
        if (!_controlPosCache.TryGetValue(objName, out Vector3 pos))
        {
            GameObject go = GameObject.Find(objName);
            if (go == null) return;
            Renderer r = go.GetComponentInChildren<Renderer>();
            pos = r != null ? r.bounds.center : go.transform.position;
            _controlPosCache[objName] = pos;
        }
        PlaceMarkerAt(ref idx, pos, color, pulse);
    }

    private void PlaceMarkerAt(ref int idx, Vector3 worldPos, Color color, float pulse)
    {
        while (_markerPool.Count <= idx)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"TutorialMarker_{_markerPool.Count}";
            if (sphere.TryGetComponent<Collider>(out var col))
                Destroy(col);
            Renderer r = sphere.GetComponent<Renderer>();
            if (_markerBaseMat == null)
            {
                Shader sh = Resources.Load<Shader>("TutorialMarkerOverlay")
                    ?? Shader.Find("Lab42/TutorialMarkerOverlay")
                    ?? Shader.Find("Sprites/Default");
                if (sh != null) _markerBaseMat = new Material(sh);
            }
            if (r != null)
            {
                if (_markerBaseMat != null)
                {
                    var mat = new Material(_markerBaseMat)
                    {
                        renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay
                    };
                    r.sharedMaterial = mat;
                }
                r.sortingOrder = 100;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            _markerPool.Add(sphere);
            _markerRenderers.Add(r);
        }

        GameObject m = _markerPool[idx];
        m.transform.position = worldPos;
        m.transform.localScale = Vector3.one * (0.042f * pulse);
        Material activeMat = _markerRenderers[idx] != null ? _markerRenderers[idx].sharedMaterial : null;
        if (activeMat != null)
        {
            if (activeMat.HasProperty("_BaseColor"))
                activeMat.SetColor("_BaseColor", color);
            else
                activeMat.color = color;
        }
        if (!m.activeSelf)
            m.SetActive(true);
        idx++;
    }

    private void HideAllMarkers()
    {
        for (int i = 0; i < _markerPool.Count; i++)
        {
            if (_markerPool[i] != null && _markerPool[i].activeSelf)
                _markerPool[i].SetActive(false);
        }
    }
}
