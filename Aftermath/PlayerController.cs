using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed, walkRange;
    public GameObject display, shadowDisplay;
    public LayerMask whatIsInteractive;

    public Animator myAnim, shadowAnim;

    private bool isFacingRight;

    [HideInInspector]
    public NavMeshAgent nav;

    private Vector3 newPos;
    private RuntimePlatform inputMethod;

    //Don't let the player leave the scene without talking to Robin
    private bool hasTalked = false;
    private bool hasPassedGrave = false;

    // Use this for initialization
    void Start()
    {
        newPos = transform.position;
        nav = GetComponent<NavMeshAgent>();
        isFacingRight = true;
        //inputMethod = GameManager.Instance.GetPlatform();
    }

    // Update is called once per frame

    void Update()
    {
        /*
         * if (Vector3.Distance(transform.position, newPos) >= walkRange)
        {
            transform.position = Vector3.MoveTowards(transform.position, newPos, Time.deltaTime * moveSpeed);
        }
        */

        /*if (nav.destination != newPos && newPos != null)
            nav.destination = newPos;*/
        //Touch input

        if (nav.remainingDistance<0.1f)
        {
            myAnim.SetBool("isWalking", false);
            shadowAnim.SetBool("isWalking", false);
        }

        if (Input.touches.Length > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                Ray ray = Camera.main.ScreenPointToRay(touch.position);

                ControlMe(ray);
            }
        }

        //Mouse input
        else
        {
            if (Input.GetMouseButton(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                //RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                ControlMe(ray);
            }
        }

        /* void OnTriggerEnter(Collider other)
         {
             if (other.gameObject.tag == "Interactive")
                 newPos = transform.position;
         }*/
    }
    void ControlMe(Ray ray)
    {
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, whatIsInteractive))
        {
            GameObject interactedObject = hit.transform.gameObject;
            if (interactedObject.tag == "Ground")
            {
                nav.stoppingDistance = 0f;
                nav.destination = hit.point;
                myAnim.SetBool("isWalking", true);
                shadowAnim.SetBool("isWalking", true);
            }
            else if (interactedObject.tag == "Interactive")
            {
                if (interactedObject.GetComponent<Interactable>() != null)
                {
                    interactedObject.GetComponent<Interactable>().MoveToInteractable(nav);
                    myAnim.SetBool("isWalking", true);
                    shadowAnim.SetBool("isWalking", true);
                }
            }
            else if (interactedObject.tag=="Robin")
            {
                hasTalked = true;
                interactedObject.GetComponent<Robin>().EnterStateWalk();
				if (interactedObject.GetComponent<Interactable>() != null)
				{
					interactedObject.GetComponent<Interactable>().MoveToInteractable(nav);
					myAnim.SetBool("isWalking", true);
					shadowAnim.SetBool("isWalking", true);
				}
            }
            CheckFlip();
        }
    }

    public float AngleDir(Vector3 from)
    {
        float dir = from.x - gameObject.transform.position.x;
        if (dir > 0.0f)
        {
            return 1.0f;
        }
        else if (dir < 0.0f)
        {
            return -1.0f;
        }
        else
        {
            return 0.0f;
        }
    }

    public virtual void Flip()
    {
        isFacingRight = !isFacingRight;
        display.GetComponent<SpriteRenderer>().flipX = !display.GetComponent<SpriteRenderer>().flipX;
        shadowDisplay.GetComponent<SpriteRenderer>().flipX = !shadowDisplay.GetComponent<SpriteRenderer>().flipX;
    }

    public void CheckFlip()
    {
        //handle flipping
        //float playerIsAt = AngleDir(rb2d.velocity.normalized, nav.destination - transform.position, transform.up);
        float destinationAt = AngleDir(nav.destination);
        //Debug.Log(playerIsAt);
        if (destinationAt == 1.0f && !isFacingRight)
        {
            Flip();
        }
        else if (destinationAt == -1.0f && isFacingRight)
        {
            Flip();
        }
    }

    public bool GetTalked()
    {
        return hasTalked;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Graves")
        {
            hasPassedGrave = true;
        }
    }

    public bool CheckGrave()
    {
        return hasPassedGrave;
    }
}
