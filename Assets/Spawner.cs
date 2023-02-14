using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public int count = 1000;
    private int currentCount;
    public GameObject prefab;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (currentCount < count) {
            var obj = Instantiate(prefab, transform);
            obj.transform.localPosition = new Vector3(0.15f * (currentCount%10), 0, 0);
            currentCount++;
        }
    }
}
