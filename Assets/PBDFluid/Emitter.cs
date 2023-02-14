using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Emitter : MonoBehaviour
{
    public Bounds bounds;


    // Start is called before the first frame update
    void Start() {
        bounds.center = transform.position;
        bounds.size = transform.lossyScale;
    }

    private void OnDrawGizmos() {
        bounds.center = transform.position;
        bounds.size = transform.lossyScale;
        Gizmos.color = new Color(0, 0, 1, 0.5f);
        Gizmos.DrawCube(bounds.center, bounds.size);
    }
}
