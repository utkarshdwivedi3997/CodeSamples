using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using System.Reflection;


public class s_playerController : MonoBehaviour {

    //Player number
    public bool isPlayer1 = false;
    private int playerNumber;
    private string suffix = "", enemySuffix="";

    //Movement
    [Header("Movement")]
    public float walkSpeed = 10f;
    public float maxWalkSpeed = 15f;
    public float runSpeed = 20f;
    public float jumpForce = 20f;
    public float gravMultiplier = 2.5f;
    public LayerMask whatIsGround;
    public Transform[] bottom;
    private Vector3 currVelocity;
    private bool isJumping = false, isFalling = false;
    public float maxJumpTimer = 0.15f;
    private float jumpTimer = 0;

    //Camera stuff
    [Header("Camera")]
    public float lookSensitivity = 3f;
    private float currentLookSensitivity;
    private float xMultiplier = 1f;
    public float maxCamY = 60f;
    public float minCamY = -60f;
    private float rotationY = 0;
    [SerializeField]
    private Camera myCam;

    private Rigidbody myRb;

    //Animation
    [Header("Animation")]
    public Animator myAnimator;

    //abilities
    [Header("Traps")]
    public List<s_parent_Trap.TRAPS> names = new List<s_parent_Trap.TRAPS>();
    public List<GameObject> trap = new List<GameObject>();
    public float throwForce = 20f;
    [SerializeField]
    public Dictionary<s_parent_Trap.TRAPS, GameObject> traps = new Dictionary<s_parent_Trap.TRAPS, GameObject>();
    private List<s_parent_Trap.TRAPS> availableTraps = new List<s_parent_Trap.TRAPS>();
    private s_parent_Trap.TRAPS selectedTrap = s_parent_Trap.TRAPS.NONE;
    private Queue<GameObject> fieldTraps = new Queue<GameObject>();            //Queue of traps currently deployed on field
    private int nTraps;
    private bool canThrow = false;
    private bool trapAxisInUse = false;
    public GameObject handTrap;

    public Transform trapSpawnPoint;

    public int maxDeployableTraps = 4;              //maximum number of traps that can be deployed simultaneously
    private int nDeployedTraps = 0;                  //the current number of deployed traps
    public int pickupGetCount = 3;
    //public int maxDecoys = 3;

    public int maxCarriedTraps = 5;
    private int selectedTrapIndex = 0;
    private List<int> nSelectedTrap = new List<int>();                    //use this instead of separate nDecoys, nSensitivityTraps etc. for each trap
    public GameObject decoySpawnLocation;
    public GameObject body;

    [Header("Decoy")]
    public int startingDecoyTraps = 2;
    //public float stillTrapCooldown = 3f;            //cooldown time for standing still trap

    [Header("Sensitivity Trap")]
    public int startingSensitivityTraps = 2;
    public float trapSlowDownMultiplier = 0.3f;     //how much should the speed and sensitivity of the player be slowed down by
    public float sensitivityTrapCooldown = 7f;      //cooldown time for the trap
    private float trapMovementMultiplier = 1;       //use this to manage dynamic changing of the Multiplier
    

    private bool scrollAxisInUse = false;

    private List<bool> isTrapped = new List<bool>();

    [Header("Hacks")]
    public float hackCooldownTime = 3f;

    private float timeToResetHack;
    private bool isHacking = false, hasMirroredSelf = false, hasBlackoutSelf = false, hackedEnemy = false, isGlitched = false;
    private int selectedAbility = 0;
    private bool hackAxisInUse = false;

    public float mirrorCooldownTime = 5f;
    public float blackoutCooldownTime = 3f;
    public float enemyBlackoutRange = 20f;
    public Image blackout;

    private int fadeDir = 1;

    [Header("UI")]
    public Text[] ui_nTraps;
    public Text ui_totalTraps, ui_currTrap, ui_flagStatus;
    public Image ui_timerImage;
    public Text ui_prompt;
    public Slider ui_progressSlider;
    public Animator ui_crosshair;
    public GameObject ui_slowdownIncrease;
    public GameObject ui_decoyIncrease;
    //flag
    [Header("Flag")]
    public GameObject myFlag;
    public float flagHoldTimer = 2f;
    private bool hasSelfFlag = true, hasEnemyFlag = false;
    private bool canTakeFlag = false;
    private float timeToTakeFlag;

