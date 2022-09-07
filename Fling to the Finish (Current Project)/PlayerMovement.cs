using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Fling.Achievements;
using FMOD.Studio;

public class PlayerMovement : MonoBehaviourPun
{
    #region parameters
    //public AnimationCurve flingAngleCurve;

    private int teamIndex = 1;
    /// <summary>
    /// Team number of this player
    /// </summary>
    public int TeamIndex
    {
        get { return teamIndex; }
        set { teamIndex = value; }
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

    [Header("Common Player Movement Data")]
    [SerializeField] private PlayerMovementVariables singleCharacterMovementData;
    [SerializeField] private PlayerMovementVariables fullTeamMovementData;
    private PlayerMovementVariables playerMovementData;

    private float groundMovementForce; // = 140f;
    private float postJumpMovementForce; // = 60f;
    private float airMovementForce; // = 100f;

    private AnimationCurve fallQuickCurve = AnimationCurve.Linear(0, 0, 1, -1000);
    private AnimationCurve fallHighCurve = AnimationCurve.Linear(0, 0, 1, -1000);

    private LayerMask clampable;
    private LayerMask jumpable;

    [Header("AudioPlayers")]
    FMOD.Studio.EventInstance jumpSoundEvent;
    FMOD.Studio.EventInstance springJumpSoundEvent;
    FMOD.Studio.EventInstance clampSoundEvent;
    FMOD.Studio.EventInstance declampSoundEvent;

    FMOD.Studio.EventInstance stickSoundEvent;


    [Header("Juice Controlers")]
    public SquashStretchController mySquashStretchController;
    public StickSphereController myStickSphereController;
    public CameraShaker cameraShake;

    [Header("Other References")]
    public GameObject clampGraphic;
    public CameraLocator camLocator;
    public Transform midLink;
    public ParticleSystem jumpPoofParticles;

    public Transform groundParticlePointer;
    public ParticleVelocityControl groundParticleVelControl;
    private Vector3 groundParticleOffset = new Vector3(0f, -0.5f, 0f);

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
    private bool inputting;
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
    private float initialJumpVelocity;

    private float jumpQuickTime;
    private float jumpPeakTime;
    private float coyoteJumpTimer;
    //Jump Variables
    private bool jumping = false;
    private bool falling = false;
    private float jumpTimer = 0.0f;
    private float fallTimer = 0.0f;
    private float jumpGraphInterpolate = 0.0f;
    private bool isGrounded = false;
    private bool isWithinCoyoteJumpTime = false;
    private float timeSinceUngrounded = 0f;

    private Rigidbody myRB;
    private RopeSyncer myRopeSyncer;

    private bool clampSearch = false;
    private bool clamped = false;
    private bool inputtingClamp = false;
    private bool isRespawning = false;
    public bool IsClamped
    {
        get { return clamped; }
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
    private bool balloonsActive = false;
    private float springInitialJumpVelocity;
    private float balloonsInitialJumpVelocity;
    private float saveInitialJumpVelocity;

    // gameplay type
    private CharacterContent.TeamInstanceType gameplayType = CharacterContent.TeamInstanceType.Gameplay;

    #endregion

    #region Initialization(Start_And_Awake)
    // Use this for initialization
    void Start()
    {
        SetPlayerMovementData(true);

        myRB = GetComponent<Rigidbody>();
        myRopeSyncer = GetComponent<RopeSyncer>();
        myRB.maxAngularVelocity = 15f;
        speed = groundMovementForce;
        normalParent = transform.parent;

        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnTeamDeath += CheckClampOnRespawn;
            RaceManager.Instance.OnTeamRespawnDelayFinished += TeamRespawnProcessComplete;
        }
    }

    private void SetPlayerMovementData(bool isFullTeam)
    {
        playerMovementData = isFullTeam ? fullTeamMovementData : singleCharacterMovementData;

        // Setup player movement data
        groundMovementForce = playerMovementData.GroundMovementForce;
        postJumpMovementForce = playerMovementData.PostJumpMovementForce;
        airMovementForce = playerMovementData.AirMovementForce;
        fallHighCurve = playerMovementData.FallHighCurve;
        fallQuickCurve = playerMovementData.FallQuickCurve;
        clampable = playerMovementData.Clampable;
        jumpable = playerMovementData.Jumpable;
        initialJumpVelocity = playerMovementData.InitialJumpVelocity;
        saveInitialJumpVelocity = initialJumpVelocity;
        jumpQuickTime = playerMovementData.JumpQuickTime;
        jumpPeakTime = playerMovementData.JumpPeakTime;
        coyoteJumpTimer = playerMovementData.CoyoteJumpTimer;
        springInitialJumpVelocity = playerMovementData.SpringInitialJumpVelocity;
        balloonsInitialJumpVelocity = playerMovementData.BalloonsInitialJumpVelocity;

        jumpSoundEvent = FMODUnity.RuntimeManager.CreateInstance(playerMovementData.JumpSoundEvent);
        springJumpSoundEvent = FMODUnity.RuntimeManager.CreateInstance(playerMovementData.SpringJumpSoundEvent);
        clampSoundEvent = FMODUnity.RuntimeManager.CreateInstance(playerMovementData.ClampSoundEvent);
        declampSoundEvent = FMODUnity.RuntimeManager.CreateInstance(playerMovementData.DeclampSoundEvent);
        stickSoundEvent = FMODUnity.RuntimeManager.CreateInstance(playerMovementData.StickSoundEvent);

        clampable |= (1 << LayerMask.NameToLayer("Clamp_" + teamIndex.ToString() + playerNumber.ToString()));
        clampable |= (1 << LayerMask.NameToLayer("Clamp_Team" + teamIndex.ToString()));
    }

    /// <summary>
    /// Should the <see cref="fullTeamMovementData"/> or <see cref="singleCharacterMovementData"/> be used?
    /// </summary>
    /// <param name="fullTeam"></param>
    public void SetMovementType(bool fullTeam)
    {
        SetPlayerMovementData(fullTeam);
    }
    #endregion

    #region Update(Clamping_And_Jumping)
    // Update is called once per frame
    void Update()
    {
        // Networked Multiplayer
        if (!PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null && !photonView.IsMine)
        {
            return;
        }

        //CheckGrass();
        CheckGround();

        if (!isGrounded && timeSinceUngrounded <= coyoteJumpTimer)
        {
            timeSinceUngrounded += Time.deltaTime;
        }
        else if (isGrounded && timeSinceUngrounded > coyoteJumpTimer)
        {
            timeSinceUngrounded = 0f;
        }

        isWithinCoyoteJumpTime = (timeSinceUngrounded <= coyoteJumpTimer) && !jumping ? true : false;

        newVel = myRB.velocity;

        if (clampSearch && !isRespawning)
        {
            bool stuckTo3dButton = false;

            QueryTriggerInteraction triggerInteraction =
                gameplayType == CharacterContent.TeamInstanceType.Gameplay ?
                QueryTriggerInteraction.Ignore :
                QueryTriggerInteraction.UseGlobal;

            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.8f, clampable, triggerInteraction);            

            if (hitColliders.Length > 0)
            {
                GameObject stuckGameobject = null;
                //if (hitColliders[1].gameObject.isStatic)
                Rigidbody rigidClampable;

                bool rigidStick = false;

                foreach (Collider collider in hitColliders)
                {
                    Rigidbody tempClampable = collider.attachedRigidbody;
                    
                    if (collider.isTrigger)
                    {
                        if (collider.tag.Equals("3DButton"))
                        {
                            stuckGameobject = collider.gameObject;
                            stuckTo3dButton = true;
                            break;
                        }
                        else
                        {
                            // only check triggers that have the 3D button tag
                            continue;
                        }
                    }
                    else
                    {
                        stuckGameobject = collider.gameObject;
                    }
                    
                    if (tempClampable != null)
                    {
                        rigidClampable = tempClampable;

                        if (stickJoint != null)     // if it already exists, first destroy it then create a new one!
                        {
                            if (PhotonNetwork.OfflineMode)
                            {
                                Destroy(stickJoint);
                                stickJoint = null;
                            }
                            else
                            {
                                Destroy(stickJoint);
                                stickJoint = null;
                                photonView.RPC("DestroyStickJointRPC", RpcTarget.Others);
                            }
                        }

                        if (PhotonNetwork.OfflineMode)
                        {
                            SetClampJointOffline(rigidClampable, false);
                        }
                        else
                        {
                            PhotonView rigidbodyPhotonView = rigidClampable.GetComponent<PhotonView>();
                            if (rigidbodyPhotonView != null)
                            {
                                SetClampJointOffline(rigidClampable, false);
                                SetClampJointRPC(rigidbodyPhotonView.ViewID);
                            }
                            else
                            {
                                Debug.LogError("Custom error, we are sticking to a rigidbody without a photonView. Add one to " + rigidClampable.gameObject.name);
                                SetClampJointOffline(rigidClampable, false);
                            }
                        }

                        // stickJoint = gameObject.AddComponent(typeof(FixedJoint)) as FixedJoint;
                        // stickJoint.connectedBody = rigidClampable;
                        // stickJoint.enableCollision = true;

                        stuckGameobject = collider.gameObject;
                        rigidStick = true;
                        break;
                    }
                }
                if (!rigidStick)
                {
                    bool shouldUseKinmaticStick = false;
                    //check if collider is Treadmill or other collider where it should use isKinematic instead of joint
                    foreach (Collider collider in hitColliders)
                    {
                        if (collider.transform.CompareTag("isKinematicStick"))
                        {
                            shouldUseKinmaticStick = true;
                            stuckGameobject = collider.gameObject;
                            Debug.Log("We're sticking to treadmill");
                        }
                    }

                    if (stuckGameobject != null)
                    {
                        if (PhotonNetwork.OfflineMode)
                        {
                            SetClampJointOffline(null, shouldUseKinmaticStick);
                        }
                        else
                        {
                            SetClampJointOffline(null, shouldUseKinmaticStick);
                            SetClampJointRPC(-2);
                        }
                    }
                    // myRB.isKinematic = true;
                }

                //myStickSphereController.StickSquash();
                //clampSoundPlayer.EmitSound();

                if (stuckGameobject != null)
                {
                    OnClampVisualsAndSound();

                    // Networked
                    if (!PhotonNetwork.OfflineMode)
                    {
                        photonView.RPC("OnClampVisualsAndSound", RpcTarget.Others);
                    }


                    clampSearch = false;
                    clamped = true;

                    if (OnPlayerClamp != null)
                    {
                        OnPlayerClamp(teamIndex, playerNumber, stuckGameobject);
                    }

                    OnPlayerClampGlobal?.Invoke(teamIndex, playerNumber, stuckGameobject);

                    if (stuckTo3dButton)
                    {
                        // unstick immediately
                        UnclampAfterButtonStick();
                    }
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

    [PunRPC]
    public void SetClampJointOnline(int stickJointID)
    {
        myRopeSyncer.SetIsSticking(true);
        if (stickJointID == -2)
        {
            // Error check!
            if (stickJoint != null)
            {
                Destroy(stickJoint);
                stickJoint = null;
            }
            //stickJoint = gameObject.AddComponent(typeof(FixedJoint)) as FixedJoint;
            myRB.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; //this needs to be set before isKinematic or future collisions may be bugged
            myRB.isKinematic = true;
            myRopeSyncer.SetHasStickJoint(false);
        }
        else //we are sticking to a rigidbody with a photon view
        {
            // Error check!
            if (stickJoint != null)
            {
                Destroy(stickJoint);
                stickJoint = null;
            }
            stickJoint = gameObject.AddComponent(typeof(FixedJoint)) as FixedJoint;
            stickJoint.connectedBody = PhotonView.Find(stickJointID).gameObject.GetComponent<Rigidbody>();
            myRopeSyncer.SetHasStickJoint(true);
            //stickJoint.enableCollision = true;
        }

        clamped = true;
    }

    public void SetClampJointOffline(Rigidbody rigidClampable = null, bool shouldUseKinematicStick = false)
    {
        // Error check!
        if (stickJoint != null)
        {
            Destroy(stickJoint);
            stickJoint = null;
        }

        if (shouldUseKinematicStick)
        {
            myRB.isKinematic = true;
        }
        else
        {
            stickJoint = gameObject.AddComponent(typeof(FixedJoint)) as FixedJoint;

            if (rigidClampable != null)
            {

                stickJoint.connectedBody = rigidClampable;
                stickJoint.enableCollision = true;
            }
        }
        
        
        
    }

    public void SetClampJointRPC(int stickJointID)
    {
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("SetClampJointOnline", RpcTarget.Others, stickJointID);
        }
    }

    #endregion

    #region FixedUpdate(Movement_Forces)
    private void FixedUpdate()
    {
        // Networked Multiplayer
        if (!PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null && !photonView.IsMine)
        {
            return;
        }


        currLength = Vector3.Distance(myRB.transform.position, midLink.position) * 2f;

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

    public static event System.Action<int, int> OnPlayerJump;
    //Called from PlayerInput. down is true if ButtonDown, false if ButtonUp
    public void InputJump(bool down)
    {
        if (down)
        {
            if (!clampSearch)
            {
                if (/*isGrounded ||*/ isWithinCoyoteJumpTime)       // the isGrounded check is redundant. Since isWithinCoyoteJumpTime is also true in all cases when isGrounded is true, we can just use that as a way to reduce one check
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

                    OnPlayerJump?.Invoke(teamIndex, playerNumber);

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
    [PunRPC]
    private void JumpVisualsAndSound()
    {
        mySquashStretchController.JumpSquash();
        EventInstance jumpEvent = springActive ? springJumpSoundEvent : jumpSoundEvent;
        FMODUnity.RuntimeManager.AttachInstanceToGameObject(jumpEvent, transform, myRB);
        //jumpSoundEvent.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(transform));
        jumpEvent.start();
        jumpPoofParticles.transform.position = transform.position - new Vector3(0f, 0.4f, 0f);
        jumpPoofParticles.Emit(30);
    }

    public delegate void OnClampInputDelegate();
    public event OnClampInputDelegate OnClampInput;

    //Called from PlayerInput. down is true if ButtonDown, false if ButtonUp
    public void InputClamp(bool down)
    {
        if (down)
        {
            if (!clamped) clampSearch = true;

            //myStickSphereController.GrowSquash();

            LookingForClampVisualsAndSound();

            // Networked
            if (!PhotonNetwork.OfflineMode)
            {
                photonView.RPC("LookingForClampVisualsAndSound", RpcTarget.Others);
            }

            inputtingClamp = true;

            if (OnClampInput != null)
            {
                OnClampInput();
            }
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
        FMODUnity.RuntimeManager.AttachInstanceToGameObject(clampSoundEvent, transform, myRB);
        //clampSoundEvent.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(transform));
        clampSoundEvent.start();
    }

    /// <summary>
    /// Use this function on clamp, for its visuals and sound effects
    /// </summary>
    [PunRPC]
    private void OnClampVisualsAndSound()
    {
        myStickSphereController.StickSquash();
        FMODUnity.RuntimeManager.AttachInstanceToGameObject(stickSoundEvent, transform, myRB);
        //clampSoundEvent.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(transform));
        stickSoundEvent.start();
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
        FMODUnity.RuntimeManager.AttachInstanceToGameObject(declampSoundEvent, transform, myRB);
        //declampSoundEvent.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(transform));
        declampSoundEvent.start();
    }
    #endregion
    #endregion

    #region OtherFunctions
    public void SetGameplayType(CharacterContent.TeamInstanceType gameplayType)
    {
        this.gameplayType = gameplayType;
    }
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

    public delegate void GroundedDelegate();
    public event GroundedDelegate OnGrounded;
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
            {
                triggerCount++;
            }
        }

        if (groundCollides.Length - triggerCount > 1) //Did the overlap check find anything? If it is only 1 then it only collided with the current player.
        {
            isGrounded = true;
            speed = groundMovementForce;
            angularSpeed = 100f;

            if (OnGrounded != null)
            {
                OnGrounded();
            }
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

        if (groundParticlePointer != null)
        {
            UpdateGroundParticles(isGrounded);
        }
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
                if (PhotonNetwork.OfflineMode)
                {
                    Destroy(stickJoint);
                    stickJoint = null;
                }
                else
                {
                    Destroy(stickJoint);
                    stickJoint = null;
                    if (photonView.IsMine)
                    {
                        photonView.RPC("DestroyStickJointRPC", RpcTarget.Others);
                    }
                }
            }
            else
            {
                Debug.LogError("Custom error, this shouldn't run lol (stickJoint should always be set to something now)... No not anymore this can happen again - Ryan");
                myRB.isKinematic = false;
                myRB.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                if (photonView.IsMine)
                {
                    photonView.RPC("DestroyStickJointRPC", RpcTarget.Others);
                }
            }

            //deClampSoundPlayer.EmitSound();
            OnUnclampSound();

            if (!PhotonNetwork.OfflineMode && photonView.IsMine)
            {
                photonView.RPC("OnUnclampSound", RpcTarget.Others);
            }

            clamped = false;

            if (PhotonNetwork.OfflineMode || PhotonNetwork.CurrentRoom == null || photonView.IsMine)
            {
                if (OnPlayerUnclamp != null)
                {
                    OnPlayerUnclamp(teamIndex, playerNumber);
                }
            }

            /*if (!PhotonNetwork.OfflineMode)
            {
                photonView.RPC("OnPlayerUnclampRPC", RpcTarget.Others, teamIndex, playerNumber);
            }*/
        }
    }

    [PunRPC]
    public void DestroyStickJointRPC()
    {
        myRopeSyncer.SetIsSticking(false);
        if (stickJoint != null)
        {
            Destroy(stickJoint);
            stickJoint = null;
        }
        else
        {
            myRB.isKinematic = false;
            myRB.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        clamped = false;
    }

    /// <summary>
    /// Checks to see if the player is clamping while respawning, and if they are, unclamps them while maintaining clamp input so that after respawn the player can stick again
    /// </summary>
    void CheckClampOnRespawn(int teamNumber)
    {
        if (teamNumber == teamIndex)
        {
            isRespawning = true;

            if (clamped)
            {
                UnclampThenStartClampSearch(2.5f);
            }
            //if (stickJoint != null)
            //{
            //    if (PhotonNetwork.OfflineMode)
            //    {
            //        Destroy(stickJoint);
            //        stickJoint = null;
            //    }
            //    else
            //    {
            //        Destroy(stickJoint);
            //        stickJoint = null;
            //        photonView.RPC("DestroyStickJointRPC", RpcTarget.Others);
            //    }
            //}
        }
    }

    private void TeamRespawnProcessComplete(int teamNumber)
    {
        if (TeamIndex == teamNumber)
        {
            isRespawning = false;
        }
    }

    public delegate void OnBumpedLoseCoinsDelegate(int playerNumber);
    public event OnBumpedLoseCoinsDelegate OnBumpedLoseCoins;
    /// <summary>
    /// Use this method to add force to push even clamped players
    /// The force and forceMode parameters are the same as Rigidbody.AddForce() parameters
    /// </summary>
    /// <param name="force">Force to be applied</param>
    /// <param name="forceMode">ForceMode</param>
    /// <param name="handleClamping">Should the player get unclamped?</param>
    /// <param name="pauseInput">Should the input get paused for a fraction of a second after getting bumped?</param>
    public void AddBumperForce(Vector3 force, bool loseCoins, ForceMode forceMode = ForceMode.VelocityChange, bool handleClamping = true, bool pauseInput = true)
    {
        StartCoroutine(AddBumperForceCoroutine(force, loseCoins, forceMode, handleClamping, pauseInput));
    }

    private IEnumerator AddBumperForceCoroutine(Vector3 force, bool loseCoins, ForceMode forceMode = ForceMode.VelocityChange, bool handleClamping = true, bool pauseInput = true)
    {
        if (handleClamping)
        {
            if (clamped)
            {
                UnclampThenStartClampSearch(0.6f);
            }
        }

        yield return null;  // wait for a frame before adding forces

        myRB.AddForce(force, forceMode);
        cameraShake.Shake(0.10f, 0.2f, 150);

        if (pauseInput)
        {
            PlayerInput pi = gameObject.GetComponent<PlayerInput>();
            pi.PauseMovementInputForTime(0.5f);
        }

        if (loseCoins)
        {
            if (OnBumpedLoseCoins != null)
            {
                OnBumpedLoseCoins(playerNumber);
            }
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
    private void UnclampThenStartClampSearch(float waitToReclampTime)
    {
        Unclamp();

        if (!PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null && !photonView.IsMine)
        {
            return;
        }

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

        StartCoroutine(StartClampSearchInSeconds(waitToReclampTime));
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

    public void UnclampAfterButtonStick(bool immediate = false)
    {
        if (!immediate && gameObject.activeSelf)
        {
            StartCoroutine(UnclampAfterButtonStickCoroutine());
        }
        else
        {
            UnclampAfterButtonStickLogic();
        }
    }

    private IEnumerator UnclampAfterButtonStickCoroutine()
    {
        yield return new WaitForSeconds(0.1f);

        UnclampAfterButtonStickLogic();
    }

    private void UnclampAfterButtonStickLogic()
    {
        Unclamp();

        if (!PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null && !photonView.IsMine)
        {
            return;
        }

        inputtingClamp = false;
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

    /// <summary>
    /// parameters are rewired ID and player Number
    /// </summary>
    public static event OnPlayerClampDelegate OnPlayerClampGlobal;

    #region RPCs
    /*[PunRPC]
    public void OnPlayerClampRPC(int teamNum, int playerNum, int GOID)
    {
        if (OnPlayerClamp != null)
        {
            GameObject go = PhotonView.Find(GOID).gameObject;

            if (go != null)
            {
                OnPlayerClamp(teamNum, playerNum, go);
            }
        }
    }

    [PunRPC]
    public void OnPlayerUnclampRPC(int teamNum, int playerNum)
    {
        if (OnPlayerUnclamp != null)
        {
            OnPlayerUnclamp(teamNum, playerNum);
        }
    }*/
    #endregion

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

    public void ChangeBalloonsStatus(bool toggle)
    {

        if (toggle)
        {
            initialJumpVelocity = balloonsInitialJumpVelocity;
        }
        else
        {
            initialJumpVelocity = saveInitialJumpVelocity;
        }
        balloonsActive = toggle;
    }

    public void SetBalloonModifier(bool toggle, float modifyFactor)
    {
        float modifierDiff = balloonsInitialJumpVelocity - saveInitialJumpVelocity;

        if (toggle)
        {
            initialJumpVelocity += modifierDiff * modifyFactor;
        }
        else
        {
            initialJumpVelocity -= modifierDiff * modifyFactor;
        }

        initialJumpVelocity = Mathf.Clamp(initialJumpVelocity, saveInitialJumpVelocity, balloonsInitialJumpVelocity);
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
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnTeamDeath -= CheckClampOnRespawn;
            RaceManager.Instance.OnTeamRespawnDelayFinished -= TeamRespawnProcessComplete;
        }
    }

    #region Gizmos_And_Debugging
    // Gizmos - for debugging
    /*
    void OnDrawGizmos()
    {
        if (teamIndex != 1)
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
