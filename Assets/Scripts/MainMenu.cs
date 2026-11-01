using UnityEngine;

// Controls the three screens of the Main Menu scene:
// safety warning -> menu -> how to use
public class MainMenu : MonoBehaviour
{
    [SerializeField] GameObject safetyPanel;
    [SerializeField] GameObject menuPanel;
    [SerializeField] GameObject howToPanel;

    // Stays true until the app is closed, so the warning is not shown again
    // every time the learner returns from a lesson.
    static bool safetyAccepted;

    void Start()
    {
        Show(safetyAccepted ? menuPanel : safetyPanel);
    }

    void Update()
    {
        // Android back button (Escape key)
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (howToPanel.activeSelf) Show(menuPanel);
        else if (menuPanel.activeSelf) Application.Quit();
    }

    // ---- Called by the buttons (On Click) ----

    public void AcceptSafety()
    {
        safetyAccepted = true;
        Show(menuPanel);
    }

    public void ShowMenu() => Show(menuPanel);

    public void ShowHowTo() => Show(howToPanel);

    // ----

    void Show(GameObject panel)
    {
        safetyPanel.SetActive(panel == safetyPanel);
        menuPanel.SetActive(panel == menuPanel);
        howToPanel.SetActive(panel == howToPanel);
    }
}