    //Particles
    [Header("Particles")]
    public GameObject hitByTrapParticle;
    public GameObject trapCollectParticle;

	// Use this for initialization
	void Start () {
        myRb = GetComponent<Rigidbody>();

        if (isPlayer1)
        {
            suffix = "_P1";
            enemySuffix = "_P2";
            playerNumber = 1;
        }
        else
        {
            suffix = "_P2";
            enemySuffix = "_P1";
            playerNumber = 2;
        }

        for (int i = 0; i < names.Count; i++)
        {
            traps.Add(names[i], trap[i]);
            isTrapped.Add(false);
            //nSelectedTrap.Add(0);
        }
        //set number of traps that can be held
        availableTraps.Add(s_parent_Trap.TRAPS.NONE);
        //availableTraps.Add(s_parent_Trap.TRAPS.DECOY);
        //availableTraps.Add(s_parent_Trap.TRAPS.SLOWDOWN);
        //availableTraps.Add(s_parent_Trap.TRAPS.STOP);
        nTraps = availableTraps.Count;
        nSelectedTrap.Add(0);
        //nSelectedTrap[0] = 0;
        //nSelectedTrap[1] = 0;
        //nSelectedTrap[2] = 0;

        //nSelectedTrap[0] = maxDecoys;
        //nSelectedTrap[1] = maxSensitivityTraps;
        //nSelectedTrap[2] = maxStopTraps;
        //Debug.Log(nTraps);

        //set UI stuff
        ui_nTraps[0].text = "Decoys: " + startingDecoyTraps;
        ui_nTraps[1].text = "Slowdowns: " + startingSensitivityTraps;
        //ui_nTraps[2].text = "Stops: " + startingStopTraps;
        ui_totalTraps.text = "Total traps on field: " + nDeployedTraps;
        ui_currTrap.text = "Selected Trap: " + selectedTrap;
        ui_prompt.gameObject.SetActive(false);
        ui_progressSlider.gameObject.SetActive(false);
        ui_flagStatus.gameObject.SetActive(false);

        /*isTrapped = new bool[nTraps];
        for (int i = 0; i < nTraps; i++)
        {
            isTrapped[i] = false;
        }*/

        currentLookSensitivity = lookSensitivity;

        timeToResetHack = hackCooldownTime;

        blackout.color = new Color(0, 0, 0, 1);
        blackout.canvasRenderer.SetAlpha(0);

        //flag
        timeToTakeFlag = 0;
        //ui_crosshair.SetBool("aim",true);

        availableTraps.Add(s_parent_Trap.TRAPS.SLOWDOWN);
        nSelectedTrap.Add(startingSensitivityTraps);
        availableTraps.Add(s_parent_Trap.TRAPS.DECOY);
        nSelectedTrap.Add(startingDecoyTraps);

        handTrap.SetActive(false);
    }
	
