using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FarmerScript : MonoBehaviour {
    private Camera mainCamera;
	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            Camera.main.GetComponent<CameraController>().SetCaught(gameObject);
            StartCoroutine(Caught());
        }
    }

    IEnumerator Caught()
    {
        yield return new WaitForSeconds(1.5f);
        StartCoroutine(LoadNextScene.Instance.NextLevel(3f, "Caught"));
    }
}
