using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;
using Photon.Pun;

public class PlayerInput : MonoBehaviourPun
{
    private bool inputPaused = false;           // is all the input paused?
    private bool movementInputPaused = false;   // is just the movement input paused?

    private Coroutine movementInputPauseCoroutine = null;

    private Player player; //The Rewired Player
    private float deadzone = 0.05f; //joystick deadzone to be used in a radial deadzone for player movement. (might remove if Rewired handles deadzones well).

    private PlayerMovement myPlayerMovement;
    private PlayerTriggerListener myTriggerListener;
    public RopeManager myRopeManager;
    public LaserControl myLaserControl;

    private string suffix = "";

    [Range(1, 4)]
    //[SerializeField]
    private int rewiredPlayerID = 0;
    /// <summary>
    /// This is the Rewired Player ID. It is NOT the index or the number of this player in the specific team.
    /// </summary>
    public int RewiredPlayerID
    {
        get { return rewiredPlayerID; }
        set { rewiredPlayerID = value; }
    }

    private int playerNumber = 0;
    public int PlayerNumber
    {
        get { return playerNumber; }
        set { playerNumber = value; }
    }

    private int teamNumber = 0;
    public int TeamNumber
    {
        get { return teamNumber; }
        set { teamNumber = value; }
    }
    //private int joystickNumber = 0; //0 = leftSide, 1 = rightSide

    private int inAirTugs = 0;

    // Use this for initialization
    void Start()
    {
        // Networked Multiplayer
        if (!PhotonNetwork.OfflineMode && !photonView.IsMine)
        {
            //return;
        }

        myPlayerMovement = GetComponent<PlayerMovement>();
        myTriggerListener = GetComponent<PlayerTriggerListener>();
        //player = ReInput.players.GetPlayer(playerNumber - 1);

        /*if (rewiredPlayerID <= 2)
            teamNumber = 1;
        else
            teamNumber = 2;
        */
        //playerNumber = 2 - (rewiredPlayerNumber % 2);

        myPlayerMovement.MyTeam = teamNumber;
        myPlayerMovement.PlayerNumber = playerNumber;

        player = ReInput.players.GetPlayer(rewiredPlayerID);

        //Debug.Log(playerNumber);
        myTriggerListener.SetTeamNumber(teamNumber);
        myRopeManager.SetTeamNumber(teamNumber);
        myLaserControl.SetTeamNumber(teamNumber);

        

        inAirTugs = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (!inputPaused && player != null)       // only take input if input is not paused
        {
            if (player.GetButtonDown("Jump" + suffix))
            {
                myPlayerMovement.InputJump(true);
            }
            else if (player.GetButtonUp("Jump" + suffix))
            {
                myPlayerMovement.InputJump(false);
            }
            if (player.GetButtonDown("Clamp" + suffix))
            {
                myPlayerMovement.InputClamp(true);
            }
            else if (player.GetButtonUp("Clamp" + suffix))
            {
                myPlayerMovement.InputClamp(false);
            }
            if (player.GetButtonDown("Tug" + suffix))
            {
                // This code below was supposed to limit air-tugs but it doesn't work as intended.

                /*if (myPlayerMovement.Clamped || myPlayerMovement.Grounded)
                {
                    myRopeManager.InputTug(playerNumber);
                    inAirTugs = 0;
                }
                else if (!myPlayerMovement.Grounded && inAirTugs < 2)         // if character is in air
                {
                    inAirTugs++;
                    myRopeManager.InputTug(playerNumber);
                }*/

                myRopeManager.InputFling(playerNumber);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!inputPaused && !movementInputPaused && player != null)       // only take input if input is not paused
        {
            float moveX = player.GetAxisRaw("Move Horizontal" + suffix);
            float moveZ = player.GetAxisRaw("Move Vertical" + suffix);

            Vector2 stickInput = new Vector2(moveX, moveZ);

            if (stickInput.magnitude > deadzone)
            {
                myPlayerMovement.InputJoystick(stickInput);
            }
            else
            {
                myPlayerMovement.SetInputting(false);
            }
        }
    }

    public void Rumble(int motor, float strength, float duration)
    {
        player.SetVibration(motor, strength, duration);
    }

    /// <summary>
    /// Pause taking input from the player
    /// </summary>
    public void PauseAllInput()
    {
        inputPaused = true;
    }

    /// <summary>
    /// Resume taking input from the player
    /// </summary>
    public void ResumeAllInput()
    {
        // Unclamp if players were clamped
        if (myPlayerMovement != null)
        {
            myPlayerMovement.ResumeAfterPause();
        }

        StartCoroutine(ResumeAllInputAfterEndOfFrame());
    }

    IEnumerator ResumeAllInputAfterEndOfFrame() {
        yield return new WaitForEndOfFrame();
        inputPaused = false;
        movementInputPaused = false;
    }

    public void PauseMovementInput()
    {
        movementInputPaused = true;
    }

    public void ResumeMovementInput()
    {
        movementInputPaused = false;
    }

    /// <summary>
    /// Pauses movement for given time
    /// </summary>
    /// <param name="t"></param>
    public void PauseMovementInputForTime(float t)
    {
        if (movementInputPauseCoroutine!=null)
        {
            StopCoroutine(movementInputPauseCoroutine);
            movementInputPauseCoroutine = null;
        }

        movementInputPauseCoroutine = StartCoroutine(PauseMovementForTime(t));
    }

    IEnumerator PauseMovementForTime(float t)
    {
        PauseMovementInput();
        yield return new WaitForSeconds(t);
        ResumeMovementInput();
        movementInputPauseCoroutine = null;
    }
}