	// Update is called once per frame
	void FixedUpdate () {

        //movement stuff. please improve this.
        float horizontal = Input.GetAxis("Horizontal" + suffix);
        float vertical = Input.GetAxis("Vertical" + suffix);

        float camXRot = Input.GetAxisRaw("CamX" + suffix);
        float camYRot = Input.GetAxisRaw("CamY" + suffix);


        //don't let diagonal movement be faster than singular axis movement
        float pythagoreanValue = ((horizontal * horizontal) + (vertical * vertical));
        if (pythagoreanValue * walkSpeed > (maxWalkSpeed * maxWalkSpeed))
        {
            float multiplier = maxWalkSpeed / (Mathf.Sqrt(pythagoreanValue));
            horizontal *= multiplier;
            vertical *= multiplier;
        }

        //do the movement
        Vector3 moveDir = (transform.right * horizontal * xMultiplier + transform.forward * vertical) * walkSpeed * trapMovementMultiplier;
        currVelocity = new Vector3(moveDir.x, myRb.velocity.y, moveDir.z);

        if (isJumping)
        {
            jumpTimer += Time.deltaTime;
            //currVelocity = new Vector3(currVelocity.x, myRb.velocity.y + jumpForce, currVelocity.z);
            myRb.AddForce(transform.up * jumpForce);
            if (jumpTimer>=maxJumpTimer)
            {
                isJumping = false;
                isFalling = true;
                jumpTimer = 0;
                myAnimator.SetTrigger("Fall");
            }

        }

        if (isFalling)
        {
            currVelocity += transform.up * Physics.gravity.y * (gravMultiplier - 1) * Time.deltaTime;
            if (IsGrounded())
            {
                isFalling = false;
                myAnimator.SetLayerWeight(1, 0.0f);
            }
        }

        myRb.velocity = currVelocity;
        //myRb.MovePosition(transform.position + moveDir * walkSpeed * trapMovementMultiplier * Time.deltaTime);
        //myRb.AddForce(moveDir * walkSpeed, ForceMode.Force);                //hope to god this shit works. edit: it doesn't. pls fix asap.

        Vector3 rotation = new Vector3(0f, camXRot, 0f) * currentLookSensitivity * xMultiplier;
        myRb.MoveRotation(myRb.rotation * Quaternion.Euler(rotation));

        rotationY += camYRot * currentLookSensitivity;
        rotationY = Mathf.Clamp(rotationY, minCamY, maxCamY);
        Vector3 camRotation = new Vector3(rotationY, myCam.transform.localEulerAngles.y, 0f);
        myCam.transform.localEulerAngles = -camRotation;
        //myCam.transform.Rotate(-camRotation);

        //Debug.Log(moveDir * walkSpeed * Time.deltaTime);

        if (myAnimator != null)
        {
            myAnimator.SetFloat("Horizontal", horizontal);
            myAnimator.SetFloat("Vertical", vertical);
        }
	}

    private bool IsGrounded()
    {
        foreach (Transform b in bottom)
        {
            Collider[] hitColliders = Physics.OverlapSphere(b.position, 0.1f, whatIsGround);
            if (hitColliders.Length > 0)
            {
                return true;
            }
        }
        return false;
    }

