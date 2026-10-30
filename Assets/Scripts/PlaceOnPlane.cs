using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Put this on the XR Origin object (next to AR Raycast Manager)
[RequireComponent(typeof(ARRaycastManager))]
public class PlaceOnPlane : MonoBehaviour
{
    [SerializeField] GameObject content;         // the content root (scene object)
    [SerializeField] GameObject controlsPanel;   // lesson UI, shown after placement
    [SerializeField] GameObject scanHint;        // "scan the floor / tap to place" text

    ARRaycastManager raycaster;
    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    bool placed;

    void Awake() => raycaster = GetComponent<ARRaycastManager>();

    void Start()
    {
        content.SetActive(false);
        controlsPanel.SetActive(false);
        if (scanHint != null) scanHint.SetActive(true);
    }

    void Update()
    {
        if (Input.touchCount == 0) return;
        Touch t = Input.GetTouch(0);
        if (t.phase != TouchPhase.Began) return;

        // Ignore taps that landed on a UI button
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(t.fingerId)) return;

        // After placing: a tap on the 3D content belongs to the lesson (tap-to-inspect),
        // not to re-placement. Tap an empty part of the floor to move the content.
        if (placed && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(t.position);
            if (Physics.Raycast(ray, out RaycastHit hit, 10f) &&
                hit.collider.GetComponentInParent<ARPlane>() == null)
                return;
        }

        if (raycaster.Raycast(t.position, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose pose = hits[0].pose;
            content.transform.SetPositionAndRotation(pose.position, pose.rotation);
            content.SetActive(true);        // activate content first so its Awake runs
            controlsPanel.SetActive(true);
            if (scanHint != null) scanHint.SetActive(false);
            placed = true;
        }
    }
}
