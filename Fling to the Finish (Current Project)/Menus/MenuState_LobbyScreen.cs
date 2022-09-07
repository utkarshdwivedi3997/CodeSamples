#if UNITY_ANDROID || UNITY_IOS || UNITY_TIZEN || UNITY_TVOS || UNITY_WEBGL || UNITY_WSA || UNITY_PS4 || UNITY_WII || UNITY_XBOXONE || UNITY_SWITCH
#define DISABLESTEAMWORKS
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Fling.Saves;
using TMPro;
using UnityEngine.EventSystems;
using System.Linq;
using ControllerType = Rewired.ControllerType;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Menus
{
    public class MenuState_LobbyScreen : MonoBehaviourPun, IMenuState
    {
        public MenuState State
        {
            get { return MenuState.LOBBY_SCREEN; }
        }

        /// <summary>
        /// ReturnToLevelSelect if Campaign, MatchmakingScreen if Race
        /// </summary>
        public MenuState NextState
        {
            get { return MenuState.WORLD_SELECT; }
        }

        public MenuState PreviousState
        {
            get { return MenuState.MAIN; }
        }

        [SerializeField]
        private Transform cameraLocation;
        public Transform CameraLocation
        {
            get { return cameraLocation; }
        }

        [Header("Menu State Variables")]

        private bool isShowingInfoMessage;

#region LobbySetupVars
        [Header("-----------------------------------------------------")]
        [Header("Lobby Setup Variables")]
        [SerializeField]
        private GameObject lobbySetupPanel;
        [SerializeField]
        private TMP_InputField roomNameInput;
        [SerializeField]
        private TextMeshProUGUI headerText;
        [SerializeField]
        private TextMeshProUGUI networkStatsText;
        [SerializeField]
        private Image headerYButton;
        [SerializeField]
        private GameObject connectionInfoPanel;
        [SerializeField]
        private TextMeshProUGUI connectionInfoText;
        [SerializeField]
        private Button infoReturnButton;

        [SerializeField]
        private Button createRoomBtn;
        [SerializeField]
        private GameObject connectingGameObject;

        private const string GO_ONLINE_STR = "Press  Y  to go online";
        private const string CREATE_OR_JOIN_STR = "Create or Join room";
        private string ROOM_CODE_STR = "Room Code: ";
        private const string PRESS_Y_TO_INVITE_FRIENDS_STR = "Press       to Invite Friends";
        private const string INVITE_FRIENDS = "Invite Friends";

        private bool isInMultiplayerPanel = false;
        private bool isLoadingRoom = false;
        private bool isInRoom = false;
        #endregion

        #region FriendInviteVars
        [Header("-----------------------------------------------------")]
        [Header("Friend Invites Variables")]
        [SerializeField]
        private InviteFriendScreen inviteFriendScreen;
        #endregion


        [Header("-----------------------------------------------------")]
        [Header("Controller Select Vars")]
        #region ControllerVars

        [SerializeField]
        private ControllerImage[] controllerImages = new ControllerImage[4];         // Array of 4. Holds icons for controllers for each player
        public ControllerImage[] ControllerImages { get { return controllerImages; } }
        [SerializeField]
        private LobbyScreenQuadrant[] teamQuadrants = new LobbyScreenQuadrant[4];   // Array of 4. Holds each of the 4 quadrants.
        public LobbyScreenQuadrant[] TeamQuadrants { get { return teamQuadrants; } }


        private bool subscribedToControllerEvents = false;
#if !DISABLESTEAMWORKS
        private bool subscribedToSteamEvent = false;
#endif

        private MenuData.PlayType playType;                                         // Campaign or Race

        #region Online UI Vars

        private int onlinePlayersCount;
        #endregion
        #endregion

        #region Character Select
        [Header("-----------------------------------------------------")]
        [Header("Character Select")]
        [SerializeField]
        private Lobby_ControllerSetup controllerSetup;
        public Lobby_ControllerSetup ControllerSetup { get { return controllerSetup; } }
        [SerializeField]
        private Lobby_NumberOfPlayersSelect numberOfPlayersSelect;
        public Lobby_NumberOfPlayersSelect NumberOfPlayersSelect { get { return numberOfPlayersSelect; } }
        [SerializeField]
        private Lobby_CharacterSelect Scr_LobbyCharSelect;
        /// <summary>
        /// The CharacterSelect part of lobby
        /// </summary>
        public Lobby_CharacterSelect CharacterSelect { get { return Scr_LobbyCharSelect; } }
#endregion

        [Header("-----------------------------------------------------")]
        [Header("General UI")]
        [SerializeField]
        private GameObject startToContinuePanel;
        [SerializeField]
        private TextMeshProUGUI startToContinueText;

#region State Start
        public void InitState()
        {
            #region ControllerInit
            controllerSetup.Init(this);

            if (!PhotonNetwork.OfflineMode)
            {
                NetworkManager.Instance.OnClientLeftRoom += OtherClientDisconnectedFromRoom;
                NetworkManager.Instance.OnDisconnectedFromNetwork += OnDisconnectedFromNetwork;
            }

            // Handle subscriptions to events
            if (!RewiredJoystickAssign.Instance.HasAssignedControllers)
            {
                // I don't think we need this anymore
                //RewiredJoystickAssign.Instance.OnJoystickConnected += ControllerConnected;
                //RewiredJoystickAssign.Instance.OnJoystickPreDisconnect += ControllerDisconnected;
                //subscribedToControllerEvents = true;
            }

#if !DISABLESTEAMWORKS
            SteamScript.Instance.OnGameJoinRequested += TeleportToStateAndTryJoinRoom;
            subscribedToSteamEvent = true;
#endif
                
#endregion

            Scr_LobbyCharSelect.Init(this);
            numberOfPlayersSelect.Init(this);
        }

        /// <summary>
        /// Sets this state to be the current active state
        /// </summary>
        public void ActivateState()
        {
            if (!PhotonNetwork.OfflineMode)
            {
                NetworkManager.Instance.OpenRoom();
            }

            isShowingInfoMessage = false;

            startToContinuePanel.SetActive(false);

            controllerSetup.ActivateState();
            Scr_LobbyCharSelect.ActivateState();

            if (!PhotonNetwork.OfflineMode)
            {
                if (MenuData.LobbyScreenData.ResetLobby)      // resetting the lobby?
                {
                    // Debug.Log("disconnecting");
                    NetworkManager.Instance.Disconnect();
                    //Debug.LogError("This hack just ran");
                }
                else
                {
                    if (MenuData.LobbyScreenData.Online.roomName != null)
                    {
                        isInRoom = true;
                        ROOM_CODE_STR = "Room Code: " + MenuData.LobbyScreenData.Online.roomName;
                    }

                    if (PhotonNetwork.IsMasterClient)
                    {
                        photonView.RPC("SetTeamReadyLogic", RpcTarget.AllBufferedViaServer, 0, false);
                        photonView.RPC("SetTeamReadyLogic", RpcTarget.AllBufferedViaServer, 1, false);
                        photonView.RPC("SetTeamReadyLogic", RpcTarget.AllBufferedViaServer, 2, false);
                        photonView.RPC("SetTeamReadyLogic", RpcTarget.AllBufferedViaServer, 3, false);
                    }
                }
            }

            CheckAllPlayersReady();

            HideMultiplayerPanel();

            MenuData.LobbyScreenData.ResetLobby = false;            // don't reset lobby anymore until we go back to the previous menu state

            #region DO NOT ADD CODE BELOW THIS REGION (only applicable inside the function)
            // !!!!!!!!!!!!!!! ADD ANY CODE ABOVE THIS POINT !!!!!!!!!!!!!!! //

            // Handle subscriptions to events
            // YES, this needs to happen both in InitState() and in ActivateState()
            // Reason: It happens in InitState() so that controller connects/disconnects can
            // be handled in the splash screen
            // It happens in ActivateState() so that when you come back to this state from
            // character select, it can be handled again
            // The reason we UNsubscribe right after this menu state is because we do NOT
            // want the controller connects and disconnects to work in the way they work here,
            // rather just assume the IDs that are looking for a controller
            // If this is confusing talk to me - Utkarsh


            if (!subscribedToControllerEvents)      // ONLY subscribe if not already subscribed. More than one subscriptions can lead to problems!
            {
                RewiredJoystickAssign.Instance.OnJoystickConnected += ControllerConnected;
                RewiredJoystickAssign.Instance.OnJoystickPreDisconnect += ControllerDisconnected;
                subscribedToControllerEvents = true;
            }

#if !DISABLESTEAMWORKS

            if (!subscribedToSteamEvent)
            {
                SteamScript.Instance.OnGameJoinRequested += TeleportToStateAndTryJoinRoom;
                subscribedToSteamEvent = true;
            }
#endif

            // !!!!!!!!!!!!!!!           DO NOT ADD CODE BELOW THIS POINT IN THIS FUNCTION          !!!!!!!!!!!!!!! //
            // !!!!!!!!!!!!!!! IF YOU NEED TO ADD CODE LOOK FOR THE "ADD CODE ABOVE THIS POINT TAG" !!!!!!!!!!!!!!! //
#endregion
        }

        /// <summary>
        /// Is called when we know this is no longer the active state
        /// </summary>
        public void DeactivateState()
        {

        }

        public void StateScreenTransitionFinish()
        {
            return;
        }
#endregion

#region Input
        // Break select up by creating two files, LobbyController and LobbyCharacter, and have a helper function in each for Select
        public bool Select(float horizontalInput, float verticalInput, int rewiredPlayerID, int playerNumber)
        {
            if (isShowingInfoMessage)
            {
                return false;
            }
            else if (isInMultiplayerPanel)
            {
                // Don't combine this with the if condition above
                if (!isLoadingRoom)
                {
                    //Debug.Log("creating or joining room");
                    if (EventSystem.current.currentSelectedGameObject == roomNameInput.gameObject && horizontalInput < -0.7f)
                    {
                        EventSystem.current.SetSelectedGameObject(createRoomBtn.gameObject);
                    }
                }
                return false;
            }
            else
            {
#region Controller movement
                if (controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Controller)
                {
                    return controllerSetup.Select(horizontalInput, verticalInput, rewiredPlayerID, playerNumber);
                }
#endregion
#region Character movement
                else if ((controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Character) || (controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Ready))
                {

                    int thisCharIndex = 0;
                    if (rewiredPlayerID == controllerSetup.layouts[controllerImages[playerNumber].QuadrantIndex].RewiredIDs[0])
                    {
                        thisCharIndex = (controllerImages[playerNumber].QuadrantIndex) * 2;
                    }
                    else if (rewiredPlayerID == controllerSetup.layouts[controllerImages[playerNumber].QuadrantIndex].RewiredIDs[1])
                    {
                        thisCharIndex = (controllerImages[playerNumber].QuadrantIndex * 2) + 1;
                    }

                    return Scr_LobbyCharSelect.Select(horizontalInput, verticalInput, rewiredPlayerID, playerNumber, thisCharIndex);
                }
#endregion
#region Number of players select
                else if (controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.NumberOfPlayersSelect)
                {
                    return numberOfPlayersSelect.Select(horizontalInput, verticalInput, rewiredPlayerID, playerNumber);
                }
#endregion
            }

            return false;
        }

        // Submit is in a good spot
        public bool Submit(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            if (isShowingInfoMessage)
            {
                return false;
            }
            else if (isInMultiplayerPanel)
            {
                if (!isLoadingRoom)
                {
                    //Debug.Log("Creating or joining room");
                }
                return false;
            }
            else
            {

                // int numReady = 0;
                // for (int i = 0; i < 4; i++) {
                //     if (PhotonNetwork.OfflineMode) {
                //         if (lobby.ControllerImages[i].quadrantMode == ControllerImage.QuadrantMode.Ready) {
                //             numReady++;
                //         }
                //     }
                //     else {
                //         if (NetworkManager.Instance.TeamClientIDs[i][0] != null) {
                //             if (lobby.ControllerImages[i].quadrantMode == ControllerImage.QuadrantMode.Ready) {
                //                 numReady++;
                //             }
                //         }
                //     }
                // }
                // Debug.Log("NumReady = " + numTeamsReady);

                bool isLobbyReady = CheckAllPlayersReady();

                if (PhotonNetwork.OfflineMode)
                {
                    if (isLobbyReady) {

                        SubmitLobby();
                    }

                    return isLobbyReady;
                }
                else if (PhotonNetwork.IsMasterClient)
                {
                    if(isLobbyReady) {

                        RPCSubmitLobby();

                        NetworkManager.Instance.CloseRoom();
                        // SubmitLobby();
                    }

                    return isLobbyReady;
                }

                return false;
            }
        }

        public void RPCSubmitLobby() {
            if (!PhotonNetwork.OfflineMode)
            {
                // Stuff to do on all clients
                photonView.RPC("SubmitLobby", RpcTarget.AllViaServer);
            }
        }

        [PunRPC]
        public void SubmitLobby() {
            //hasBeenHereAlready = true;
            Scr_LobbyCharSelect.Submit();
            if (!PhotonNetwork.OfflineMode && photonView.IsMine)
            {
                PhotonNetwork.RemoveRPCs(photonView);
            }
            // MenuData.SaveStuff !!! When refactoring, use the MenuData class
            controllerSetup.SavePlayerPrefs();
            RewiredJoystickAssign.Instance.EndJoystickAssignment(true, true);
            //MenuManager.Instance.MoveToNextMenu();

            RewiredJoystickAssign.Instance.OnJoystickConnected -= ControllerConnected;
            RewiredJoystickAssign.Instance.OnJoystickPreDisconnect -= ControllerDisconnected;

            NetworkManager.Instance.ClearCachesFromLobby();

            //// HACK FOR MATCHMAKING
            //NetworkManager.Instance.OnClientLeftRoom -= OtherClientDisconnectedFromRoom;

            subscribedToControllerEvents = false;
        }

        public bool Cancel(int rewiredPlayerID, int playerNumber)
        {
            if (isShowingInfoMessage)
            {
                return false;
            }
            else if (isInMultiplayerPanel)
            {
                // Don't combine this with the if condition above!
                if (!isLoadingRoom)
                {
                    HideMultiplayerPanel();
                    //Debug.Log("Creating or joining room");
                    return false;
                }
            }
            else
            {
#region ControllerCancel
                if (controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Controller)
                {
                    return controllerSetup.Cancel(rewiredPlayerID, playerNumber);
                }
#endregion
#region CharacterCancel
                else if ((controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Character) ||
                    (controllerSetup.layouts[controllerImages[playerNumber].QuadrantIndex].Layout == ControllerLayout.LayoutStyle.Separate && controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Ready))
                {

                    int thisCharIndex = 0;
                    int otherCharIndex = 0;
                    if (rewiredPlayerID == controllerSetup.layouts[controllerImages[playerNumber].QuadrantIndex].RewiredIDs[0])
                    {
                        thisCharIndex = (controllerImages[playerNumber].QuadrantIndex) * 2;
                        otherCharIndex = (controllerImages[playerNumber].QuadrantIndex * 2) + 1;
                    }
                    else if (rewiredPlayerID == controllerSetup.layouts[controllerImages[playerNumber].QuadrantIndex].RewiredIDs[1])
                    {
                        thisCharIndex = (controllerImages[playerNumber].QuadrantIndex * 2) + 1;
                        otherCharIndex = (controllerImages[playerNumber].QuadrantIndex * 2);
                    }

                    return Scr_LobbyCharSelect.Cancel(rewiredPlayerID, playerNumber, thisCharIndex, otherCharIndex);
                }
#endregion
#region Number of players select Cancel
                else if (controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.NumberOfPlayersSelect ||
                        (controllerSetup.layouts[controllerImages[playerNumber].QuadrantIndex].Layout == ControllerLayout.LayoutStyle.Shared && controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Ready))
                {
                    return numberOfPlayersSelect.Cancel(rewiredPlayerID, playerNumber);
                }
#endregion
            }

            // If all else fails, return false
            return false;
        }

        public bool Pick(int rewiredPlayerID, int playerNumber)
        {
            if (isShowingInfoMessage)
            {
                return false;
            }
            else if (isInMultiplayerPanel)
            {
                // Don't combine this with the if condition above
                if (!isLoadingRoom)
                {
                    //Debug.Log("Creating or joining room");
                    if (EventSystem.current.currentSelectedGameObject == roomNameInput.gameObject)
                    {
                        OnJoinRoomBtnClicked(roomNameInput);
                    }
                }
                return false;
            }
            else
            {
                //Debug.Log("Pick with id " + rewiredPlayerID);
#region ControllerPick
                if (controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Controller)
                {
                    return controllerSetup.Pick(rewiredPlayerID, playerNumber);
                }
#endregion
#region CharacterPick
                else if (controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Character)
                {

                    int thisCharIndex = 0;
                    int otherCharIndex = 0;
                    if (rewiredPlayerID == controllerSetup.layouts[controllerImages[playerNumber].QuadrantIndex].RewiredIDs[0])
                    {
                        thisCharIndex = (controllerImages[playerNumber].QuadrantIndex) * 2;
                        otherCharIndex = (controllerImages[playerNumber].QuadrantIndex * 2) + 1;
                    }
                    else if (rewiredPlayerID == controllerSetup.layouts[controllerImages[playerNumber].QuadrantIndex].RewiredIDs[1])
                    {
                        thisCharIndex = (controllerImages[playerNumber].QuadrantIndex * 2) + 1;
                        otherCharIndex = (controllerImages[playerNumber].QuadrantIndex * 2);
                    }

                    return Scr_LobbyCharSelect.Pick(playerNumber, thisCharIndex, otherCharIndex);
                }
#endregion
#region Number of players select pick
                else if (controllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.NumberOfPlayersSelect)
                {
                    return numberOfPlayersSelect.Pick(rewiredPlayerID, playerNumber);
                }
#endregion
            }

            return false;
        }
        #endregion

#region Online
        /// <summary>
        /// Called when the client is connected to an online room
        /// </summary>
        private void JoinedRoom(string roomName)
        {
            // Unsubscribe because we no longer need to listen to this
            NetworkManager.Instance.JoinedRoom -= JoinedRoom;

            // If another client disconnects
            NetworkManager.Instance.OnClientLeftRoom += OtherClientDisconnectedFromRoom;

            // If I disconnect
            NetworkManager.Instance.OnDisconnectedFromNetwork += OnDisconnectedFromNetwork;

            isInRoom = true;
            connectingGameObject.SetActive(false);
            ROOM_CODE_STR = "Room Code: " + roomName;
            MenuData.LobbyScreenData.Online.roomName = roomName;

            // Reset all clients except for the master client
            if (!PhotonNetwork.IsMasterClient)
            {
                RewiredJoystickAssign.Instance.EndJoystickAssignment(true, true);       // pause joystick assignment
                ActivateState();                                                        // Reset current state. ActivateState() also resumes joystick assignment
            }
            else {
                Scr_LobbyCharSelect.JoinedRoom();

                if (NetworkManager.Instance.ConnectUsing == NetworkManager.ConnectionType.FriendInvites)
                {
#if !DISABLESTEAMWORKS
                    isLoadingRoom = false;
                    UpdateInviteFriendsScreen();
                    //SteamScript.Instance.Invite();
#endif
                }
                else if (NetworkManager.Instance.ConnectUsing == NetworkManager.ConnectionType.RoomCodes)
                {
                    // Nothing
                    HideMultiplayerPanel(); // false);
                }
            }

            controllerSetup.SyncPlayersAcrossClients();
        }

        /// <summary>
        /// To be called when a client disconnects from the room.
        /// </summary>
        private void OtherClientDisconnectedFromRoom(string clientID, Photon.Realtime.Player otherPlayer)
        {
            Debug.Log("Player with clientID " + clientID + " has left the room.");

            controllerSetup.OtherClientDisconnectedFromRoom(clientID, otherPlayer);
        }

        /// <summary>
        /// To be called when this client disconnects from the network.
        /// </summary>
        /// <param name="cause"></param>
        private void OnDisconnectedFromNetwork(Photon.Realtime.DisconnectCause cause)
        {
            // Some lobby cleanup?
            MenuData.LobbyScreenData.Online.roomName = null;
            MenuData.LobbyScreenData.Online.IdentifyingKeys = new List<string>();
            isInRoom = false;

            // TODO - Disconnect cause stuff!
            
            // Unsubscribe
            NetworkManager.Instance.OnClientLeftRoom -= OtherClientDisconnectedFromRoom;
            NetworkManager.Instance.OnDisconnectedFromNetwork -= OnDisconnectedFromNetwork;

            // If not disconnected on purpose!
            if (cause != Photon.Realtime.DisconnectCause.DisconnectByClientLogic)
            {
                RewiredJoystickAssign.Instance.EndJoystickAssignment(true, true);       // pause joystick assignment
                ActivateState();

                ShowInfoMessage(-103, "Disconnected");
            }
            // Debug.Log("DISCONNECTED!");
        }

        /// <summary>
        /// Called when trying to create a room with an invalid room name
        /// </summary>
        private void JoinRoomFailed(short returnCode, string message)
        {
            NetworkManager.Instance.JoinedRoom -= JoinedRoom;
            NetworkManager.Instance.JoinRoomFailed -= JoinRoomFailed;

            ShowInfoMessage(returnCode, message);
        }

        public void SetTeamReady(int quadrantNum, bool isReady)
        {
            if (!PhotonNetwork.OfflineMode)
            {
                photonView.RPC("SetTeamReadyLogic", RpcTarget.AllBufferedViaServer, quadrantNum, isReady);
            }
            else
            {
                SetTeamReadyLogic(quadrantNum, isReady);
            }
                
        }

        [PunRPC]
        private void SetTeamReadyLogic(int quadrantNum, bool isReady)
        {
            teamQuadrants[quadrantNum].SetQuadrantReady(isReady);
            CheckAllPlayersReady();
        }

        #endregion

#region Helper Functions

#region Lobby setup Helpers
        /// <summary>
        /// Shows:
        /// The room setup panel if using the RoomCodes method
        /// The corresponding invite friends panel if using the InviteFriends method
        /// </summary>
        /// <param name="selectedButton"></param>
        public void ShowMultiplayerPanel(GameObject selectedButton = null)
        {
            // Friend invites
            if (NetworkManager.Instance.ConnectUsing == NetworkManager.ConnectionType.FriendInvites)
            {
                // First, connect to server and create a room!
                ConnectToRoom();

                // If Steam
#if !DISABLESTEAMWORKS
                if (isInRoom)
                {
                    UpdateInviteFriendsScreen();
                }
                SetSteamInviteFriendsPanelStatus(true);
#endif
            }
            // Room codes
            else if (NetworkManager.Instance.ConnectUsing == NetworkManager.ConnectionType.RoomCodes)
            {
                // Here, we connect after the player hits the create room button, so no need to call ConnectToRoom() here.
                OpenRoomSetupPanel(selectedButton);
            }
        }

        /// <summary>
        /// Hides the multiplayer room code or invite friends panel that is currently active
        /// </summary>
        public void HideMultiplayerPanel()
        {
            // Friend invites
            if (NetworkManager.Instance.ConnectUsing == NetworkManager.ConnectionType.FriendInvites)
            {
                SetConnectionInformationPanelActive(false);

                // If Steam
#if !DISABLESTEAMWORKS
                SetSteamInviteFriendsPanelStatus(false);
#endif
            }
            // Room codes
            else if (NetworkManager.Instance.ConnectUsing == NetworkManager.ConnectionType.RoomCodes)
            {
                CloseRoomSetupPanel();
            }
        }

        /// <summary>
        /// Bring up the Create or Join room panel
        /// I don't know what this selectedButton is supposed to do. It might be useless, please figure out what it's doing and get rid of it if it's useless
        /// </summary>
        private void OpenRoomSetupPanel(GameObject selectedButton = null)
        {
            if (!isInRoom)
            {
                SetRoomSetupPanelStatus(true, selectedButton);
            }
        }

        /// <summary>
        /// Return to the lobby screen
        /// </summary>
        private void CloseRoomSetupPanel()
        {
            SetConnectionInformationPanelActive(false);
            SetRoomSetupPanelStatus(false);
        }

        /// <summary>
        /// Closes the panel that shows info messages about the current connection.
        /// </summary>
        public void CloseConnectionInfoPanel()
        {
            SetConnectionInformationPanelActive(false);

            roomNameInput.text = "";

            if (isInMultiplayerPanel && !isLoadingRoom)
            {
                isInMultiplayerPanel = false;
                isLoadingRoom = false;
                ShowMultiplayerPanel();
            }
        }

        /// <summary>
        /// Sets the active status of connection information panel
        /// </summary>
        /// <param name="status"></param>
        private void SetConnectionInformationPanelActive(bool status)
        {
            connectionInfoPanel.SetActive(status);
            connectionInfoText.gameObject.SetActive(status);
            infoReturnButton.gameObject.SetActive(status);
            if (status)
                EventSystem.current.SetSelectedGameObject(infoReturnButton.gameObject);
            else
                EventSystem.current.SetSelectedGameObject(null);

            StartCoroutine(SetShowingInfoMessageVariable(status));
        }

        private IEnumerator SetShowingInfoMessageVariable(bool status)
        {
            // We just have to wait for a frame because Pick can also get called along with the OK button.
            yield return new WaitForEndOfFrame();
            isShowingInfoMessage = status;
        }

        /// <summary>
        /// Helper function for OpenRoomSetupPanel() and CloseRoomSetupPanel()
        /// </summary>
        /// <param name="status"></param>
        /// <param name="btnToSelect">Optional parameter to be set as selected button</param>
        private void SetRoomSetupPanelStatus(bool status, GameObject btnToSelect = null)
        {
            // Check for internet connection first!
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                ShowInfoMessage(-100, "No internet connection");
                return;
            }

            // if (alsoSetConnectStatus) SetConnectStatus(status);
            isInMultiplayerPanel = status;
            isLoadingRoom = false;

            lobbySetupPanel.SetActive(status);
            headerText.text = status ? CREATE_OR_JOIN_STR : (isInRoom ? ROOM_CODE_STR : GO_ONLINE_STR);
            headerYButton.gameObject.SetActive(!status & !isInRoom);

            roomNameInput.text = "";
            // Debug.Log(isInRoom + " " + ROOM_CODE_STR);
            if (status)
            {
                if (btnToSelect == null)
                    EventSystem.current.SetSelectedGameObject(createRoomBtn.gameObject);
                else
                    EventSystem.current.SetSelectedGameObject(btnToSelect);
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            //if (DevScript.Instance != null) DevScript.Instance.DevMode = !status; // pause / resume reading input
        }

        /// <summary>
        /// Called when the CreateRoom button is clicked.
        /// </summary>
        public void OnCreateRoomBtnClicked()
        {
            ConnectToRoom();
        }

        /// <summary>
        /// Called when the JoinRoom button is clicked.
        /// </summary>
        public void OnJoinRoomBtnClicked(TMP_InputField input)
        {
            if (!input.text.All(System.Char.IsLetter))
            {
                JoinRoomFailed(-102, "Invalid characters entered");
            }
            else if (input.text.Length != 4)
            {
                // Debug.Log("Enter a 4 letter code.");
                JoinRoomFailed(-101, "Invalid code length");
            }
            else
            {
                string rmCode = input.text.ToUpper();
                JoinRoom(rmCode);
            }
        }

        /// <summary>
        /// Connects to a server and then to a room
        /// </summary>
        private void ConnectToRoom()
        {
            // Make a room if not already in one
            if (!isInRoom)
            {
                // Debug.Log("Try create room");
                isLoadingRoom = true;
                connectingGameObject.SetActive(true);
                NetworkManager.Instance.ConnectThenCreateOrJoinRoom();      // create a new one!
                NetworkManager.Instance.JoinedRoom += JoinedRoom;
            }
        }

        private void JoinRoom(string rmCode)
        {
            //Debug.Log("Try join room");
            isLoadingRoom = true;
            connectingGameObject.SetActive(true);
            NetworkManager.Instance.ConnectThenCreateOrJoinRoom(rmCode);

            NetworkManager.Instance.JoinRoomFailed += JoinRoomFailed;
            NetworkManager.Instance.JoinedRoom += JoinedRoom;
        }

        /// <summary>
        /// Takes a command line argument in the form of SteamScript.
        /// </summary>
        /// <param name="cmdArg"></param>
        public void TeleportToStateAndTryJoinRoom(string rmCode)
        {
            if (isInRoom)
            {
                Debug.Log("You're already in a room!");
                return;
            }

            MenuManager.Instance.MoveToMenuState(State);

            JoinRoom(rmCode);
        }

#if !DISABLESTEAMWORKS
        public void SetSteamInviteFriendsPanelStatus(bool status)
        {
            //if (Application.internetReachability == NetworkReachability.NotReachable)
            //{
            //    ShowInfoMessage(-100, "No internet connection");
            //    return;
            //}

            isInMultiplayerPanel = status;

            // Show invite panel
            if (status)
            {
                inviteFriendScreen.Activate();
            }
            else
            {
                inviteFriendScreen.Deactivate();
            }

            headerText.text = status ? INVITE_FRIENDS : PRESS_Y_TO_INVITE_FRIENDS_STR;
            headerYButton.gameObject.SetActive(!status);
        }
#endif

        public void UpdateInviteFriendsScreen()
        {
            headerText.text = INVITE_FRIENDS;
#if !DISABLESTEAMWORKS
            inviteFriendScreen.ShowFriendButtons(SteamScript.Instance.GetOnlineFriends());
#endif
        }

        /// <summary>
        /// Checks to see if all players (local) are ready, and tells them to move on to the next screen
        /// </summary>
        public bool CheckAllPlayersReady()
        {
            int numTeamsReadyNew = 0;
            for (int i = 0; i < teamQuadrants.Length; i++)
            {
                if (teamQuadrants[i].TeamReady) numTeamsReadyNew++;
            }

            if (numTeamsReadyNew > 0)
            {
                /*for (int i = 0; i < lobby.ControllerImages.Length; i++)
                {
                    // If the controller is in a quadrant and hasn't fully readied up
                    if (lobby.ControllerImages[i].IsInQuadrant && lobby.ControllerImages[i].quadrantMode != ControllerImage.QuadrantMode.Ready)
                    {
                        // Hide the prompt
                        startToContinuePanel.SetActive(false);
                        return;
                    }
                }*/

                int clientsWithLocalPlayers = 0;
                int quadrantsWithPlayers = 0;
                if (!controllerSetup.CheckAllPlayersReady(out quadrantsWithPlayers))
                {
                    // Hide the prompt
                    startToContinuePanel.SetActive(false);
                    return false;
                }

                // This check should only happen in online mode!
                if (!PhotonNetwork.OfflineMode)
                {
                    for (int i = 0; i < PhotonNetwork.CurrentRoom.Players.Keys.Count; i++)
                    {
                        for (int j = 0; j < NetworkManager.Instance.TeamClientIDs.Count; j++)
                        {
                            string thisClientID = PhotonNetwork.CurrentRoom.Players[PhotonNetwork.CurrentRoom.Players.Keys.ToList()[i]].UserId;
                            bool isLocalPlayerInTeam = NetworkManager.Instance.TeamClientIDs[j].Contains(thisClientID);

                            if (isLocalPlayerInTeam)
                            {
                                clientsWithLocalPlayers++;
                                break;
                            }
                        }

                        if (clientsWithLocalPlayers == PhotonNetwork.CurrentRoom.Players.Keys.Count)
                        {
                            break;
                        }
                    }

                    if (clientsWithLocalPlayers != PhotonNetwork.CurrentRoom.Players.Keys.Count)
                    {
                        startToContinuePanel.SetActive(false);
                        Debug.Log(clientsWithLocalPlayers + ", not all clients have at least one controller ready");
                        return false;
                    }
                }

                if (quadrantsWithPlayers != numTeamsReadyNew)
                {
                    // Hide the prompt
                    startToContinuePanel.SetActive(false);
                    Debug.Log("there's an unready controller in a quadrant");
                    return false;
                }

                // If all players are ready, the code gets to this point
                // If so, show the prompt
                startToContinuePanel.SetActive(true);
                startToContinueText.text = "Press START to continue";

                if (!PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient)
                {
                    startToContinueText.text = "Waiting for lobby leader to continue...";
                }
                Debug.Log("Players are raedy to go");

                return true;
            }

            startToContinuePanel.SetActive(false);
            Debug.Log("Players are not ready");

            return false;
        }

        /// <summary>
        /// Shows an error message
        /// </summary>
        /// <param name="returnCode"></param>
        /// <param name="message"></param>
        private void ShowInfoMessage(short returnCode, string message)
        {
            isLoadingRoom = false;
            connectingGameObject.SetActive(false);
            SetConnectionInformationPanelActive(true);

            string primaryText = "Error: ";
            string secondaryText = "Please provide a valid room code and try again.";

            switch (returnCode)
            {
                case -100:
                    primaryText = "Error: ";
                    secondaryText = "Make sure you have an internet connection and try again.";
                    break;
                case -101:
                    primaryText = "Could not join room due to error: ";
                    secondaryText = "Please provide a 4 letter room code and try again.";
                    break;
                case -102:
                    primaryText = "Could not join room due to error: ";
                    secondaryText = "Please provide a 4 letter room code without any numbers or special characters.";
                    break;
                case -103:
                    primaryText = "Error: ";
                    secondaryText = "You were disconnected from the internet. Make sure you have an internet connection if you want to play online.";
                    break;
                case Photon.Realtime.ErrorCode.GameDoesNotExist:
                    primaryText = "Could not join room due to error: ";
                    secondaryText = "You either typed the code wrong or you are trying to connect to a room that is on a different version of the game. Please proved a valid room code and try again.";
                    break;
                default:
                    primaryText = "Could not join room due to error: ";
                    secondaryText = "Please provide a valid room code and try again.";
                    break;
            }

            connectionInfoText.text = primaryText + "<color=red>" + message.ToString() + "</color>.\n\n" + secondaryText;
        }
        #endregion

        /// <summary>
        /// Returns whether a team is online or not (at least one player in the team is on a different client)
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <returns></returns>
        public bool IsTeamOnline(int quadrant)
        {
            bool p0Online = teamQuadrants[quadrant].IsOnlinePlayer[0];
            bool p1Online = teamQuadrants[quadrant].IsOnlinePlayer[1];

            // This function will never get called when both teammates are online
            // We can use this fact to simplify this code

            if (!p0Online && !p1Online) return false;           // both players are on this client
            else if (!p0Online) return p1Online;                // if player 0 is offline (or was set to offline by the controller that was last there), we need the online status of player 1
            else if (!p1Online) return p0Online;                // if player 1 is offline (or was set to offline by the controller that was last there), we need the online status of player 0
            else return true;                                   // if both are online, return true
        }

        /// <summary>
        /// Returns whether a player in a quadrant is online or not
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="playerSide">Side of the player to check for online status: 0 for left player, 1 for right player</param>
        /// <returns></returns>
        public bool IsPlayerOnSideOnline(int quadrant, int playerSide)
        {
            if (playerSide < 0 || playerSide > 1) return false;

            return teamQuadrants[quadrant].IsOnlinePlayer[playerSide];
        }

        /// <summary>
        /// Returns the controller image index of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate is required</param>
        /// <returns></returns>
        public int GetTeammateOnlinePlayerIndex(int quadrant)
        {
            bool p0Online = teamQuadrants[quadrant].IsOnlinePlayer[0];
            bool p1Online = teamQuadrants[quadrant].IsOnlinePlayer[1];

            // This function will never get called when both teammates are online
            // We can use this fact to simplify this code

            if (!p0Online && !p1Online) return -1;                                                          // both players are on this client
            else if (!p0Online) return teamQuadrants[quadrant].OnlinePlayerNumberOnOwner[1];                // if player 0 is offline (or was set to offline by the controller that was last there), we need the online status of player 1
            else if (!p1Online) return teamQuadrants[quadrant].OnlinePlayerNumberOnOwner[0];                // if player 1 is offline (or was set to offline by the controller that was last there), we need the online status of player 0
            else return -1;
        }

        /// <summary>
        /// Returns the controller image index of the player on a given side (left - 0 or right - -1) in a quadrant
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="playerSide">The side of the player for which the online owner controller image index is requested</param>
        /// <returns></returns>
        public int GetOnlinePlayerIndexOnSide(int quadrant, int playerSide)
        {
            bool pOnline = teamQuadrants[quadrant].IsOnlinePlayer[playerSide];

            if (!pOnline) return -1;
            else return teamQuadrants[quadrant].OnlinePlayerNumberOnOwner[playerSide];
        }

        /// <summary>
        /// Returns the client id of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Client ID of the player whose teammate is required</param>
        /// <returns></returns>
        public string GetTeammateClientID(int quadrant)
        {
            bool p0Online = teamQuadrants[quadrant].IsOnlinePlayer[0];
            bool p1Online = teamQuadrants[quadrant].IsOnlinePlayer[1];

            // This function will never get called when both teammates are online
            // We can use this fact to simplify this code

            if (!p0Online && !p1Online) return "";                                                          // both players are on this client
            else if (!p0Online) return teamQuadrants[quadrant].OnlinePlayerClientIDs[1];                // if player 0 is offline (or was set to offline by the controller that was last there), we need the online status of player 1
            else if (!p1Online) return teamQuadrants[quadrant].OnlinePlayerClientIDs[0];                // if player 1 is offline (or was set to offline by the controller that was last there), we need the online status of player 0
            else return "";
        }

        /// <summary>
        /// Returns the client ID of the specified side of a quadrant
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="playerSide">Side of the player whose client ID is requested</param>
        /// <returns></returns>
        public string GetClientIDofPlayerOnSide(int quadrant, int playerSide)
        {
            bool pOnline = teamQuadrants[quadrant].IsOnlinePlayer[playerSide];

            if (!pOnline) return "";
            else return teamQuadrants[quadrant].OnlinePlayerClientIDs[playerSide];
        }

        /// <summary>
        /// Returns the controller image index of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate is required</param>
        /// <returns></returns>
        public int GetTeammateOnlinePlayerPosInQuadrant(int quadrant, int controllerImgIndex)
        {
            bool p0Online = teamQuadrants[quadrant].IsOnlinePlayer[0];
            bool p1Online = teamQuadrants[quadrant].IsOnlinePlayer[1];

            // This function will never get called when both teammates are online
            // We can use this fact to simplify this code

            if (!p0Online && !p1Online) return -1;       // both players are on this client
            else if (!p0Online) return 1;                // if player 0 is offline (or was set to offline by the controller that was last there), the online player is in pos 0
            else if (!p1Online) return 0;                // if player 1 is offline (or was set to offline by the controller that was last there), the online player is in pos 1
            else return -1;
        }

        #endregion

        void Update()
        {
            // PLEASE change these INPUT.GetKeys to RewiredInput.GetButtons
            if (Input.GetKeyDown(KeyCode.BackQuote)) networkStatsText.gameObject.SetActive(!networkStatsText.gameObject.activeSelf);
            if (lobbySetupPanel.activeSelf && PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
            {
                networkStatsText.text = "Ping: " + PhotonNetwork.GetPing() + "\nServer: " + PhotonNetwork.ServerAddress + "\nRegion: " + PhotonNetwork.CloudRegion;
            }
            else
            {
                networkStatsText.text = "Ping: " + PhotonNetwork.GetPing() + "\nServer: " + PhotonNetwork.ServerAddress + "\nRegion: " + PhotonNetwork.CloudRegion; ;
            }

            if ( (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) && (isInMultiplayerPanel && !isLoadingRoom) ) {
                if (EventSystem.current.currentSelectedGameObject == roomNameInput.gameObject) {
                    OnJoinRoomBtnClicked(roomNameInput);
                }
            }
        }
        private void OnDestroy()
        {
            //Debug.Log("unsubbing");
            // Unsubscribe from events
            RewiredJoystickAssign.Instance.OnJoystickConnected -= ControllerConnected;
            RewiredJoystickAssign.Instance.OnJoystickPreDisconnect -= ControllerDisconnected;

            NetworkManager.Instance.JoinRoomFailed -= JoinRoomFailed;
            NetworkManager.Instance.OnClientLeftRoom -= OtherClientDisconnectedFromRoom;
            NetworkManager.Instance.OnDisconnectedFromNetwork -= OnDisconnectedFromNetwork;

#if !DISABLESTEAMWORKS
            SteamScript.Instance.OnGameJoinRequested -= TeleportToStateAndTryJoinRoom;
#endif

            subscribedToControllerEvents = false;
        }

#region Event Subscriptions
        public void ControllerConnected(int rewiredID, int playerNumber, ControllerType type)
        {
            controllerSetup.ControllerConnected(rewiredID, playerNumber, type);
        }

        public void ControllerDisconnected(int rewiredID, int playerNumber)
        {
            controllerSetup.ControllerDisconnected(rewiredID, playerNumber);
        }
#endregion
    }
}

// Here
// right here
// do the thing
//
// Love,
// Alldogsgotoheaven
