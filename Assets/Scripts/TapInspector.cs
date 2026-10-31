using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Put this on the content root. Tapping an object with an Inspectable shows its details.
public class TapInspector : MonoBehaviour
{
    [SerializeField] TMP_Text infoText;      // UI text that shows the details
    [SerializeField] GameObject infoPanel;   // optional: the panel behind the text
    [SerializeField] string defaultText = "Tap a phase to see its goal and deliverable.";

    Camera cam;

    void OnEnable()
    {
        if (infoPanel != null) infoPanel.SetActive(true);
        if (infoText != null) infoText.text = defaultText;
    }

    void Update()
    {
        if (Input.touchCount == 0) return;
        Touch t = Input.GetTouch(0);
        if (t.phase != TouchPhase.Began) return;
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(t.fingerId)) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(t.position);
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            var item = hit.collider.GetComponentInParent<Inspectable>();
            if (item != null)
            {
                Inspectable.Selected = item;
                if (infoText != null)
                    infoText.text = "<b>" + item.title + "</b>\n" + item.description;
            }
        }
    }
}
