using UnityEngine;

// Put this on the number label so it always faces the phone camera
public class FaceCamera : MonoBehaviour
{
    Camera cam;

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