    void Update()
    {
        //Set jump = true for next call to FixedUpdate() to make the character jump
        //Can't check for ButtonDown in FixedUpdate because it can run multiple times a single frame, making jump become inconsistent
        if (Input.GetButtonDown("Jump" + suffix) && IsGrounded())
        {
            isJumping = true;
            myAnimator.SetLayerWeight(1, 1.0f);
            myAnimator.SetTrigger("Jump");
        }

        if (isHacking)
        {
            if (timeToResetHack>=0)
            {
                timeToResetHack -= Time.deltaTime;
                ui_timerImage.fillAmount = timeToResetHack / hackCooldownTime;
            }
            if (timeToResetHack<=0)
            {
                timeToResetHack = hackCooldownTime;
                isHacking = false;
                ui_timerImage.fillAmount = 1f;
                if (hackedEnemy)
                {
                    hackedEnemy = false;
                    //s_gameManager.Instance.HackEnemy((2 - playerNumber) + 1, 1);
                }
                else if (hasMirroredSelf)
                {
                    UseMirror(true);
                    hasMirroredSelf = false;
                }
                else if (hasBlackoutSelf)
                {
                    UseBlackout(true);
                    hasBlackoutSelf = false;
                }
            }
        }

        //handle glitchy-ness
        if (isGlitched)
        {
            float currDistance = s_roundManager.Instance.GetPlayerDistance();
            float glitchVal = 1 - Mathf.Pow(currDistance, 3) / Mathf.Pow(enemyBlackoutRange, 3);           //exponential curve math function to determine amount of blackout, normalized between 0 and 1
            glitchVal = Mathf.Clamp(glitchVal, 0, 1);                                                      //clamp it so it never goes beyond 1 -- extra precautionary measure
            blackout.CrossFadeAlpha(glitchVal, 0.1f, true);
        }

        // handle trap drop input
        // scrollAxisInUse - use this to reset the scroll axis input to 0 once there is no input
        if (Input.GetAxisRaw("TrapScroll" + suffix) == 1f && availableTraps.Count > 0 && !scrollAxisInUse)
        {
            scrollAxisInUse = true;
            if (availableTraps.Count > 1)
            {
                if (selectedTrapIndex + 1 < availableTraps.Count)
                {
                    selectedTrapIndex++;
                }
                else
                {
                    selectedTrapIndex = 1;
                }
                selectedTrap = availableTraps[selectedTrapIndex];
                ui_currTrap.text = "Selected trap: " + selectedTrap;
                //Debug.Log("Current trap: " + selectedTrap + " num: " + nSelectedTrap[selectedTrapIndex]);
            }
        }
        if (Input.GetAxisRaw("TrapScroll" + suffix) == -1f && availableTraps.Count > 0 && !scrollAxisInUse)
        {
            scrollAxisInUse = true;
            if (availableTraps.Count > 1)
            {
                if (selectedTrapIndex - 1 > 0)
                {
                    selectedTrapIndex--;
                }
                else
                {
                    selectedTrapIndex = availableTraps.Count - 1;
                }
                selectedTrap = availableTraps[selectedTrapIndex];
                ui_currTrap.text = "Selected trap: " + selectedTrap;
                //Debug.Log("Current trap: " + selectedTrap);
            }
        }
        //reset scroll axis in use
        if (Input.GetAxisRaw("TrapScroll" + suffix) == 0 && scrollAxisInUse)
        {
            scrollAxisInUse = false;
        }

        if ((Input.GetAxisRaw("Drop" + suffix) <= -0.3f || Input.GetAxisRaw("Drop" + suffix) >= 0.3f) && !canThrow)
        {
            //Debug.Log("Trap dropped");
            //trapAxisInUse = true;
            if (selectedTrapIndex > 0)
            {
                canThrow = true;
                ui_crosshair.SetBool("aim", false);
                myAnimator.SetLayerWeight(2, 1.0f);
                myAnimator.SetTrigger("Aim");
                handTrap.SetActive(true);
            }
        }

        if ((Input.GetAxisRaw("Drop" + suffix) > -0.3f && Input.GetAxisRaw ("Drop" + suffix)<= 0.3f) && canThrow)
        {
            canThrow = false;
            ui_crosshair.SetBool("aim", true);
            //myAnimator.SetLayerWeight(2, 0.0f);
            
            DropTrap();
        }

        //handle taking flag
        if (canTakeFlag)
        {
            if (Input.GetButton("Capture" + suffix))
            {
                if (!ui_progressSlider.gameObject.activeSelf)
                {
                    ui_progressSlider.gameObject.SetActive(true);
                }
                if (timeToTakeFlag <= flagHoldTimer)
                {
                    timeToTakeFlag += Time.deltaTime;
                    ui_progressSlider.value = timeToTakeFlag/flagHoldTimer;
                }
                else
                {
                    hasEnemyFlag = true;
                    canTakeFlag = false;
                    Debug.Log(suffix+" flag taken");
                    s_roundManager.Instance.SetWinner(playerNumber);
                    /*
                     * This is for getting back to base. Don't delete it just in case we need it again.
                     * 
                    s_roundManager.Instance.RemoveFlag((2 - playerNumber) + 1);
                    ui_prompt.gameObject.SetActive(false);
                    ui_progressSlider.gameObject.SetActive(false);
                    ui_flagStatus.gameObject.SetActive(true);
                    ui_flagStatus.text = "Data retrieved.\nHead back to base.";
                    StartCoroutine(DeactivateFlagUI());*/
                }
            }
            else
            {
                timeToTakeFlag = 0;
                ui_progressSlider.value = 0;
                if (ui_progressSlider.gameObject.activeSelf)
                {
                    ui_progressSlider.gameObject.SetActive(false);
                }
            }
        }

        if (!isHacking)
        {
            if (Input.GetButtonDown("UseMirror" + suffix))
            {
                UseMirror(false);
                isHacking = true;
            }

            if (Input.GetButtonDown("UseBlackout" + suffix) && s_roundManager.Instance.GetPlayerDistance() < enemyBlackoutRange)
            {
                UseBlackout(false);
                isHacking = true;
            }
        }

        /*
         * ================  HACK SELECTION AND SELF HACK. ONLY REMOVE WHEN CONFIRMED THAT WE DON'T WANT THIS FEATURE ================
         * 
        // handle hack (ability) selection input
        // hackAxisInUse - bool to check whether there is any hack scrolling input from the player
        // this is necessary because without the bool the axis will never reset to 0
        if (Input.GetAxisRaw("Ability" + suffix) == 1f && !hackAxisInUse)
        {
            hackAxisInUse = true;
            selectedAbility = 1;
            //Debug.Log("AbilityUp");
        }
        if (Input.GetAxisRaw("Ability" + suffix) == -1f && !hackAxisInUse)
        {
            hackAxisInUse = true;
            selectedAbility = 2;
            //Debug.Log("AbilityDown");
        }
        //reset hackAxisInUse to 0 once there is no input
        if (Input.GetAxisRaw("Ability" + suffix) == 0f && hackAxisInUse)
        {
            hackAxisInUse = false;
        }

        //handle hack (ability) use input
        if (Input.GetButtonDown("UseOnSelf" + suffix))
        {
            //Debug.Log("UseSelf");
            if (selectedAbility == 1)
            {
                if (hasMirroredSelf || !isHacking)
                {
                    UseMirror(true);
                    isHacking = true;
                    hasMirroredSelf = !hasMirroredSelf;
                }
            }
            else if (selectedAbility == 2)
            {
                if (hasBlackoutSelf || !isHacking)
                {
                    UseBlackout(true);
                    isHacking = true;
                    hasBlackoutSelf = !hasBlackoutSelf;
                }
            }
        }
        if (Input.GetButtonDown("UseOnEnemy" + suffix) && !isHacking)
        {
            hackedEnemy = true;
            //Debug.Log("UseEnemy");
            if (selectedAbility == 1)
            {
                UseMirror(false);
                isHacking = true;
            }
            else if (selectedAbility == 2 && s_roundManager.Instance.GetPlayerDistance() <= enemyBlackoutRange)
            {
                UseBlackout(false);
                isHacking = true;
            }
            else
            {
                Debug.Log("Enemy Too Far!!");
            }
        }
        *
        * 
        */
    }

