using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Put this on a Button. Pressing it loads the scene with the given name.
// Tick "Also On Back Key" on the Menu button inside the lessons so the
// Android back button returns to the Main Menu too.
[RequireComponent(typeof(Button))]
public class SceneButton : MonoBehaviour
{
    [SerializeField] string sceneName;
    [SerializeField] bool alsoOnBackKey;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Load);
    }

    void Update()
    {
        if (alsoOnBackKey && Input.GetKeyDown(KeyCode.Escape)) Load();
    }

    public void Load()
    {
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }
}
