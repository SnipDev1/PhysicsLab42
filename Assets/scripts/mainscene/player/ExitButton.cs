using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private void EnsurePauseMenu()
    {
        if (pauseMenuRoot == null)
        {
            var found = GameObject.Find("Canvas/PauseMenu");
            if (found != null)
                pauseMenuRoot = found;
        }
    }
}