    //TRAP DROPPING CODE FOLLOWS

    private void DropTrap()
    {
        /*if (nDeployedTraps >= maxDeployableTraps)
        {
            Debug.Log("MAX POSSIBLE TRAPS DEPLOYED");
            return;
        }*/
        if (nSelectedTrap[selectedTrapIndex] > 0)
        {
            float offset = 0.7f;
            GameObject trap = null; // = Instantiate(traps[selectedTrap], myCam.transform.position + myCam.transform.forward * offset, Quaternion.identity) as GameObject;

            if (selectedTrap != s_parent_Trap.TRAPS.DECOY)
            {
                trap = Instantiate(traps[selectedTrap], trapSpawnPoint.transform.position /* + myCam.transform.forward * offset */, Quaternion.identity) as GameObject;
                trap.GetComponent<Rigidbody>().AddForce(myCam.transform.forward * throwForce);
                trap.GetComponent<s_parent_Trap>().SetOwner(playerNumber);
            }
            if (selectedTrap == s_parent_Trap.TRAPS.DECOY)
            {
                trap = Instantiate(traps[selectedTrap], decoySpawnLocation.transform.position, decoySpawnLocation.transform.rotation) as GameObject;
                //trap.transform.Rotate(0, body.transform.rotation.y * 5, 0);
                trap.GetComponent<s_parent_Trap>().SetOwner(playerNumber);
            }
            //trap.GetComponent<s_parent_Trap>().SetOwner(playerNumber);

            handTrap.SetActive(false);
            myAnimator.SetTrigger("Throw");
            StartCoroutine(ResetAnimatorLayerWeight());

            nSelectedTrap[selectedTrapIndex]--;

            switch (selectedTrap)
            {
                case s_parent_Trap.TRAPS.DECOY:
                    ui_nTraps[0].text = "Decoys: " + nSelectedTrap[selectedTrapIndex];
                    break;
                case s_parent_Trap.TRAPS.SLOWDOWN:
                    ui_nTraps[1].text = "Slowdowns: " + nSelectedTrap[selectedTrapIndex];
                    break;

                /* ================ STOP TRAPS ================
                 * 
                 * 
                case s_parent_Trap.TRAPS.STOP:
                    ui_nTraps[2].text = "Stop Traps: " + nSelectedTrap[selectedTrapIndex];
                    break;
                    *
                    * 
                    */
            }
            if (nSelectedTrap[selectedTrapIndex]<=0)
            {
                nSelectedTrap.RemoveAt(selectedTrapIndex);
                availableTraps.RemoveAt(selectedTrapIndex);
                if (availableTraps.Count > selectedTrapIndex + 1)
                {
                    selectedTrapIndex++;
                }
                else
                {
                    selectedTrapIndex = availableTraps.Count - 1;
                }

                selectedTrap = availableTraps[selectedTrapIndex];
                ui_currTrap.text = "Selected trap: " + selectedTrap;

            }
            //ui_nTraps[selectedTrapIndex].text = (selectedTrap==0 ? "Decoys: ":"Slowdowns: ") + nSelectedTrap[selectedTrapIndex];
            nDeployedTraps++;
            if (nDeployedTraps <= maxDeployableTraps)
            {
                fieldTraps.Enqueue(trap.gameObject);               // Add trap at end of queue
            }
            else
            {
                Destroy(fieldTraps.Peek().gameObject);             // Destroy trap at start of queue
                fieldTraps.Dequeue();                   // Pop the queue
            }
            ui_totalTraps.text = "Total traps on field: " + nDeployedTraps;
        }
    }

