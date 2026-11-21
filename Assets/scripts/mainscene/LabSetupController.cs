using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central controller for Physics Lab №42 (CdSe photoresistor),
/// managing power, light intensity J/J0, wavelength λ0..λ7, meters,
/// button states, hover tooltips, and the [Tab] reference overlay.
/// </summary>
public class LabSetupController : MonoBehaviour
{
    public static LabSetupController Instance { get; private set; }
    public static GameObject HoveredObject { get; private set; }

    [Header("Дисплеи")]
    public IpsDisplayController ipsDisplay;
    public MeterDisplayController meterDisplay;

    [Header("Питание")]
    public bool isIpsPowered = false;
    public bool isMeterPowered = false;

    [Header("Параметры ИПС-1")]
    [Range(0, 7)] public int lambda = 0;         // 0..7 (8 излучателей С3-ОК01)
    [Range(0f, 1f)] public float baseCurrentIo = 0f; // относительная интенсивность J/J0 (0.000..1.000)
    [Range(0f, 20f)] public float voltageU = 0f;     // напряжение 0...20 В
    [Range(0f, 6.3f)] public float voltageUo = 0f;   // вспомогательная ручка 0...6,3 В (на 0)

    [Header("Пределы и единицы измерения")]
    public float ampRange = 20f;   // 20, 200, 2000
    public float voltRange = 20f;  // 2, 20, 200 (по умолчанию 20 В)
    public bool isAmpMicro = true; // true = мкА, false = мА
    public bool isVoltMilli = false; // false = В, true = мВ

    // Таблица излучателей стенда С3-ОК01 (нм) и спектральная чувствительность CdSe (λ0 ≈ 715 нм, ΔE ≈ 1.74 эВ)
    public static readonly int[] WavelengthsNm = { 430, 470, 520, 565, 590, 660, 700, 860 };
    private static readonly float[] SpectralSensitivity = { 0.30f, 0.52f, 0.85f, 1.00f, 0.94f, 0.55f, 0.18f, 0.005f };

    // Калибровка под методичку №42 и реальный снимок (λ=1, J/J0=0.407, U=2.79 В -> I=7.37 мкА)
    private const float DarkConductanceMicroAPerV = 0.04f; // R_т = 25 МОм
    private const float PhotoGainMicroAPerV = 7.26636f;

    public float jJ0 { get => baseCurrentIo; set => baseCurrentIo = Mathf.Clamp01(value); }
    public int lambdaIndex => lambda;

    private struct ButtonVisual
    {
        public Transform transform;
        public Animator animator;
        public float restZ;
        public float threshold;
    }

    private struct CircuitPort
    {
        public string path;
        public string label;
        public GameObject go;
        public Vector3 worldPos;
    }

    private struct CircuitWire
    {
        public Color color;
        public int portA;
        public int portB;
        public LineRenderer line;
    }

    private struct UnionFind
    {
        private readonly int[] _parent;

        public UnionFind(int n)
        {
            _parent = new int[n];
            for (int i = 0; i < n; i++) _parent[i] = i;
        }

        public int Find(int x)
        {
            while (_parent[x] != x)
            {
                _parent[x] = _parent[_parent[x]];
                x = _parent[x];
            }
            return x;
        }

