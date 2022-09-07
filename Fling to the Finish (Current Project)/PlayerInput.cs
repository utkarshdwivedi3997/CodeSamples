using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;
using Photon.Pun;
using Fling.Saves;
using Menus;
using System;

public class PlayerInput : MonoBehaviourPun
{
    private bool canSpectate = false;
    private bool allGameplayInputPaused = false;           // is all the input paused?
    private bool movementInputPaused = false;   // is just the movement input paused?
    private bool isSelectingNumPlayersInMenu = false;
    private bool lastFrameIsSelectingNumPlayersInMenu = false;


    private Coroutine movementInputPauseCoroutine = null;

    private Player player; //The Rewired Player
    public Player Player => player;
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

    private CharacterContent.TeamInstanceType gameplayType = CharacterContent.TeamInstanceType.Gameplay;

    public CharacterContent MyCharacterContent { get; set; }

    // Use this for initialization

    public event Func<float, float, int, bool> OnMenuTeamSelectInput;
    public event Action OnMenuTeamPickInput;
    private float menuPlayerSelectTimer = 0f;
    private float selectRefresh = 0.3f;
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

        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnTeamFinishedLevel += FinishedRace;
            RaceManager.Instance.OnRaceBegin += OnRaceBegin;
            RaceManager.Instance.OnTeamDeath += TeamRespawned;
        }
        
        PopUpNotification.OnPopUpShown += PopupShown;
        PopUpNotification.OnPopUpDismissed += PopupDismissed;
    }

    // Update is called once per frame
    void Update()
    {
        if (!PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null && !photonView.IsMine)
        {
            return;
        }

        if (!allGameplayInputPaused && player != null)       // only take input if input is not paused
        {
            if (gameplayType == CharacterContent.TeamInstanceType.Gameplay || !isSelectingNumPlayersInMenu)
            {
                if (player.GetButtonDown("Jump"))
                {
                    if (player.GetButton("Clamp") && gameplayType == CharacterContent.TeamInstanceType.Gameplay)
                    {
                        // no flinging in menu
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
            }
            if (player.GetButtonDown("Clamp"))
            {
                myPlayerMovement.InputClamp(true);
            }
            else if (player.GetButtonUp("Clamp"))
            {
                myPlayerMovement.InputClamp(false);
            }
            if (player.GetButtonDown("Tug") && gameplayType == CharacterContent.TeamInstanceType.Gameplay)
            {
                // no flinging in menu
                myRopeManager.InputFling(playerNumber);
            }

            if (gameplayType == CharacterContent.TeamInstanceType.Menu && !lastFrameIsSelectingNumPlayersInMenu)
            {
                if (player.GetButtonDown("ChangeCharacterPrevious"))
                {
                    ChangeCharacter(-1);
                }
                else if (player.GetButtonDown("ChangeCharacterNext"))
                {
                    ChangeCharacter(1);
                }
                else if (player.GetButtonDown("ChangeSkinPrevious"))
                {
                    ChangeSkin(-1);
                }
                else if (player.GetButtonDown("ChangeSkinNext"))
                {
                    ChangeSkin(1);
                }
            }

            if (gameplayType == CharacterContent.TeamInstanceType.Menu)
            {
                // menu specific inputs
                float menuHorizontal = player.GetAxisRaw("Horizontal");
                float menuVertical = player.GetAxisRaw("Vertical");

                if (menuPlayerSelectTimer > selectRefresh)
                {
                    if (OnMenuTeamSelectInput != null)
                    {
                        if (OnMenuTeamSelectInput(menuHorizontal, menuVertical, teamIndex))
                        {
                            menuPlayerSelectTimer = 0f;
                        }
                    }
                }
                else
                {
                    menuPlayerSelectTimer += Time.deltaTime;
                }

                if (player.GetButtonDown("Pick"))
                {
                    OnMenuTeamPickInput?.Invoke();
                }
            }

        }

        lastFrameIsSelectingNumPlayersInMenu = isSelectingNumPlayersInMenu;
    }

    public bool InputtingMovement { get; private set; } = false;
    public static event Action<int, int> OnMovementInputStarted, OnMovementInputStopped;
    private void FixedUpdate()
    {
        if (!PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null && !photonView.IsMine)
        {
            return;
        }

        if (!allGameplayInputPaused && !movementInputPaused && player != null)       // only take input if input is not paused
        {
            float moveX = player.GetAxisRaw("Move Horizontal");
            float moveZ = player.GetAxisRaw("Move Vertical");

            if (isUsingMouse)
            {
                const float mouseFriction = 0.01f;
                moveX *= mouseFriction * SaveManager.Instance.loadedSave.OptionsMenuData.MouseSensitivity;
                moveZ *= mouseFriction * SaveManager.Instance.loadedSave.OptionsMenuData.MouseSensitivity;

                mouseXDelta = Mathf.Clamp(moveX + mouseXDelta, -1f, 1f);
                mouseZDelta = Mathf.Clamp(moveZ + mouseZDelta, -1f, 1f);

                moveX = mouseXDelta;
                moveZ = mouseZDelta;
            }

            Vector2 stickInput = new Vector2(moveX, moveZ);

            if (isUsingMouse)
            {
                if (gameplayType == CharacterContent.TeamInstanceType.Gameplay)
                {
                    RaceManager.Instance.RaceUI.SetMouseInput(stickInput);
                }
                else
                {
                    MenuManager.Instance.ControllerAssigner.SetMouseInput(stickInput);
                }
            }

            float actualDeadzone = isUsingMouse ? mouseDeadzone : deadzone;
            if (stickInput.magnitude > actualDeadzone)
            {
                myPlayerMovement.InputJoystick(stickInput);

                myJoystickVisualization.InputJoystick(stickInput); //NEW, from Ryan 

                if (!InputtingMovement)
                {
                    InputtingMovement = true;
                    OnMovementInputStarted?.Invoke(teamIndex, playerNumber);
                }
            }
            else
            {
                myJoystickVisualization.NoInput();
                myPlayerMovement.SetInputting(false);

                if (InputtingMovement)
                {
                    InputtingMovement = false;
                    OnMovementInputStopped?.Invoke(teamIndex, playerNumber);
                }
            }
        }
    }

    private void OnSpectateInputLeft(InputActionEventData data)
    {
        if (canSpectate)
        {
            myPlayerSpectate.SpectateLeft();
        }
    }

    private void OnSpectateInputRight(InputActionEventData data)
    {
        if (canSpectate)
        {
            myPlayerSpectate.SpectateRight();
        }
    }

    public event Action<int, int, int> OnTryChangeCharacter;
    public event Action<int, int, int> OnTryChangeSkin;
    private void ChangeCharacter(int direction)
    {
        OnTryChangeCharacter?.Invoke(TeamIndex, playerNumber, direction);
    }

    private void ChangeSkin(int direction)
    {
        OnTryChangeSkin?.Invoke(TeamIndex, playerNumber, direction);
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

    public void ResumeAllInputIfPaused()
    {
        if (allGameplayInputPaused)
        {
            ResumeAllInput();
        }
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

        if (gameObject.activeSelf)
        {
            StartCoroutine(ResumeAllInputAfterEndOfFrame());
        }
        else
        {
            allGameplayInputPaused = false;
            movementInputPaused = false;
        }
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

    public void SetIsSelectingNumPlayersInMenu(bool isSelecting)
    {
        if (gameplayType == CharacterContent.TeamInstanceType.Gameplay)
        {
            isSelecting = false;
        }

        this.isSelectingNumPlayersInMenu = isSelecting;
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

    private void OnRaceBegin()
    {
        RaceManager.Instance.OnRaceBegin -= OnRaceBegin;

        ResumeAllInput();
    }

    public void SetIsUsingMouse(bool isUsingMouse)
    {
        this.isUsingMouse = isUsingMouse;
    }

    private void FinishedRace(int teamIndex)
    {
        if (teamIndex == this.teamIndex)
        {
            canSpectate = true;

            RaceManager.Instance.OnTeamFinishedLevel -= FinishedRace;
            if (player != null)
            {
                player.AddInputEventDelegate(OnSpectateInputLeft, UpdateLoopType.Update, InputActionEventType.ButtonJustPressed, "SpectateLeft");
                player.AddInputEventDelegate(OnSpectateInputRight, UpdateLoopType.Update, InputActionEventType.ButtonJustPressed, "SpectateRight");
            }
        }
    }

    private void TeamRespawned(int teamIndex)
    {
        if (TeamIndex == teamIndex)
        {
            // this team respawned
            if (isUsingMouse)
            {
                mouseXDelta = 0f;
                mouseZDelta = 0f;
            }
        }
    }

    public void SetGameplayType(CharacterContent.TeamInstanceType gameplayType)
    {
        this.gameplayType = gameplayType;

        if (gameplayType == CharacterContent.TeamInstanceType.Menu)
        {
            // unsubscribe as this is handled by TeamSetupInstance
            PopUpNotification.OnPopUpShown -= PauseAllInput;
            PopUpNotification.OnPopUpDismissed -= ResumeAllInput;
        }
    }

    private void PopupShown()
    {
        if (gameplayType == CharacterContent.TeamInstanceType.Gameplay)
        {
            PauseAllInput();
        }
    }

    private void PopupDismissed()
    {
        if (gameplayType == CharacterContent.TeamInstanceType.Gameplay)
        {
            ResumeAllInput();
        }
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnTeamFinishedLevel -= FinishedRace;
            RaceManager.Instance.OnRaceBegin -= OnRaceBegin;
            RaceManager.Instance.OnTeamDeath -= TeamRespawned;
        }

        PopUpNotification.OnPopUpShown -= PopupShown;
        PopUpNotification.OnPopUpDismissed -= PopupDismissed;

        if (player != null)
        {
            player.RemoveInputEventDelegate(OnSpectateInputLeft);
            player.RemoveInputEventDelegate(OnSpectateInputRight);
        }
    }
}