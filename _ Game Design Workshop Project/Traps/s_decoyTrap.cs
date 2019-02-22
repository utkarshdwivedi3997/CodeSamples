using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class s_decoyTrap : s_parent_Trap {

	// Use this for initialization
	public override void Start () {
        SetTrapType(TRAPS.DECOY);
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
}