    //TRAP CODE FOLLOWS

    /* trigger the trap of type -- trap type
     * trapType:
     *          0 - Decoy trap
     *          1 - Slowdown trap
     *          2 - Stay still trap
    */
    public void TriggerTrap(s_parent_Trap.TRAPS trapType)
    {
        if (trapType == s_parent_Trap.TRAPS.DECOY)
        {
            return;
        }
        else if (trapType == s_parent_Trap.TRAPS.SLOWDOWN)
        {
            isTrapped[selectedTrapIndex] = true;
            currentLookSensitivity *= trapSlowDownMultiplier;
            trapMovementMultiplier = trapSlowDownMultiplier;
            hitByTrapParticle.SetActive(true);
            StartCoroutine(TrapCountdown(trapType));
        }

        /* ================ STOP TRAPS ================
         * 
         * 
        else if (trapType == s_parent_Trap.TRAPS.STOP)
        {
            isTrapped[selectedTrapIndex] = true;
            StartCoroutine(TrapCountdown(trapType));
        }
        *
        * 
        */
    }

    //trap countdown code
    IEnumerator TrapCountdown(s_parent_Trap.TRAPS trapType)
    {
        if (trapType == s_parent_Trap.TRAPS.SLOWDOWN)
        {
            yield return new WaitForSeconds(sensitivityTrapCooldown);
            currentLookSensitivity = lookSensitivity;
            trapMovementMultiplier = 1;
            hitByTrapParticle.SetActive(false);
        }

        /*
         * /* ================ STOP TRAPS ================
         * 
        else if (trapType == s_parent_Trap.TRAPS.STOP)
        {
            walkSpeed = 0f;
            //myRb.isKinematic = true;
            yield return new WaitForSeconds(3);
            //myRb.isKinematic = false;
            walkSpeed = 10f;
        }
        *
        * 
        */

        else yield return null;

        isTrapped[selectedTrapIndex] = false;
    }

    IEnumerator ResetAnimatorLayerWeight()
    {
        yield return new WaitForSeconds(0.4f);
        myAnimator.SetLayerWeight(2, 0.0f);
    }