        public void Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra != rb) _parent[ra] = rb;
        }

        public bool Same(int a, int b) => Find(a) == Find(b);
    }

    // Порты: 0:IPS-, 1:IPS+, 2:V-, 3:V+, 4:A*, 5:A+, 6..7:Фоторезистор вывод 1, 8..9:Фоторезистор вывод 2, 10..11:Плата С3-ОК01
    private readonly CircuitPort[] _ports = new CircuitPort[]
    {
        new CircuitPort { path = "IPS1/Cylinder.008", label = "ИПС-1 «0...20 В» (−)" },
        new CircuitPort { path = "IPS1/Cylinder.009", label = "ИПС-1 «0...20 В» (+)" },
        new CircuitPort { path = "Ampermeter2222/Cylinder.008", label = "Вольтметр (* / −)" },
        new CircuitPort { path = "Ampermeter2222/Cylinder.009", label = "Вольтметр (U / +)" },
        new CircuitPort { path = "Ampermeter2222/Cylinder.005", label = "Амперметр (* / −)" },
        new CircuitPort { path = "Ampermeter2222/Cylinder.007", label = "Амперметр (I / +)" },
        new CircuitPort { path = "C3-OK01/Cylinder.026 (2)", label = "С3-ОК01 «Фоторезистор R» (вывод 1, гнездо 1)" },
        new CircuitPort { path = "C3-OK01/Cylinder.026 (3)", label = "С3-ОК01 «Фоторезистор R» (вывод 1, гнездо 2)" },
        new CircuitPort { path = "C3-OK01/Cylinder.026 (4)", label = "С3-ОК01 «Фоторезистор R» (вывод 2, гнездо 1)" },
        new CircuitPort { path = "C3-OK01/Cylinder.026 (5)", label = "С3-ОК01 «Фоторезистор R» (вывод 2, гнездо 2)" },
        new CircuitPort { path = "C3-OK01/Cylinder.010", label = "С3-ОК01 «Плата» (вход +)" },
        new CircuitPort { path = "C3-OK01/Cylinder.010 (1)", label = "С3-ОК01 «Плата» (к амперметру I)" }
    };

    private static readonly string[] UnusedIpsPortPaths = { "IPS1/Cylinder.007", "IPS1/Cylinder.010" };
    private static readonly string[] UnusedC3OkPortPaths =
    {
        "C3-OK01/Cylinder", "C3-OK01/Cylinder.001", "C3-OK01/Cylinder.002", "C3-OK01/Cylinder.003",
        "C3-OK01/Cylinder.004", "C3-OK01/Cylinder.005", "C3-OK01/Cylinder.006", "C3-OK01/Cylinder.007",
        "C3-OK01/Cylinder.008", "C3-OK01/Cylinder.009", "C3-OK01/Cylinder.010 (2)", "C3-OK01/Cylinder.011",
        "C3-OK01/Cylinder.012", "C3-OK01/Cylinder.013", "C3-OK01/Cylinder.014", "C3-OK01/Cylinder.015",
        "C3-OK01/Cylinder.016", "C3-OK01/Cylinder.017", "C3-OK01/Cylinder.018", "C3-OK01/Cylinder.019",
        "C3-OK01/Cylinder.020", "C3-OK01/Cylinder.021", "C3-OK01/Cylinder.022", "C3-OK01/Cylinder.023",
        "C3-OK01/Cylinder.024", "C3-OK01/Cylinder.025", "C3-OK01/Cylinder.026", "C3-OK01/Cylinder.026 (1)"
    };

    private readonly Dictionary<GameObject, string> _unusedInfoPorts = new Dictionary<GameObject, string>();
    private readonly Dictionary<GameObject, int> _portLookup = new Dictionary<GameObject, int>();
    private readonly Dictionary<Color, Material> _colorMaterials = new Dictionary<Color, Material>();
    private readonly List<CircuitWire> _wires = new List<CircuitWire>();

    private int _selectedPort = -1;
    private GameObject _selectionMarker;
    private Material _wireBaseMat;
    private Transform _c3Root;
    private Transform _ampRoot;
    private const float TableTopY = 1.8107f;

    private readonly List<ButtonVisual> _ampButtons = new List<ButtonVisual>();
    private readonly List<ButtonVisual> _voltButtons = new List<ButtonVisual>();
    private readonly List<RotaryKnob> _registeredKnobs = new List<RotaryKnob>();

    private Camera _mainCamera;
    private string _hoverText = "";
    private string _statusHint = "";
    private float _statusHintUntil = 0f;
    private bool _showHelp = false;
    private bool _showControls = false;

    private GameObject _hudRoot;
    private GameObject _tooltipGo;
    private TextMeshProUGUI _tooltipTmp;
    private GameObject _quickHelpGo;
    private GameObject _controlsGo;
    private GameObject _reticleGo;
    private string _lastTooltipText = "";

    public bool IsVoltmeterConnected { get; private set; }
    public bool IsPhotoCircuitConnected { get; private set; }
    public bool IsPowerShorted { get; private set; }
    public bool ArePortsInitialized { get; private set; }
    public int LambdaSwitchCount { get; private set; }
    public bool IsAmpOverloaded => GetMeasuredCurrent() > ampRange;
    public int ConnectedWireCount => _wires.Count;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        FixCameraAndSceneState();
    }

    void Start()
    {
        _mainCamera = GetMainCamera();
        if (ipsDisplay == null) ipsDisplay = Object.FindAnyObjectByType<IpsDisplayController>();
        if (meterDisplay == null) meterDisplay = Object.FindAnyObjectByType<MeterDisplayController>();

        InitLimitButtonGroup(_ampButtons, ("Limit_Button20AMP", 20f, -7.725594f), ("Limit_Button200amp", 200f, -7.725594f), ("Limit_Button2000AMP", 2000f, -7.732322f));
        InitLimitButtonGroup(_voltButtons, ("Limit_Button2VOLT", 2f, -7.724036f), ("Limit_Button20VOLT", 20f, -7.728207f), ("Limit_Button200VOLT", 200f, -7.732322f));
        InitCircuitPorts();
        SetupCrosshair();
        BuildHudUi();
    }

    void Update()
    {
        bool modalBlocked = ExitButton.IsPaused || HelpPanelController.IsOpen || LabTutorialController.IsModalOpen;
        if (_reticleGo != null && _reticleGo.activeSelf == modalBlocked)
            _reticleGo.SetActive(!modalBlocked);
        if (_hudRoot != null && _hudRoot.activeSelf == modalBlocked)
            _hudRoot.SetActive(!modalBlocked);

        if (modalBlocked)
        {
            HoveredObject = null;
            _hoverText = "";
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.H))
            ToggleQuickHelpPanel();

        if (Input.GetKeyDown(KeyCode.I))
            ToggleControlsPanel();

        // Горячие клавиши автосборки [C] и авторазборки [R]/[Backspace] отключены по методике;
        // сами методы AutoConnectStandardCircuit() и ClearAllWires() сохранены.

        UpdateHoverTooltip();

        if (Input.GetMouseButtonDown(0) && HoveredObject != null)
        {
            var ma = HoveredObject.GetComponentInParent<MouseAction>();
            if (ma != null)
                ma.InvokeDown();

            if (_portLookup.TryGetValue(HoveredObject, out int portIdx))
                OnPortClicked(portIdx);
        }

        UpdateHud();
    }

    public static Camera GetMainCamera()
    {
        if (Instance != null && Instance._mainCamera != null)
            return Instance._mainCamera;

        Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (Instance != null)
            Instance._mainCamera = cam;
        return cam;
    }

    private void FixCameraAndSceneState()
    {
        Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam == null) return;

        if (!cam.CompareTag("MainCamera"))
            cam.tag = "MainCamera";

        Transform parent = cam.transform.parent;
        if (parent != null)
        {
            foreach (var col in parent.GetComponents<Collider>())
                col.enabled = false;
            foreach (var mr in parent.GetComponents<MeshRenderer>())
                mr.enabled = false;

            if (parent.localScale.sqrMagnitude < 0.01f)
            {
                Vector3 savedWorldPos = cam.transform.position;
                parent.localScale = Vector3.one;
                cam.transform.localScale = Vector3.one;
                cam.transform.position = savedWorldPos;
            }
        }

        _mainCamera = cam;
    }

    public void RegisterKnob(RotaryKnob knob)
    {
        if (knob != null && !_registeredKnobs.Contains(knob))
            _registeredKnobs.Add(knob);
    }

    // --- Питание и переключение длины волны ---
    public void ToggleIpsPower() => isIpsPowered = !isIpsPowered;
    public void ToggleMeterPower() => isMeterPowered = !isMeterPowered;

    public void NextLambda()
    {
        if (!isIpsPowered) return;
        lambda = (lambda + 1) % WavelengthsNm.Length;
        LambdaSwitchCount++;
    }

    public void ChangeLambda() => NextLambda();
    public void CycleLambda() => NextLambda();
    public void SetLambda(int index) => lambda = Mathf.Clamp(index, 0, WavelengthsNm.Length - 1);

    // --- Ручки ИПС-1 ---
    public void SetVoltage(float u) => voltageU = Mathf.Clamp(u, 0f, 20f);
    public void SetVoltageU(float u) => voltageU = Mathf.Clamp(u, 0f, 20f);
    public void SetVoltageUo(float uo) => voltageUo = Mathf.Clamp(uo, 0f, 6.3f);

    public void SetIntensity(float intensity, RotaryKnob source = null)
    {
        baseCurrentIo = Mathf.Clamp01(intensity);
        // Синхронизируем дублирующие ручки J/J0 (ИПС-1 и С3-ОК01)
        foreach (var k in _registeredKnobs)
        {
            if (k != null && k != source && k.typeOfKnob == RotaryKnob.KnobType.BaseCurrentIo)
                k.SetNormalizedValue(baseCurrentIo);
        }
    }

    public void SetJ0(float value) => SetIntensity(value);

    // --- Пределы и единицы измерения мультиметра ---
    public void SetAmpRange20() => SetAmpRange(20f);
    public void SetAmpRange200() => SetAmpRange(200f);
    public void SetAmpRange2000() => SetAmpRange(2000f);
    public void SetAmpRange(float range)
    {
        ampRange = range;
        PlayLimitButtonAnim(_ampButtons, range);
    }

    public void SetVoltRange2() => SetVoltRange(2f);
    public void SetVoltRange20() => SetVoltRange(20f);
    public void SetVoltRange200() => SetVoltRange(200f);
    public void SetVoltRange(float range)
    {
        voltRange = range;
        PlayLimitButtonAnim(_voltButtons, range);
    }

    public void ToggleAmpUnits() => isAmpMicro = !isAmpMicro;
    public void ToggleAmpUnit() => ToggleAmpUnits();
    public void ToggleVoltUnits() => isVoltMilli = !isVoltMilli;
    public void ToggleVoltUnit() => ToggleVoltUnits();

    // --- Физическая модель ЛР №42 (фоторезистор CdSe) ---
    public int GetCurrentWavelengthNm() => WavelengthsNm[Mathf.Clamp(lambda, 0, WavelengthsNm.Length - 1)];
    public float GetDisplayedJ0() => isIpsPowered ? baseCurrentIo : 0f;

    public float GetTotalCurrentMicroA()
    {
        if (!isIpsPowered || voltageU <= 0f || !IsPhotoCircuitConnected) return 0f;
        float x = Mathf.Clamp01(baseCurrentIo);
        // Сублинейная люкс-амперная зависимость: линейна при малых J, насыщение при больших J (бимолекулярная рекомбинация)
        float luxFactor = (1.45f * x) / (0.45f + x);
        float sLambda = SpectralSensitivity[Mathf.Clamp(lambda, 0, SpectralSensitivity.Length - 1)];
        return voltageU * (DarkConductanceMicroAPerV + PhotoGainMicroAPerV * sLambda * luxFactor);
    }

    public float GetDarkCurrentMicroA() => (isIpsPowered && IsPhotoCircuitConnected) ? voltageU * DarkConductanceMicroAPerV : 0f;
    public float GetPhotocurrent() => GetTotalCurrentMicroA();

    public float GetMeasuredVoltage()
    {
        if (!isIpsPowered || !IsVoltmeterConnected) return 0f;
        return voltageU * (isVoltMilli ? 1000f : 1f);
    }

    public float GetMeasuredCurrent()
    {
        float microA = GetTotalCurrentMicroA();
        return isAmpMicro ? microA : microA * 0.001f;
    }

    // --- Порты и интерактивная сборка схемы (ЛР №42) ---
    private void InitCircuitPorts()
    {
        _portLookup.Clear();
        _unusedInfoPorts.Clear();
        _c3Root = GameObject.Find("C3-OK01")?.transform;
        _ampRoot = GameObject.Find("Ampermeter2222")?.transform;

        for (int i = 0; i < _ports.Length; i++)
        {
            GameObject parentGo = GameObject.Find(_ports[i].path);
            if (parentGo == null) continue;

            AddPortBoxCollider(parentGo, 1.45f);
            Renderer rend = parentGo.GetComponent<Renderer>();
            _ports[i].go = parentGo;
            _ports[i].worldPos = rend != null ? rend.bounds.center : parentGo.transform.position;
            _portLookup[parentGo] = i;
        }

        foreach (string path in UnusedIpsPortPaths)
            RegisterUnusedPort(path, "Канал ИПС-1 «0...6,3 В» — не используется в ЛР №42");

        foreach (string path in UnusedC3OkPortPaths)
            RegisterUnusedPort(path, "Клемма С3-ОК01 — не задействована в схеме ЛР №42");

        ArePortsInitialized = true;
    }

    public bool TryGetPortWorldPos(int portIndex, out Vector3 pos)
    {
        if (portIndex >= 0 && portIndex < _ports.Length && _ports[portIndex].go != null)
        {
            pos = _ports[portIndex].worldPos;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    public bool HasWireBetweenNodes(int nodeA, int nodeB)
    {
        for (int i = 0; i < _wires.Count; i++)
        {
            int na = MapPortToNode(_wires[i].portA);
            int nb = MapPortToNode(_wires[i].portB);
            if (IsPair(na, nb, nodeA, nodeB))
                return true;
        }
        return false;
    }

    private void RegisterUnusedPort(string path, string description)
    {
        GameObject go = GameObject.Find(path);
        if (go == null) return;
        AddPortBoxCollider(go, 1.1f);
        _unusedInfoPorts[go] = description;
    }

    private static void AddPortBoxCollider(GameObject go, float scale)
    {
        if (go.TryGetComponent<Collider>(out _)) return;
        BoxCollider col = go.AddComponent<BoxCollider>();
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            col.center = b.center;
            col.size = b.size * scale;
        }
        else
        {
            col.size = Vector3.one * 0.6f;
        }
    }

    private static bool IsPair(int a, int b, int x, int y) => (a == x && b == y) || (a == y && b == x);

    private int FindWireIndex(int a, int b)
    {
        for (int i = 0; i < _wires.Count; i++)
            if (IsPair(_wires[i].portA, _wires[i].portB, a, b))
                return i;
        return -1;
    }

    private int GetPortWireCount(int portIdx)
    {
        int count = 0;
        for (int i = 0; i < _wires.Count; i++)
            if (_wires[i].portA == portIdx || _wires[i].portB == portIdx)
                count++;
        return count;
    }

    private void OnPortClicked(int clickedPort)
    {
        if (_selectedPort == -1)
        {
            _selectedPort = clickedPort;
            UpdateSelectionMarker();
            return;
        }

        if (_selectedPort == clickedPort)
        {
            ClearPortSelection();
            return;
        }

        int existingIdx = FindWireIndex(_selectedPort, clickedPort);
        if (existingIdx >= 0)
        {
            if (_wires[existingIdx].line != null)
                Destroy(_wires[existingIdx].line.gameObject);
            _wires.RemoveAt(existingIdx);
            EvaluateCircuit();
            ShowStatus($"Провод отсоединён: {_ports[_selectedPort].label} ↔ {_ports[clickedPort].label}");
            ClearPortSelection();
            return;
        }

        ConnectWire(_selectedPort, clickedPort);
        string msg = IsPowerShorted
            ? "Внимание: короткое замыкание источника ИПС-1!"
            : (IsVoltmeterConnected && IsPhotoCircuitConnected
                ? "Схема ЛР №42 собрана верно! Включите питание ИПС-1 и мультиметра"
                : $"Провод подключён: {_ports[_selectedPort].label} ↔ {_ports[clickedPort].label}");
        ClearPortSelection();
        ShowStatus(msg);
    }

    private void ConnectWire(int a, int b, bool evaluate = true)
    {
        if (a == b || FindWireIndex(a, b) >= 0) return;

        Color wireColor = ChooseWireColor(a, b);
        var wire = new CircuitWire { color = wireColor, portA = a, portB = b };
        wire.line = SpawnWireVisual(wire);
        _wires.Add(wire);

        if (evaluate)
            EvaluateCircuit();
    }

    private static Color ChooseWireColor(int a, int b)
    {
        // Конвенциальная цветовая маркировка DC:
        // Чёрный — общий / «−» (ИПС −, Вольтметр * / −)
        if ((a == 0 && (b == 6 || b == 7)) || (b == 0 && (a == 6 || a == 7))
            || (a == 2 && (b == 6 || b == 7)) || (b == 2 && (a == 6 || a == 7)))
            return new Color(0.14f, 0.14f, 0.16f);
        // Красный — питание / измерение «+» (ИПС +, Вольтметр U / +)
        if (IsPair(a, b, 1, 10)
            || (a == 3 && (b == 8 || b == 9)) || (b == 3 && (a == 8 || a == 9)))
            return new Color(0.86f, 0.16f, 0.16f);
        // Оранжевый — токовый вход измерительного прибора (Плата I ↔ Амперметр I / +)
        if (IsPair(a, b, 5, 11))
            return new Color(0.91f, 0.45f, 0.10f);
        // Жёлтый — токовый выход амперметра к нагрузке (Амперметр * ↔ Фоторезистор вывод 2)
        if ((a == 4 && (b == 8 || b == 9)) || (b == 4 && (a == 8 || a == 9)))
            return new Color(0.92f, 0.75f, 0.12f);

        if (a == 1 || b == 1 || a == 3 || b == 3)
            return new Color(0.86f, 0.16f, 0.16f);
        if (a == 0 || b == 0 || a == 2 || b == 2)
            return new Color(0.14f, 0.14f, 0.16f);
        if (a == 5 || b == 5)
            return new Color(0.91f, 0.45f, 0.10f);
        if (a == 4 || b == 4)
            return new Color(0.92f, 0.75f, 0.12f);

        return new Color(0.45f, 0.47f, 0.50f);
    }

    public void ClearAllWires()
    {
        for (int i = 0; i < _wires.Count; i++)
        {
            if (_wires[i].line != null)
                Destroy(_wires[i].line.gameObject);
        }
        _wires.Clear();
        ClearPortSelection();
        EvaluateCircuit();
        ShowStatus("Все провода отсоединены");
    }

    public void AutoConnectStandardCircuit()
    {
        ClearAllWires();
        ConnectWire(0, 6, false);  // ИПС(−) ↔ С3-ОК01 Cylinder.026 (2) [чёрный]
        ConnectWire(1, 10, false); // ИПС(+) ↔ С3-ОК01 Cylinder.010 [красный]
        ConnectWire(5, 11, false); // Амперметр(I) ↔ С3-ОК01 Cylinder.010 (1) [оранжевый]
        ConnectWire(4, 8, false);  // Амперметр(*) ↔ С3-ОК01 Cylinder.026 (4) [жёлтый]
        ConnectWire(2, 7, false);  // Вольтметр(−) ↔ С3-ОК01 Cylinder.026 (3) [чёрный]
        ConnectWire(3, 9, false);  // Вольтметр(+) ↔ С3-ОК01 Cylinder.026 (5) [красный]
        EvaluateCircuit();
        ShowStatus("Автосборка: схема собрана (6 проводов)");
    }

    private int MapPortToNode(int port)
    {
        switch (port)
        {
            case 0: return 0; // IPS-
            case 1: return 1; // IPS+
            case 2: return 2; // Volt-
            case 3: return 3; // Volt+
            case 4: return 4; // Amp*
            case 5: return 5; // Amp I
            case 6:
            case 7: return 6; // Photo terminal 1 (Cylinder.026 (2), (3))
            case 8:
            case 9: return 7; // Photo terminal 2 (Cylinder.026 (4), (5))
            case 10:
            case 11: return 8; // Board supply bus (Cylinder.010, Cylinder.010 (1))
            default: return -1;
        }
    }

    private void EvaluateCircuit()
    {
        var ufWires = new UnionFind(9);
        for (int i = 0; i < _wires.Count; i++)
        {
            int na = MapPortToNode(_wires[i].portA);
            int nb = MapPortToNode(_wires[i].portB);
            if (na >= 0 && nb >= 0)
                ufWires.Union(na, nb);
        }

        var ufPower = new UnionFind(9);
        ufPower.Union(4, 5); // шунт амперметра
        for (int i = 0; i < _wires.Count; i++)
        {
            int na = MapPortToNode(_wires[i].portA);
            int nb = MapPortToNode(_wires[i].portB);
            if (na >= 0 && nb >= 0)
                ufPower.Union(na, nb);
        }

        IsPowerShorted = ufPower.Same(0, 1);
        if (IsPowerShorted)
        {
            IsVoltmeterConnected = false;
            IsPhotoCircuitConnected = false;
            return;
        }

        IsVoltmeterConnected = (ufWires.Same(2, 6) && ufWires.Same(3, 7))
                            || (ufWires.Same(2, 7) && ufWires.Same(3, 6))
                            || (ufWires.Same(2, 0) && ufWires.Same(3, 1))
                            || (ufWires.Same(2, 1) && ufWires.Same(3, 0));

        if (ufWires.Same(6, 7) || ufWires.Same(4, 5))
        {
            IsPhotoCircuitConnected = false;
            return;
        }

        bool seriesConnected = false;
        int[] ipsPins = { 0, 1 };
        int[] ampPins = { 4, 5 };
        int[] photoPins = { 6, 7 };

        foreach (int ipsOut in ipsPins)
        {
            int ipsIn = ipsOut == 0 ? 1 : 0;
            foreach (int ampIn in ampPins)
            {
                int ampOut = ampIn == 4 ? 5 : 4;
                foreach (int photoIn in photoPins)
                {
                    int photoOut = photoIn == 6 ? 7 : 6;
                    if (ufWires.Same(ipsOut, ampIn) && ufWires.Same(ampOut, photoIn) && ufWires.Same(photoOut, ipsIn))
                        seriesConnected = true;
                    if (ufWires.Same(ipsOut, photoIn) && ufWires.Same(photoOut, ampIn) && ufWires.Same(ampOut, ipsIn))
                        seriesConnected = true;
                }
            }
        }

        IsPhotoCircuitConnected = seriesConnected;
    }

    private void ClearPortSelection()
    {
        _selectedPort = -1;
        UpdateSelectionMarker();
    }

    private void UpdateSelectionMarker()
    {
        if (_selectedPort < 0 || _ports[_selectedPort].go == null)
        {
            if (_selectionMarker != null) _selectionMarker.SetActive(false);
            return;
        }

        if (_selectionMarker == null)
        {
            _selectionMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _selectionMarker.name = "PortSelectionMarker";
            if (_selectionMarker.TryGetComponent<Collider>(out Collider col))
                Destroy(col);
            _selectionMarker.transform.localScale = Vector3.one * 0.032f;
            Renderer r = _selectionMarker.GetComponent<Renderer>();
            if (r != null)
            {
                Material mat = GetColorMaterial(new Color(0.15f, 0.95f, 0.45f));
                if (mat != null) r.sharedMaterial = mat;
            }
        }

        _selectionMarker.transform.position = _ports[_selectedPort].worldPos;
        _selectionMarker.SetActive(true);
    }

    private Material GetColorMaterial(Color color)
    {
        if (_colorMaterials.TryGetValue(color, out Material cached) && cached != null)
            return cached;

        if (_wireBaseMat == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            _wireBaseMat = new Material(shader);
        }
        Material m = new Material(_wireBaseMat) { color = color };
        _colorMaterials[color] = m;
        return m;
    }

    private LineRenderer SpawnWireVisual(in CircuitWire wire)
    {
        GameObject go = new GameObject($"Wire_{wire.portA}_{wire.portB}");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sortingOrder = 1;
        const int sampleCount = 32;
        lr.positionCount = sampleCount;
        lr.startWidth = 0.008f;
        lr.endWidth = 0.008f;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.startColor = wire.color;
        lr.endColor = wire.color;

        Material mat = GetColorMaterial(wire.color);
        if (mat != null) lr.sharedMaterial = mat;

        float spread = (Mathf.Abs(wire.portA * 7 + wire.portB * 13) % 5) * 0.0045f;
        List<Vector3> waypoints = BuildWireWaypoints(wire.portA, wire.portB, spread);
        Vector3[] pts = SampleCatmullRom(waypoints, sampleCount);

        for (int i = 1; i < sampleCount - 1; i++)
        {
            float fade = Mathf.Clamp01(Mathf.Min(i, sampleCount - 1 - i) / 4f);
            pts[i] = ClampWirePointOutsideObstacles(pts[i], fade, spread);
        }

        // Два прохода сглаживания + повторный прижим к поверхностям, чтобы убрать изломы вокруг препятствий
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 2; i < sampleCount - 2; i++)
                pts[i] = 0.25f * pts[i - 1] + 0.5f * pts[i] + 0.25f * pts[i + 1];
            for (int i = 1; i < sampleCount - 1; i++)
            {
                float fade = Mathf.Clamp01(Mathf.Min(i, sampleCount - 1 - i) / 4f);
                pts[i] = ClampWirePointOutsideObstacles(pts[i], fade, spread);
            }
        }

        lr.SetPositions(pts);
        return lr;
    }

    private List<Vector3> BuildWireWaypoints(int portA, int portB, float spread)
    {
        Vector3 start = _ports[portA].worldPos;
        Vector3 end = _ports[portB].worldPos;

        // Оба порта на С3-ОК01
        if (portA >= 6 && portB >= 6)
        {
            Vector3 normal = new Vector3(0.15f, 0.89f, -0.44f).normalized;
            return new List<Vector3>
            {
                start,
                start + normal * 0.04f,
                Vector3.Lerp(start, end, 0.5f) + normal * (0.07f + spread * 1.5f),
                end + normal * 0.04f,
                end
            };
        }

        // Оба порта на измерителе / ИПС-1 (порты 0..5)
        if (portA <= 5 && portB <= 5)
        {
            // Вертикальная перемычка (например ИПС к Вольтметру: порты почти друг над другом)
            if (Mathf.Abs(start.x - end.x) < 0.15f)
            {
                return new List<Vector3>
                {
                    start,
                    start + new Vector3(0f, -0.01f, -0.05f - spread),
                    Vector3.Lerp(start, end, 0.5f) + new Vector3(0f, -0.03f, -0.09f - spread),
                    end + new Vector3(0f, 0.01f, -0.05f - spread),
                    end
                };
            }

            // Горизонтальная перемычка на передней панели (например Амперметр к Вольтметру 4 ↔ 2)
            // Плавно опускается к крышке стола перед выступающими кнопками и корпусом
            float tableY = TableTopY + 0.014f + spread * 0.003f;
            float frontZ = 0.62f - spread;
            return new List<Vector3>
            {
                start,
                new Vector3(start.x, start.y - 0.01f, start.z - 0.06f - spread),
                new Vector3(Mathf.Lerp(start.x, end.x, 0.25f), tableY, frontZ),
                new Vector3(Mathf.Lerp(start.x, end.x, 0.75f), tableY, frontZ),
                new Vector3(end.x, end.y - 0.01f, end.z - 0.06f - spread),
                end
            };
        }

        // Соединение С3-ОК01 ↔ измеритель / ИПС-1
        bool aIsC3 = portA >= 6;
        Vector3 c3Pos = aIsC3 ? start : end;
        Vector3 instPos = aIsC3 ? end : start;

        // Направление выхода из наклонных гнезд С3-ОК01 (вверх и наружу)
        Vector3 c3Out = new Vector3(0.15f, 0.89f, -0.44f).normalized;
        // Точка схода с деки С3-ОК01 через передний правый край (над бортиком)
        Vector3 c3FrontDeck = new Vector3(
            Mathf.Lerp(c3Pos.x, 0.0f, 0.45f),
            TableTopY + 0.055f,
            0.65f - spread);
        // Траектория по столу перед приборами
        float tableLevelY = TableTopY + 0.012f + spread * 0.003f;
        float tableFrontZ = 0.52f - spread;

        var list = new List<Vector3>
        {
            c3Pos,
            c3Pos + c3Out * 0.045f,
            c3FrontDeck,
            new Vector3(Mathf.Lerp(c3Pos.x, instPos.x, 0.35f), tableLevelY, tableFrontZ),
            new Vector3(instPos.x, tableLevelY, tableFrontZ + 0.04f),
            new Vector3(instPos.x, instPos.y - 0.015f, instPos.z - 0.07f - spread),
            instPos
        };
        if (!aIsC3) list.Reverse();
        return list;
    }

    private static Vector3[] SampleCatmullRom(IReadOnlyList<Vector3> pts, int sampleCount)
    {
        Vector3[] result = new Vector3[sampleCount];
        int n = pts.Count;
        for (int i = 0; i < sampleCount; i++)
        {
            float u = (i / (float)(sampleCount - 1)) * (n - 1);
            int idx = Mathf.Min(Mathf.FloorToInt(u), n - 2);
            float t = u - idx;
            float t2 = t * t;
            float t3 = t2 * t;

            Vector3 p0 = idx > 0 ? pts[idx - 1] : pts[0] + (pts[0] - pts[1]);
            Vector3 p1 = pts[idx];
            Vector3 p2 = pts[idx + 1];
            Vector3 p3 = idx + 2 < n ? pts[idx + 2] : pts[n - 1] + (pts[n - 1] - pts[n - 2]);

            result[i] = 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
        result[0] = pts[0];
        result[sampleCount - 1] = pts[n - 1];
        return result;
    }

    private Vector3 ClampWirePointOutsideObstacles(Vector3 p, float endpointFade, float spread)
    {
        // 1. Поверхность стола: провод никогда не уходит под крышку стола
        float minTableY = TableTopY + 0.006f + spread * 0.003f;
        if (p.y < minTableY) p.y = minTableY;

        // 2. Корпус комбинированного прибора (Амперметр2222) и ИПС-1
        // В зоне передней панели провод не врезается в корпус и выступающие кнопки
        if (p.x >= 0.25f && p.x <= 2.25f && p.y >= 1.80f && p.y <= 3.08f)
        {
            // На удалении от гнезд (endpointFade -> 1) провод огибает выступающие элементы спереди
            float maxZ = Mathf.Lerp(0.85f, 0.65f - spread, endpointFade);
            if (p.z > maxZ)
                p.z = maxZ;
        }

        // 3. Стенд С3-ОК01: наклонная дека и корпус
        if (p.x >= -1.38f && p.x <= 0.08f)
        {
            if (p.z >= 0.63f && p.z <= 1.45f)
            {
                // Поверхность наклонной деки С3-ОК01: линейный подъем от z=0.63 (y=1.811) до z=1.30 (y=2.356)
                float deckY = TableTopY + 0.813f * (p.z - 0.63f) + 0.03f;
                if (p.y < deckY)
                    p.y = deckY;
            }
            else if (p.z < 0.63f && p.z >= 0.40f)
            {
                // Передний скос/бортик у основания стенда
                if (p.y < TableTopY + 0.012f)
                    p.y = TableTopY + 0.012f;
            }
        }

        // Финальный прижим к уровню стола
        if (p.y < minTableY) p.y = minTableY;
        return p;
    }

    public void ShowCameraStatus(string text) => ShowStatus(text);

    private void ShowStatus(string text)
    {
        _statusHint = text;
        _statusHintUntil = Time.time + 2.6f;
    }

    // --- Визуализация кнопок пределов и прицела ---
    private void InitLimitButtonGroup(List<ButtonVisual> group, params (string name, float val, float restZ)[] specs)
    {
        foreach (var (objName, val, restZ) in specs)
        {
            GameObject go = GameObject.Find(objName);
            if (go == null) continue;
            var anim = go.GetComponent<Animator>();
            if (anim != null) anim.enabled = true;

            Vector3 p = go.transform.localPosition;
            p.z = restZ;
            go.transform.localPosition = p;

            group.Add(new ButtonVisual
            {
                transform = go.transform,
                animator = anim,
                restZ = restZ,
                threshold = val
            });
        }
    }

    private static void PlayLimitButtonAnim(List<ButtonVisual> group, float activeValue)
    {
        foreach (var b in group)
        {
            if (Mathf.Approximately(b.threshold, activeValue) && b.animator != null)
            {
                b.animator.enabled = true;
                b.animator.ResetTrigger("activate");
                b.animator.SetTrigger("activate");
                break;
            }
        }
    }

    private void SetupCrosshair()
    {
        _reticleGo = GameObject.Find("Canvas/Image");
        if (_reticleGo == null) return;
        var rt = _reticleGo.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(6f, 6f);
        var img = _reticleGo.GetComponent<Image>();
        if (img != null) img.color = new Color(1f, 1f, 1f, 0.85f);
    }

    // --- Подсказки при наведении и справка [Tab] ---
    public static Ray GetPointerRay(Camera cam)
    {
        Vector3 pos = Cursor.lockState == CursorLockMode.Locked
            ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
            : Input.mousePosition;
        return cam.ScreenPointToRay(pos);
    }

    private void UpdateHoverTooltip()
    {
        HoveredObject = null;
        _hoverText = "";
        _mainCamera = GetMainCamera();
        if (_mainCamera == null) return;

        if (!Physics.Raycast(GetPointerRay(_mainCamera), out RaycastHit hit, 25f) || hit.collider == null)
            return;

        HoveredObject = hit.collider.gameObject;
        if (_portLookup.TryGetValue(HoveredObject, out int portIdx))
            _hoverText = DescribePortHover(portIdx);
        else if (_unusedInfoPorts.TryGetValue(HoveredObject, out string unusedDesc))
            _hoverText = unusedDesc;
        else
            _hoverText = DescribeControl(HoveredObject.name);
    }

    private string DescribePortHover(int portIdx)
    {
        string label = _ports[portIdx].label;
        if (_selectedPort == -1)
        {
            int count = GetPortWireCount(portIdx);
            return count > 0
                ? $"{label} (проводов: {count})  [ЛКМ — выбрать клемму]"
                : $"{label}  [ЛКМ — выбрать клемму]";
        }

        if (_selectedPort == portIdx)
            return $"Выбрана клемма: {label}  [Повторный ЛКМ — отмена]";

        if (FindWireIndex(_selectedPort, portIdx) >= 0)
            return $"Отсоединить провод: {_ports[_selectedPort].label} ↔ {label}  [ЛКМ]";

        return $"Соединить: {_ports[_selectedPort].label} ↔ {label}  [ЛКМ]";
    }

    private string DescribeControl(string objectName)
    {
        switch (objectName)
        {
            case "Power_Button2":
                return $"ИПС-1 «Сеть»: {(isIpsPowered ? "ВКЛ" : "ВЫКЛ")}  [ЛКМ]";
            case "Power_Button":
                return $"Мультиметр «Сеть»: {(isMeterPowered ? "ВКЛ" : "ВЫКЛ")}  [ЛКМ]";
            case "volna_button":
                return $"Переключатель λ: {lambda} ({GetCurrentWavelengthNm()} нм)  [ЛКМ]";
            case "0_20_volt":
                return $"Напряжение U (0...20 В): {voltageU:0.00} В  [ЛКМ тянуть / Колёсико]";
            case "0_6_volt":
                return $"Канал 0...6,3 В (не подключён — держать на 0): {voltageUo:0.00} В  [ЛКМ / Колёсико]";
            case "J0rotate":
            case "Circle":
                return $"Освещённость J/J0: {baseCurrentIo:0.000}  [ЛКМ тянуть / Колёсико]";
            case "Limit_Button20AMP":
                return $"Предел амперметра: 20{(Mathf.Approximately(ampRange, 20f) ? " [ВЫБРАН]" : "")}  [ЛКМ]";
            case "Limit_Button200amp":
                return $"Предел амперметра: 200{(Mathf.Approximately(ampRange, 200f) ? " [ВЫБРАН]" : "")}  [ЛКМ]";
            case "Limit_Button2000AMP":
                return $"Предел амперметра: 2000{(Mathf.Approximately(ampRange, 2000f) ? " [ВЫБРАН]" : "")}  [ЛКМ]";
            case "Limit_Button2VOLT":
                return $"Предел вольтметра: 2{(Mathf.Approximately(voltRange, 2f) ? " [ВЫБРАН]" : "")}  [ЛКМ]";
            case "Limit_Button20VOLT":
                return $"Предел вольтметра: 20{(Mathf.Approximately(voltRange, 20f) ? " [ВЫБРАН]" : "")}  [ЛКМ]";
            case "Limit_Button200VOLT":
                return $"Предел вольтметра: 200{(Mathf.Approximately(voltRange, 200f) ? " [ВЫБРАН]" : "")}  [ЛКМ]";
            case "Change_MA-A_Button":
                return $"Единицы амперметра: {(isAmpMicro ? "мкА" : "мА")}  [ЛКМ]";
            case "Change_V_mV_Button":
                return $"Единицы вольтметра: {(isVoltMilli ? "мВ" : "В")}  [ЛКМ]";
            case "Change_NOTHING":
                return "Режим постоянный/переменный (по умолчанию =)";
            default:
                return "";
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            HoveredObject = null;
        }
        foreach (var kv in _colorMaterials)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }
        _colorMaterials.Clear();
        if (_wireBaseMat != null)
        {
            Destroy(_wireBaseMat);
            _wireBaseMat = null;
        }
    }

    public void ToggleQuickHelpPanel()
    {
        _showHelp = !_showHelp;
        if (_showHelp)
        {
            _showControls = false;
            if (_controlsGo != null) _controlsGo.SetActive(false);
        }
        if (_quickHelpGo != null)
            _quickHelpGo.SetActive(_showHelp);
    }

    public void ToggleControlsPanel()
    {
        _showControls = !_showControls;
        if (_showControls)
        {
            _showHelp = false;
            if (_quickHelpGo != null) _quickHelpGo.SetActive(false);
        }
        if (_controlsGo != null)
            _controlsGo.SetActive(_showControls);
    }

    private void BuildHudUi()
    {
        Canvas canvas = MainMenuUiFactory.FindOrCreateCanvas();
        var hudRt = new GameObject("LabHud", typeof(RectTransform)).GetComponent<RectTransform>();
        hudRt.SetParent(canvas.transform, false);
        hudRt.anchorMin = Vector2.zero;
        hudRt.anchorMax = Vector2.one;
        hudRt.offsetMin = Vector2.zero;
        hudRt.offsetMax = Vector2.zero;
        hudRt.SetAsFirstSibling();
        _hudRoot = hudRt.gameObject;

        // 1. Аккуратная подпись в нижнем левом углу экрана
        RectTransform cornerRt = MainMenuUiFactory.CreatePanel(hudRt, "CornerHintPanel", MainMenuUiFactory.CardColor, true, false);
        cornerRt.anchorMin = Vector2.zero;
        cornerRt.anchorMax = Vector2.zero;
        cornerRt.pivot = Vector2.zero;
        cornerRt.anchoredPosition = new Vector2(16f, 16f);
        var cornerHl = cornerRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        cornerHl.padding = new RectOffset(16, 16, 8, 8);
        cornerHl.childControlWidth = true;
        cornerHl.childControlHeight = true;
        var cornerCsf = cornerRt.gameObject.AddComponent<ContentSizeFitter>();
        cornerCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        cornerCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        MainMenuUiFactory.CreateText(
            cornerRt, "CornerHintText", "[I] Управление   •   [Tab] Памятка",
            18f, FontStyles.Normal, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.Left, false);

        // 2. Подсказка при наведении / статусе
        RectTransform tooltipRt = MainMenuUiFactory.CreatePanel(hudRt, "TooltipPanel", MainMenuUiFactory.ModalCardColor, true, false);
        tooltipRt.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRt.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRt.pivot = new Vector2(0.5f, 1f);
        tooltipRt.anchoredPosition = new Vector2(0f, -34f);
        tooltipRt.sizeDelta = new Vector2(780f, 52f);
        var tipVl = tooltipRt.gameObject.AddComponent<VerticalLayoutGroup>();
        tipVl.padding = new RectOffset(18, 18, 10, 10);
        tipVl.childControlWidth = true;
        tipVl.childControlHeight = true;
        tipVl.childForceExpandWidth = true;
        var tipCsf = tooltipRt.gameObject.AddComponent<ContentSizeFitter>();
        tipCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _tooltipTmp = MainMenuUiFactory.CreateText(
            tooltipRt, "TooltipText", "", 21f, FontStyles.Bold, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.Center, false);
        _tooltipGo = tooltipRt.gameObject;
        _tooltipGo.SetActive(false);

        // 3. Памятка [Tab] / [H] (без технических обозначений)
        RectTransform helpRt = MainMenuUiFactory.CreatePanel(hudRt, "QuickHelpPanel", MainMenuUiFactory.ModalCardColor, true, false);
        helpRt.anchorMin = new Vector2(0f, 1f);
        helpRt.anchorMax = new Vector2(0f, 1f);
        helpRt.pivot = new Vector2(0f, 1f);
        helpRt.anchoredPosition = new Vector2(16f, -16f);
        helpRt.sizeDelta = new Vector2(780f, 520f);
        var helpVl = helpRt.gameObject.AddComponent<VerticalLayoutGroup>();
        helpVl.padding = new RectOffset(24, 24, 20, 20);
        helpVl.spacing = 10f;
        helpVl.childControlWidth = true;
        helpVl.childControlHeight = true;
        helpVl.childForceExpandWidth = true;
        var helpCsf = helpRt.gameObject.AddComponent<ContentSizeFitter>();
        helpCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MainMenuUiFactory.CreateText(
            helpRt, "QuickHelpTitle",
            "ЛАБОРАТОРНАЯ РАБОТА № 42 — Исследование фоторезистора (CdSe)",
            22f, FontStyles.Bold, MainMenuUiFactory.AccentColor, TextAlignmentOptions.TopLeft, false);
        MainMenuUiFactory.CreateDivider(helpRt);

        string helpBody =
            "1. Сборка схемы (клик ЛКМ по двум клеммам; повторный клик — снять провод):\n" +
            "   • ИПС «0...20 В» (−) ↔ Фоторезистор, вывод 1 (чёрный)\n" +
            "   • ИПС «0...20 В» (+) ↔ Плата С3-ОК01, вход «+» (красный)\n" +
            "   • Амперметр (I / +) ↔ Плата С3-ОК01, выход «I» (оранжевый)\n" +
            "   • Амперметр (* / −) ↔ Фоторезистор, вывод 2 (жёлтый)\n" +
            "   • Вольтметр (* / −) ↔ Фоторезистор, вывод 1 (чёрный)\n" +
            "   • Вольтметр (U / +) ↔ Фоторезистор, вывод 2 (красный)\n\n" +
            "2. Включите «Сеть» на ИПС-1 и на комбинированном вольтметре/амперметре.\n" +
            "3. Ручка «0...6,3 В» остаётся на 0; напряжение U подаётся ручкой «0...20 В».\n" +
            "4. Кнопкой λ выбирается светодиод стенда С3-ОК01:\n" +
            "   λ0 = 430 нм   λ1 = 470 нм   λ2 = 520 нм   λ3 = 565 нм\n" +
            "   λ4 = 590 нм   λ5 = 660 нм   λ6 = 700 нм   λ7 = 860 нм (ИК)\n\n" +
            "5. Индикация «1   » — перегрузка (выберите больший предел 20 / 200 / 2000).\n" +
            "Закрыть памятку: [Tab] или [H]. Управление: [I]. Полная справка и PDF: [Esc] → Справка.";

        MainMenuUiFactory.CreateText(
            helpRt, "QuickHelpBody", helpBody,
            18f, FontStyles.Normal, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.TopLeft, false);

        _quickHelpGo = helpRt.gameObject;
        _quickHelpGo.SetActive(_showHelp);

        // 4. Меню управления и биндов на кнопку [I]
        RectTransform ctrlRt = MainMenuUiFactory.CreatePanel(hudRt, "ControlsPanel", MainMenuUiFactory.ModalCardColor, true, false);
        ctrlRt.anchorMin = new Vector2(0f, 1f);
        ctrlRt.anchorMax = new Vector2(0f, 1f);
        ctrlRt.pivot = new Vector2(0f, 1f);
        ctrlRt.anchoredPosition = new Vector2(16f, -16f);
        ctrlRt.sizeDelta = new Vector2(600f, 440f);
        var ctrlVl = ctrlRt.gameObject.AddComponent<VerticalLayoutGroup>();
        ctrlVl.padding = new RectOffset(24, 24, 20, 20);
        ctrlVl.spacing = 10f;
        ctrlVl.childControlWidth = true;
        ctrlVl.childControlHeight = true;
        ctrlVl.childForceExpandWidth = true;
        var ctrlCsf = ctrlRt.gameObject.AddComponent<ContentSizeFitter>();
        ctrlCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MainMenuUiFactory.CreateText(
            ctrlRt, "ControlsTitle",
            "Управление и горячие клавиши",
            22f, FontStyles.Bold, MainMenuUiFactory.AccentColor, TextAlignmentOptions.TopLeft, false);
        MainMenuUiFactory.CreateDivider(ctrlRt);

        string controlsBody =
            "• Мышь — осмотр лабораторного стенда\n" +
            "• [ПКМ] (удерживать) — лупа (приближение)\n" +
            "• [+] / [−] — высота камеры\n" +
            "• [Shift + +/-] — угол обзора камеры\n" +
            "• [ПКМ + +/-] или колёсико в лупе — зум лупы\n" +
            "• [Ctrl + +/-] или [ / ] — чувствительность мыши\n" +
            "• [ЛКМ] по клеммам — подключить / снять провод\n" +
            "• [ЛКМ] по кнопкам — питание, пределы и выбор λ\n" +
            "• [ЛКМ тянуть] или [Колёсико] по ручкам — настройка U и J/J0\n" +
            "• [Tab] / [H] — открыть / закрыть памятку\n" +
            "• [I] — открыть / закрыть это окно\n" +
            "• [Enter] / [B] — навигация по шагам (в обучении)\n" +
            "• [Esc] — меню паузы и справка";

        MainMenuUiFactory.CreateText(
            ctrlRt, "ControlsBody", controlsBody,
            18f, FontStyles.Normal, MainMenuUiFactory.TextPrimary, TextAlignmentOptions.TopLeft, false);

        _controlsGo = ctrlRt.gameObject;
        _controlsGo.SetActive(_showControls);

        UpdateHud();
    }

    private void UpdateHud()
    {
        string tip = !string.IsNullOrEmpty(_hoverText)
            ? _hoverText
            : (Time.time < _statusHintUntil ? _statusHint : "");

        bool hasTip = !string.IsNullOrEmpty(tip);
        if (_tooltipGo != null && _tooltipGo.activeSelf != hasTip)
            _tooltipGo.SetActive(hasTip);
        if (hasTip && _tooltipTmp != null && tip != _lastTooltipText)
        {
            _lastTooltipText = tip;
            _tooltipTmp.text = tip;
        }
    }
}
