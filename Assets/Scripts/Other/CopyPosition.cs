// Este componente copia la posición de una Transformación para seguirla automáticamente, 
// lo cual es útil para la cámara.
using UnityEngine;

public class CopyPosition : MonoBehaviour
{
    public bool x, y, z;
    public Transform target;

    void Update()
    {
        if (!target) return;

        transform.position = new Vector3(
            (x ? target.position.x : transform.position.x),
            (y ? target.position.y : transform.position.y),
            (z ? target.position.z : transform.position.z));
    }
}
