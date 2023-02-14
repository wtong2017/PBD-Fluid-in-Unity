using PBDFluid;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class Barrier : MonoBehaviour
{
    public Bounds bounds;
    public int id;


    // Start is called before the first frame update
    void Start()
    {
        bounds.center = transform.position;
        bounds.size = transform.lossyScale;
    }

    private void OnDrawGizmos() {
        bounds.center = transform.position;
        bounds.size = transform.lossyScale;
        Gizmos.color = new Color(1, 1, 1, 0.5f);
        Gizmos.DrawCube(bounds.center, bounds.size);
    }
}
