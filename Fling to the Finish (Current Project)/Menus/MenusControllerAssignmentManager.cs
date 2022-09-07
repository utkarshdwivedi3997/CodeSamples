using Fling.Saves;
using Menus.Settings;
using Photon.Pun;
using Rewired;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Menus
{
    public partial class MenusControllerAssignmentManager : MonoBehaviourPun
    {
        public bool IsListeningForControllerAssignments { get; private set; } = false;

        [SerializeField] private GameObject teamPrefab;                   // Prefab containing the rope, camera and character rigidbodies
        [SerializeField] private Transform topLeftSpawn, bottomRightSpawn;

        private static Dictionary<int, TeamSetupInstance> spawnedInstances;    // < main (left) rewired ID, instance associated with it >
        public static int NumberOfLocalInstances 
        {
            get
            {
                if (spawnedInstances != null)
                {
                    return spawnedInstances.Count;
                }
                else
                {
                    return 0;
                }
            }
        }

        private static Dictionary<string, TeamSetupInstance> otherClientInstances; // key is "ClientID_LeftRewiredID"

        [SerializeField] private Button3D swapLayoutButton;
        [SerializeField] private Button3D disconnectControllerButton;
        [SerializeField] private MouseJoystickVisualizer mouseJoystickVisualizer;
        [SerializeField] private GameObject playerDisconnectedIconPrefab;
        [SerializeField] private RectTransform playerDisconnectedIconSpawnParent;
        [SerializeField] private MenusJoinControllerPrompt joinPrompt;

        private bool isShowingMouseJoystick = false;

        private int n2DUIsOnScreen = 0;
        
        public void Init()
        {
            if (MenuData.LobbyScreenData.ResetLobby)
            {
                spawnedInstances = new Dictionary<int, TeamSetupInstance>();
            }
            otherClientInstances = new Dictionary<string, TeamSetupInstance>(); // other client instances will always be set by other players so we can reset it everytime

            if (MenuData.HasBeenToMainMenuBefore)
            {
                // we are returning after playing some levels --> instances don't exist. Respawn them
                foreach (TeamSetupInstance ownedInstance in spawnedInstances.Values)
                {
                    RespawnOwnedInstanceAfterMainMenuReturn(ownedInstance);
                }

                UnattachAllInstancesFromTeams();
            }

            joinPrompt.Init();

            MenuManager.OnMenuStateChanged += MenuManager_OnMenuStateChanged;
            MenuManager.OnMenuStateChangeStarted += MenuManager_OnMenuStateChangeStarted;

            NetworkManager.LeftRoom += LeftRoom;
            NetworkManager.Instance.OnClientLeftRoom += OtherClientDisconnectedFromRoom;
            MetaManager.Instance.OnNewLevelLoadStarted += Deactivate;
        }


        public void Activate()
        {
            if (IsListeningForControllerAssignments)
            {
                return;
            }

            RewiredJoystickAssign.Instance.BeginJoystickAssignment(MenuData.LobbyScreenData.ResetLobby);
            RewiredJoystickAssign.Instance.OnJoystickConnected += ControllerRegistered;
            RewiredJoystickAssign.Instance.OnJoystickPreDisconnect += ControllerDisconnected;
            NetworkManager.Instance.JoinedRoom += JoinedRoom;
            TeamSetupInstance.OnShowMouseJoystickUI += UseMouseJoystickUI;
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnMouseAndKeyboardButtonsChanged += CheckIfUsingMouseOnAnyInstance;
            }

            PopupScreensManager.WatchForScreens(On2DUIShown, On2DUIDismissed, 
                PopupScreenType.PopupNotification, PopupScreenType.PrivacyPolicy, 
                PopupScreenType.Settings, PopupScreenType.InviteFriends, PopupScreenType.Customization,
                PopupScreenType.NewUnlockNotification);

            IsListeningForControllerAssignments = true;

            swapLayoutButton.onClickByInstance.AddListener((instance, playerNum) => SwapLayoutForInstance(instance));
            disconnectControllerButton.onClickByInstance.AddListener((instance, playerNum) => DisconnectInstance(instance));

            CheckIfUsingMouseOnAnyInstance();
            SetDisconnectControllerButtonStatus(spawnedInstances.Count > 0);
        }

        public void Deactivate()
        {
            IsListeningForControllerAssignments = false;
            swapLayoutButton.onClickByInstance.RemoveAllListeners();
            disconnectControllerButton.onClickByInstance.RemoveAllListeners();
            NetworkManager.Instance.JoinedRoom -= JoinedRoom;
            RewiredJoystickAssign.Instance.OnJoystickConnected -= ControllerRegistered;
            RewiredJoystickAssign.Instance.OnJoystickPreDisconnect -= ControllerDisconnected;
            TeamSetupInstance.OnShowMouseJoystickUI -= UseMouseJoystickUI;
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnMouseAndKeyboardButtonsChanged -= CheckIfUsingMouseOnAnyInstance;
            }

            PopupScreensManager.Unsubscribe(On2DUIShown, On2DUIDismissed,
                            PopupScreenType.PopupNotification, PopupScreenType.PrivacyPolicy,
                            PopupScreenType.Settings, PopupScreenType.InviteFriends, PopupScreenType.Customization,
                            PopupScreenType.NewUnlockNotification);            

            UseMouseJoystickUI(false, -1, -1);
            SetDisconnectControllerButtonStatus(false);
            MenuData.LobbyScreenData.ResetLobby = false;
        }

        private void ControllerRegistered(int rewiredID, int playerNumber, ControllerType type)
        {
            float x = Random.Range(topLeftSpawn.position.x, bottomRightSpawn.position.x);
            float z = Random.Range(topLeftSpawn.position.z, bottomRightSpawn.position.z);
            Vector3 ranPos = new Vector3(x, topLeftSpawn.position.y, z);

            GameObject teamInstance = null;
            if (PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom)
            {
                teamInstance = Instantiate(teamPrefab, ranPos, Quaternion.identity);
            }
            else
            {
                teamInstance = PhotonNetwork.Instantiate(teamPrefab.name, ranPos, Quaternion.identity);
            }

            RegisterTeamSetupInstance(teamInstance, rewiredID, (type == ControllerType.Keyboard || type == ControllerType.Mouse));

            SetDisconnectControllerButtonStatus(spawnedInstances.Count > 0);
        }

        public static event Action<int> OnLocalOwnedInstanceRegistered;
        private void RegisterTeamSetupInstance(GameObject spawnedInstance, int rewiredID, bool isUsingMouseAndKeyboard)
        {
            if (!spawnedInstances.ContainsKey(rewiredID))
            {
                TeamSetupInstance spawnedInst = new TeamSetupInstance(spawnedInstance, rewiredID, isUsingMouseAndKeyboard);
                spawnedInstances.Add(rewiredID, spawnedInst);
                OnLocalOwnedInstanceRegistered?.Invoke(rewiredID);
                spawnedInst.CheckIfUsingMouse();
            }

            TeamSetupInstance instance = spawnedInstances[rewiredID];

            if (!PhotonNetwork.OfflineMode && PhotonNetwork.InRoom)
            {
                int[] charIdx = new int[] { instance.CharSkinIndices[0].Key, instance.CharSkinIndices[1].Key };
                int[] skinIdx = new int[] { instance.CharSkinIndices[0].Value, instance.CharSkinIndices[1].Value };
                photonView.RPC(nameof(SetInstanceDataOnOtherClients), RpcTarget.OthersBuffered,
                    spawnedInstance.GetPhotonView().ViewID, rewiredID, PhotonNetwork.LocalPlayer.UserId, (int)ControllerLayout.LayoutStyle.Separate, charIdx, skinIdx);
            }
        }

        public static event Action<int, string, bool> OnInstancePreDisconnect;
        public static event Action<int, string, bool> OnInstanceAfterDisconnect;

        private void ControllerDisconnected(int rewiredID, int playerNumber)
        {
            if (spawnedInstances.ContainsKey(rewiredID))
            {
                if (PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom)
                {
                    InstanceDisconnected(rewiredID);
                }
                else
                {
                    photonView.RPC(nameof(InstanceDisconnected), RpcTarget.AllBuffered, rewiredID, PhotonNetwork.LocalPlayer.UserId, true);
                }
            }
        }

        [PunRPC]
        private void InstanceDisconnected(int instanceId, string clientId = "", bool alsoDestroy = true)
        {
            bool isOwnedByThisClient = false;
            if (((PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom) && string.IsNullOrEmpty(clientId)) ||
                (!PhotonNetwork.OfflineMode && PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.UserId.Equals(clientId)))
            {
                isOwnedByThisClient = true;
            }

            OnInstancePreDisconnect?.Invoke(instanceId, clientId, isOwnedByThisClient);

            TeamSetupInstance instance = null;
            string instanceIdOnOtherClient = GetOtherClientInstanceId(clientId, instanceId);

            if (isOwnedByThisClient)
            {
                instance = GetInstance(instanceId);
            }
            else
            {
                instance = GetOtherClientInstance(instanceIdOnOtherClient);
            }

            if (instance == null)
            {
                return;
            }

            if (instance.TeamContent != null)
            {
                Rigidbody[] players = instance.TeamContent.GetPlayerRigidbodies();

                ShowDisconnectIcon(players[0].transform);
                if (instance.Layout == ControllerLayout.LayoutStyle.Shared)
                {
                    ShowDisconnectIcon(players[1].transform);
                }
            }
            
            instance.UnregisterFloatingNames();
            if (alsoDestroy)
            {
                instance.DestroyInstance();
            }

            if (isOwnedByThisClient)
            {
                if (instance.IsUsingMouseAndKeyboard)
                {
                    UseMouseJoystickUI(false, -1, -1);
                }

                if (instance.Layout == ControllerLayout.LayoutStyle.Shared)
                {
                    RewiredJoystickAssign.Instance.RemoveSecondPlayerOfSeparateLayout(instance.RewiredId2);
                }    
                spawnedInstances.Remove(instanceId);

                SetDisconnectControllerButtonStatus(spawnedInstances.Count > 0);
            }
            else
            {
                otherClientInstances.Remove(instanceIdOnOtherClient);
            }

            OnInstanceAfterDisconnect?.Invoke(instanceId, clientId, isOwnedByThisClient);
        }

        private void ShowDisconnectIcon(Transform location)
        {
            GameObject spawnedIcon = Instantiate(playerDisconnectedIconPrefab, playerDisconnectedIconSpawnParent);
            spawnedIcon.transform.position = location.position;

            Destroy(spawnedIcon, 2f);
        }

        [PunRPC]
        public void SetInstanceDataOnOtherClients(int viewID, int instanceIdOnClient, string clientId, int layoutInt, int[] charIdx, int[] skinIdx)
        {
            PhotonView view = PhotonView.Find(viewID);
            if (view == null)
            {
                return;
            }

            GameObject go = view.gameObject;
            if (go == null)
            {
                return;
            }

            ControllerLayout.LayoutStyle layout = (ControllerLayout.LayoutStyle)layoutInt;

            KeyValuePair<int, int>[] charSkinIndices = new KeyValuePair<int, int>[]
            {
                new KeyValuePair<int, int>(charIdx[0], skinIdx[0]),
                new KeyValuePair<int, int>(charIdx[1], skinIdx[1])
            };

            RegisterOtherClientInstance(instanceIdOnClient, go, layout, clientId, charSkinIndices);
        }

        private Dictionary<string, System.Action> onOtherClientRegistered = new Dictionary<string, System.Action>();
        private void RegisterOtherClientInstance(int instanceIdOnClient, GameObject instanceGo, ControllerLayout.LayoutStyle layout, string clientId, KeyValuePair<int, int>[] charSkinIndices)
        {
            string instanceId = GetOtherClientInstanceId(clientId, instanceIdOnClient);

            if (!otherClientInstances.ContainsKey(instanceId))
            {
                otherClientInstances.Add(instanceId, new TeamSetupInstance(instanceGo, layout, instanceId, charSkinIndices));
            }
            else
            {
                otherClientInstances[instanceId] = new TeamSetupInstance(instanceGo, layout, instanceId, charSkinIndices);
            }

            if (onOtherClientRegistered.ContainsKey(instanceId))
            {
                onOtherClientRegistered[instanceId]?.Invoke();
                onOtherClientRegistered.Remove(instanceId);
            }
        }

        public static string GetOtherClientInstanceId(string clientId, int instanceIdOnClient)
        {
            return clientId + "|" + instanceIdOnClient;
        }

        public static string GetOtherClientIdFromInstanceId(string instanceId)
        {
            string clientId = instanceId.Split('|')[0];
            return clientId;
        }

        public static int GetOtherClientInstanceIdOnClientFromInstanceId(string instanceId)
        {
            string instanceIdOnClientStr = instanceId.Split('|')[1];
            int instanceIdOnClient = -1;
            int.TryParse(instanceIdOnClientStr, out instanceIdOnClient);

            return instanceIdOnClient;
        }

        /// <summary>
        /// Swaps the layout of an instance between SHARED <---> SEPARATE.
        /// If there are other clients in this room then other clients also show the change visually.
        /// </summary>
        /// <param name="instanceId"></param>
        public void SwapLayoutForInstance(int instanceId)
        {
            if (spawnedInstances.ContainsKey(instanceId))
            {
                // def swap locally
                spawnedInstances[instanceId].SwapLayout();
            }

            if (!PhotonNetwork.OfflineMode) // online, on other clients
            {
                string id = GetOtherClientInstanceId(PhotonNetwork.LocalPlayer.UserId, instanceId);
                photonView.RPC(nameof(SwapLayoutForOtherClientInstance), RpcTarget.OthersBuffered, id);
            }
        }

        [PunRPC]
        private void SwapLayoutForOtherClientInstance(string instanceId)
        {
            if (otherClientInstances.ContainsKey(instanceId))
            {
                otherClientInstances[instanceId].SwapLayout();
            }
        }

        private void DisconnectInstance(int instanceId)
        {
            if (spawnedInstances.ContainsKey(instanceId))
            {
                // def swap locally
                RewiredJoystickAssign.Instance.UnassignUsedId(instanceId);
            }
        }

        public TeamSetupInstance GetInstance(int instanceId)
        {
            if (spawnedInstances.ContainsKey(instanceId))
            {
                return spawnedInstances[instanceId];
            }
            else
            {
                return null;
            }
        }

        public TeamSetupInstance GetOtherClientInstance(string instanceId)
        {
            if (otherClientInstances.ContainsKey(instanceId))
            {
                return otherClientInstances[instanceId];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a local instance if one is available
        /// </summary>
        /// <returns></returns>
        public TeamSetupInstance GetNextLocalInstance()
        {
            if (spawnedInstances != null && spawnedInstances.Count > 0)
            {
                return spawnedInstances.Values.ElementAt(0);
            }
            else
            {
                return null;
            }
        }

        #region Respawning instances (after returning to the main menu)
        private void RespawnOwnedInstanceAfterMainMenuReturn(TeamSetupInstance instanceToRespawn)
        {
            if (instanceToRespawn.IsOtherClientInstance)
            {
                return;
            }

            float x = Random.Range(topLeftSpawn.position.x, bottomRightSpawn.position.x);
            float z = Random.Range(topLeftSpawn.position.z, bottomRightSpawn.position.z);
            Vector3 ranPos = new Vector3(x, topLeftSpawn.position.y, z);

            GameObject teamInstance = null;
            if (PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom)
            {
                teamInstance = Instantiate(teamPrefab, ranPos, Quaternion.identity);
            }
            else
            {
                teamInstance = PhotonNetwork.Instantiate(teamPrefab.name, ranPos, Quaternion.identity);

                int[] charIdx = new int[] { instanceToRespawn.CharSkinIndices[0].Key, instanceToRespawn.CharSkinIndices[1].Key };
                int[] skinIdx = new int[] { instanceToRespawn.CharSkinIndices[0].Value, instanceToRespawn.CharSkinIndices[1].Value };
                photonView.RPC(nameof(SetInstanceDataOnOtherClients), RpcTarget.OthersBuffered, teamInstance.GetPhotonView().ViewID, instanceToRespawn.RewiredId1, PhotonNetwork.LocalPlayer.UserId, (int)instanceToRespawn.Layout, charIdx, skinIdx);
            }

            ReregisterOwnedInstance(teamInstance, instanceToRespawn);
        }

        private void ReregisterOwnedInstance(GameObject spawnedInstance, TeamSetupInstance instanceToReregister)
        {
            instanceToReregister.ReregisterOwnedInstance(spawnedInstance);
        }
        #endregion

        #region Unattaching Instances From Teams
        public void UnattachAllInstancesFromTeams()
        {
            foreach (TeamSetupInstance instance in spawnedInstances.Values)
            {
                instance.AttachedToTeamIndex = -1;
            }

            foreach (TeamSetupInstance instance in otherClientInstances.Values)
            {
                instance.AttachedToTeamIndex = -1;
            }
        }
        #endregion

        #region Online
        /// <summary>
        /// Called when the client is connected to an online room
        /// </summary>
        private void JoinedRoom(string roomName)
        {
            RespawnLocalInstancesAfterConnectivityChange();
        }

        private void RespawnLocalInstancesAfterConnectivityChange()
        {
            // Convert to online instances
            List<TeamSetupInstance> spawnedInstList = spawnedInstances.Values.ToList();
            foreach (TeamSetupInstance spawnedInstance in spawnedInstList)
            {
                if (spawnedInstance != null)
                {
                    RespawnInstanceAfterConnectivityChange(spawnedInstance);
                }
            }
        }

        public static event Action<TeamSetupInstance> OnInstanceRespawnedAfterConnectivityChange;

        /// <summary>
        /// Converts a locally owned instance to an online instance (spawns it for all clients over the network)
        /// OR converts an online owned instance to an offline one
        /// </summary>
        /// <param name="instance"></param>
        private void RespawnInstanceAfterConnectivityChange(TeamSetupInstance instance)
        {
            GameObject teamInstance = null;
            if (PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom)
            {
                teamInstance = Instantiate(teamPrefab, instance.SpawnedInstance.transform.position, Quaternion.identity);
            }
            else
            {
                teamInstance = PhotonNetwork.Instantiate(teamPrefab.name, instance.SpawnedInstance.transform.position, Quaternion.identity);
                int layoutInt = (int)instance.Layout;

                int[] charIdx = new int[] { instance.CharSkinIndices[0].Key, instance.CharSkinIndices[1].Key };
                int[] skinIdx = new int[] { instance.CharSkinIndices[0].Value, instance.CharSkinIndices[1].Value };

                photonView.RPC(nameof(SetInstanceDataOnOtherClients), RpcTarget.OthersBuffered, teamInstance.GetPhotonView().ViewID, instance.RewiredId1, PhotonNetwork.LocalPlayer.UserId, layoutInt, charIdx, skinIdx);
            }


            instance.InstanceRespawnedAfterConnectivityChange(teamInstance);
            OnInstanceRespawnedAfterConnectivityChange?.Invoke(instance);
        }

        private void ChangeOrSpawnCharacterOnOtherClients(int instanceId, int playerIndex, KeyValuePair<int, int> newCharSkinIndex)
        {
            string instanceIdStr = GetOtherClientInstanceId(PhotonNetwork.LocalPlayer.UserId, instanceId);
            photonView.RPC(nameof(ChangeOrSpawnCharacterOnOtherClientsLogic), RpcTarget.OthersBuffered, instanceIdStr, playerIndex, newCharSkinIndex.Key, newCharSkinIndex.Value);
        }

        [PunRPC]
        private void ChangeOrSpawnCharacterOnOtherClientsLogic(string instanceId, int playerIndex, int charIndex, int skinIndex)
        {
            KeyValuePair<int, int> newCharSkinIndex = new KeyValuePair<int, int>(charIndex, skinIndex);
            if (otherClientInstances.ContainsKey(instanceId))
            {
                ChangeOrSpawnCharacterFromOtherClient(instanceId, playerIndex, newCharSkinIndex);
            }
            else
            {
                RegisterActionToFireWhenOtherClientInstanceIsRegistered(instanceId, () => ChangeOrSpawnCharacterFromOtherClient(instanceId, playerIndex, newCharSkinIndex));
            }
        }

        private void ChangeOrSpawnCharacterFromOtherClient(string instanceId, int playerIndex, KeyValuePair<int, int> newCharSkinIndex)
        {
            TeamSetupInstance instance = otherClientInstances[instanceId];
            instance.ChangeOrSpawnCharacterLogic(playerIndex, newCharSkinIndex);
        }

        public void RegisterActionToFireWhenOtherClientInstanceIsRegistered(string instanceId, Action callback)
        {
            if (onOtherClientRegistered.ContainsKey(instanceId))
            {
                onOtherClientRegistered[instanceId] += callback;
            }
            else
            {
                onOtherClientRegistered.Add(instanceId, callback);
            }
        }

        /// <summary>
        /// To be called when a client disconnects from the room.
        /// </summary>
        private void OtherClientDisconnectedFromRoom(string clientID, Photon.Realtime.Player otherPlayer)
        {
            List<string> otherClientInstanceIds = otherClientInstances.Keys.ToList();
            int len = otherClientInstanceIds.Count;

            List<string> toRemove = new List<string>();
            for (int i = 0; i < len; i++)
            {
                string instanceId = otherClientInstanceIds[i];
                if (instanceId.Contains(clientID))
                {
                    toRemove.Add(instanceId);
                }
            }

            len = toRemove.Count;
            for (int i = 0; i < len; i++)
            {
                string removeInstance = toRemove[i];
                InstanceDisconnected(GetOtherClientInstanceIdOnClientFromInstanceId(removeInstance), GetOtherClientIdFromInstanceId(removeInstance), false);

                if (onOtherClientRegistered.ContainsKey(removeInstance))
                {
                    onOtherClientRegistered.Remove(removeInstance);
                }
            }

            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.DestroyPlayerObjects(otherPlayer);
            }
        }

        /// <summary>
        /// To be called when this client disconnects from a room.
        /// </summary>
        private void LeftRoom()
        {
            List<string> otherClientInstanceIds = otherClientInstances.Keys.ToList();
            int len = otherClientInstanceIds.Count;

            for (int i = 0; i < len; i++)
            {
                string removeInstance = otherClientInstanceIds[i];
                InstanceDisconnected(GetOtherClientInstanceIdOnClientFromInstanceId(removeInstance), GetOtherClientIdFromInstanceId(removeInstance));
            }

            onOtherClientRegistered.Clear();
            RespawnLocalInstancesAfterConnectivityChange();
        }
        #endregion

        #region Miscellaneous
        private void CheckIfUsingMouseOnAnyInstance()
        {
            foreach (TeamSetupInstance instance in spawnedInstances.Values)
            {
                if (instance.CheckIfUsingMouse())
                {
                    break;
                }
            }
        }

        private void UseMouseJoystickUI(bool isActive, int instanceId, int player)
        {
            if (isActive && (instanceId < 0 || player <= 0)) return;

            if (isActive)
            {
                mouseJoystickVisualizer.ActivateForMenuInstance(instanceId, player);
                MetaManager.Instance.CursorHandler.SetToHideCursor();
            }
            else
            {
                mouseJoystickVisualizer.Deactivate();
                MetaManager.Instance.CursorHandler.SetToShowCursorWhenPossible();
            }

            isShowingMouseJoystick = isActive;
        }



        /// <summary>
        /// Hides the cursor if a player is using mouse joystick for controlling and there is no 2D UI on screen that can be interacted with using the mouse
        /// </summary>
        private void CheckAndHideCursor()
        {
            if (isShowingMouseJoystick && n2DUIsOnScreen <= 0)
            {
                MetaManager.Instance.CursorHandler.SetToHideCursor();
            }
        }

        public void SetMouseInput(Vector2 stickInput)
        {
            mouseJoystickVisualizer.SetMouseInput(stickInput);
        }

        private void On2DUIShown()
        {
            n2DUIsOnScreen++;
            MetaManager.Instance.CursorHandler.SetToShowCursorWhenPossible();

            if (n2DUIsOnScreen == 1)
            {
                // first UI on screen, stop player input
                PauseAllInput();
            }
        }

        private void On2DUIDismissed()
        {
            n2DUIsOnScreen = 0;
            CheckAndHideCursor();
            ResumeAllInput();
        }
        #endregion

        private void MenuManager_OnMenuStateChanged(MenuState currentMenuState)
        {
            if (currentMenuState == MenuState.LOBBY_SCREEN_3D)
            {
                SetSwapLayoutButtonStatus(true);
            }
            else
            {
                SetSwapLayoutButtonStatus(false);
            }

            if (!MenuManager.Is3DMenuState(currentMenuState))
            {
                PauseAllInput();
            }
            else if (n2DUIsOnScreen <= 0)
            {
                ResumeAllInput();
            }
        }

        private void MenuManager_OnMenuStateChangeStarted(MenuState from, MenuState to)
        {
            if (from == MenuState.LOBBY_SCREEN_3D)
            {
                if (to != MenuState.LOBBY_SCREEN_3D)
                {
                    SetSwapLayoutButtonStatus(false);
                }
                if (to < MenuState.LOBBY_SCREEN_3D)
                {
                    SwapAllLocalSharedInstancesToSeparate();
                }
            }
            else if (from == MenuState.TUTORIAL_3D)
            {
                if (to != MenuState.LOBBY_SCREEN_3D)
                {
                    SwapAllLocalSharedInstancesToSeparate();
                }
            }
            else if (from == MenuState.PEDESTAL_3D)
            {
                if (to < MenuState.LOBBY_SCREEN_3D)
                {
                    SwapAllLocalSharedInstancesToSeparate();
                }
            }
        }

        private void SetSwapLayoutButtonStatus(bool enable)
        {
            if (swapLayoutButton.isEnabled != enable)
            {
                swapLayoutButton.SetButtonEnabled(enable);
            }
        }

        private void SwapAllLocalSharedInstancesToSeparate()
        {
            // swap all full teams to single characters if going back from the lobby screen
            foreach (TeamSetupInstance instance in spawnedInstances.Values)
            {
                if (instance.Layout == ControllerLayout.LayoutStyle.Shared)
                {
                    SwapLayoutForInstance(instance.RewiredId1);
                }
            }
        }

        private void SetDisconnectControllerButtonStatus(bool enable)
        {
            if (MenuManager.Instance.CurrentMenuState == MenuState.TUTORIAL_3D)
            {
                enable = false; // don't show disconnect controller button in the tutorial
            }

            if (disconnectControllerButton.isEnabled != enable)
            {
                disconnectControllerButton.SetButtonEnabled(enable);
            }
        }

        private void PauseAllInput()
        {
            foreach (TeamSetupInstance instance in spawnedInstances.Values)
            {
                instance.PauseInputs();
            }
        }

        private void ResumeAllInput()
        {
            foreach (TeamSetupInstance instance in spawnedInstances.Values)
            {
                instance.ResumeInputs();
            }
        }

        private void OnDestroy()
        {
            RewiredJoystickAssign.Instance.OnJoystickConnected -= ControllerRegistered;
            RewiredJoystickAssign.Instance.OnJoystickPreDisconnect -= ControllerDisconnected;

            swapLayoutButton.onClickByInstance.RemoveAllListeners();
            disconnectControllerButton.onClickByInstance.RemoveAllListeners();
            joinPrompt.OnDestroyed();
            NetworkManager.Instance.JoinedRoom -= JoinedRoom;
            NetworkManager.LeftRoom -= LeftRoom;
            NetworkManager.Instance.OnClientLeftRoom -= OtherClientDisconnectedFromRoom;
            TeamSetupInstance.OnShowMouseJoystickUI -= UseMouseJoystickUI;
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnMouseAndKeyboardButtonsChanged += CheckIfUsingMouseOnAnyInstance;
            }

            PopupScreensManager.Unsubscribe(On2DUIShown, On2DUIDismissed,
                            PopupScreenType.PopupNotification, PopupScreenType.PrivacyPolicy,
                            PopupScreenType.Settings, PopupScreenType.InviteFriends, PopupScreenType.Customization,
                            PopupScreenType.NewUnlockNotification);

            MenuManager.OnMenuStateChanged -= MenuManager_OnMenuStateChanged;
            MenuManager.OnMenuStateChangeStarted -= MenuManager_OnMenuStateChangeStarted;
            MetaManager.Instance.OnNewLevelLoadStarted -= Deactivate;

            if (RewiredJoystickAssign.Instance != null)
            {
                RewiredJoystickAssign.Instance.EndJoystickAssignment(true, true);
            }
        }
    }
}
