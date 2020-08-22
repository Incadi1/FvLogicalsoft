
using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    Transform cam;

    void Awake()
    {
        cam = Camera.main.transform;

        enabled = false;
    }

    void LateUpdate()
    {
        transform.forward = cam.forward;
    }

    void OnBecameVisible() { enabled = true; }
    void OnBecameInvisible() { enabled = false; }
}
