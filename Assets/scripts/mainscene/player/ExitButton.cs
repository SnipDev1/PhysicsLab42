using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ExitButton : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [SerializeField] public GameObject pauseMenuRoot;

    void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        EnsurePauseMenu();
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);
    }

    void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (DevMenuController.IsOpen)
            {
                DevMenuController.CloseActive();
                return;
            }
            if (SettingsPanelController.IsOpen)
            {
                SettingsPanelController.CloseActive();
                return;
            }
            if (HelpPanelController.IsOpen)
            {
                HelpPanelController.CloseActive();
                return;
            }
            if (LabSetupController.Instance != null && LabSetupController.Instance.CloseOpenPanels())
            {
                return;
            }
            if (IsPaused)
                ResumeGame();
            else
                PauseGame();
        }
        else if (Input.GetKeyDown(KeyCode.O))
        {
            if (!DevMenuController.IsOpen && !LabTutorialController.IsModalOpen)
            {
                if (SettingsPanelController.IsOpen)
                    SettingsPanelController.CloseActive();
                else
                    SettingsPanelController.OpenOrCreate();
            }
        }
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        EnsurePauseMenu();
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        SettingsPanelController.CloseActive();
        HelpPanelController.CloseActive();
        IsPaused = false;
        Time.timeScale = 1f;
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);
        if (!LabTutorialController.IsModalOpen && !DevMenuController.IsOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void ExitToMainMenu()
    {
        SettingsPanelController.CloseActive();
        HelpPanelController.CloseActive();
        IsPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("mainMenu");
    }

    public void OpenManual()
    {
        HelpPanelController.OpenOrCreate();
    }

    public void OpenSettings()
    {
        SettingsPanelController.OpenOrCreate();
    }

    private void EnsurePauseMenu()
    {
        if (pauseMenuRoot == null)
        {
            var found = GameObject.Find("Canvas/PauseMenu");
            if (found != null)
                pauseMenuRoot = found;
        }
        if (pauseMenuRoot != null)
        {
            EnsureSettingsButtonInPauseMenu();
        }
    }

    private void EnsureSettingsButtonInPauseMenu()
    {
        var menuCard = pauseMenuRoot.transform.Find("MenuCard");
        if (menuCard != null && menuCard.Find("SettingsButton") == null)
        {
            var btn = MainMenuUiFactory.CreateButton(
                menuCard, "SettingsButton", "Настройки", 24f,
                OpenSettings, true, MainMenuUiFactory.TextPrimary);

            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 65f;

            int lastIdx = menuCard.childCount - 1;
            if (lastIdx > 0)
                btn.transform.SetSiblingIndex(lastIdx - 1);

            if (menuCard.TryGetComponent<RectTransform>(out var rt))
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 480f);
            }
        }
    }
}
