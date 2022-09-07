using Fling.Localization;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Menus
{
    public class MenuState_LobbyScreen3D : MonoBehaviourPunCallbacks, IMenuState
    {

        public MenuState State
        {
            get { return MenuState.LOBBY_SCREEN_3D; }
        }

        public MenuState NextState
        {
            get { return (MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign)? MenuState.WORLD_SELECT : MenuState.GRID_LEVEL_SELECT; }
        }

        public MenuState PreviousState
        {
            get { return (MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign) ? MenuState.CAMPAIGN_ONLINE_OFFLINE_3D : MenuState.RACE_ONLINE_OFFLINE_3D; }
        }

        [SerializeField]
        private Transform cameraLocation;
        public Transform CameraLocation
        {
            get { return cameraLocation; }
        }
        public float WaitBeforeDeactivate => 0.5f;

        [SerializeField]
        private CanvasGroup mainCanvasGroup;
        [SerializeField] private GameObject main3DParent;

        public Action CustomInputChecks => null;

        private const int MAX_LOCAL_TEAMS_ALLOWED = 2;                                      // The number of local teams that are allowed. This is either 1 for campaign or 2 for race

        [Header("Lobby Buttons")]
        [SerializeField]
        private LobbyTeamButton3D[] teamQuadrantButtons;
        private MenuTeam[] menuTeam;
        private int numTeams;

        private EventSystem eventSystem;
        private Coroutine continuingInTimeCoroutine;

        [SerializeField] private GameObject raceTVScreen, campaignTVScreen;
        [SerializeField]
        private LocalizedText raceTVScreenText, campaignTVScreenText;

        private event Action onStateSyncedWithMasterClientActions;
        private event Action onActivateActions;
        private bool isActive = false;
        public static bool isStateSyncedWithMasterClient = true;
        private bool hasLobbySubmitted = false;
        public void InitState()
        {
            mainCanvasGroup.interactable = false;
            eventSystem = EventSystem.current;

            numTeams = teamQuadrantButtons.Length;

            menuTeam = MenuData.LobbyScreenData.ControllerSetup.MenuTeams;
            if (menuTeam == null)
            {
                menuTeam = new MenuTeam[numTeams];
                for (int i = 0; i < numTeams; i++)
                {
                    int team = i;
                    menuTeam[i] = new MenuTeam();
                    menuTeam[i].TeamIndex = team;
                }
            }

            for (int i = 0; i < numTeams; i++)
            {
                int team = i;
                LobbyTeamButton3D teamQuadrantButton = teamQuadrantButtons[i];

                teamQuadrantButton.InitButton(i, menuTeam[i]);
                teamQuadrantButton.onClickByInstance.AddListener((instanceId, playerNum) => TeamQuadrantButtonClicked(team, instanceId));
            }

            LobbyTeamButton3D.OnNumberOfPlayersSelected += LobbyTeamButton3D_OnNumberOfPlayersSelected;

            onActivateActions = null;
            onStateSyncedWithMasterClientActions = null;
        }

        public void ActivateState()
        {
            mainCanvasGroup.interactable = true;
            main3DParent.SetActive(true);
            hasLobbySubmitted = false;

            if (!MenuManager.Instance.ControllerAssigner.IsListeningForControllerAssignments)
            {
                MenuManager.Instance.ControllerAssigner.Activate();
            }
            if (!MenuManager.Instance.OnlineRoomHandler.IsActive)
            {
                MenuManager.Instance.OnlineRoomHandler.Activate();
            }

            MenusControllerAssignmentManager.OnInstancePreDisconnect += OnInstanceDisconnected;
            MenusControllerAssignmentManager.OnInstanceRespawnedAfterConnectivityChange += OnInstanceRespawned;

            UpdateAllMenuTeamsAndInstances();

            teamQuadrantButtons[0].SetButtonEnabled(true);  // team 1 is always enabled
            for (int i = 1; i < numTeams; i++)
            {
                teamQuadrantButtons[i].SetButtonEnabled(MenuData.MainMenuData.PlayType == MenuData.PlayType.Race);
            }

            MenuManager.Instance.MatrixBlender.BlendTo3DMenuFOV(0.2f, 0f);
            NetworkManager.Instance.ResetTeamClientIDs();

            SetTeamsAvailable();
            CheckAndSetTeamAvailabilitiesForAllRoomClients();

            UpdateTVScreens();

            NetworkManager.Instance.OnClientEnteredRoom += OnClientEnteredRoom;
            NetworkManager.Instance.OnClientLeftRoom += OnClientLeftRoom;
            NetworkManager.Instance.OnDisconnectedFromNetwork += OnDisconnectedFromNetwork;
            NetworkManager.Instance.JoinedRoom += JoinedRoom;

            isActive = true;
            onActivateActions?.Invoke();
            onActivateActions = null;
        }

        public void PreDeactivate()
        {
            for (int i = 0; i < numTeams; i++)
            {
                teamQuadrantButtons[i].SetButtonEnabled(false);
            }
        }

        /// <summary>
        /// Is called when we know this is no longer the active state
        /// </summary>
        public void DeactivateState(bool movingToNextMenuState)
        {
            mainCanvasGroup.interactable = false;
            main3DParent.SetActive(false);

            if (movingToNextMenuState)
            {
                if (MenuManager.Instance.ControllerAssigner.IsListeningForControllerAssignments)
                {
                    MenuManager.Instance.ControllerAssigner.Deactivate();
                }

                if (MenuManager.Instance.OnlineRoomHandler.IsActive)
                {
                    MenuManager.Instance.OnlineRoomHandler.Deactivate();
                }
                MenuManager.Instance.MatrixBlender.BlendToNormalFOV(0.2f, 0.2f);
            }
            else
            {
                for (int i = 0; i < numTeams; i++)
                {
                    teamQuadrantButtons[i].SetButtonEnabled(false);
                    teamQuadrantButtons[i].DetachEverything();
                    menuTeam[i].Reset();
                }

                MenuManager.Instance.ControllerAssigner.UnattachAllInstancesFromTeams();
            }

            MenusControllerAssignmentManager.OnInstancePreDisconnect -= OnInstanceDisconnected;
            MenusControllerAssignmentManager.OnInstanceRespawnedAfterConnectivityChange -= OnInstanceRespawned;


            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnClientEnteredRoom -= OnClientEnteredRoom;
                NetworkManager.Instance.JoinedRoom -= JoinedRoom;
                NetworkManager.Instance.OnClientLeftRoom -= OnClientLeftRoom;
                NetworkManager.Instance.OnDisconnectedFromNetwork -= OnDisconnectedFromNetwork;

            }
            onActivateActions = null;
            isActive = false;
            isStateSyncedWithMasterClient = true;
            onStateSyncedWithMasterClientActions = null;

            if (!movingToNextMenuState)
            {
                StopContinuingToNextState();
            }
        }

        /// <summary>
        /// For each menu team does the following:
        /// 1. Updates team instance positions if the layout of the menu team is not NONE
        /// 2. Resets the menu team
        /// </summary>
        private void UpdateAllMenuTeamsAndInstances()
        {
            for (int i = 0; i < numTeams; i++)
            {
                // Set other character positions
                MenuTeam curTeam = menuTeam[i];
                LobbyTeamButton3D teamQuadrantButton = teamQuadrantButtons[i];

                switch (curTeam.Layout)
                {
                    case ControllerLayout.LayoutStyle.Shared:
                        if (!curTeam.IsOnlinePlayer[0])
                        {
                            // shared team owned by us
                            MenusControllerAssignmentManager.TeamSetupInstance instance = MenuManager.Instance.ControllerAssigner.GetInstance(curTeam.RewiredIDs[0]);
                            if (instance != null && instance.TeamContent != null)
                            {
                                Rigidbody[] players = instance.TeamContent.GetPlayerRigidbodies();
                                if (players[0] != null)
                                {
                                    teamQuadrantButton.UpdateLeftPlayerPosition(players[0]);
                                }
                                if (players[1] != null)
                                {
                                    teamQuadrantButton.UpdateRightPlayerPosition(players[1]);
                                }

                                Rigidbody[] ropeLinks = instance.TeamContent.GetLinkRigidbodies();
                                if (ropeLinks != null)
                                {
                                    teamQuadrantButton.UpdateRopePosition(ropeLinks);
                                }
                            }
                        }
                        break;
                    case ControllerLayout.LayoutStyle.Separate:
                        if (!curTeam.IsOnlinePlayer[0])
                        {
                            // we own the left character
                            MenusControllerAssignmentManager.TeamSetupInstance instance = MenuManager.Instance.ControllerAssigner.GetInstance(curTeam.RewiredIDs[0]);
                            if (instance != null && instance.TeamContent != null)
                            {
                                Rigidbody[] players = instance.TeamContent.GetPlayerRigidbodies();
                                if (players[0] != null)
                                {
                                    teamQuadrantButton.UpdateLeftPlayerPosition(players[0]);
                                }
                            }
                        }
                        if (!curTeam.IsOnlinePlayer[1])
                        {
                            // we own the right character
                            MenusControllerAssignmentManager.TeamSetupInstance instance = MenuManager.Instance.ControllerAssigner.GetInstance(curTeam.RewiredIDs[1]);
                            if (instance != null && instance.TeamContent != null)
                            {
                                Rigidbody[] players = instance.TeamContent.GetPlayerRigidbodies();
                                if (players[0] != null)
                                {
                                    teamQuadrantButton.UpdateRightPlayerPosition(players[0]);
                                }
                            }
                        }
                        break;
                }

                teamQuadrantButton.DetachEverything();
                curTeam.Reset();
            }
            MenuManager.Instance.ControllerAssigner.UnattachAllInstancesFromTeams();
        }

        public void StateScreenTransitionFinish()
        {
            return;
        }

        #region INPUT
        public bool Select(float horizontalInput, float verticalInput, int rewiredPlayerID = 0, int playerNumber = 0)
        {
            // return false because Unity's EventSystem handles this
            return false;
        }

        public bool Submit(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            // return false because Unity's EventSystem handles this
            return false;
        }

        public void SubmitLobby3D()
        {
            SaveDataForProgressingToNextState();
            RewiredJoystickAssign.Instance.EndJoystickAssignment(true, true);

            if (!PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient)
            {
                NetworkManager.Instance.CloseRoom();
            }
            hasLobbySubmitted = true;
        }

        public bool Pick(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            // return false because Unity's EventSystem handles this
            return false;
        }

        public bool Cancel(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            return false;
        }

        public bool BackButtonClicked()
        {
            // return to the previous menu
            if (PhotonNetwork.OfflineMode || NetworkManager.Instance.RoomType == RoomType.Private)
            {
                return true;
            }
            else
            {
                MetaManager.Instance.NotificationControl.ShowYesNoNotification(LocalizationKeys.POPUP_EXIT_ROOM,
                () =>
                {
                    NetworkManager.Instance.Disconnect(ReturnToMainMenu);
                },
                null,
                null);

                return false;
            }
        }

        public bool PickHoldStart(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            return false;
        }

        public bool PickHoldSuccess(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            return false;
        }

        public bool PickHoldFail(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            return false;
        }

        public bool CancelHoldStart(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            return false;
        }

        public bool CancelHoldSuccess(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            return false;
        }

        public bool CancelHoldFail(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            return false;
        }
        #endregion

        private void TeamQuadrantButtonClicked(int quadrant, int instanceId)
        {
            if (PhotonNetwork.OfflineMode)
            {
                TeamQuadrantButtonClickedLogic(quadrant, instanceId, "");
            }
            else if (PhotonNetwork.InRoom)
            {
                photonView.RPC(nameof(TeamQuadrantButtonClickedLogic), RpcTarget.AllViaServer, quadrant, instanceId, PhotonNetwork.LocalPlayer.UserId);
            }
            else
            {
                Button3D.MarkActionDoneForLocalInstanceId(instanceId);
            }
        }

        [PunRPC]
        private void TeamQuadrantButtonClickedLogic(int quadrant, int instanceId, string clientId)
        {
            if (hasLobbySubmitted)
            {
                return;
            }

            if (!isStateSyncedWithMasterClient)
            {
                onStateSyncedWithMasterClientActions += () => TeamQuadrantButtonClickedLogic(quadrant, instanceId, clientId);
                return;
            }

            bool isOwnedByThisClient = false;
            if ((PhotonNetwork.OfflineMode && string.IsNullOrEmpty(clientId)) ||
                (!PhotonNetwork.OfflineMode && PhotonNetwork.LocalPlayer.UserId.Equals(clientId)))
            {
                isOwnedByThisClient = true;
            }

            MenusControllerAssignmentManager.TeamSetupInstance instance = null;

            if (isOwnedByThisClient)
            {
                instance = MenuManager.Instance.ControllerAssigner.GetInstance(instanceId);
            }
            else
            {
                string instanceIdWithClient = MenusControllerAssignmentManager.GetOtherClientInstanceId(clientId, instanceId);
                instance = MenuManager.Instance.ControllerAssigner.GetOtherClientInstance(instanceIdWithClient);
            }

            MenuTeam team = menuTeam[quadrant];
            LobbyTeamButton3D btn = teamQuadrantButtons[quadrant];

            if (isOwnedByThisClient)
            {
                Button3D.MarkActionDoneForLocalInstanceId(instanceId);
            }

            if (instance == null)
            {
                return;
            }
            if (team.HasInstanceAttached(instance))
            {
                if (team.State == MenuTeam.MenuTeamState.Ready || team.State == MenuTeam.MenuTeamState.NotReady)
                {
                    // remove
                    team.RemoveInstance(instance);
                    if (team.NumControllersAttached == 0)
                    {
                        btn.SwitchToNoControllersAttachedState();
                        btn.DetachLeftPlayer();
                        btn.DetachRightPlayer();
                        btn.DetachRope();
                    }
                    else if (team.NumControllersAttached == 1)
                    {
                        if (!team.LeftPlayerAttached)
                        {
                            btn.SwitchToNeedsPartner(false);
                            btn.DetachLeftPlayer();
                        }
                        else
                        {
                            btn.SwitchToNeedsPartner();
                            btn.DetachRightPlayer();
                        }
                    }
                }
                else if (team.State == MenuTeam.MenuTeamState.NumberOfPlayersSelect)
                {
                    btn.NumberOfPlayersPromptUI.Pick();
                }
            }
            else if ((isOwnedByThisClient && team.AvailableForClientSelection) || (!isOwnedByThisClient && team.IsAvailableForOtherClientSelection(clientId)))
            {
                // attach
                if (instance.Layout == ControllerLayout.LayoutStyle.Shared && team.NumControllersAttached == 0)
                {
                    team.AttachInstance(instance);
                    btn.AttachLeftPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                    btn.AttachRightPlayer(instance.TeamContent.GetPlayerRigidbody(2));
                    btn.AttachRopeJoints(instance.TeamContent.GetLinkRigidbodies());

                    if (team.State == MenuTeam.MenuTeamState.NumberOfPlayersSelect)
                    {
                        btn.SwitchToNumPlayersSelectUI(instance);
                        instance.SetSelectingNumPlayersInMenu(true);
                    }
                    else if (team.State == MenuTeam.MenuTeamState.Ready)
                    {
                        btn.SwitchToReady();
                    }
                }
                else if (instance.Layout == ControllerLayout.LayoutStyle.Separate
                    && (team.NumControllersAttached == 0
                        || (team.NumControllersAttached == 1 && team.Layout == ControllerLayout.LayoutStyle.Separate)))
                {
                    bool wasLeftAttached = team.LeftPlayerAttached;
                    bool wasRightAttached = team.RightPlayerAttached;

                    team.AttachInstance(instance);

                    if (!wasLeftAttached && team.LeftPlayerAttached)
                    {
                        btn.AttachLeftPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                    }
                    if (!wasRightAttached && team.RightPlayerAttached)
                    {
                        btn.AttachRightPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                    }

                    if (team.NumControllersAttached == 1)
                    {
                        if (!team.LeftPlayerAttached)
                        {
                            btn.SwitchToNeedsPartner(false);
                        }
                        else if (!team.RightPlayerAttached)
                        {
                            btn.SwitchToNeedsPartner();
                        }
                    }
                    else
                    {
                        btn.SwitchToReady();
                    }
                }
            }
            else if (isOwnedByThisClient && !team.AvailableForClientSelection)
            {
                MetaManager.Instance.NotificationControl.ShowOKNotification(LocalizationKeys.POPUP_TWO_TEAMS_SPLITSCREENED, null, null);
            }

            if (isOwnedByThisClient)
            {
                SetTeamsAvailable();
                SetButtonTooManySplitScreensStatus();
            }
            else
            {
                SetTeamsAvailableOnOtherClient(clientId);
            }

            CheckAndMoveToNextState();
        }

        private void OnInstanceDisconnected(int instanceId, string clientId, bool isOwnedByThisClient)
        {
            MenusControllerAssignmentManager.TeamSetupInstance instance = null;

            if (isOwnedByThisClient)
            {
                instance = MenuManager.Instance.ControllerAssigner.GetInstance(instanceId);
            }
            else
            {
                string instanceIdWithClient = MenusControllerAssignmentManager.GetOtherClientInstanceId(clientId, instanceId);
                instance = MenuManager.Instance.ControllerAssigner.GetOtherClientInstance(instanceIdWithClient);
                NoLongerWaitingForClientInstance(instanceIdWithClient);
            }

            if (instance == null)
            {
                return;
            }

            bool changed = false;

            for (int i = 0; i < numTeams; i++)
            {
                MenuTeam team = menuTeam[i];
                if (team.HasInstanceAttached(instance))
                {
                    changed = true;

                    LobbyTeamButton3D btn = teamQuadrantButtons[i];

                    // remove
                    team.RemoveInstance(instance);
                    btn.StopListeningForNumberOfSelectInput();
                    if (team.NumControllersAttached == 0)
                    {
                        btn.SwitchToNoControllersAttachedState();
                        btn.DetachLeftPlayer();
                        btn.DetachRightPlayer();
                        btn.DetachRope();
                    }
                    else if (team.NumControllersAttached == 1)
                    {
                        if (!team.LeftPlayerAttached)
                        {
                            btn.SwitchToNeedsPartner(false);
                            btn.DetachLeftPlayer();
                        }
                        else
                        {
                            btn.SwitchToNeedsPartner();
                            btn.DetachRightPlayer();
                        }
                    }

                    break;
                }
            }

            if (changed)
            {
                if (isOwnedByThisClient)
                {
                    SetTeamsAvailable();
                    SetButtonTooManySplitScreensStatus();
                }
                else
                {
                    SetTeamsAvailableOnOtherClient(clientId);
                }

                CheckAndMoveToNextState();
            }
        }

        private void OnInstanceRespawned(MenusControllerAssignmentManager.TeamSetupInstance instance)
        {
            if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
            {
                ReattachInstanceIfItWasAttached(instance);
            }
        }

        private void ReattachInstanceIfItWasAttached(MenusControllerAssignmentManager.TeamSetupInstance instance)
        {
            for (int i = 0; i < numTeams; i++)
            {
                MenuTeam team = menuTeam[i];
                if (team.HasInstanceAttached(instance))
                {
                    ReattachLocalInstanceToButton(instance, i);

                    //bool numPlayersSetInCaseOfShared = false;
                    //if (team.Layout == ControllerLayout.LayoutStyle.Shared)
                    //{
                    //    numPlayersSetInCaseOfShared = team.State == MenuTeam.MenuTeamState.Ready;
                    //}

                    //photonView.RPC(nameof(AttachOtherClientInstance), RpcTarget.Others, instance.RewiredId1, PhotonNetwork.LocalPlayer.UserId, i, numPlayersSetInCaseOfShared);
                }
                if (team.Layout != ControllerLayout.LayoutStyle.None)
                {
                    team.ConvertToOnlineTeam();
                }
            }
        }

        private void ReattachLocalInstanceToButton(MenusControllerAssignmentManager.TeamSetupInstance instance, int teamIndex)
        {
            MenuTeam team = menuTeam[teamIndex];
            LobbyTeamButton3D btn = teamQuadrantButtons[teamIndex];

            if (team.Layout == ControllerLayout.LayoutStyle.Shared)
            {
                btn.AttachLeftPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                btn.AttachRightPlayer(instance.TeamContent.GetPlayerRigidbody(2));
                btn.AttachRopeJoints(instance.TeamContent.GetLinkRigidbodies());
            }
            else if (team.Layout == ControllerLayout.LayoutStyle.Separate)
            {
                if (team.RewiredIDs[0] == instance.RewiredId1)
                {
                    btn.AttachLeftPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                }
                else if (team.RewiredIDs[1] == instance.RewiredId1)
                {
                    btn.AttachRightPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                }
            }
        }

        private void SetTeamsAvailable()
        {
            int teamsSelected = menuTeam.Count(x => x.IsSelectedByThisClient());

            List<int> idxOfQuadrantsToMakeUnavailable = new List<int>();
            List<int> idxOfQuadrantsToMakeAvailable = new List<int>();

            if (MenuData.MainMenuData.PlayType == MenuData.PlayType.Race)
            {
                if (teamsSelected == MAX_LOCAL_TEAMS_ALLOWED)
                {
                    idxOfQuadrantsToMakeUnavailable = menuTeam.FindAllIndexOfConditionMet(x => !x.IsSelectedByThisClient());
                }
                else if (teamsSelected >= 0 && teamsSelected < MAX_LOCAL_TEAMS_ALLOWED)
                {
                    idxOfQuadrantsToMakeAvailable = menuTeam.FindAllIndexOfConditionMet(x => !x.IsSelectedByThisClient());
                }
            }
            else
            {
                idxOfQuadrantsToMakeUnavailable = new List<int> { 1, 2, 3 };
                idxOfQuadrantsToMakeAvailable = new List<int> { 0 };
            }

            foreach (int i in idxOfQuadrantsToMakeUnavailable)
            {
                menuTeam[i].SetAvailableForThisClient(false);
            }
            foreach (int i in idxOfQuadrantsToMakeAvailable)
            {
                menuTeam[i].SetAvailableForThisClient(true);
            }
        }

        private void SetTeamsAvailableOnOtherClient(string clientId)
        {
            int teamsSelected = menuTeam.Count(x => x.IsSelectedByOtherClient(clientId));

            List<int> idxOfQuadrantsToMakeUnavailable = new List<int>();
            List<int> idxOfQuadrantsToMakeAvailable = new List<int>();

            if (MenuData.MainMenuData.PlayType == MenuData.PlayType.Race)
            {
                if (teamsSelected == MAX_LOCAL_TEAMS_ALLOWED)
                {
                    idxOfQuadrantsToMakeUnavailable = menuTeam.FindAllIndexOfConditionMet(x => !x.IsSelectedByOtherClient(clientId));
                }
                else if (teamsSelected >= 0 && teamsSelected < MAX_LOCAL_TEAMS_ALLOWED)
                {
                    idxOfQuadrantsToMakeAvailable = menuTeam.FindAllIndexOfConditionMet(x => !x.IsSelectedByOtherClient(clientId));
                }
            }
            else
            {
                idxOfQuadrantsToMakeUnavailable = new List<int> { 1, 2, 3 };
                idxOfQuadrantsToMakeAvailable = new List<int> { 0 };
            }

            foreach (int i in idxOfQuadrantsToMakeUnavailable)
            {
                menuTeam[i].SetAvailableForClient(false, clientId);
            }
            foreach (int i in idxOfQuadrantsToMakeAvailable)
            {
                menuTeam[i].SetAvailableForClient(true, clientId);
            }
        }

        private void CheckAndSetTeamAvailabilitiesForAllRoomClients()
        {
            if (!PhotonNetwork.OfflineMode && PhotonNetwork.InRoom)
            {
                List<Player> clients = PhotonNetwork.CurrentRoom.Players.Values.ToList();
                foreach (Player client in clients)
                {
                    SetTeamsAvailableOnOtherClient(client.UserId);
                }
            }
        }

        private void SetButtonTooManySplitScreensStatus()
        {
            foreach (LobbyTeamButton3D btn in teamQuadrantButtons)
            {
                btn.CheckTooManySplitScreensStatus();
            }
        }

        public void SaveDataForProgressingToNextState()
        {
            int team = 0;
            int localTeam = 0;

            // Reset player prefs first. This makes sure unused teams have IDs -1.
            //for (int i = 0; i < 2; i++)
            //{
            //    PlayerPrefs.SetString("team" + (i).ToString() + "Layout", ControllerLayout.LayoutStyle.None.ToString());
            //    PlayerPrefs.SetInt("player_" + (i).ToString() + "1_RewiredID", -1);
            //    PlayerPrefs.SetInt("player_" + (i).ToString() + "2_RewiredID", -1);
            //    PlayerPrefs.SetInt("team" + (i).ToString() + "SharedControllerOwnerID", -1);
            //    PlayerPrefs.SetInt("team" + (i).ToString() + "NumPlayers", -1);
            //}

            MenuData.LobbyScreenData.ControllerSetup.ClearData();
            int prevLocalTeam = localTeam;

            // Now set player prefs
            for (int i = 0; i < numTeams; i++)
            {
                MenuTeam curTeam = menuTeam[i];


                if (curTeam.Layout != ControllerLayout.LayoutStyle.None)
                {
                    // update menu team character data
                    SaveCharacterDataForTeam(curTeam);

                    if (!PhotonNetwork.OfflineMode)
                    {
                        if (PhotonNetwork.LocalPlayer.UserId.Equals(NetworkManager.Instance.TeamClientIDs[i][0]) || PhotonNetwork.LocalPlayer.UserId.Equals(NetworkManager.Instance.TeamClientIDs[i][1]))
                        {
                            // team++;
                            NetworkManager.Instance.LocalTeamIndices[localTeam] = team;
                            localTeam++;
                        }
                    }
                    else
                    {
                        NetworkManager.Instance.LocalTeamIndices[localTeam] = team;
                        localTeam++;
                    }
                    // else {
                    //     team++;
                    // }
                    team++;
                    MenuData.LobbyScreenData.ControllerSetup.TeamQuadrantIndex[team - 1] = i;
                    if (localTeam != prevLocalTeam)
                    {
                        Debug.Log("Team " + (team - 1) + " is Quadrant " + (i + 1));
                        //PlayerPrefs.SetString("team" + (prevLocalTeam).ToString() + "Layout", layouts[i].Layout.ToString());
                        //PlayerPrefs.SetInt("player_" + (prevLocalTeam).ToString() + "1_RewiredID", layouts[i].RewiredIDs[0]);
                        //PlayerPrefs.SetInt("player_" + (prevLocalTeam).ToString() + "2_RewiredID", layouts[i].RewiredIDs[1]);
                        //PlayerPrefs.SetInt("team" + (prevLocalTeam).ToString() + "SharedControllerOwnerID", layouts[i].SharedControllerOwnerID);

                        MenuData.LobbyScreenData.ControllerSetup.LocalMenuTeams[prevLocalTeam] = menuTeam[i];

                        int numPlayers = 2;
                        //PlayerPrefs.SetInt("team" + (prevLocalTeam).ToString() + "NumPlayers", numPlayers);

                        Debug.Log("<color=blue> Team" + (team - 1).ToString() + "Layout: " + menuTeam[i].Layout.ToString() + "</color>");
                        Debug.Log("<color=green> Player" + (team - 1).ToString() + "1_RewiredID: " + menuTeam[i].RewiredIDs[0].ToString() + "</color>");
                        Debug.Log("<color=green> Team" + (team - 1).ToString() + "2_RewiredID: " + menuTeam[i].RewiredIDs[1].ToString() + "</color>");

                        prevLocalTeam = localTeam;
                    }
                }
            }

            NetworkManager.Instance.ClearEmptyTeamClientIDs();

            if (PhotonNetwork.OfflineMode)
            {
                MenuData.LobbyScreenData.TeamsOnThisClient = team;
                //PlayerPrefs.SetInt("teamsOnThisClient", team);
                if (team <= 1)
                {
                    MenuManager.Instance.SelectedMode = 0;
                }
                else
                {
                    // Debug.Log("Else for screen select running");
                    MenuManager.Instance.SelectedMode = 1;
                }
            }
            else
            {
                MenuData.LobbyScreenData.TeamsOnThisClient = localTeam;
                //PlayerPrefs.SetInt("teamsOnThisClient", localTeam);
                if (localTeam <= 1)
                {
                    // Debug.Log("Screen mode: Single screen");
                    MenuManager.Instance.SelectedMode = 0;
                }
                else
                {
                    // Debug.Log("Screen mode: Split-screen");
                    MenuManager.Instance.SelectedMode = 1;
                }
            }

            MenuData.LobbyScreenData.ControllerSetup.MenuTeams = menuTeam;

            // Also need to set this in offline mode, because in the case that people are offline in lobby then go to matchmake, NetworkManager needs to have the correct number of teams.
            NetworkManager.Instance.SetNumberOfTeams(team);

            MenuData.LobbyScreenData.ScreenMode = MenuManager.Instance.SelectedMode;
            //PlayerPrefs.SetInt("screenMode", MenuManager.Instance.SelectedMode);
        }

        private void SaveCharacterDataForTeam(MenuTeam curTeam)
        {
            if (curTeam.Layout == ControllerLayout.LayoutStyle.None)
            {
                return;
            }

            MenusControllerAssignmentManager.TeamSetupInstance instance = null;
            if (curTeam.Layout == ControllerLayout.LayoutStyle.Shared)
            {
                if (curTeam.IsOnlinePlayer[0])
                {
                    instance = MenuManager.Instance.ControllerAssigner.GetOtherClientInstance(curTeam.OnlineAttachedInstanceIds[0]);
                }
                else
                {
                    instance = MenuManager.Instance.ControllerAssigner.GetInstance(curTeam.RewiredIDs[0]);
                }

                if (instance != null)
                {
                    curTeam.CharSkinIndices[0] = instance.CharSkinIndices[0];
                    curTeam.CharSkinIndices[1] = instance.CharSkinIndices[1];
                }
            }
            else if (curTeam.Layout == ControllerLayout.LayoutStyle.Separate)
            {
                for (int player = 0; player < 2; player++)
                {
                    if (curTeam.IsOnlinePlayer[player])
                    {
                        instance = MenuManager.Instance.ControllerAssigner.GetOtherClientInstance(curTeam.OnlineAttachedInstanceIds[player]);
                    }
                    else
                    {
                        instance = MenuManager.Instance.ControllerAssigner.GetInstance(curTeam.RewiredIDs[player]);
                    }

                    if (instance != null)
                    {
                        curTeam.CharSkinIndices[player] = instance.CharSkinIndices[0];
                    }
                }
            }
        }
        private void CheckAndMoveToNextState()
        {
            if (hasLobbySubmitted)
            {
                return;
            }

            if (CanContinuePastLobbyScreen())
            {
                ShowContinueInTimePanel();
            }
            else
            {
                StopContinuingToNextState();
            }
        }

        private void ShowContinueInTimePanel()
        {
            if (continuingInTimeCoroutine != null)
            {
                StopCoroutine(continuingInTimeCoroutine);
            }

            continuingInTimeCoroutine = StartCoroutine(ContinueInTimeCoroutine());

            if (!PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient)
            {
                NetworkManager.Instance.CloseRoom();
            }
        }

        private void StopContinuingToNextState()
        {
            if (continuingInTimeCoroutine != null)
            {
                StopCoroutine(continuingInTimeCoroutine);
                continuingInTimeCoroutine = null;
                //startToContinuePanel.SetActive(false);
            }
            UpdateTVScreens();
            if (!PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient)
            {
                NetworkManager.Instance.OpenRoom();
            }
        }

        private IEnumerator ContinueInTimeCoroutine()
        {
            int secondsToWait = 5;

            string curMode = LocalizationManager.Instance.GetLocalizedValue(MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign ? LocalizationKeys.CAMPAIGN : LocalizationKeys.RACE);
            string countdownStr = LocalizationManager.Instance.GetLocalizedValue(LocalizationKeys.READY_CONTINUE_COUNTDOWN);
            string baseStr = countdownStr.Replace(LocalizationKeys.MODE_TAG, curMode);

            string timeLeft = baseStr.Replace(LocalizationKeys.TIME_TAG, secondsToWait.ToString());

            UpdateTVScreens();
            LocalizedText tvSreenText = MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign ? campaignTVScreenText : raceTVScreenText;
            tvSreenText.UpdateWithoutKey();
            tvSreenText.SetText(timeLeft);

            while (secondsToWait > 0)
            {
                yield return new WaitForSeconds(1f);
                secondsToWait--;
                timeLeft = baseStr.Replace(LocalizationKeys.TIME_TAG, secondsToWait.ToString());
                tvSreenText.SetText(timeLeft);
            }

            // prevent the players from unreadying during the last second before the lobby is submitted
            SubmitLobby3D();    // any unready received after submission will not be considered
            MoveToNextState();
            continuingInTimeCoroutine = null;
        }

        private void MoveToNextState()
        {
            if (MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign 
                || NetworkManager.Instance.RoomType != RoomType.Matchmake)
            {
                MenuManager.Instance.MoveToNextMenu();
            }
            else
            {
                TournamentManager.Instance.TournamentStarted();

                if (NetworkManager.Instance.RoomType == RoomType.Matchmake)
                {
                    MenuData.LevelSelectData.GameMode = Fling.GameModes.GameMode.Race;
                    MenuData.MainMenuData.RaceType = RaceType.Tournament;
                    NetworkManager.Instance.Matchmaking_MatchStarted();
                }

                if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
                {
                    MetaManager.Instance.LoadNextTournamentRace();
                }
            }
        }


        /// <summary>
        /// Checks to see if all players (local) are ready, and tells them to move on to the next screen
        /// </summary>
        public bool CanContinuePastLobbyScreen()
        {
            int numTeamsReady = 0;
            int teamsWithPlayers = 0;

            numTeamsReady = menuTeam.Count(team => team.State == MenuTeam.MenuTeamState.Ready);
            teamsWithPlayers = menuTeam.Count(team => team.NumControllersAttached >= 1);

            bool enoughTeamsFormed =
                (numTeamsReady > 0 && (MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign || NetworkManager.Instance.RoomType == RoomType.Matchmake))
                || (numTeamsReady > 1);

            if (enoughTeamsFormed)
            {
                // This check should only happen in online mode!
                if (!PhotonNetwork.OfflineMode && PhotonNetwork.InRoom)
                {
                    int clientsWithAtLeastOnePlayerInTeam = 0;

                    for (int i = 0; i < PhotonNetwork.CurrentRoom.Players.Keys.Count; i++)
                    {
                        for (int j = 0; j < NetworkManager.Instance.TeamClientIDs.Count; j++)
                        {
                            string thisClientID = PhotonNetwork.CurrentRoom.Players[PhotonNetwork.CurrentRoom.Players.Keys.ToList()[i]].UserId;
                            bool isClientPlayerInTeam = NetworkManager.Instance.TeamClientIDs[j].Contains(thisClientID);

                            if (isClientPlayerInTeam)
                            {
                                clientsWithAtLeastOnePlayerInTeam++;
                                break;
                            }
                        }

                        if (clientsWithAtLeastOnePlayerInTeam == PhotonNetwork.CurrentRoom.Players.Keys.Count)
                        {
                            break;
                        }
                    }

                    if (clientsWithAtLeastOnePlayerInTeam != PhotonNetwork.CurrentRoom.Players.Keys.Count)
                    {
                        //startToContinuePanel.SetActive(false);
                        Debug.Log(clientsWithAtLeastOnePlayerInTeam + ", not all clients have at least one player in a team");
                        return false;
                    }
                }

                if (teamsWithPlayers != numTeamsReady)
                {
                    // Hide the prompt
                    //startToContinuePanel.SetActive(false);
                    Debug.Log("not all teams are complete");
                    return false;
                }

                // If all players are ready, the code gets to this point
                // If so, show the prompt
                //startToContinuePanel.SetActive(true);

                if (!PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient)
                {
                    //startToContinueText.gameObject.SetActive(false);
                    //waitingForHostText.gameObject.SetActive(true);
                }
                else
                {
                    //startToContinueText.gameObject.SetActive(true);
                    //waitingForHostText.gameObject.SetActive(false);
                }
                Debug.Log("Players are raedy to go");

                if (numTeamsReady == 1)
                {
                    MenuData.MainMenuData.PlayType = MenuData.PlayType.Campaign;
                }
                else
                {
                    MenuData.MainMenuData.PlayType = MenuData.PlayType.Race;
                }

                return true;
            }

            //startToContinuePanel.SetActive(false);
            Debug.Log("No team is ready");

            return false;
        }

        private void UpdateTVScreens()
        {
            raceTVScreen.gameObject.SetActive(MenuData.MainMenuData.PlayType == MenuData.PlayType.Race);
            campaignTVScreen.gameObject.SetActive(MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign);
            raceTVScreenText.OverrideKeyAndUpdate(LocalizationKeys.RACE);
            campaignTVScreenText.OverrideKeyAndUpdate(LocalizationKeys.CAMPAIGN);
        }

        private void ReturnToMainMenu()
        {
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.MoveToMenuState(MenuState.MAINMENU_3D);
            }
            else
            {
                MetaManager.Instance.LoadMainMenu();
            }
        }

        private void LobbyTeamButton3D_OnNumberOfPlayersSelected(int teamIndex, int numberOfPlayers)
        {
            if (PhotonNetwork.OfflineMode)
            {
                OnNumberOfPlayersSelectedLogic(teamIndex, numberOfPlayers);
            }
            else
            {
                photonView.RPC(nameof(OnNumberOfPlayersSelectedLogic), RpcTarget.All, teamIndex, numberOfPlayers);
            }
        }
        
        [PunRPC]
        private void OnNumberOfPlayersSelectedLogic(int teamIndex, int numberOfPlayers)
        {
            if (!isStateSyncedWithMasterClient)
            {
                onStateSyncedWithMasterClientActions += () => OnNumberOfPlayersSelectedLogic(teamIndex, numberOfPlayers);
                return;
            }

            LobbyTeamButton3D btn = teamQuadrantButtons[teamIndex];
            btn.PickNumberOfPlayersAfterSyncing(numberOfPlayers);

            MenuTeam team = menuTeam[teamIndex];
            if (team.State == MenuTeam.MenuTeamState.NumberOfPlayersSelect)
            {
                team.OnNumberOfPlayersSet(numberOfPlayers);

                if (team.Layout == ControllerLayout.LayoutStyle.Shared)
                {
                    if (!team.IsOnlinePlayer[0])
                    {
                        int instanceId = team.RewiredIDs[0];
                        var instance = MenuManager.Instance.ControllerAssigner.GetInstance(instanceId);
                        if (instance != null)
                        {
                            instance.SetSelectingNumPlayersInMenu(false);
                        }
                    }
                }

                CheckAndMoveToNextState();
            }
        }

        #region SyncStateWhenJoiningRoom
        List<string> waitingForInstanceIds = new List<string>();
        [PunRPC]
        private void SyncStateWithMasterClient(int[] teamIndices, int[] layoutInts, int[] stateInts, string[] teamOnlineAttachedInstaceIds1, string[] teamOnlineAttachedInstaceIds2)
        {
            if (!isActive)
            {
                onActivateActions += () => SyncStateWithMasterClient(teamIndices, layoutInts, stateInts, teamOnlineAttachedInstaceIds1, teamOnlineAttachedInstaceIds2);
                return;
            }

            waitingForInstanceIds.Clear();
            int len = teamIndices.Length;
            for (int i = 0; i < len; i++)
            {
                int teamIdx = teamIndices[i];
                MenuTeam team = menuTeam[teamIdx];
                ControllerLayout.LayoutStyle layout = (ControllerLayout.LayoutStyle)layoutInts[i];
                MenuTeam.MenuTeamState state = (MenuTeam.MenuTeamState)stateInts[i];
                string teamOnlineAttachedInstanceId1 = teamOnlineAttachedInstaceIds1[i];
                string teamOnlineAttachedInstanceId2 = teamOnlineAttachedInstaceIds2[i];

                bool numPlayersSetInCaseOfShared = false;
                if (layout == ControllerLayout.LayoutStyle.Shared)
                {
                    numPlayersSetInCaseOfShared = state == MenuTeam.MenuTeamState.Ready;
                }

                string clientId = "";
                int instanceId = 0;

                if (!string.IsNullOrEmpty(teamOnlineAttachedInstanceId1))
                {
                    clientId = MenusControllerAssignmentManager.GetOtherClientIdFromInstanceId(teamOnlineAttachedInstanceId1);
                    instanceId = MenusControllerAssignmentManager.GetOtherClientInstanceIdOnClientFromInstanceId(teamOnlineAttachedInstanceId1);
                    AttachOtherClientInstance(instanceId, clientId, teamIdx, numPlayersSetInCaseOfShared, 0);
                }
                if (!string.IsNullOrEmpty(teamOnlineAttachedInstanceId2))
                {
                    clientId = MenusControllerAssignmentManager.GetOtherClientIdFromInstanceId(teamOnlineAttachedInstanceId2);
                    instanceId = MenusControllerAssignmentManager.GetOtherClientInstanceIdOnClientFromInstanceId(teamOnlineAttachedInstanceId2);
                    AttachOtherClientInstance(instanceId, clientId, teamIdx, numPlayersSetInCaseOfShared, 1);
                }
            }

            if (waitingForInstanceIds.Count <= 0)
            {
                isStateSyncedWithMasterClient = true;
                onStateSyncedWithMasterClientActions?.Invoke();
            }
        }

        /// <summary>
        /// Use this when the master client needs to sync the lobby data when new clients enter the room
        /// </summary>
        /// <param name="instanceIdOnOtherClient"></param>
        /// <param name="clientId"></param>
        /// <param name="teamIndex"></param>
        private bool AttachOtherClientInstance(int instanceIdOnOtherClient, string clientId, int teamIndex, bool numPlayerSetInCaseOfShared, int playerSide = 0)
        {
            string instanceId = MenusControllerAssignmentManager.GetOtherClientInstanceId(clientId, instanceIdOnOtherClient);
            MenusControllerAssignmentManager.TeamSetupInstance instance = MenuManager.Instance.ControllerAssigner.GetOtherClientInstance(instanceId);

            if (instance == null)
            {
                MenuManager.Instance.ControllerAssigner.RegisterActionToFireWhenOtherClientInstanceIsRegistered(instanceId, () =>
                {
                    AttachOtherClientInstance(instanceIdOnOtherClient, clientId, teamIndex, numPlayerSetInCaseOfShared);
                    NoLongerWaitingForClientInstance(instanceId);
                });

                waitingForInstanceIds.Add(instanceId);
                return false;
            }

            MenuTeam team = menuTeam[teamIndex];
            LobbyTeamButton3D btn = teamQuadrantButtons[teamIndex];

            // attach
            if (instance.Layout == ControllerLayout.LayoutStyle.Shared && team.NumControllersAttached == 0)
            {
                team.AttachInstance(instance);
                if (numPlayerSetInCaseOfShared)
                {
                    btn.SwitchToReady();
                    team.OnNumberOfPlayersSet(2);
                }
                else
                {
                    btn.SwitchToNumPlayersSelectUI(instance);
                }
                btn.AttachLeftPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                btn.AttachRightPlayer(instance.TeamContent.GetPlayerRigidbody(2));
                btn.AttachRopeJoints(instance.TeamContent.GetLinkRigidbodies());
            }
            else if (instance.Layout == ControllerLayout.LayoutStyle.Separate
                && (team.NumControllersAttached == 0
                    || (team.NumControllersAttached == 1 && team.Layout == ControllerLayout.LayoutStyle.Separate)))
            {
                bool wasLeftAttached = team.LeftPlayerAttached;
                bool wasRightAttached = team.RightPlayerAttached;

                team.AttachInstance(instance, playerSide);

                if (!wasLeftAttached && team.LeftPlayerAttached)
                {
                    btn.AttachLeftPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                }
                if (!wasRightAttached && team.RightPlayerAttached)
                {
                    btn.AttachRightPlayer(instance.TeamContent.GetPlayerRigidbody(1));
                }

                if (team.NumControllersAttached == 1)
                {
                    if (!team.LeftPlayerAttached)
                    {
                        btn.SwitchToNeedsPartner(false);
                    }
                    else if (!team.RightPlayerAttached)
                    {
                        btn.SwitchToNeedsPartner();
                    }
                }
                else
                {
                    btn.SwitchToReady();
                }
            }

            SetTeamsAvailableOnOtherClient(clientId);
            CheckAndMoveToNextState();

            return true;
        }

        private void NoLongerWaitingForClientInstance(string instanceId)
        {
            if (waitingForInstanceIds.Contains(instanceId))
            {
                waitingForInstanceIds.Remove(instanceId);

                if (waitingForInstanceIds.Count <= 0)
                {
                    isStateSyncedWithMasterClient = true;
                    onStateSyncedWithMasterClientActions?.Invoke();
                }
            }
        }
        #endregion

        #region Online callbacks
        private void OnClientEnteredRoom(Player other)
        {
            SetTeamsAvailableOnOtherClient(other.UserId);

            if (PhotonNetwork.IsMasterClient)
            {
                List<int> teams = new List<int>();
                List<int> teamLayoutInts = new List<int>();
                List<int> teamStateInts = new List<int>();
                List<string> teamOnlineAttachedInstanceIds1 = new List<string>();
                List<string> teamOnlineAttachedInstanceIds2 = new List<string>();

                // the master client will send the current state to new joinees
                for (int i = 0; i < menuTeam.Length; i++)
                {
                    MenuTeam team = menuTeam[i];
                    if (team.Layout != ControllerLayout.LayoutStyle.None)
                    {
                        teams.Add(i);
                        teamLayoutInts.Add((int)team.Layout);
                        teamStateInts.Add((int)team.State);

                        string id1 = "";
                        if (team.LeftPlayerAttached)
                        {
                            if (team.IsOnlinePlayer[0])
                            {
                                id1 = team.OnlineAttachedInstanceIds[0];
                            }
                            else if (team.RewiredIDs[0] >= 0)
                            {
                                id1 = MenusControllerAssignmentManager.GetOtherClientInstanceId(PhotonNetwork.LocalPlayer.UserId, team.RewiredIDs[0]);
                            }
                        }
                        string id2 = "";
                        if (team.RightPlayerAttached && team.Layout == ControllerLayout.LayoutStyle.Separate)
                        {
                            if (team.IsOnlinePlayer[1])
                            {
                                id2 = team.OnlineAttachedInstanceIds[1];
                            }
                            else if (team.RewiredIDs[1] >= 0)
                            {
                                id2 = MenusControllerAssignmentManager.GetOtherClientInstanceId(PhotonNetwork.LocalPlayer.UserId, team.RewiredIDs[1]);
                            }
                        }
                        teamOnlineAttachedInstanceIds1.Add(id1);
                        teamOnlineAttachedInstanceIds2.Add(id2);
                    }
                }

                photonView.RPC(nameof(SyncStateWithMasterClient), other, teams.ToArray(), teamLayoutInts.ToArray(), teamStateInts.ToArray(), teamOnlineAttachedInstanceIds1.ToArray(), teamOnlineAttachedInstanceIds2.ToArray());
            }
        }

        private void OnClientLeftRoom(string clientID, Player otherPlayer)
        {
            for (int i = waitingForInstanceIds.Count - 1; i >= 0; i--)
            {
                string instanceId = waitingForInstanceIds[i];
                if (instanceId.Contains(clientID))
                {
                    NoLongerWaitingForClientInstance(instanceId);
                }
            }
        }

        private void JoinedRoom(string roomName)
        {
            isStateSyncedWithMasterClient = PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient;
            if (PhotonNetwork.IsMasterClient)
            {
                onStateSyncedWithMasterClientActions?.Invoke();
            }

            waitingForInstanceIds.Clear();
            CheckAndSetTeamAvailabilitiesForAllRoomClients();
        }

        private void OnDisconnectedFromNetwork(DisconnectCause cause)
        {
            waitingForInstanceIds.Clear();
            isStateSyncedWithMasterClient = true;
            onStateSyncedWithMasterClientActions = null;
        }
        #endregion

        private void OnDestroy()
        {
            MenusControllerAssignmentManager.OnInstancePreDisconnect -= OnInstanceDisconnected;
            MenusControllerAssignmentManager.OnInstanceRespawnedAfterConnectivityChange -= OnInstanceRespawned;
            LobbyTeamButton3D.OnNumberOfPlayersSelected -= LobbyTeamButton3D_OnNumberOfPlayersSelected;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnClientEnteredRoom -= OnClientEnteredRoom;
                NetworkManager.Instance.OnClientLeftRoom -= OnClientLeftRoom;
                NetworkManager.Instance.JoinedRoom -= JoinedRoom;
                NetworkManager.Instance.OnDisconnectedFromNetwork -= OnDisconnectedFromNetwork;
            }
        }
    }
}