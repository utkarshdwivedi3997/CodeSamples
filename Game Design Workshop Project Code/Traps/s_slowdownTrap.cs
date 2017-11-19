using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class s_slowdownTrap : s_parent_Trap {

    public GameObject hitPlayerParticle;
	// Use this for initialization
	public override void Start () {
        SetTrapType(TRAPS.SLOWDOWN);
        base.Start();
	}
	
	// Update is called once per frame
	public override void Update () {
        base.Update();
	}

    public override void SetOwner(int playerNumber)
    {
        base.SetOwner(playerNumber);
    }

    public override int GetOwner()
    {
        return base.GetOwner();
    }

    public override TRAPS GetTrapType()
    {
        return base.GetTrapType();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag=="Player")
        {
            if (other.gameObject.GetComponent<s_playerController>().GetPlayerNumber() != GetOwner())
            {
                other.gameObject.GetComponent<s_playerController>().TriggerTrap(GetTrapType());
                s_roundManager.Instance.DecreasePlayerTrapsDeployed(GetOwner());
                Destroy(gameObject);
            }
        }
    }
}
