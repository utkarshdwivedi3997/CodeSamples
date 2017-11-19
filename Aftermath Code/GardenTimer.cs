using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class GardenTimer : MonoBehaviour {
    public GameObject player;
    public float explorationTime = 120f;

    public GameObject farmer, otherFarmer;

    //UI stuff
    public GameObject timerPanel;
    public Text timerText;
    public Image timerImage;

    //Farmer's dialogue
    private bool timerStarted = false;
    private float currTime;
	// Use this for initialization
	void Start () {
        farmer.SetActive(false);
        otherFarmer.SetActive(false);
        currTime = explorationTime;
        timerPanel.SetActive(false);
	}
	
	// Update is called once per frame
	void Update () {

        if (timerStarted)
        {
            if (currTime >= 0f)
            {
                currTime -= Time.deltaTime;
                float minutes = (int)(currTime / 60);
                float seconds = (int)(currTime % 60);
                timerText.text = minutes.ToString() + ":" + seconds.ToString();
                timerImage.fillAmount = currTime / explorationTime;
                //Debug.Log(currTime);
            }
            else
            {
                StartCoroutine(DeactivateTimer());
            }
        }
		
	}

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag=="Player")
        {
            timerStarted = true;
            timerPanel.SetActive(true);

            //stop the player from resetting the timer by entering the trigger again
            GetComponent<BoxCollider>().enabled = false;
            //StartCoroutine(Timer());
        }
    }

    IEnumerator DeactivateTimer()
    {
        timerPanel.GetComponent<Animator>().SetTrigger("Deactivate");
        timerStarted = false;
        if (player.GetComponent<PlayerController>().CheckGrave())
        {
            farmer.SetActive(true);
        }
        else
        {
            otherFarmer.SetActive(true);
        }
        yield return new WaitForSeconds(5f);
        timerPanel.SetActive(false);
        
    }
}
