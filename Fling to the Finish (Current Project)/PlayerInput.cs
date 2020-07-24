using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;
using Photon.Pun;
using SettingsMenuManager = Menus.Settings.SettingsMenuManager;
using Fling.Saves;
using Menus;

public class PlayerInput : MonoBehaviourPun
{
    private bool canSpectate = false;
    private bool allGameplayInputPaused = false;           // is all the input paused?
    private bool movementInputPaused = false;   // is just the movement input paused?

    private Coroutine movementInputPauseCoroutine = null;

    private Player player; //The Rewired Player
    private float deadzone = 0.05f; //joystick deadzone to be used in a radial deadzone for player movement. (might remove if Rewired handles deadzones well).

    private PlayerMovement myPlayerMovement;
    [SerializeField]
    private PlayerSpectate myPlayerSpectate;
    private PlayerTriggerListener myTriggerListener;
    public RopeManager myRopeManager;
    public LaserControl myLaserControl;
    [SerializeField]
    private BumpControl myBumpControl;

    [SerializeField]
    private JoystickVisualizationHandler myJoystickVisualization;

    private bool isUsingMouse = false;
    private float mouseDeadzone = 0.2f;
    private float mouseXDelta = 0f;
    private float mouseZDelta = 0f;

    [Range(1, 4)]
    //[SerializeField]
    private int rewiredPlayerID = -1;
    /// <summary>
    /// This is the Rewired Player ID. It is NOT the index or the number of this player in the specific team.
    /// </summary>
    public int RewiredPlayerID
    {
        get { return rewiredPlayerID; }
        set { { rewiredPlayerID = value; }
            // Debug.Log("setting id " + value + " for p" + teamNumber + "" + playerNumber);

            // Since a rewiredID change means the player will change, we can add this line of code right here
            if (value != -1)
            {
                player = ReInput.players.GetPlayer(value);
            }
        }
    }

    private int playerNumber = 0;
    public int PlayerNumber
    {
        get { return playerNumber; }
        set { playerNumber = value; }
    }

    private int teamIndex = -1;
    public int TeamIndex
    {
        get { return teamIndex; }
        set { teamIndex = value; }
    }

    private int inAirTugs = 0;

    // Use this for initialization
    void Start()
    {
        myPlayerMovement = GetComponent<PlayerMovement>();
        myTriggerListener = GetComponent<PlayerTriggerListener>();

        myPlayerMovement.TeamIndex = teamIndex;
        myPlayerSpectate.TeamIndex = teamIndex;
        myPlayerMovement.PlayerNumber = playerNumber;

        if (rewiredPlayerID >= 0)
        {
            player = ReInput.players.GetPlayer(rewiredPlayerID);
        }

        myTriggerListener.SetTeam(teamIndex);
        // Only run this once
        if (playerNumber == 1) myRopeManager.SetTeam(teamIndex);
        myLaserControl.SetTeam(teamIndex);

        inAirTugs = 0;

        canSpectate = false;

        RaceManager.Instance.OnRaceCountdownStarted += OnRaceCountdownStarted; 
        RaceManager.Instance.OnTeamWin += FinishedRace;
    }

