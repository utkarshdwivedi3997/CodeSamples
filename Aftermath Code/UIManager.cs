using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; set; }

    public GameObject UIPanel;
    public Text cluesCollectedText;

    private GameObject[] interactables;
    private int totalInteractables = 0;
    private int numInteracted = 0;
    // Use this for initialization
    void Start()
    {
        //make sure only one instance of the UIManager exists
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }

        interactables = GameObject.FindGameObjectsWithTag("Interactive");
        totalInteractables = interactables.Length;

        //Update the text at start of level
        cluesCollectedText.text = "Interactions: " + numInteracted.ToString() + " / " + totalInteractables.ToString();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void UpdateText()
    {
        cluesCollectedText.text = "Interactions: " + numInteracted.ToString() + " / " + totalInteractables.ToString();
        //Debug.Log("updated");
    }

    public void AddInteraction()
    {
        if (numInteracted < totalInteractables)
        {
            numInteracted++;
            UpdateText();
        }
    }
}
