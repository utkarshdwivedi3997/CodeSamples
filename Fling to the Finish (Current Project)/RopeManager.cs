using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class RopeManager : MonoBehaviourPun
{

    [Header("Rope Adjustments")]
    public float ropeLength = 3f;
    public float maxRopeLength = 7f;
    public float elasticityConstant = 10f;
    public float pullConstant = 35f;
    public AnimationCurve tugCurve = AnimationCurve.Linear(0f,0f,6.00f,900f);

    private ConfigurableJoint[] linkJoints;

    [Header("References")]
    public PlayerInput vibrationJoystick;   
    public Transform[] links;
    public Transform[] ropeJoints;
    public Transform[] cutJoints;
    private CapsuleCollider[] linkColliders;
    private Rigidbody[] linkRBs;
    private Transform[] linkDisplay;
    public Rigidbody[] partnerRB;
    private SphereCollider[] partnerColliders = new SphereCollider[2];
    public Transform[] partnerModelHolders;
    public RopeColor myRopeColor;
    private ConfigurableJoint midLink;
    private Rigidbody midConnectedBody;
    public Rigidbody tempBody;
    public Rigidbody anvil;
    public SolidRopeLink[] solidRopeLinks;

    [Header("Fling Feedback")]
    public GameObject[] strainParticles;
    public GameObject[] flingTrail;

    public AnimationCurve flingTrailSize;

    public GameObject[] perfectFling;

    [Header("Arrow")]
    public GameObject tugArrow;
    public ArrowColor myArrowColor;
    private float arrowTime = 1.0f;

    [Header("Rope Types")]
    public GameObject attachedRope;
    public GameObject cutRope;
    private bool isRopeCut;
    /// <summary>
    /// The current joined/cut status of the rope
    /// </summary>
    public bool IsRopeCut { get { return isRopeCut; } }

    [Header("AudioPlayers")]
    public GenericSoundPlayer whipSoundPlayer;
    public GenericSoundPlayer flingSoundPlayer;
    public GenericSoundPlayer cutSoundPlayer;

    [Header("ParticlePlayers")]
    public ParticleSystem cutParticles;

    private bool pullReady = true;
    private float pullTime = 1.3f;
    private float pullTimer = 0.0f;

    private float partnerDistance;
    private Vector3 forceToBeApplied;
    private Vector3 newVel;

    private float averageLinkDistance;

    private int teamNumber;
    public int TeamNumber
    {
        get { return teamNumber; }
        set { teamNumber = value; }
    }
    public void SetTeamNumber(int teamNum)
    {
        TeamNumber = teamNum;
        LateStart();
    }

    private int partnerNum;
    private bool isStraining;
    private float strainShakeRot = 25f;
    private float strainShakeScale = 0.3f;
    private float characterSavedDrag;
    private float characterStrainDrag = 10f;
    private float characterSavedAngularDrag;
    private float characterStrainAngularDrag = 10f;
    private float linkSavedDrag;
    private float linkStrainDrag = 5f;
    private float anvilSavedMass;
    private float anvilSavedDrag;
    private float anvilStrainMass = 2f;
    private float anvilStrainDrag = 5f;

    private bool debugRope = false;

    public bool useNewFlingDirection = true;
    public AnimationCurve flingDirectionAngleCurve;

    private bool isRopeSolid = false;
    private float solidSpringForce = 100f;

    void Awake()
    {
        // Please always use an odd number of links
        int mid = links.Length / 2;
        midLink = links[mid].GetComponent<ConfigurableJoint>();
        midConnectedBody = links[mid - 1].GetComponent<Rigidbody>();
    }

    // Use this for initialization
    void Start()
    { 
        // Game starts with the rope connected
        attachedRope.SetActive(true);
        cutRope.SetActive(false);
        tempBody.gameObject.SetActive(false);

        linkColliders = new CapsuleCollider[links.Length];
        linkDisplay = new Transform[links.Length];
        linkRBs = new Rigidbody[links.Length];

        partnerColliders[0] = partnerRB[0].GetComponent<SphereCollider>();
        partnerColliders[1] = partnerRB[1].GetComponent<SphereCollider>();

        for (int i = 0; i < links.Length; i++)
        {
            linkColliders[i] = links[i].GetComponent<CapsuleCollider>();
            linkDisplay[i] = links[i].transform.GetChild(0);
            linkRBs[i] = links[i].GetComponent<Rigidbody>();

            Physics.IgnoreCollision(linkColliders[i], partnerColliders[0], true);
            Physics.IgnoreCollision(linkColliders[i], partnerColliders[1], true);
        }

        for (int i = 0; i < links.Length - 1; i++)
        {
            for (int u = i+1; u < links.Length; u++)
            {
                Physics.IgnoreCollision(linkColliders[i], linkColliders[u], true);
            }
        }

        strainParticles[0].SetActive(false); strainParticles[1].SetActive(false);
        flingTrail[0].SetActive(false); flingTrail[1].SetActive(false);
    }
    void LateStart()
    {
        
        for (int i = 0; i < links.Length; i++)
        {
            if (teamNumber == 1)
            {
                links[i].gameObject.layer = 14;
            }
            else
            {
                links[i].gameObject.layer = 15;
            }
        }
        

        strainParticles[0].SetActive(false); strainParticles[1].SetActive(false);
        flingTrail[0].SetActive(false); flingTrail[1].SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyUp("r"))
        {
            ToggleRopeDebug();
        }

        if (pullReady)
        {
            
        }
        else
        {
            pullTimer -= Time.deltaTime;
            if (pullTimer < 0.0f)
                pullReady = true;
        }

        averageLinkDistance = (Vector3.Distance(links[2].position, links[4].position) + Vector3.Distance(links[8].position, links[10].position)) / 2.0f;

        myRopeColor.UpdateRopeColor(averageLinkDistance);
        UpdateLinkColliders(averageLinkDistance);

        if (isRopeCut)
            UpdateCutRopeJoints();
        else
            UpdateRopeJoints();

        // =============== MOVE THIS TO TRIGGER LISTENER ONCE LASER IS FUNCTIONAL ===============
        /*if (Input.GetKeyDown(KeyCode.C))
        {
            if (!isRopeCut)
                GameManager.Instance.CutRope(teamNumber);
            else
                GameManager.Instance.JoinRope(teamNumber);

            //isRopeCut = !isRopeCut;
        }*/
    }

    void UpdateRopeJoints()
    {
        for (int i = 0; i < ropeJoints.Length; i++)
        {

            if (i == 0)
            {
                ropeJoints[i].position = partnerRB[0].transform.position;
                ropeJoints[i].LookAt(links[0].transform.position);
            }
            else if (i < ropeJoints.Length - 2)
            {
                ropeJoints[i].position = links[i-1].transform.position;
                ropeJoints[i].LookAt(links[i].transform.position);
            }
            else if (i < ropeJoints.Length - 1)
            {
                ropeJoints[i].position = links[i - 1].transform.position;
                ropeJoints[i].LookAt(partnerRB[1].transform.position);
            }
            else
            {
                ropeJoints[i].position = partnerRB[1].transform.position;
                ropeJoints[i].rotation = ropeJoints[i - 1].rotation;
            }
        }
    }
    void UpdateCutRopeJoints()
    {
        for (int i = 0; i < cutJoints.Length; i++)
        {

            if (i == 0)
            {
                cutJoints[i].position = partnerRB[0].transform.position;
                cutJoints[i].LookAt(links[0].transform.position);
            }
            else if (i < ropeJoints.Length - 2)
            {
                cutJoints[i].position = links[i - 1].transform.position;
                cutJoints[i].LookAt(links[i].transform.position);
            }
            else if (i < ropeJoints.Length - 1)
            {
                cutJoints[i].position = links[i - 1].transform.position;
                cutJoints[i].LookAt(partnerRB[1].transform.position);
            }
            else
            {
                cutJoints[i].position = partnerRB[1].transform.position;
                cutJoints[i].rotation = cutJoints[i - 1].rotation;
            }
        }
    } 
    void UpdateLinkColliders(float distance)
    {
        
        float newHeight = 0.85f;
        if (distance < 0.82f)
        {
            newHeight = 0.85f;
        }
        else if(distance < 1.1f){
            distance = Mathf.InverseLerp(0.82f, 1.1f, distance);
            newHeight = Mathf.Lerp(0.85f, 1.7f, distance);
        }
        else 
        {
            newHeight = 1.7f;
        }
        for (int i = 0; i < links.Length; i++)
        {
            linkColliders[i].height = newHeight;
            linkDisplay[i].localScale = new Vector3(0.29f, newHeight / 2f, 0.29f);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // make the temporary rigidbody follow the midlink if the rope is connected
        if (!isRopeCut)
        {
            tempBody.transform.position = midLink.transform.position;

            if (isRopeSolid)
            {
                /*
                Vector3 midPoint = (partnerRB[0].transform.position + partnerRB[1].transform.position) / 2f;
                //Vector3 partnerDifference = partnerRB[0].transform.position - partnerRB[1].transform.position;
                Vector3 midDifference = linkRBs[6].position - midPoint;
                linkRBs[6].AddForce((midPoint - linkRBs[6].position) * 300);

                partnerRB[0].AddForce((midDifference) * 200);
                partnerRB[1].AddForce((midDifference) * 200);
                //linkRBs[6].position = midpoint;
                */
                Vector3 partnerDifference = partnerRB[0].transform.position - partnerRB[1].transform.position;
                foreach (SolidRopeLink links in solidRopeLinks)
                {
                    links.ApplyRopeForces(partnerDifference);

                }

            }
        }

        if (isStraining)
        {
            // foreach(Rigidbody body in linkRBs)
            // {
            //     if (body.GetComponent<PhotonView>().IsMine) {
            //         body.AddForce(Vector3.up * 30f, ForceMode.Force);
            //     }
            //     else {
            //         FlingRigidbody(body.GetComponent<PhotonView>().ViewID);
            //     }
            // }

            for (int i = 0; i < linkRBs.Length; i++) {
                Rigidbody body = linkRBs[i];
                FlingLinkRBAtIndex(i);

                /*if (PhotonNetwork.OfflineMode || body.GetComponent<PhotonView>().IsMine) {
                    body.AddForce(Vector3.up * 30f, ForceMode.Force);
                }
                else {
                    FlingLinkRBAtIndex(i);
                    body.AddForce(Vector3.up * 30f, ForceMode.Force);
                }*/
            }
            
            anvil.AddForce(Vector3.up * 100f, ForceMode.Force);

            partnerModelHolders[partnerNum].localRotation = Quaternion.identity;
            partnerModelHolders[partnerNum].Rotate(new Vector3(Random.Range(-strainShakeRot, strainShakeRot), Random.Range(-strainShakeRot, strainShakeRot), Random.Range(-strainShakeRot, strainShakeRot)));
            partnerModelHolders[partnerNum].localScale = new Vector3(1.00f + Random.Range(-strainShakeScale, strainShakeScale), 1.00f + Random.Range(-strainShakeScale, strainShakeScale), 1.00f + Random.Range(-strainShakeScale, strainShakeScale));
        }
    }

    /// <summary>
    /// Flings a link's Rigidbody at index (the rigidbody is grabbed from the linkRBs array)
    /// </summary>
    /// <param name="linkIndex">Index of the rigidbody to be flung</param>
    private void FlingLinkRBAtIndex(int linkIndex) {
        
        Rigidbody body = linkRBs[linkIndex];
        if (!PhotonNetwork.OfflineMode && !body.GetComponent<PhotonView>().IsMine)
        {            
            // Stuff to do on all clients
            photonView.RPC("FlingLinkRBAtIndexRPC", linkRBs[linkIndex].GetComponent<PhotonView>().Owner, linkIndex);
        }

        // Fling this client's rigidbody in all scenarios
        // We fling the RB on both the owner and the partner client even in case of two clients in one team for consistency reasons
        body.AddForce(Vector3.up * 30f, ForceMode.Force);
    }

    /// <summary>
    /// RPC for the FlingLinkRBAtIndex() function.
    /// </summary>
    /// <param name="linkIndex"></param>
    [PunRPC]
    private void FlingLinkRBAtIndexRPC(int linkIndex) {

        Rigidbody body = linkRBs[linkIndex];
        body.AddForce(Vector3.up * 30f, ForceMode.Force);
    }

	public void MakeRopeSolid(bool state)
    {
		isRopeSolid = state;
        PoofPooler.Instance.SpawnFromPool("SpherePoof", midLink.transform.position, 40);
        myRopeColor.SetRopeMaterialSolid (state);

    }
    void ToggleRopeDebug()
    {
        debugRope = !debugRope;

        foreach (Transform link in linkDisplay)
        {
            link.GetComponent<MeshRenderer>().enabled = debugRope;
        }
    }

        //Called from PlayerInput. PlayerInput sends the player's number so that we know who is pulling 
    public void InputFling(int playerNum)
    {
        if (!isRopeCut)                     // if rope is cut, tug should not work
        {
            if (pullReady)
            {
                pullReady = false;
                pullTimer = pullTime;

                if (playerNum == 1 || playerNum == 3)
                {
                    partnerNum = 0;
                    vibrationJoystick.Rumble(0, 0.4f, 0.3f);
                }
                else
                {
                    partnerNum = 1;
                    vibrationJoystick.Rumble(1, 0.4f, 0.3f);
                }

                StartCoroutine(StrainFling(partnerNum, true));
                /*if (!PhotonNetwork.OfflineMode && TeamManager.Instance.IsTeamSharedOnline(TeamNumber))
                {
                    Debug.Log("Online with two clients sharing this team");
                    
                    photonView.RPC("StrainFlingOnPartnerClientRPC", partnerRB[1 - partnerNum].GetComponent<PhotonView>().Owner, partnerNum);
                }*/
                PlayWhipSound();
                if (!PhotonNetwork.OfflineMode)
                {
                    photonView.RPC("PlayWhipSound", RpcTarget.Others);
                }
            }
        }
    }

    [PunRPC]
    private void PlayWhipSound()
    {
        whipSoundPlayer.EmitSound();
    }

    [PunRPC]
    private void StrainFlingOnPartnerClientRPC(int flingerIndex)
    {
        StartCoroutine(StrainFling(flingerIndex, false));
    }
    IEnumerator StrainFling (int flingerIndex, bool shouldCallFlingFunction)
    {
        isStraining = true;
        //strainParticles[partnerNum].SetActive(true);
        StrainParticlesDisplayStatus(partnerNum, true);
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("StrainParticlesDisplayStatus", RpcTarget.Others, partnerNum, true);
        }
        characterSavedDrag = partnerRB[partnerNum].drag;
        characterSavedAngularDrag = partnerRB[partnerNum].angularDrag;

        /*foreach (Rigidbody body in partnerRB)
        {
            body.drag = characterStrainDrag;
            body.angularDrag = characterStrainAngularDrag;
        }*/
        for (int i = 0; i < partnerRB.Length; i++)
        {
            SetPlayerRBDrag(i, characterStrainDrag, characterStrainAngularDrag);
        }
        linkSavedDrag = linkRBs[0].drag;
        /*foreach (Rigidbody body in linkRBs)
        {
            body.drag = linkStrainDrag;
        }*/
        for (int i = 0; i < linkRBs.Length; i++)
        {
            SetLinkRBDrag(i, linkStrainDrag);
        }

        anvilSavedDrag = anvil.drag;
        anvilSavedMass = anvil.mass;
        anvil.mass = anvilStrainMass;
        anvil.drag = anvilStrainDrag;
        yield return new WaitForSeconds(0.3f);

        //strainParticles[partnerNum].SetActive(false);
        StrainParticlesDisplayStatus(partnerNum, false);
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("StrainParticlesDisplayStatus", RpcTarget.Others, partnerNum, false);
        }
        isStraining = false;
        partnerModelHolders[partnerNum].localRotation = Quaternion.identity;
        partnerModelHolders[partnerNum].localScale = Vector3.one;

        /*foreach (Rigidbody body in partnerRB)
        {
            body.drag = characterSavedDrag;
            body.angularDrag = characterSavedAngularDrag;
        }*/
        for (int i = 0; i < partnerRB.Length; i++)
        {
            SetPlayerRBDrag(i, characterSavedDrag, characterSavedAngularDrag);
        }
        /*foreach (Rigidbody body in linkRBs)
        {
            body.drag = linkSavedDrag;
        }*/
        for (int i = 0; i < linkRBs.Length; i++)
        {
            SetLinkRBDrag(i, linkSavedDrag);
        }

        anvil.mass = anvilSavedMass;
        anvil.drag = anvilSavedDrag;

        if (shouldCallFlingFunction)
        {
            ForceFling();
        }
    }

    /// <summary>
    /// Sets the drag on a link rigidbody
    /// </summary>
    /// <param name="linkIndex">Index of link in linkRBs array</param>
    /// <param name="drag">Drag to be set to link's RB</param>
    private void SetLinkRBDrag(int linkIndex, float drag)
    {
        Rigidbody body = linkRBs[linkIndex];

        if (!PhotonNetwork.OfflineMode && TeamManager.Instance.IsTeamSharedOnline(TeamNumber - 1))
        {
            // Stuff to do on all clients
            photonView.RPC("SetLinkRBDragRPC", partnerRB[1 - partnerNum].GetComponent<PhotonView>().Owner, linkIndex, drag);
        }

        // Set drag on client's rigidbody in all scenarios
        // We set RB's drag on both the owner and the partner client even in case of two clients in one team for consistency reasons
        body.drag = drag;
    }

    /// <summary>
    /// RPC for the SetLinkRBDrag() function
    /// </summary>
    /// <param name="linkIndex">Index of link in linkRBs array</param>
    /// <param name="drag">Drag to be set to link's RB</param>
    [PunRPC]
    private void SetLinkRBDragRPC(int linkIndex, float drag)
    {
        Rigidbody body = linkRBs[linkIndex];
        body.drag = drag;
    }

    /// <summary>
    /// Sets the drag on a player rigidbody
    /// </summary>
    /// <param name="playerIndex">Index of player in partnerRB array</param>
    /// <param name="drag">Drag value</param>
    /// <param name="angularDrag">Angular drag value</param>
    private void SetPlayerRBDrag(int playerIndex, float drag, float angularDrag)
    {
        Rigidbody body = partnerRB[playerIndex];

        if (!PhotonNetwork.OfflineMode && TeamManager.Instance.IsTeamSharedOnline(TeamNumber - 1))
        {
            // Stuff to do on all clients
            photonView.RPC("SetPlayerRBDragRPC", partnerRB[1 - partnerNum].GetComponent<PhotonView>().Owner, playerIndex, drag, angularDrag);
        }

        // Set drag on client's rigidbody in all scenarios
        // We set RB's drag on both the owner and the partner client even in case of two clients in one team for consistency reasons
        body.drag = drag;
        body.angularDrag = angularDrag;
    }

    /// <summary>
    /// RPC for the SetPlayerRBDrag() function
    /// </summary>
    /// <param name="playerIndex">Index of player in partnerRB array</param>
    /// <param name="drag">Drag value</param>
    /// <param name="angularDrag">Angular drag value</param>
    [PunRPC]
    private void SetPlayerRBDragRPC(int playerIndex, float drag, float angularDrag)
    {
        Rigidbody body = partnerRB[playerIndex];
        body.drag = drag;
        body.angularDrag = angularDrag;
    }

    [PunRPC]
    private void StrainParticlesDisplayStatus(int playerNum, bool active)
    {
        strainParticles[playerNum].SetActive(active);
    }

    void ForceFling()
    {
        Transform flinger;
        Rigidbody flung;
        Vector3 dir;

        Transform activeLink;

        if (partnerNum == 0)
        {
            flinger = partnerRB[0].transform;
            flung = partnerRB[1];
            vibrationJoystick.Rumble(1, 1f, 0.2f);
            activeLink = links[10];
        }
        else
        {
            partnerNum = 1;
            flinger = partnerRB[1].transform;
            flung = partnerRB[0];
            vibrationJoystick.Rumble(0, 1f, 0.2f);
            activeLink = links[2];
        }

        /* ============ New way of handling direction ============ */

        if (useNewFlingDirection)
        {
            Vector3 a = flinger.position - flung.position;      // flung -> flinger
            Vector3 b = midLink.transform.position - flung.position;   // flung -> midlink

            Debug.DrawRay(flung.position, a, Color.green, 1f);
            Debug.DrawRay(flung.position, b, Color.red, 1f);

            // Rotation axis
            Vector3 axis = Vector3.Cross(a, b).normalized;

            // Direction of fling
            float angle = Vector3.Angle(a, b); 

            // If the angle is too small, increase it
            if (angle < 20f)
            {
                angle = flingDirectionAngleCurve.Evaluate(angle);
            }

            dir = b;
            Quaternion rot = Quaternion.AngleAxis(angle, axis);
            dir = (rot * dir).normalized;

            Debug.DrawRay(flung.position, dir * 4f, Color.magenta, 1f, false);
        }
        else
        {
            // ============ Old way of handling direction ============ */
            if (activeLink.position.y > (flung.transform.position.y + 0.15f) && partnerDistance > ropeLength)
                dir = ((activeLink.position - flung.transform.position).normalized + Vector3.up).normalized;

            //otherwise, set the direction to be the direction between the two players
            else
                dir = (activeLink.position - flung.transform.position).normalized;
        }

        
        float distance = Vector3.Distance(links[6].transform.position, flung.transform.position);


        float forceMagnitude = tugCurve.Evaluate(distance);

        if (forceMagnitude > 650f)
        {
            OnPerfectFlingVisualsAndSound(1 - partnerNum);
            if (!PhotonNetwork.OfflineMode)
            {
                photonView.RPC("OnPerfectFlingVisualsAndSound", RpcTarget.Others, 1 - partnerNum);
            }
        }
        

        //float forceMagnitude = 8f * pullConstant * distance;
        //Clamp the force so it's magnitude never exceeds a maximum force,
        //which is determined by the maximum length that the rope can be stretched
        //forceMagnitude = Mathf.Clamp(forceMagnitude, 0f, 4f * 6f * pullConstant);

        forceToBeApplied = dir * forceMagnitude;

        //if (forceMagnitude>500f)
            StartCoroutine(PlayFlingTrail(1 - partnerNum, forceMagnitude));
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("PlayFlingTrailRPC", RpcTarget.Others, 1 - partnerNum, forceMagnitude);
        }

        ApplyTugArrow(flung.transform, dir, forceMagnitude);
        FlingPlayerRB(1 - partnerNum, forceToBeApplied);

        /*if (PhotonNetwork.OfflineMode || flung.GetComponent<PhotonView>().IsMine) {
            flung.AddForce(forceToBeApplied, ForceMode.Acceleration);
        }
        else {
            FlingPlayerRigidbody(partnerNum, forceToBeApplied);
            flung.AddForce(forceToBeApplied, ForceMode.Acceleration);
        }*/

        //Debug.LogWarning("Distance: " + distance + ", Force: " + forceMagnitude);

        //Debug.Log("New Link Based Tug Force: " + forceMagnitude);
        //Debug.Log(currLength);
        //Debug.DrawRay(partner.transform.position, dir, Color.cyan, 0.3f);
    }

    /// <summary>
    /// Fling a player's rigidbody at index flungIndex
    /// </summary>
    /// <param name="flungIndex">Index of player to be flung</param>
    /// <param name="forceToBeApplied">Force to be applied while flinging</param>
    private void FlingPlayerRB(int flungIndex, Vector3 forceToBeApplied) {
        Rigidbody flung = partnerRB[flungIndex];

        if (!PhotonNetwork.OfflineMode && TeamManager.Instance.IsTeamSharedOnline(TeamNumber - 1))
        {            
            // Stuff to do on all clients
            photonView.RPC("FlingPlayerRBRPC", partnerRB[1 - partnerNum].GetComponent<PhotonView>().Owner, flungIndex, forceToBeApplied.x, forceToBeApplied.y, forceToBeApplied.z);
        }

        // Add force on this client in all scenarios
        // We fling the RB on both the owner and the partner client even in case of two clients in one team for consistency reasons
        flung.AddForce(forceToBeApplied, ForceMode.Acceleration);
        Debug.Log(flung.name + " is flung in this direction " + forceToBeApplied.ToString());
    }

    /// <summary>
    /// RPC for the FlingPlayerRB() function
    /// </summary>
    /// <param name="flungIndex">Index of player to be flung</param>
    /// <param name="forceX">x component of force to be applied</param>
    /// <param name="forceY">y component of force to be applied</param>
    /// <param name="forceZ">z component of force to be applied</param>
    [PunRPC]
    private void FlingPlayerRBRPC(int flungIndex, float forceX, float forceY, float forceZ) {
        Rigidbody flung = partnerRB[flungIndex];
        Vector3 thisForceToBeApplied = new Vector3(forceX, forceY, forceZ);
        flung.AddForce(thisForceToBeApplied, ForceMode.Acceleration);

        Debug.Log(flung.name + " is flung in this direction " + thisForceToBeApplied.ToString());
    }

    [PunRPC]
    private void PlayFlingTrailRPC(int partnerNum, float force)
    {
        StartCoroutine(PlayFlingTrail(partnerNum, force));
    }

    [PunRPC]
    private void OnPerfectFlingVisualsAndSound(int playerNum)
    {
        flingSoundPlayer.EmitSound();
        //Debug.Log(1 - partnerNum);
        perfectFling[playerNum].SetActive(true);
        perfectFling[playerNum].GetComponent<ParticleSystem>().Play();
    }

    IEnumerator PlayFlingTrail(int partnerNum, float force)
    {
        GameObject trailObj = flingTrail[partnerNum];

        force = Mathf.Clamp(force, 0.1f, 650);
        float normalizedForce = force / 650f;
        //float t = normalizedForce * 1.5f;

        for (int i = 0; i < trailObj.transform.childCount; i++)
        {
            TrailRenderer trail = trailObj.transform.GetChild(i).GetComponent<TrailRenderer>();
            trail.Clear();
            trail.time = Mathf.Clamp(flingTrailSize.Evaluate(normalizedForce) * 2, 0.4f, 1f);
            float a = Mathf.Clamp(flingTrailSize.Evaluate(normalizedForce), 0.2f, 0.5f);
            //trail.widthMultiplier = flingTrailSize.Evaluate(normalizedForce);
            Color tintColor = trail.material.GetColor("_TintColor");
            tintColor = new Color(tintColor.r, tintColor.g, tintColor.b, a);
            trail.material.SetColor("_TintColor", tintColor);
            //Debug.Log(trail.time);
        }
        trailObj.SetActive(true);
        yield return new WaitForSeconds(1f);

        for (int i = 0; i < trailObj.transform.childCount; i++)
        {
            TrailRenderer trail = trailObj.transform.GetChild(i).GetComponent<TrailRenderer>();
            float rate = trail.time / 15f;
            while (trail.time > 0)
            {
                trail.time -= rate;
                yield return 0;
            }
        }

        flingTrail[partnerNum].SetActive(false);
    }


    private void ApplyTugArrow(Transform flung, Vector3 forceDir, float forceMagnitude)
    {
        tugArrow.SetActive(true);
        //move arrow
        tugArrow.transform.position = flung.position + new Vector3(0f, 2f, 0f);
        //point arrow
        tugArrow.transform.rotation = Quaternion.LookRotation(forceDir);
        //scale arrow
        tugArrow.transform.localScale = new Vector3(1f, 1f, forceMagnitude / 500f);
        //color arrow
        myArrowColor.UpdateArrowColor(averageLinkDistance);

        StartCoroutine("UpdateArrow");
    }
    IEnumerator UpdateArrow()
    {
        yield return new WaitForSeconds(arrowTime);
        tugArrow.SetActive(false);
    }

    public void ChangeRopeCutStatus(bool cut)
    {
        int mid = links.Length / 2;
        isRopeCut = cut;
        tempBody.gameObject.SetActive(isRopeCut);
        if (isRopeCut)
        {
            midLink.connectedBody = tempBody;
            attachedRope.SetActive(false);
            cutRope.SetActive(true);

            cutParticles.gameObject.transform.position = tempBody.position;
            cutParticles.Emit(200);
            cutSoundPlayer.EmitSound();

            linkRBs[linkRBs.Length / 2].AddForce(Vector3.up * 30f, ForceMode.Impulse);
            linkRBs[(linkRBs.Length / 2)-1].AddForce(Vector3.up * 30f, ForceMode.Impulse);
        }
        else
        {
            midLink.connectedBody = midConnectedBody;
            attachedRope.SetActive(true);
            cutRope.SetActive(false);
        }
    }

    /* --------------- Public functions accessible by other scripts */

        /// <summary>
        /// Nullifies all forces on the rope RigidBodies
        /// </summary>
    public void NullifyAllForces()
    {
        foreach (Rigidbody link in linkRBs)
        {
            link.velocity = Vector3.zero;
            link.angularVelocity = Vector3.zero;
        }
    }


}

