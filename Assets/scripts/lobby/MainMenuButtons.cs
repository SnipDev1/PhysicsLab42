using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuButtons : MonoBehaviour
{
    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureSettingsButton();
    }

    public void StartGame() => SceneManager.LoadScene("SampleScene");

    public void StartTutorial() => SceneManager.LoadScene("TutorialScene");

    public void OpenHelp() => HelpPanelController.OpenOrCreate();

    public void OpenSettings() => SettingsPanelController.OpenOrCreate();

    public void ExitGame() => Application.Quit();

    private void EnsureSettingsButton()
    {
        var menuCard = GameObject.Find("Canvas/MenuCard") ?? GameObject.Find("MenuCard");
        if (menuCard != null && menuCard.transform.Find("SettingsButton") == null)
        {
            var btn = MainMenuUiFactory.CreateButton(
                menuCard.transform, "SettingsButton", "Настройки", 24f,
                OpenSettings, true, MainMenuUiFactory.TextPrimary);

            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 65f;

            var exitBtn = menuCard.transform.Find("ExitButton");
            if (exitBtn != null)
                btn.transform.SetSiblingIndex(exitBtn.GetSiblingIndex());

            if (menuCard.TryGetComponent<RectTransform>(out var rt))
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 570f);
            }
        }
    }
}
