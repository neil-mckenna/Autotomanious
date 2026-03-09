using UnityEngine;
using UnityEngine.UI;

public class QuitButton : MonoBehaviour
{
    [SerializeField] private Button quitButton;

    void Start()
    {
        if (quitButton == null)
            quitButton = GetComponent<Button>();

        quitButton.onClick.AddListener(QuitGame);
    }

    // exit the game on exe and in editor
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