    //MIRROR HACK CODE FOLLOWS

    //use mirror ability.
    //bool useOnSelf is true if used on own camera, false if used on enemy
    //useOnSelf - hacking self? true, hacking enemy? false
    private void UseMirror(bool useOnSelf)
    {
        /*
         *  ================ REQUIRED ONLY FOR SELF HACKS ================
         *  
        if (useOnSelf)
        {
            if (selectedAbility==1)
            {
                MirrorMe(true);
                xMultiplier *= -1;              //set controls to become inverse
            }
        }
        else
        *
        */

        {
            s_roundManager.Instance.HackEnemy((2 - playerNumber) + 1, 1);
        }
    }

    //mirror the player this script is attached to.
    //bool mirrorAttack is true if this player gets hacked by another player, otherwise false
    //useOnSelf - hack initiated by this player? true. hack attack by enemy? false.
    public void MirrorMe(bool useOnSelf)
    {
        //Debug.Log(xMultiplier);
        myCam.GetComponent<s_MirrorCamera>().Invert();
        if (!useOnSelf)
        {
            StartCoroutine(ResetHack(1));
        }
    }

    //use blackout ability.
    //bool useOnSelf is true if used on own camera, false if used on enemy
    //useOnSelf - hacking self? true, hacking enemy? false
    private void UseBlackout(bool useOnSelf)
    {
        /* ================ REQUIRED ONLY FOR SELF HACKS ================
         * 
         * 
        if (useOnSelf)
        {
            if (selectedAbility == 2)
            {
                BlackoutMe(true);
            }
        }
        else
        *
        * 
        */
        {
            s_roundManager.Instance.HackEnemy((2 - playerNumber) + 1, 2);
        }
    }

    //blackout the player this script is attached to.
    //useOnSelf - hack initiated by this player? true. hack attack by enemy? false.

    public void BlackoutMe(bool useOnSelf)
    {
        /* ================ REQUIRED ONLY FOR SELF HACKS ================
         * 
         * 
        if (useOnSelf)
        {
            if (fadeDir > 0)
            {
                blackout.CrossFadeAlpha(1.0f, 0.7f, true);
            }
            else if (fadeDir < 0)
            {
                blackout.CrossFadeAlpha(0, 1.3f, true);
            }
            fadeDir *= -1;
        }
        else
        *
        * 
        */
        {
            StartCoroutine(ResetHack(2));
            isGlitched = true;
        }
    }

