using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class s_parent_Trap : MonoBehaviour {
    private int ownerNumber = 0;
    public enum TRAPS { NONE, DECOY, SLOWDOWN, STOP };
    public LayerMask whereToStop;
    private TRAPS trapType;

    private bool lookForSurface = false;
    private Rigidbody myRb;

    [SerializeField]
    private GameObject trapMesh;

    // Use this for initialization
    public virtual void Start () {
        myRb = GetComponent<Rigidbody>();
        lookForSurface = true;
	}
	
	// Update is called once per frame
	public virtual void Update () {
        if (trapType != TRAPS.DECOY && lookForSurface)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.2f, whereToStop, QueryTriggerInteraction.Ignore);
            if (hitColliders.Length > 0)
            {
                myRb.isKinematic = true;
                myRb.useGravity = false;
            }
        }
	}



    public virtual void SetOwner(int playerNumber)
    {
        ownerNumber = playerNumber;
    }

    public virtual int GetOwner()
    {
        return ownerNumber;
    }

    public virtual void SetTrapType(s_parent_Trap.TRAPS type)
    {
        trapType = type;
    }

    public virtual TRAPS GetTrapType()
    {
        return trapType;
    }

    public virtual GameObject GetTrapMesh()
    {
        return trapMesh;
    }
}