    // Update is called once per frame
    void Update()
    {
        if (!PhotonNetwork.OfflineMode && !photonView.IsMine)
        {
            return;
        }

        if (canSpectate && player != null)
        {
            if (player.GetButtonDown("Spectate Left"))
            {
                myPlayerSpectate.SpectateLeft();
            }

            if (player.GetButtonDown("Spectate Right"))
            {
                myPlayerSpectate.SpectateRight();
            }
        }

        if (!allGameplayInputPaused && player != null)       // only take input if input is not paused
        {
            if (player.GetButtonDown("Jump"))
            {
                if (player.GetButton("Clamp"))
                {
                    myRopeManager.InputFling(playerNumber);
                }
                else
                {
                    myPlayerMovement.InputJump(true);
                }
            }
            else if (player.GetButtonUp("Jump"))
            {
                myPlayerMovement.InputJump(false);
            }
            if (player.GetButtonDown("Clamp"))
            {
                myPlayerMovement.InputClamp(true);

                // HACK for attractive video for PAX
                // DemoManager.Instance.ResetTimer();
            }
            else if (player.GetButtonUp("Clamp"))
            {
                myPlayerMovement.InputClamp(false);
            }
            if (player.GetButtonDown("Tug"))
            {
                myRopeManager.InputFling(playerNumber);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!PhotonNetwork.OfflineMode && !photonView.IsMine)
        {
            return;
        }

        if (!allGameplayInputPaused && !movementInputPaused && player != null)       // only take input if input is not paused
        {
            float moveX = player.GetAxisRaw("Move Horizontal");
            float moveZ = player.GetAxisRaw("Move Vertical");

            if (isUsingMouse)
            {
                float mouseSensitivity = 0.01f;
                moveX *= mouseSensitivity;
                moveZ *= mouseSensitivity;

                mouseXDelta = Mathf.Clamp(moveX + mouseXDelta, -1f, 1f);
                mouseZDelta = Mathf.Clamp(moveZ + mouseZDelta, -1f, 1f);

                moveX = mouseXDelta;
                moveZ = mouseZDelta;
            }

            Vector2 stickInput = new Vector2(moveX, moveZ);

            if (isUsingMouse && ScreenManager.Instance != null)
            {
                RaceManager.Instance.RaceUI.SetMouseInput(stickInput);
            }

            float actualDeadzone = isUsingMouse ? mouseDeadzone : deadzone;
            if (stickInput.magnitude > actualDeadzone)
            {
                myPlayerMovement.InputJoystick(stickInput);

                myJoystickVisualization.InputJoystick(stickInput); //NEW, from Ryan 



                // HACK for attractive video for PAX
                // DemoManager.Instance.ResetTimer();
            }
            else
            {
                myJoystickVisualization.NoInput();
                myPlayerMovement.SetInputting(false);
            }
        }
    }

    public void Rumble(int motor, float strength, float duration)
    {
        if (player != null)
        {
            player.SetVibration(motor, strength, duration);
        }
    }

    /// <summary>
    /// Pause taking input from the player
    /// </summary>
    public void PauseAllInput()
    {
        allGameplayInputPaused = true;
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
        allGameplayInputPaused = false;
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
    
    private void OnRaceCountdownStarted()
    {
        RaceManager.Instance.OnRaceCountdownStarted -= OnRaceCountdownStarted;
        DisplayMouseUIIfNeeded();
        SaveManager.Instance.OnMouseAndKeyboardButtonsChanged += DisplayMouseUIIfNeeded;
    }

    private void DisplayMouseUIIfNeeded()
    {
        ControllerLayout.LayoutStyle layout = MenuData.LobbyScreenData.ControllerSetup.LocalTeamLayouts[teamIndex].Layout;
        // Should the mouse be displayed?
        if (player != null)
        {
            if (player.controllers.hasMouse)
            {
                // Is this player a separate layout?
                if (layout == Menus.ControllerLayout.LayoutStyle.Separate)
                {
                    // Using left hand layout for single character?
                    if (SaveManager.Instance.loadedSave.OptionsMenuData.UseLeftLayoutForSingleCharacter)
                    {
                        isUsingMouse = SaveManager.Instance.loadedSave.OptionsMenuData.IsP1UsingMouseMovement;
                    }
                    // Using right hand layout for single character?
                    else if (SaveManager.Instance.loadedSave.OptionsMenuData.UseRightLayoutForSingleCharacter)
                    {
                        isUsingMouse = SaveManager.Instance.loadedSave.OptionsMenuData.IsP2UsingMouseMovement;
                    }
                }
                else    // the layout is shared. In this case, only one side can use mouse movement, so we just use that
                {
                    isUsingMouse = playerNumber == 1 ? SaveManager.Instance.loadedSave.OptionsMenuData.IsP1UsingMouseMovement : SaveManager.Instance.loadedSave.OptionsMenuData.IsP2UsingMouseMovement;
                }

                if (isUsingMouse)
                    Debug.Log("using mouse: player " + PlayerNumber + " of team " + teamIndex);
            }
            else
            {
                isUsingMouse = false;
            }
            if (isUsingMouse && RaceManager.Instance != null)
            {
                RaceManager.Instance.RaceUI.UseMouseJoystickUI(true, TeamIndex, playerNumber);
            }
            else if (layout == ControllerLayout.LayoutStyle.Shared && (!SaveManager.Instance.loadedSave.OptionsMenuData.IsP1UsingMouseMovement && !SaveManager.Instance.loadedSave.OptionsMenuData.IsP2UsingMouseMovement))            
            {
                Debug.Log("no one using mouse movement");
                // if neither P1 nor P2 are using mouse movement, hide th emouse joystick UI
                RaceManager.Instance.RaceUI.UseMouseJoystickUI(false, 0, 1);
            }
            else if (layout == ControllerLayout.LayoutStyle.Separate)       // if separate layout (this will happen when ONE person is using MnK and the TEAMMATE is using a controller
            {
                // check to see if the layout being used is using mouse movement, and if not, hide the mouse UI
                if ((SaveManager.Instance.loadedSave.OptionsMenuData.UseLeftLayoutForSingleCharacter && !SaveManager.Instance.loadedSave.OptionsMenuData.IsP1UsingMouseMovement)
                    || (SaveManager.Instance.loadedSave.OptionsMenuData.UseRightLayoutForSingleCharacter && !SaveManager.Instance.loadedSave.OptionsMenuData.IsP2UsingMouseMovement))
                {
                    RaceManager.Instance.RaceUI.UseMouseJoystickUI(false, 0, 1);
                }
            }
        }
    }

    private void FinishedRace(int teamIndex)
    {
        if (teamIndex == this.teamIndex)
        {
            canSpectate = true;
            myPlayerSpectate.StartSpectate();

            // HACK for Omegathon
            PauseAllInput();
            RaceManager.Instance.OnTeamWin -= FinishedRace;
        }
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnTeamWin -= FinishedRace;
            RaceManager.Instance.OnRaceCountdownStarted -= OnRaceCountdownStarted;
        }
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnMouseAndKeyboardButtonsChanged -= DisplayMouseUIIfNeeded;
        }
    }
}