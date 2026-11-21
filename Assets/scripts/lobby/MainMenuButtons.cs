using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuButtons : MonoBehaviour
{
    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StartGame() => SceneManager.LoadScene("SampleScene");

    public void StartTutorial() => SceneManager.LoadScene("TutorialScene");

    public void OpenHelp() => HelpPanelController.OpenOrCreate();

    public void ExitGame() => Application.Quit();
}
