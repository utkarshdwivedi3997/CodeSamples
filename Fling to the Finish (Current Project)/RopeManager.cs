using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using FMODUnity;

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
    public Transform[] springRopeJoints;
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
    public RopeFlingResetTrigger[] ropeFlingResetTriggers;
    [SerializeField]
    private TeamDragManager teamDragManager;

    [Header("Fling Feedback")]
    public CameraShaker cameraShake;
    public GameObject[] strainParticles;
    public GameObject[] flingTrail;

    public AnimationCurve flingTrailSize;

    public GameObject[] perfectFling;
    public RopeFlashLine ropeFlashLine;
    private bool ropeIsFlashing = true;

    [Header("Arrow")]
    public GameObject tugArrow;
    public ArrowColor myArrowColor;
    private float arrowTime = 1.0f;

    [Header("Rope Types")]
    public GameObject attachedRope;
    public GameObject springRope;
    public GameObject cutRope;
    private bool isRopeCut;
    /// <summary>
    /// The current joined/cut status of the rope
    /// </summary>
    public bool IsRopeCut { get { return isRopeCut; } }

    [Header("Audio")]
    

    [SerializeField, EventRef] private string whipSoundEventPath;
    private FMOD.Studio.EventInstance whipSound;
    [SerializeField, EventRef] private string flingSoundEventPath;
    private FMOD.Studio.EventInstance flingSound;
    [SerializeField, EventRef] private string cutSoundEventPath;
    private FMOD.Studio.EventInstance cutSound;

    [Header("ParticlePlayers")]
    public ParticleSystem cutParticles;

    private bool pullReady = true;
    private float pullTime = 1f;
    private float pullTimer = 0.0f;

    // Limit in-air flinging variables
    [SerializeField]
    private int maxAirFlings = 2;
    private int inAirFlings = 0;
    private int nPlayersClamped = 0;
    private bool subscribedToFlingResetEvents = false;

    private float partnerDistance;
    private float flingModifier = 1.0f;
    private float balloonFlingModifier = 0.2f;
    private float defaultFlingModifier = 1f;
    private Vector3 forceToBeApplied;
    private Vector3 newVel;

    public float averageLinkDistance { get; private set; }
    public int TeamIndex { get; set; }
    public void SetTeam(int teamIndex)
    {
        TeamIndex = teamIndex;
        LateStart();
    }

    private PlayerMovement[] partnerPMs;

    private int partnerNum;
    private bool isStraining;
    private float strainShakeRot = 25f;
    private float strainShakeScale = 0.3f;
    private float anvilSavedMass;
    private float anvilSavedDrag;
    private float anvilStrainMass = 2f;
    private float anvilStrainDrag = 5f;

    private bool debugRope = false;

    //public bool useNewFlingDirection = true;
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
        springRope.SetActive(false);
        tempBody.gameObject.SetActive(false);

        linkColliders = new CapsuleCollider[links.Length];
        linkDisplay = new Transform[links.Length];
        linkRBs = new Rigidbody[links.Length];

        partnerColliders[0] = partnerRB[0].GetComponent<SphereCollider>();
        partnerColliders[1] = partnerRB[1].GetComponent<SphereCollider>();

        partnerPMs = new PlayerMovement[2];
        partnerPMs[0] = partnerRB[0].GetComponent<PlayerMovement>();
        partnerPMs[1] = partnerRB[1].GetComponent<PlayerMovement>();

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

        // Subscribe resetting inAirFlings to 0 as soon as a RopeFlingResetTrigger is triggered
        foreach (RopeFlingResetTrigger trigger in ropeFlingResetTriggers)
        {
            if (!subscribedToFlingResetEvents) trigger.OnTrigger += () => inAirFlings = 0;

            // Since the RopeFlingResetTrigger colliders are on the Default layer, Player's think of them as a jumpable object.
            // Instead of making a new layer for these objects, just ignore collision for each of them
            Physics.IgnoreCollision(partnerColliders[0], trigger.GetComponent<Collider>());
            Physics.IgnoreCollision(partnerColliders[1], trigger.GetComponent<Collider>());
        }

        // Also ignore collision between all three RopeFlingResetTriggers
        Physics.IgnoreCollision(ropeFlingResetTriggers[0].GetComponent<Collider>(), ropeFlingResetTriggers[1].GetComponent<Collider>());
        Physics.IgnoreCollision(ropeFlingResetTriggers[0].GetComponent<Collider>(), ropeFlingResetTriggers[2].GetComponent<Collider>());
        Physics.IgnoreCollision(ropeFlingResetTriggers[1].GetComponent<Collider>(), ropeFlingResetTriggers[2].GetComponent<Collider>());

        if (!subscribedToFlingResetEvents)
        {
            // Also subscribe resetting inAirFlings to 0 as soon as any player is grounded
            for (int i = 0; i < 2; i++)
            {
                partnerPMs[i].OnGrounded += () => inAirFlings = 0;
                partnerPMs[i].OnPlayerClamp += (t, p, c) => { nPlayersClamped++; };
                partnerPMs[i].OnPlayerUnclamp += (t, p) => { inAirFlings = 0; nPlayersClamped--; };
            }

            subscribedToFlingResetEvents = true;
        }

        inAirFlings = 0;

        // Setup FMOD audio stuff
        whipSound = RuntimeManager.CreateInstance(whipSoundEventPath);
        flingSound = RuntimeManager.CreateInstance(flingSoundEventPath);
        cutSound = RuntimeManager.CreateInstance(cutSoundEventPath);
    }

    private void LateStart()
    {
        for (int i = 0; i < links.Length; i++)
        {
            if (TeamIndex == 0)
            {
                links[i].gameObject.layer = 14;
            }
            else
            {
                links[i].gameObject.layer = 15;
            }
        }
    }

    private void Update()
    {
        if (DevScript.Instance.DevMode)
        {
            if (Input.GetKeyUp("r"))
            {
                ToggleRopeDebug();
            }
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
        else if (isRopeSolid)
            UpdateSpringRopeJoints();
        else
            UpdateRopeJoints();

        ShouldRopeFlashCheck();
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
    void UpdateSpringRopeJoints()
    {
        for (int i = 0; i < ropeJoints.Length; i++)
        {

            if (i == 0)
            {
                springRopeJoints[i].position = partnerRB[0].transform.position;
                springRopeJoints[i].LookAt(links[0].transform.position);
            }
            else if (i < ropeJoints.Length - 2)
            {
                springRopeJoints[i].position = links[i - 1].transform.position;
                springRopeJoints[i].LookAt(links[i].transform.position);
            }
            else if (i < ropeJoints.Length - 1)
            {
                springRopeJoints[i].position = links[i - 1].transform.position;
                springRopeJoints[i].LookAt(partnerRB[1].transform.position);
            }
            else
            {
                springRopeJoints[i].position = partnerRB[1].transform.position;
                springRopeJoints[i].rotation = ropeJoints[i - 1].rotation;
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
            for (int i = 0; i < linkRBs.Length; i++)
            {
                Rigidbody body = linkRBs[i];
                FlingLinkRBAtIndex(i);
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
        else
        {
            // Fling this client's rigidbody in all scenarios
            // We fling the RB on both the owner and the partner client even in case of two clients in one team for consistency reasons
            body.AddForce(Vector3.up * 30f, ForceMode.Force);
        }
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
        //myRopeColor.SetRopeMaterialSolid (state);
        attachedRope.SetActive(!state);
        springRope.SetActive(state);

    }
    void ToggleRopeDebug()
    {
        debugRope = !debugRope;

        foreach (Transform link in linkDisplay)
        {
            link.GetComponent<MeshRenderer>().enabled = debugRope;
            //myRopeColor.gameObject.GetComponent<MeshRenderer>().enabled = !debugRope;
            attachedRope.SetActive(!debugRope);
        }
    }

    //Called from PlayerInput. PlayerInput sends the player's number so that we know who is pulling 
    public void InputFling(int playerNum)
    {
        if (!isRopeCut)                     // if rope is cut, tug should not work
        {
            if (pullReady && (inAirFlings < maxAirFlings || nPlayersClamped > 0))
            {
                pullReady = false;
                pullTimer = pullTime;

                if (playerNum % 2 == 1) // || playerNum == 3)
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
                /*if (!PhotonNetwork.OfflineMode && TeamManager.Instance.IsTeamSharedOnline(TeamIndex))
                {
                    Debug.Log("Online with two clients sharing this team");
                    
                    photonView.RPC("StrainFlingOnPartnerClientRPC", partnerRB[1 - partnerNum].GetComponent<PhotonView>().Owner, partnerNum);
                }*/

                inAirFlings++;

                PlayWhipSound();
                if (!PhotonNetwork.OfflineMode)
                {
                    if (TeamManager.Instance.IsTeamSharedOnline(TeamIndex))
                    {
                        photonView.RPC("InputFlingRPC", partnerRB[partnerNum].GetComponent<PhotonView>().Owner);
                    }

                    photonView.RPC("PlayWhipSound", RpcTarget.Others);
                }
            }
        }
    }

    /// <summary>
    /// To sync up InputFling variables that need to be synced up on a client sharing a team with this client
    /// </summary>
    [PunRPC]
    private void InputFlingRPC()
    {
        // Debug.Log("Input fling sync");

        pullReady = false;
        pullTimer = pullTime;

        inAirFlings++;
    }

    [PunRPC]
    private void PlayWhipSound()
    {
        RuntimeManager.AttachInstanceToGameObject(whipSound, midConnectedBody.transform, midConnectedBody);
        //whipSound.set3DAttributes(RuntimeUtils.To3DAttributes(midConnectedBody.transform));
        whipSound.start();
    }

    [PunRPC]
    private void StrainFlingOnPartnerClientRPC(int flingerIndex)
    {
        StartCoroutine(StrainFling(flingerIndex, false));
    }

    IEnumerator StrainFling (int flingerIndex, bool shouldCallFlingFunction)
    {
        cameraShake.Shake(0.04f, 0.3f, 100);

        isStraining = true;
        //strainParticles[partnerNum].SetActive(true);
        StrainParticlesDisplayStatus(partnerNum, true);
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("StrainParticlesDisplayStatus", RpcTarget.Others, partnerNum, true);
        }

        // Start drag change
        teamDragManager.SetAllPlayerDrag(teamDragManager.CharacterStrainDrag, teamDragManager.CharacterStrainAngularDrag);
        teamDragManager.SetAllLinksDrag(teamDragManager.LinkStrainDrag);

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

        // End drag change
        teamDragManager.SetAllPlayerDrag(teamDragManager.CharacterDefaultDrag, teamDragManager.CharacterDefaultAngularDrag);
        teamDragManager.SetAllLinksDrag(teamDragManager.LinkDefaultDrag);

        anvil.mass = anvilSavedMass;
        anvil.drag = anvilSavedDrag;

        if (shouldCallFlingFunction)
        {
            ForceFling();
        }
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

        //if (useNewFlingDirection)
        //{
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
        //}
        //else
        //{
        //    // ============ Old way of handling direction ============ */
        //    if (activeLink.position.y > (flung.transform.position.y + 0.15f) && partnerDistance > ropeLength)
        //        dir = ((activeLink.position - flung.transform.position).normalized + Vector3.up).normalized;

        //    //otherwise, set the direction to be the direction between the two players
        //    else
        //        dir = (activeLink.position - flung.transform.position).normalized;
        //}

        
        float distance = Vector3.Distance(links[6].transform.position, flung.transform.position);


        float forceMagnitude = tugCurve.Evaluate(distance);
        forceMagnitude *= flingModifier;

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

    public void ActivateBalloonsModifier(bool state)
    {
        if (state)
        {
            flingModifier = defaultFlingModifier + balloonFlingModifier;
        }
        else
        {
            flingModifier = defaultFlingModifier;
        }
    }
    public void SetBalloonsModifier(bool state, float modifyFactor)
    {
        if (state)
        {
            flingModifier += (balloonFlingModifier * modifyFactor);
        }
        else
        {
            flingModifier -= (balloonFlingModifier * modifyFactor);
        }

        flingModifier = Mathf.Clamp(flingModifier, defaultFlingModifier, defaultFlingModifier + balloonFlingModifier);
    }


    /// <summary>
    /// Fling a player's rigidbody at index flungIndex
    /// </summary>
    /// <param name="flungIndex">Index of player to be flung</param>
    /// <param name="forceToBeApplied">Force to be applied while flinging</param>
    private void FlingPlayerRB(int flungIndex, Vector3 forceToBeApplied) {
        Rigidbody flung = partnerRB[flungIndex];

        if (!PhotonNetwork.OfflineMode && TeamManager.Instance.IsTeamSharedOnline(TeamIndex))
        {
            // Stuff to do on all clients
            photonView.RPC("FlingPlayerRBRPC", partnerRB[1 - partnerNum].GetComponent<PhotonView>().Owner, flungIndex, forceToBeApplied.x, forceToBeApplied.y, forceToBeApplied.z);
        }
        else
        {
            // Add force on this client in all scenarios
            flung.AddForce(forceToBeApplied, ForceMode.Acceleration);
            // Debug.Log(flung.name + " is flung in this direction " + forceToBeApplied.ToString());
        }
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

        // Debug.Log(flung.name + " is flung in this direction " + thisForceToBeApplied.ToString());
    }

    [PunRPC]
    private void PlayFlingTrailRPC(int partnerNum, float force)
    {
        StartCoroutine(PlayFlingTrail(partnerNum, force));
    }

    [PunRPC]
    private void OnPerfectFlingVisualsAndSound(int playerNum)
    {
        cameraShake.Shake(0.15f, 0.2f, 150);
        RuntimeManager.AttachInstanceToGameObject(flingSound, partnerRB[playerNum].transform, partnerRB[playerNum]);
        //flingSound.set3DAttributes(RuntimeUtils.To3DAttributes(midConnectedBody.transform));
        flingSound.start();
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
            springRope.SetActive(false);
            cutRope.SetActive(true);

            cutParticles.gameObject.transform.position = tempBody.position;
            cutParticles.Emit(200);
            RuntimeManager.AttachInstanceToGameObject(cutSound, midConnectedBody.transform, midConnectedBody);
            //cutSound.set3DAttributes(RuntimeUtils.To3DAttributes(midConnectedBody.transform));
            cutSound.start();

            linkRBs[linkRBs.Length / 2].AddForce(Vector3.up * 30f, ForceMode.Impulse);
            linkRBs[(linkRBs.Length / 2)-1].AddForce(Vector3.up * 30f, ForceMode.Impulse);
        }
        else
        {
            midLink.connectedBody = midConnectedBody;
            cutRope.SetActive(false);

            if (isRopeSolid)
            {
                springRope.SetActive(true);
            }
            else
            {
                attachedRope.SetActive(true);
            }
        }
    }
    private void ShouldRopeFlashCheck()
    {
        if (!IsRopeCut && averageLinkDistance > 0.75f && (pullReady || isStraining) && !isRopeSolid) //should flash
        {
            if (!ropeIsFlashing)
            {
                ropeFlashLine.ToggleFlash(true);
                ropeIsFlashing = true;
                
            }
        }
        //else if (IsRopeCut)
        //{

        //}
        else
        {
            if (ropeIsFlashing)
            {
                ropeFlashLine.ToggleFlash(false);
                ropeIsFlashing = false;
            }
        }
    }

    private void OnDestroy()
    {
        // Unubscribe resetting inAirFlings to 0 as soon as a RopeFlingResetTrigger is triggered
        foreach (RopeFlingResetTrigger trigger in ropeFlingResetTriggers)
        {
            trigger.OnTrigger -= () => inAirFlings = 0;
        }

        for (int i = 0; i < 2; i++)
        {
            partnerPMs[i].OnGrounded -= () => inAirFlings = 0;
            partnerPMs[i].OnPlayerClamp -= (t, p, c) => { nPlayersClamped++; };
            partnerPMs[i].OnPlayerUnclamp -= (t, p) => { inAirFlings = 0; nPlayersClamped--; };
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