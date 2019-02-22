using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PlayerMovement : MonoBehaviourPun
{
    #region parameters
    //public AnimationCurve flingAngleCurve;

    private int myTeam = 1;
    /// <summary>
    /// Team number of this player
    /// </summary>
    public int MyTeam
    {
        get { return myTeam; }
        set { myTeam = value; }
    }

    public int playerNumber = 1;

    /// <summary>
    /// This player's number in the team (1/2)
    /// </summary>
    public int PlayerNumber
    {
        get { return playerNumber; }
        set { playerNumber = value; }
    }

    [Header("Speeds")]
    private float groundMovementForce = 140f;
    private float postJumpMovementForce = 60f;
    private float airMovementForce = 100f;

    [Header("Fall Curves")]
    public AnimationCurve fallQuickCurve = AnimationCurve.Linear(0, 0, 1, -1000);
    public AnimationCurve fallHighCurve = AnimationCurve.Linear(0, 0, 1, -1000);

    [Header("Masks")]
    public LayerMask clampable;
    public LayerMask badGrass;
    public LayerMask jumpable;

    [Header("AudioPlayers")]
    public GenericSoundPlayer jumpSoundPlayer;
    public GenericSoundPlayer clampSoundPlayer;
    public GenericSoundPlayer deClampSoundPlayer;

    [Header("Juice Controlers")]
    public SquashStretchController mySquashStretchController;
    public StickSphereController myStickSphereController;

    [Header("Other References")]
    public GameObject clampGraphic;
    public CameraLocator camLocator;
    public Transform midLink;
    public SpringControl ourSpringControl;
    public ParticleSystem jumpPoofParticles;
   

    [HideInInspector]
    public Transform groundParticlePointer;
    [HideInInspector]
    public ParticleVelocityControl groundParticleVelControl;
    private Vector3 groundParticleOffset = new Vector3(0f, -0.5f, 0f);


    private bool isGrounded = false;

    //Bringing ApplyForce() back into this script periodically
    [Header("Apply Force Variables")]
    [SerializeField]
    private Rigidbody partnerRB;
    /// <summary>
    /// Rigidbody of this player's partner/teammate
    /// </summary>
    public Rigidbody PartnerRB
    {
        get { return partnerRB; }
    }
    [HideInInspector] private bool inputting;
    private float ropeLength = 3f;
    private float maxRopeLength = 7f;
    private float elasticityConstant = 10f;
    //private float pullConstant = 35f;
    private float currLength;
    private Vector3 forceToBeApplied;
    private Vector3 newVel;
    private bool isRopeCut = false;         // Rope is not cut at start of the game
    public bool IsRopeCut
    { get { return isRopeCut; } }

    private float speed = 100f;
    private float angularSpeed = 100f;
    private float speedLastFrame;
    //private float slowGrassSpeed = 0.1f;

    //Jump Tweakable
    private float initialJumpVelocity = 11f;
    
    private float jumpQuickTime = 0.15f;
    private float jumpPeakTime = 0.65f;
    //Jump Variables
    private bool jumping = false;
    private bool falling = false;
    private float jumpTimer = 0.0f;
    private float fallTimer = 0.0f;
    private float jumpGraphInterpolate = 0.0f;

    private Rigidbody myRB;

    private bool clampSearch = false;
    private bool clamped = false;
    private bool inputtingClamp = false;
    public bool IsClamped {
        get{return clamped;}
    }
    

    //groundcheck
    private Collider[] groundCollides;


    //death stuff
    [HideInInspector]
    public bool isInDeathZone = false;

    private Transform normalParent;

    //stick stuff
    FixedJoint stickJoint;

    //abillity stuff: springs
    private bool springActive = false;
    private float springInitialJumpVelocity = 22f;
    private float saveInitialJumpVelocity;

    #endregion

    #region Initialization(Start_And_Awake)
    // Use this for initialization
    void Start()
    {
        myRB = GetComponent<Rigidbody>();
        myRB.maxAngularVelocity = 15f;
        speed = groundMovementForce;
        normalParent = transform.parent;

        RaceManager.Instance.OnTeamRespawn += DestroyStickJointOnRespawn;

        saveInitialJumpVelocity = initialJumpVelocity;

        clampable |= (1 << LayerMask.NameToLayer("Clamp_" + myTeam.ToString() + playerNumber.ToString()));
        clampable |= (1 << LayerMask.NameToLayer("Clamp_Team" + myTeam.ToString()));
    }
    #endregion

    #region Update(Clamping_And_Jumping)
    // Update is called once per frame
    void Update()
    {
        // Networked Multiplayer
        if (!PhotonNetwork.OfflineMode && !photonView.IsMine)
        {
            return;
        }

        //CheckGrass();
        CheckGround();
        newVel = myRB.velocity;

        if (clampSearch)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.8f, clampable, QueryTriggerInteraction.Ignore);

            if (hitColliders.Length > 0)
            {
                //if (hitColliders[1].gameObject.isStatic)
                Rigidbody rigidClampable;

                bool rigidStick = false;

                foreach (Collider collider in hitColliders)
                {
                    Rigidbody tempClampable = collider.GetComponent<Rigidbody>();

                    if (tempClampable != null)
                    {
                        rigidClampable = tempClampable;

                        if (stickJoint != null)     // if it already exists, first destroy it then create a new one!
                        {
                            Destroy(stickJoint);
                        }
                        stickJoint = gameObject.AddComponent(typeof(FixedJoint)) as FixedJoint;
                        stickJoint.connectedBody = rigidClampable;
                        stickJoint.enableCollision = true;

                        rigidStick = true;
                        break;
                    }
                }
                if (!rigidStick)
                {
                    myRB.isKinematic = true;
                    myRB.useGravity = false;
                }

                //myStickSphereController.StickSquash();
                //clampSoundPlayer.EmitSound();

                OnClampVisualsAndSound();

                // Networked
                if (!PhotonNetwork.OfflineMode)
                {
                    photonView.RPC("OnClampVisualsAndSound", RpcTarget.Others);
                }


                clampSearch = false;
                clamped = true;

                if (OnPlayerClamp!=null)
                {
                    OnPlayerClamp(myTeam, playerNumber, hitColliders[0].gameObject);
                }
            }
        }
        if (jumping)
        {
            jumpTimer += Time.deltaTime;
            if ((jumpTimer > 0.2) && (isGrounded))
            {
                jumping = false;
                //print("Hitground. Fall Timer: " + fallTimer);
            }
            else if (falling)
            {
                fallTimer += Time.deltaTime;
                if (fallTimer < 0.5f)
                {
                    myRB.AddForce(Vector3.up * Mathf.Lerp(fallQuickCurve.Evaluate(fallTimer), fallHighCurve.Evaluate(fallTimer), jumpGraphInterpolate) * Time.deltaTime, ForceMode.Impulse);
                }
                else
                {
                    jumping = false;
                    //print("Fallover. Jump Timer: " + jumpTimer);
                    falling = false;
                }
            }
            else if (myRB.velocity.y <= 0.0f)
            {
                //print("Peak Hit. Jump Timer: " + jumpTimer);
                jumpGraphInterpolate = 1.0f;
                falling = true;
            }
        }
    }

    #endregion

    #region FixedUpdate(Movement_Forces)
    private void FixedUpdate()
    {
        // Networked Multiplayer
        if (!PhotonNetwork.OfflineMode && !photonView.IsMine)
        {
            return;
        }


        currLength = Vector3.Distance(myRB.transform.position, midLink.position)*2f;

        if (currLength > ropeLength && !isRopeCut)
        {
            ApplyForce();
        }
    }
    #endregion

    #region Input
    //Called from PlayerInput. if the joystick passed the deadzone test.
    public void InputJoystick(Vector2 joystick)
    {
        Vector3 movement = new Vector3(joystick.x, 0, joystick.y).normalized;
        Vector3 rotation = new Vector3(joystick.y, 0, joystick.x * -1).normalized;

        float joyMagnitude = Mathf.Clamp(joystick.magnitude, 0.0f, 1.0f);
        if (joyMagnitude > 0.90f)
            joyMagnitude = 1.0f;

        //print("Joystick Magnitude: " + joyMagnitude);

        movement = Quaternion.LookRotation(camLocator.transform.forward) * movement;
        rotation = Quaternion.LookRotation(camLocator.transform.forward) * rotation;

        myRB.AddForce(movement * 0.85f * joyMagnitude * speed);
        myRB.AddTorque(rotation * 0.4f * joyMagnitude * angularSpeed);

        inputting = true;
    }

    //Called from PlayerInput. down is true if ButtonDown, false if ButtonUp
    public void InputJump(bool down)
    {
        if (down)
        {
            if (!clampSearch)
            {
                if (isGrounded)
                {
                    Vector3 currentVelocity = myRB.velocity;

                    myRB.velocity = new Vector3(currentVelocity.x, initialJumpVelocity, currentVelocity.z);
                    jumpTimer = 0.0f;
                    fallTimer = 0.0f;
                    jumping = true;
                    falling = false;

                    /*mySquashStretchController.JumpSquash();

                    jumpSoundPlayer.EmitSound();
                    //ourSpringControl.JumpSpring();
                    jumpPoofParticles.transform.position = transform.position - new Vector3(0f, 0.4f, 0f);
                    jumpPoofParticles.Emit(30);*/

                    JumpVisualsAndSound();

                    if (!PhotonNetwork.OfflineMode)
                    {
                        // RPC on all players other than me
                        photonView.RPC("JumpVisualsAndSound", RpcTarget.Others);
                    }
                }
            }
        }
        else if (jumping)
        {
            //print("Let go. Jump Timer: " + jumpTimer);
            jumpGraphInterpolate = InterpolateVariableJump(jumpTimer);
            falling = true;
            fallTimer = 0.0f;
        }
    }

    /// <summary>
    /// Use this function to display jumping visuals and sound effects
    /// </summary>
    [PunRPC] private void JumpVisualsAndSound()
    {
        mySquashStretchController.JumpSquash();
        jumpSoundPlayer.EmitSound();
        jumpPoofParticles.transform.position = transform.position - new Vector3(0f, 0.4f, 0f);
        jumpPoofParticles.Emit(30);
    }

    //Called from PlayerInput. down is true if ButtonDown, false if ButtonUp
    public void InputClamp(bool down)
    {
        if (down)
        {
            clampSearch = true;

            //myStickSphereController.GrowSquash();

            LookingForClampVisualsAndSound();

            // Networked
            if (!PhotonNetwork.OfflineMode)
            {
                photonView.RPC("LookingForClampVisualsAndSound", RpcTarget.Others);
            }

            inputtingClamp = true;
        }
        else
        {
            if (inputtingClamp)
            {
                //myStickSphereController.ShrinkSquash();   // please don't move this to the Unclamp() function.
                                                            // There is one specific instance when the sphere shouldn't shrink
                                                            // This is when the player gets bumped by a bumper and stops holding down the clamp button
                OnUnclampVisuals();

                // Networked
                if (!PhotonNetwork.OfflineMode)
                {
                    photonView.RPC("OnUnclampVisuals", RpcTarget.Others);
                }
            }
            inputtingClamp = false;

            Unclamp();
            
        }
    }

    #region PUN Callbacks For Visuals and SFX
    /// <summary>
    /// Use this function when looking for clamp, for its visuals and sound effects
    /// </summary>
    [PunRPC]
    private void LookingForClampVisualsAndSound()
    {
        myStickSphereController.GrowSquash();
    }

    /// <summary>
    /// Use this function on clamp, for its visuals and sound effects
    /// </summary>
    [PunRPC]
    private void OnClampVisualsAndSound()
    {
        myStickSphereController.StickSquash();
        clampSoundPlayer.EmitSound();
    }

    /// <summary>
    /// Use this function on unclamp, for its visuals
    /// </summary>
    [PunRPC]
    private void OnUnclampVisuals()
    {
        myStickSphereController.ShrinkSquash();
    }

    /// <summary>
    /// Use this function on unclamp, for its sound effects
    /// </summary>
    [PunRPC]
    private void OnUnclampSound()
    {
        deClampSoundPlayer.EmitSound();
    }
    #endregion
    #endregion

    #region OtherFunctions

    private void ApplyForce()
    {
        //if there is an input from the player, don't stop movement in that direction unless the player is in the air
        //if (!CheckGround())
        Vector3 dir = (transform.position - midLink.position).normalized;

        if (!inputting)
        {

            float forceMagnitude = elasticityConstant * (currLength - ropeLength);
            forceToBeApplied = -dir * forceMagnitude;

            myRB.AddForce(forceToBeApplied, ForceMode.Acceleration);
            //Debug.Log(currLength);
        }

        //if beyond max length, apply full force = stretched length * elasticity constant
        if (currLength > maxRopeLength && !clamped)
        {
            //Vector3 negativeForce = -Vector3.ClampMagnitude((newVel * (currLength * 1.0f)), newVel.magnitude * 3.0f);
            //Debug.DrawRay(transform.position, negativeForce, Color.red, 0.3f);
            //myRB.AddForce(negativeForce, ForceMode.Acceleration);

            /* ------ uncomment the line below and comment everything else to return to previous state ------ */
            //myRB.velocity = newVel - Vector3.Project(newVel, dir);


            //float parallelVel = Mathf.Abs(Vector3.Dot(newVel, dir));

            Vector3 parallelVel = Vector3.Project(newVel, dir);
            //Debug.DrawRay(transform.position, parallelVel, Color.red);

            float forceVal = (myRB.mass * Mathf.Pow(myRB.velocity.magnitude, 2)) / (currLength);         // centripetal force = (m * v^2) / r
            Vector3 centripetalForce = -Vector3.ClampMagnitude(forceVal * (dir), newVel.magnitude * 3f);
            //Vector3 centripetalForce = -parallelVel * dir;
            myRB.AddForce(1.5f * centripetalForce, ForceMode.Acceleration);
            //Debug.DrawRay(transform.position, centripetalForce, Color.blue);
            //Debug.Log(currLength);
        }
    }

    public void CheckGround()
    {
        /*
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -Vector3.up, out hit, 0.8f, jumpable))
        {
            isGrounded = true;
            speed = groundMovementForce;
        }
        else
        {
            isGrounded = false;
            if (jumping)
                speed = postJumpMovementForce;
            else
                speed = airMovementForce;
        }
        */
        Vector3 sphereCenter = transform.position + Vector3.down * 0.32f;
        float radius = 0.42f;

        groundCollides = Physics.OverlapSphere(sphereCenter, radius, jumpable);

        int triggerCount = 0;
        foreach (Collider collides in groundCollides)
        {
            if (collides.isTrigger)
                triggerCount++;
        }

        if (groundCollides.Length - triggerCount > 1) //Did the overlap check find anything? If it is only 1 then it only collided with the current player.
        {
            isGrounded = true;
            speed = groundMovementForce;
            angularSpeed = 100f;
        }
        else
        {
            isGrounded = false;
            if (jumping)
            {
                speed = postJumpMovementForce;
                angularSpeed = 10f;
            }
            else
            {
                speed = airMovementForce;
                angularSpeed = 10f;
            }
        }
        UpdateGroundParticles(isGrounded);
    }

    private void UpdateGroundParticles(bool onGround)
    {
        groundParticlePointer.transform.position = transform.position + groundParticleOffset;

        if (onGround)
        {
            if (myRB.velocity.magnitude > 0.1)
            {
                groundParticlePointer.transform.rotation = Quaternion.LookRotation(myRB.velocity * -1);
                groundParticleVelControl.UpdateEmission(myRB.velocity.magnitude);
            }
            else
                groundParticleVelControl.UpdateEmission(0f);

        }
        else
        {
            groundParticleVelControl.UpdateEmission(0f);
        } 
    }

    //Takes how long the player held down the jump for and outputs a 0.0 - 1.0 value so that we can lerp between the two fall graphs.
    float InterpolateVariableJump(float holdTime)
    {
        if (holdTime < jumpQuickTime)
        {
            return 0.0f;
        }
        else if (holdTime < jumpPeakTime)
        {
            return Mathf.InverseLerp(jumpQuickTime, jumpPeakTime, holdTime);
        }
        else
        {
            return 1.0f;
        }
    }

    /// <summary>
    /// Unclamp the player
    /// </summary>
    public void Unclamp()
    {
        clampSearch = false;
        if (clamped)
        {

            if (stickJoint != null)
            {
                //Vector3 parentVelocity = transform.parent.GetComponent<Rigidbody>().velocity;
                //transform.parent = normalParent;
                //myRB.AddForce(parentVelocity, ForceMode.VelocityChange);
                //stickJoint.connectedBody = null;
                Destroy(stickJoint);
            }
            else
            {
                myRB.isKinematic = false;
                myRB.useGravity = true;
            }

            //deClampSoundPlayer.EmitSound();
            OnUnclampSound();

            if (!PhotonNetwork.OfflineMode)
            {
                photonView.RPC("OnUnclampSound", RpcTarget.Others);
            }

            clamped = false;

            if (OnPlayerUnclamp != null)
            {
                OnPlayerUnclamp(MyTeam, playerNumber);
            }
        }
    }

    /// <summary>
    /// Use this function for destroying the stick joint.
    /// </summary>
    void DestroyStickJointOnRespawn(int teamNumber)
    {
        if (teamNumber == myTeam)
        {
            if (stickJoint != null)
            {
                Destroy(stickJoint);
            }
        }
    }

    /// <summary>
    /// Use this method to add force to push even clamped players
    /// The force and forceMode parameters are the same as Rigidbody.AddForce() parameters
    /// </summary>
    /// <param name="force">Force to be applied</param>
    /// <param name="forceMode">ForceMode</param>
    /// <param name="handleClamping">Should the player get unclamped?</param>
    /// <param name="pauseInput">Should the input get paused for a fraction of a second after getting bumped?</param>
    public void AddBumperForce(Vector3 force, ForceMode forceMode = ForceMode.VelocityChange, bool handleClamping = true, bool pauseInput = true)
    {
        if (handleClamping)
        {
            if (clamped)
            {
                UnclampThenStartClampSearch();
            }
        }

        myRB.AddForce(force, forceMode);

        if (pauseInput)
        {
            PlayerInput pi = gameObject.GetComponent<PlayerInput>();
            pi.PauseMovementInputForTime(0.5f);
        }
    }

    public void NullifyAllForces()
    {
        myRB.velocity = Vector3.zero;
        myRB.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// Unclamp from surfaces then start searching for clampable surfaces if still holding down clamp button
    /// </summary>
    private void UnclampThenStartClampSearch()
    {
        Unclamp();


        if (!inputtingClamp)
        {
            //myStickSphereController.ShrinkSquash();
            OnUnclampVisuals();

            // Networked
            if (!PhotonNetwork.OfflineMode)
            {
                photonView.RPC("OnUnclampVisuals", RpcTarget.Others);
            }
        }

        StartCoroutine(StartClampSearchInSeconds(0.3f));
    }

    private IEnumerator StartClampSearchInSeconds(float t)
    {
        yield return new WaitForSeconds(t);
        
        // If still holding down clamp button, start searching for clampable surfaces again
        if (inputtingClamp)
        {
            clampSearch = true;
            //myStickSphereController.GrowSquash();
            LookingForClampVisualsAndSound();

            // Networked
            if (!PhotonNetwork.OfflineMode)
            {
                photonView.RPC("LookingForClampVisualsAndSound", RpcTarget.Others);
            }
        }
    }

    public void ResumeAfterPause()
    {
        // Unclamp
        InputClamp(false);
    }

    #endregion

    #region Public_Events_And_Delegates
    /* -------------------- Subscribe-able events follow -------------------- */

    public delegate void OnPlayerClampDelegate(int teamNum, int playerNum, GameObject clampedObj);
    /// <summary>
    /// Subscribe to this to get info on which team/player clamps to the subscribed object
    /// </summary>
    public event OnPlayerClampDelegate OnPlayerClamp;

    public delegate void OnPlayerUnclampDelegate(int teamNum, int playerNum);
    /// <summary>
    /// Subscribe to this to get info on which team/player clamps from the subscribed object
    /// </summary>
    public event OnPlayerUnclampDelegate OnPlayerUnclamp;
    #endregion

    #region Getters_And_Setters
    /* -------------------- Getter and Setter Methods Follow -------------------- */

    public bool GetInputting()
    {
        return inputting;
    }

    public void SetInputting(bool val)
    {
        inputting = val;
    }

    /// <summary>
    /// Changes the status of this characters's rope
    /// </summary>
    /// <param name="cut">bool value. true if rope should be cut. false otherwise.</param>
    public void ChangeRopeStatus(bool cut)
    {
        isRopeCut = cut;
    }
    public void ChangeSpringStatus(bool toggle)
    {
        if (toggle)
            initialJumpVelocity = springInitialJumpVelocity;
        else
            initialJumpVelocity = saveInitialJumpVelocity;

        springActive = toggle;
    }

    /// <summary>
    /// The isGrounded value of this playerMovement script.
    /// </summary>
    public bool Grounded
    {
        get
        {
            return isGrounded;
        }
    }

    /// <summary>
    /// The clamped value of this playerMovement script.
    /// </summary>
    public bool Clamped
    {
        get
        {
            return clamped;
        }
    }
    #endregion

    /// <summary>
    /// Unsubscribe from (most) events here.
    /// </summary>
    void OnDestroy()
    {
        RaceManager.Instance.OnTeamRespawn -= DestroyStickJointOnRespawn;
    }

    #region Gizmos_And_Debugging
    // Gizmos - for debugging
    /*
    void OnDrawGizmos()
    {
        if (myTeam != 1)
            return;

        if (GetComponent<PlayerInput>().PlayerNumber != 1)
            return;

        Gizmos.color = Color.green;

        Vector3 a = transform.position - partnerRB.position;
        Vector3 b = midLink.position - partnerRB.position;

        Gizmos.DrawRay(partnerRB.position, a);
        Gizmos.DrawRay(partnerRB.position, b);

        // Rotation axis
        Gizmos.color = Color.blue;
        Vector3 rotAxis = Vector3.Cross(a, b);

        Gizmos.DrawRay(partnerRB.position, rotAxis);

        // Getting the fling direction
        

        float angle = Vector3.Angle(a, b);
        Gizmos.color = Color.white;
        

        Vector3 flingDir = b;
        //Vector3 c = new Vector3(b.x, b.y, b.z);
        Quaternion rot = Quaternion.AngleAxis(angle, rotAxis);
        flingDir = rot * flingDir;

        Gizmos.DrawRay(partnerRB.position, flingDir);
    }
    */
    #endregion
}
