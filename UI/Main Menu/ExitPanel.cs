using UnityEngine;
using Memori.UI;
using UnityEngine.UI;

namespace TJ.MainMenu
{
public class ExitPanel : MainMenuPanel
{
    [SerializeField] private Button confirmButton;
    public override void SetUp(MainMenu _mainMenu)
    {
        base.SetUp(_mainMenu);
        confirmButton.ClearClickListeners();
        confirmButton.onClick.AddListener(ExitGame);
    }
    public override void OpenPanel()
    {
        base.OpenPanel();
    }
    public override void ClosePanel()
    {
        base.ClosePanel();
    }
    public void ExitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
}