    //reset state after cooldown is over (when hacked by enemy)
    IEnumerator ResetHack(int hackType)
    {
        if (hackType == 1)
        {
            yield return new WaitForSeconds(mirrorCooldownTime);
            MirrorMe(true);
        }
        else if (hackType==2)
        {
            yield return new WaitForSeconds(blackoutCooldownTime);
            blackout.CrossFadeAlpha(0, 1.3f, true);
            isGlitched = false;
        }
        yield return null;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag.Equals("Flag" + enemySuffix) && !hasEnemyFlag)
        {
            Debug.Log("in");
            canTakeFlag = true;
            ui_prompt.gameObject.SetActive(true);
            ui_prompt.text = "Hold either bumper to disable opponent";
        }
        else if (other.gameObject.tag.Equals("PickupDecoy"))
        {
            other.gameObject.SetActive(false);
            if (!availableTraps.Contains(s_parent_Trap.TRAPS.DECOY))
            {
                availableTraps.Add(s_parent_Trap.TRAPS.DECOY);
                nSelectedTrap.Add(0);
            }
            nSelectedTrap[availableTraps.IndexOf(s_parent_Trap.TRAPS.DECOY)] += pickupGetCount;
            ui_nTraps[0].text = "Decoys: " + nSelectedTrap[availableTraps.IndexOf(s_parent_Trap.TRAPS.DECOY)];
            StartCoroutine(PlayPickupAnimation(ui_decoyIncrease));
        }
        else if (other.gameObject.tag.Equals("PickupSlowdown"))
        {
            other.gameObject.SetActive(false);
            if (!availableTraps.Contains(s_parent_Trap.TRAPS.SLOWDOWN))
            {
                availableTraps.Add(s_parent_Trap.TRAPS.SLOWDOWN);
                nSelectedTrap.Add(0);
            }
            nSelectedTrap[availableTraps.IndexOf(s_parent_Trap.TRAPS.SLOWDOWN)] += pickupGetCount;
            ui_nTraps[1].text = "Slowdowns: " + nSelectedTrap[availableTraps.IndexOf(s_parent_Trap.TRAPS.SLOWDOWN)];
            StartCoroutine(PlayPickupAnimation(ui_slowdownIncrease));
        }
        /* ================ STOP TRAPS ================
         * 
         * 
        else if (other.gameObject.tag.Equals("PickupStop"))
        {
            Destroy(other.gameObject);
            if (!availableTraps.Contains(s_parent_Trap.TRAPS.STOP))
            {
                availableTraps.Add(s_parent_Trap.TRAPS.STOP);
                nSelectedTrap.Add(0);
            }
            nSelectedTrap[availableTraps.IndexOf(s_parent_Trap.TRAPS.STOP)]++;
            ui_nTraps[2].text = "Stops: " + nSelectedTrap[availableTraps.IndexOf(s_parent_Trap.TRAPS.STOP)];
        }
        *
        * 
        */
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag.Equals("Flag" + enemySuffix) && !hasEnemyFlag)
        {
            Debug.Log("out");
            canTakeFlag = false;
            ui_prompt.gameObject.SetActive(false);
            ui_progressSlider.gameObject.SetActive(false);
        }
    }

    IEnumerator PlayPickupAnimation(GameObject anim)
    {
        anim.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        anim.SetActive(false);
    }
    public int GetPlayerNumber()
    {
        return playerNumber;
    }

    public void DecreaseDeployedTraps()
    {
        if (nDeployedTraps > 0) 
        {
            nDeployedTraps--;
            ui_totalTraps.text = "Total traps on field: " + nDeployedTraps;
        }
    }

    //remove my flag

    /* Required for getting back to base feature
     * 
     * 
    public void RemoveFlag()
    {
        hasSelfFlag = false;
        myFlag.SetActive(false);
        Debug.Log(suffix + " flag lost");
        ui_flagStatus.gameObject.SetActive(true);
        ui_flagStatus.text = "Data stolen!\nRetrieve it!";
        StartCoroutine(DeactivateFlagUI());
    }

    //return whether this player has enemy flag or not
    public bool HasEnemyFlag()
    {
        return hasEnemyFlag;
    }

    IEnumerator DeactivateFlagUI()
    {
        yield return new WaitForSeconds(2f);
        ui_flagStatus.gameObject.SetActive(false);
    }
    *
    * 
    */


        /* -------------------- REFLECTION TO GET AND SET PARAMETERS -------------------- */

        //use c# concept "reflection" to set variable value using variable names
    public void SetIntParameter(string paramName, int paramValue)
    {
        int val = (int)GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField).GetValue(this);
        GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField).SetValue(this, paramValue);
    }

    public void SetFloatParameter(string paramName, float paramValue)
    {
        float val = (float)GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField).GetValue(this);
        GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField).SetValue(this, paramValue);
        if (paramName.Equals("lookSensitivity"))
        {
            currentLookSensitivity = lookSensitivity;
        }
        /*else if (paramName.Equals("maxDecoys"))
        {
            nSelectedTrap[0] = maxDecoys;
        }
        else if (paramName.Equals("maxSensitivityTraps"))
        {
            nSelectedTrap[1] = maxSensitivityTraps;
        }
        else if (paramName.Equals("maxStopTraps"))
        {
            nSelectedTrap[2] = maxStopTraps;
        }*/
    }

    public int GetIntParameter(string paramName)
    {
        int val = (int)GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField).GetValue(this);
        return val;
    }

    public float GetFloatParameter(string paramName)
    {
        float val = (float)GetType().GetField(paramName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField).GetValue(this);
        return val;
    }
}
 