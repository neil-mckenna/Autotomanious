using UnityEngine;
using TMPro;

public class UITest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== UI TEST ===");

        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        Debug.Log($"Found {texts.Length} TextMeshPro objects");

        foreach (TextMeshProUGUI text in texts)
        {
            Debug.Log($"Text: '{text.name}' = '{text.text}'");
            text.text = "TEST"; // Temporarily change text to see if it updates
        }
    }
}
