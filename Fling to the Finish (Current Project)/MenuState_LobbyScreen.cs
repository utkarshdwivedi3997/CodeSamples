using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

namespace Menus
{
    public class MenuState_LobbyScreen : MonoBehaviourPun, IMenuState
    {
        public MenuState State
        {
            get { return MenuState.LOBBY_SCREEN; }
        }

        public MenuState NextState
        {
            get { return MenuState.CHARACTER_SELECT; }
        }
        public MenuState PreviousState
        {
            get { return MenuState.MAIN; }
        }

        #region Vars
        [Header("Menu State Variables")]
        [SerializeField]
        private Transform cameraLocation;
        public Transform CameraLocation
        {
            get { return cameraLocation; }
        }

        #region Quadrant descriptors
        [SerializeField, Tooltip("Threshold angle to make regions in the quadrants that don't count as diagonal input")]
        private float diagonalThreshold = 15f;                          // Threshold angle to make regions in the quadrants that don't count as diagonal input
        private const float QUAD_1_STARTING_ANGLE = 0;                  // Quadrant 1 starts at this angle
        private const float QUAD_2_STARTING_ANGLE = 90;                 // Quadrant 2 starts at this angle
        private const float QUAD_3_STARTING_ANGLE = 180;                // Quadrant 3 starts at this angle
        private const float QUAD_4_STARTING_ANGLE = -90;                // Quadrant 4 starts at this angle
        #endregion

        [Header("-----------------------------------------------------")]
        [Header("UI")]
        [SerializeField]
        private Color availableControllerColor;                                     // Color for controller images in the middle
        [SerializeField]
        private Color[] colors;                                                     // Colors for all teams
        [SerializeField]
        private ControllerImage[] controllerImages = new ControllerImage[4];        // Array of 4. Holds icons for controllers for each player
        private Vector3[] controllerOriginalPositions = new Vector3[4];             // Original (middle/no team) position for all 4 controller images
        [SerializeField]
        private LobbyScreenQuadrant[] teamQuadrants = new LobbyScreenQuadrant[4];   // Array of 4. Holds each of the 4 quadrants.
        [SerializeField]
        private RectTransform centerPosition;
        [SerializeField]
        private Text text, angle;

        private ControllerLayouts[] layouts;                                        // Array of ControllerLayouts() class. Holds the layout of each of the 4 quadrants
        private int[] nPlayersInQuadrant;                                           // Array of 4. Holds the number of controllers/online players in each quadrant
        private int[][] controllerImageIndices;                                     // Indices of controller images for each quadrant
        private bool subscribedToControllerEvents = false;

        private Coroutine[] controllerImageMoveCoroutines = new Coroutine[4];       // Array to keep track of all coroutines running to move controller UI images

        #region Online UI Vars
        [SerializeField]
        private OnlinePlayerIcon[] onlinePlayerIcons;                               // Array of online player icons
        private Vector3[] onlinePlayerImageOriginalPositions;                       // Original (middle/no team) position for all online player icons
        private Coroutine[] onlinePlayerIconMoveCoroutines;                         // Array to keep track of all coroutines running to move online player icon UI images

        private int onlinePlayersCount;
        private Dictionary<string, List<int>> onlinePlayerNumberLocalIndex;          // Dictionary of ( client ID , local client player numbers of players in those clients )
        private bool hasResetValues = false;                                        // Has reset the values required to be reset after joining the room?
        #endregion

        #endregion

        #region State Start
        public void InitState()
        {
            text.text = "";

            // Add whatever needs to be initialized during loading
            for (int i = 0; i < 4; i++)
            {
                controllerOriginalPositions[i] = controllerImages[i].GetComponent<RectTransform>().position;
            }

            onlinePlayerImageOriginalPositions = new Vector3[onlinePlayerIcons.Length];
            for (int i = 0; i < onlinePlayerIcons.Length; i++)
            {
                onlinePlayerImageOriginalPositions[i] = onlinePlayerIcons[i].GetComponent<RectTransform>().position;
            }

            for (int i = 0; i < controllerImages.Length; i++)
            {
                controllerImages[i].FullController.canvasRenderer.SetAlpha(1);
                controllerImages[i].LeftHalf.canvasRenderer.SetAlpha(0);
                controllerImages[i].RightHalf.canvasRenderer.SetAlpha(0);

                // Don't display images for controllers that are't connected yet
                if (i >= RewiredJoystickAssign.Instance.ConnectedControllers)
                {
                    controllerImages[i].gameObject.SetActive(false);
                }

                controllerImages[i].Init();
            }

            // Handle subscriptions to events
            if (!RewiredJoystickAssign.Instance.HasAssignedControllers)
            {
                RewiredJoystickAssign.Instance.OnJoystickConnected += ControllerConnected;
                RewiredJoystickAssign.Instance.OnJoystickPreDisconnect += ControllerDisconnected;
                subscribedToControllerEvents = true;
            }
        }

        /// <summary>
        /// Sets this state to be the current active state
        /// </summary>
        public void ActivateState()
        {
            if (!PhotonNetwork.OfflineMode)
            {
                hasResetValues = false;
            }

            layouts = new ControllerLayouts[4];
            nPlayersInQuadrant = new int[4];

            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i] = new ControllerLayouts();
                nPlayersInQuadrant[i] = 0;
                layouts[i].QuadrantIndex = i;
            }

            RewiredJoystickAssign.Instance.BeginJoystickAssignment(true);

            // Reset color and positions of controller icons
            for (int i = 0; i < controllerImages.Length; i++)
            {
                controllerImages[i].FullController.canvasRenderer.SetColor(availableControllerColor);
                controllerImages[i].LeftHalf.canvasRenderer.SetColor(availableControllerColor);
                controllerImages[i].RightHalf.canvasRenderer.SetColor(availableControllerColor);
                controllerImages[i].FullController.canvasRenderer.SetAlpha(1);
                controllerImages[i].LeftHalf.canvasRenderer.SetAlpha(0);
                controllerImages[i].RightHalf.canvasRenderer.SetAlpha(0);
                controllerImages[i].LeftArrow.canvasRenderer.SetAlpha(1f);
                controllerImages[i].RightArrow.canvasRenderer.SetAlpha(1f);

                controllerImages[i].Unready();

                if (i >= RewiredJoystickAssign.Instance.ConnectedControllers)
                {
                    controllerImages[i].gameObject.SetActive(false);
                }
                else
                {
                    controllerImages[i].gameObject.SetActive(true);
                }

                controllerImages[i].SetQuadrant(-1);
            }

            // Initially, no controllers have been assigned
            for (int i = 0; i < 4; i++)
            {
                controllerImages[i].transform.position = controllerOriginalPositions[i];
            }

            // Reset positions of online player icons
            for (int i = 0; i < onlinePlayerIcons.Length; i++)
            {
                onlinePlayerIcons[i].transform.position = onlinePlayerImageOriginalPositions[i];
                onlinePlayerIcons[i].Icon.canvasRenderer.SetAlpha(0);
            }

            // Reset controller image indices
            controllerImageIndices = new int[4][];
            for (int i = 0; i < 4; i++)
            {
                controllerImageIndices[i] = new int[2];
                controllerImageIndices[i][0] = -1;
                controllerImageIndices[i][1] = -1;
            }

            // Reset controller image movement coroutines
            controllerImageMoveCoroutines = new Coroutine[4];
            onlinePlayerIconMoveCoroutines = new Coroutine[onlinePlayerIcons.Length];

            // Reset online variables
            onlinePlayersCount = 0;
            onlinePlayerNumberLocalIndex = new Dictionary<string, List<int>>();

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

            if (!PhotonNetwork.OfflineMode)
            {
                hasResetValues = true;
            }
        }
        #endregion

        #region Input
        public bool Select(float horizontalInput, float verticalInput, int rewiredPlayerID, int playerNumber)
        {
            // Debug.Log("Select with player ID " + rewiredPlayerID);
            #region Controller movement
            Vector2 dir = new Vector2(horizontalInput, verticalInput);
            float angle = Vector2.SignedAngle(dir, Vector2.left);
            string direction = "";

            //Debug.Log(playerNumber);
            ControllerImage thisCtrlImage = controllerImages[playerNumber];
            int oldQuadrant = thisCtrlImage.QuadrantIndex;
            int quadrant = oldQuadrant;

            // Deadzone. While in deadzone, move the controllers inside the center circle. Don't do any actual layout selection.
            if (Mathf.Abs(verticalInput) <= 0.8f && Mathf.Abs(horizontalInput) <= 0.8f)
            {
                if (!thisCtrlImage.IsInQuadrant)
                {
                    //Debug.Log("first block");
                    thisCtrlImage.GetComponent<RectTransform>().anchoredPosition = dir * 20f;
                }
                return false;
            }
            else
            {
                #region CONTROLLER_IS_IN_CENTER
                if (!thisCtrlImage.IsInQuadrant)
                {
                    // Top left
                    if (angle >= (QUAD_1_STARTING_ANGLE + diagonalThreshold) && angle < (QUAD_2_STARTING_ANGLE - diagonalThreshold))
                    {
                        //direction = "TOP\nLEFT";
                        quadrant = 0;
                    }
                    // Top right
                    else if (angle >= (QUAD_2_STARTING_ANGLE + diagonalThreshold) && angle < (QUAD_3_STARTING_ANGLE - diagonalThreshold))
                    {
                        //direction = "TOP\nRIGHT";
                        quadrant = 1;
                    }
                    // Bottom left
                    else if (angle >= (-QUAD_3_STARTING_ANGLE + diagonalThreshold) && angle < (QUAD_4_STARTING_ANGLE - diagonalThreshold))
                    {
                        //direction = "BOTTOM\nRIGHT";
                        quadrant = 3;
                    }
                    // Bottom right
                    else if (angle >= (QUAD_4_STARTING_ANGLE + diagonalThreshold) && angle < (QUAD_1_STARTING_ANGLE - diagonalThreshold))
                    {
                        //direction = "BOTTOM\nLEFT";
                        quadrant = 2;
                    }
                    //Debug.Log("second block");
                }
                #endregion
                #region CONTROLLER_IS_IN_QUADRANT
                else if (thisCtrlImage.IsInQuadrant && !thisCtrlImage.HasLockedOnQuadrant)
                {
                    if (oldQuadrant == 2 || oldQuadrant == 3)
                    {
                        // Up
                        if (angle >= (QUAD_2_STARTING_ANGLE - diagonalThreshold) && angle < (QUAD_2_STARTING_ANGLE + diagonalThreshold))
                        {
                            quadrant = oldQuadrant == 2 ? 0 : 1;
                        }
                        // Left
                        else if (oldQuadrant == 3 && Mathf.Abs(angle) <= (QUAD_1_STARTING_ANGLE + diagonalThreshold))// && angle < (QUAD_1_STARTING_ANGLE + diagonalThreshold))
                        {
                            quadrant = 2;
                        }
                        // Right
                        else if (oldQuadrant == 2 && Mathf.Abs(angle) >= (QUAD_3_STARTING_ANGLE - diagonalThreshold))// && angle < (QUAD_1_STARTING_ANGLE + diagonalThreshold))
                        {
                            quadrant = 3;
                        }
                        // Back from bottom left
                        else if (oldQuadrant == 2 && angle >= (QUAD_2_STARTING_ANGLE + diagonalThreshold) && angle < (QUAD_3_STARTING_ANGLE - diagonalThreshold))
                        {
                            quadrant = -1;
                        }
                        // Back from bottom right
                        if (oldQuadrant == 3 && angle >= (QUAD_1_STARTING_ANGLE + diagonalThreshold) && angle < (QUAD_2_STARTING_ANGLE - diagonalThreshold))
                        {
                            quadrant = -1;
                        }
                    }
                    else if (oldQuadrant == 0 || oldQuadrant == 1)
                    {
                        // Down
                        if (angle >= (QUAD_4_STARTING_ANGLE - diagonalThreshold) && angle < (QUAD_4_STARTING_ANGLE + diagonalThreshold))
                        {
                            quadrant = oldQuadrant == 1 ? 3 : 2;
                        }
                        // Left
                        else if (oldQuadrant == 1 && Mathf.Abs(angle) <= (QUAD_1_STARTING_ANGLE + diagonalThreshold))// && angle < (QUAD_1_STARTING_ANGLE + diagonalThreshold))
                        {
                            quadrant = 0;
                        }
                        // Right
                        else if (oldQuadrant == 0 && Mathf.Abs(angle) >= (QUAD_3_STARTING_ANGLE - diagonalThreshold))// && angle < (QUAD_1_STARTING_ANGLE + diagonalThreshold))
                        {
                            quadrant = 1;
                        }
                        // Back from top left
                        else if (oldQuadrant == 0 && angle >= (-QUAD_3_STARTING_ANGLE + diagonalThreshold) && angle < (QUAD_4_STARTING_ANGLE - diagonalThreshold))
                        {
                            quadrant = -1;
                        }
                        // Back from top right
                        else if (oldQuadrant == 1 && angle >= (QUAD_4_STARTING_ANGLE + diagonalThreshold) && angle < (QUAD_1_STARTING_ANGLE - diagonalThreshold))
                        {
                            quadrant = -1;
                        }
                    }
                    //Debug.Log("third block");
                }
                #endregion
                #region CONTROLLER_LOCKED
                #endregion

                if (quadrant != oldQuadrant)
                {
                    if (quadrant == -1)
                    {
                        thisCtrlImage.SetQuadrant(quadrant);
                        nPlayersInQuadrant[oldQuadrant]--;
                        if (nPlayersInQuadrant[oldQuadrant] < 0)
                        {
                            nPlayersInQuadrant[oldQuadrant] = 0;
                        }

                        // This needs to come before the next if-else segment
                        StartCoroutine(HandleVisuals(playerNumber, oldQuadrant, quadrant, 0.1f));

                        if (controllerImageIndices[oldQuadrant][0] == playerNumber)
                        {
                            controllerImageIndices[oldQuadrant][0] = -1;
                        }
                        else if (controllerImageIndices[oldQuadrant][1] == playerNumber)
                        {
                            controllerImageIndices[oldQuadrant][1] = -1;
                        }

                        //thisCtrlImage.transform.position = controllerOriginalPositions[playerNumber];
                        //LerpControllerImage(playerNumber, thisCtrlImage.gameObject, controllerOriginalPositions[playerNumber], 0.1f);
                        Debug.Log("<color=green>=============== BOI ===============</color>");
                        Debug.Log(nPlayersInQuadrant[0] + "          ,          " + nPlayersInQuadrant[1]);
                        Debug.Log(nPlayersInQuadrant[2] + "          ,          " + nPlayersInQuadrant[3]);
                        return true;
                    }
                    else if (nPlayersInQuadrant[quadrant] < 2)
                    {
                        thisCtrlImage.SetQuadrant(quadrant);
                        //thisCtrlImage.transform.position = teamQuadrants[quadrant].TeamCharacterPositions[nPlayersInQuadrant[quadrant]].position;
                        //LerpControllerImage(playerNumber, thisCtrlImage.gameObject, teamQuadrants[quadrant].TeamCharacterPositions[nPlayersInQuadrant[quadrant]].position, 0.1f);
                        nPlayersInQuadrant[quadrant]++;

                        if (controllerImageIndices[quadrant][0] == -1)
                        {
                            controllerImageIndices[quadrant][0] = playerNumber;
                        }
                        else if (controllerImageIndices[quadrant][1] == -1)
                        {
                            controllerImageIndices[quadrant][1] = playerNumber;
                        }

                        StartCoroutine(HandleVisuals(playerNumber, oldQuadrant, quadrant, 0.1f));

                        if (oldQuadrant != -1)
                        {
                            nPlayersInQuadrant[oldQuadrant]--;
                            if (nPlayersInQuadrant[oldQuadrant] < 0)
                            {
                                nPlayersInQuadrant[oldQuadrant] = 0;
                            }

                            if (controllerImageIndices[oldQuadrant][0] == playerNumber)
                            {
                                controllerImageIndices[oldQuadrant][0] = -1;
                            }
                            else if (controllerImageIndices[oldQuadrant][1] == playerNumber)
                            {
                                controllerImageIndices[oldQuadrant][1] = -1;
                            }
                        }
                        Debug.Log("<color=green>=============== BOI ===============</color>");
                        Debug.Log(nPlayersInQuadrant[0] + "          ,          " + nPlayersInQuadrant[1]);
                        Debug.Log(nPlayersInQuadrant[2] + "          ,          " + nPlayersInQuadrant[3]);
                        return true;
                    }
                }
            }

            //this.angle.text = angle.ToString();
            return false;
            #endregion
        }

        public bool Submit(int rewiredPlayerID = 0, int playerNumber = 0)
        {
            /*for (int i = 0; i < 4; i++)
            {
                layouts[i].EndJoystickAssignment();
            }*/

            SavePlayerPrefs();
            RewiredJoystickAssign.Instance.EndJoystickAssignment(true, true);
            MenuManager.Instance.MoveToNextMenu();

            RewiredJoystickAssign.Instance.OnJoystickConnected -= ControllerConnected;
            RewiredJoystickAssign.Instance.OnJoystickPreDisconnect -= ControllerDisconnected;
            subscribedToControllerEvents = false;

            return true;
        }

        public bool Cancel(int rewiredPlayerID, int playerNumber)
        {
            //Debug.Log("Cancel with id " + rewiredPlayerID);
            ControllerImage thisCtrlImage = controllerImages[playerNumber];
            int quadrant = thisCtrlImage.QuadrantIndex;

            if (quadrant != -1 && thisCtrlImage.HasLockedOnQuadrant && layouts[quadrant].Layout != ControllerLayouts.LayoutStyle.None)
            {
                thisCtrlImage.Unready();

                if (!PhotonNetwork.OfflineMode)
                {
                    photonView.RPC("RemoveControllerFromQuadrant", RpcTarget.AllBufferedViaServer, quadrant, rewiredPlayerID, PhotonNetwork.LocalPlayer.UserId);
                }
                else
                {
                    layouts[quadrant].RemoveController(rewiredPlayerID, PhotonNetwork.LocalPlayer.UserId);
                }
                Debug.Log("<color=green>Controller for player with rewired ID " + rewiredPlayerID + " removed from quadrant " + quadrant + ", which now has layout type " + layouts[quadrant].Layout + ".</color>");

                //SetControllerLayout(quadrant);
            }

            return false;
        }

        public bool Pick(int rewiredPlayerID, int playerNumber)
        {
            //Debug.Log("Pick with id " + rewiredPlayerID);

            ControllerImage thisCtrlImage = controllerImages[playerNumber];
            int quadrant = thisCtrlImage.QuadrantIndex;

            if (quadrant != -1 && !thisCtrlImage.HasLockedOnQuadrant && thisCtrlImage.IsInQuadrant && layouts[quadrant].Layout != ControllerLayouts.LayoutStyle.Separate)
            {
                thisCtrlImage.Ready();

                if (!PhotonNetwork.OfflineMode)
                {
                    photonView.RPC("AddControllerToQuadrant", RpcTarget.AllBufferedViaServer, quadrant, rewiredPlayerID, PhotonNetwork.LocalPlayer.UserId);
                }
                else
                {
                    layouts[quadrant].AddController(rewiredPlayerID, PhotonNetwork.LocalPlayer.UserId);
                }
                Debug.Log("<color=green>Controller added to player with rewired ID " + rewiredPlayerID + " for quadrant " + quadrant + ", which now has layout type " + layouts[quadrant].Layout + ".</color>");

                //SetControllerLayout(quadrant);
            }

            return false;
        }

        public void Connect()
        {
            PhotonNetwork.OfflineMode = false;
            NetworkManager.Instance.Connect();

            // Subscribe to JoinedRoom so that we can do additional stuff as soon as we join a room
            NetworkManager.Instance.JoinedRoom += JoinedRoom;
        }
        #endregion

        #region Visuals
        private void LerpControllerImage(int playerNum, GameObject toMove, Vector3 finalPos, float t)
        {
            if (controllerImageMoveCoroutines[playerNum] != null)
            {
                StopCoroutine(controllerImageMoveCoroutines[playerNum]);
            }
            controllerImageMoveCoroutines[playerNum] = StartCoroutine(LerpUIGameObjectCoroutine(toMove, finalPos, t));

            //Simple Move to without lerping for debugging:
            //toMove.transform.position = new Vector3(finalPos.x, toMove.transform.position.y, finalPos.z);
        }

        private void LerpOnlinePlayerIcon(int playerNum, GameObject toMove, Vector3 finalPos, float t)
        {
            if (onlinePlayerIconMoveCoroutines[playerNum] != null)
            {
                StopCoroutine(onlinePlayerIconMoveCoroutines[playerNum]);
            }
            onlinePlayerIconMoveCoroutines[playerNum] = StartCoroutine(LerpUIGameObjectCoroutine(toMove, finalPos, t));
        }

        IEnumerator LerpUIGameObjectCoroutine(GameObject toMove, Vector3 finalPos, float t)
        {
            Vector3 startPos = toMove.transform.position;
            float timeSinceStarted = 0f;
            float percentComplete = 0f;
            while (percentComplete < 1f)
            {
                timeSinceStarted += Time.deltaTime;
                percentComplete = timeSinceStarted / t;
                Vector3 pos = Vector3.Lerp(startPos, finalPos, percentComplete);
                toMove.transform.position = pos;
                //Debug.Log(pos);

                yield return null;
            }

            yield return 0;
        }

        IEnumerator HandleVisuals(int playerNum, int oldQuadrant, int newQuadrant, float t)
        {
            ControllerImage image = controllerImages[playerNum];
            if (newQuadrant != -1)
            {
                if (nPlayersInQuadrant[newQuadrant] == 1)
                {
                    // Display halves/full controllers
                    image.FullController.CrossFadeAlpha(0, t, true);
                    image.LeftHalf.CrossFadeColor(colors[newQuadrant * 2], t, true, true);
                    image.RightHalf.CrossFadeColor(colors[(newQuadrant * 2) + 1], t, true, true);

                    // Handle motion
                    Vector3 centerPos = (teamQuadrants[newQuadrant].TeamCharacterPositions[0].position + teamQuadrants[newQuadrant].TeamCharacterPositions[1].position) / 2;
                    if (!PhotonNetwork.OfflineMode)
                    {
                        LerpControllerImage(playerNum, image.gameObject, centerPos, 0.1f);
                        photonView.RPC("SyncObjectAcrossClients", RpcTarget.Others, PhotonNetwork.LocalPlayer.UserId, playerNum, centerPos, 0.1f, oldQuadrant, newQuadrant, 0, -1);
                    }
                    else
                    {
                        LerpControllerImage(playerNum, image.gameObject, centerPos, 0.1f);
                    }

                    teamQuadrants[newQuadrant].IsOnlinePlayer[0] = false;
                }
                else if (nPlayersInQuadrant[newQuadrant] == 2)
                {
                    // Display halves/full controllers
                    image.FullController.CrossFadeColor(colors[(newQuadrant * 2) + 1], t, true, true);
                    image.LeftHalf.CrossFadeAlpha(0, t, true);
                    image.RightHalf.CrossFadeAlpha(0, t, true);

                    // Handle motion
                    if (!PhotonNetwork.OfflineMode)
                    {
                        LerpControllerImage(playerNum, image.gameObject, teamQuadrants[newQuadrant].TeamCharacterPositions[1].position, 0.1f);
                        photonView.RPC("SyncObjectAcrossClients", RpcTarget.Others, PhotonNetwork.LocalPlayer.UserId, playerNum, teamQuadrants[newQuadrant].TeamCharacterPositions[1].position, 0.1f, oldQuadrant, newQuadrant, 0, -1);
                    }
                    else
                    {
                        LerpControllerImage(playerNum, image.gameObject, teamQuadrants[newQuadrant].TeamCharacterPositions[1].position, 0.1f);
                    }

                    if (!IsTeammateOnline(newQuadrant))
                    {
                        int teammateControllerIndex = GetTeammateControllerImageIndex(newQuadrant, playerNum);
                        ControllerImage teammate = controllerImages[teammateControllerIndex];
                        teammate.FullController.CrossFadeColor(colors[newQuadrant * 2], t, true, true);
                        teammate.LeftHalf.CrossFadeAlpha(0, t, true);
                        teammate.RightHalf.CrossFadeAlpha(0, t, true);

                        if (!PhotonNetwork.OfflineMode)
                        {
                            LerpControllerImage(teammateControllerIndex, teammate.gameObject, teamQuadrants[newQuadrant].TeamCharacterPositions[0].position, 0.25f);
                            photonView.RPC("SyncObjectAcrossClients", RpcTarget.Others, PhotonNetwork.LocalPlayer.UserId, teammateControllerIndex, teamQuadrants[newQuadrant].TeamCharacterPositions[0].position, 0.25f, oldQuadrant, newQuadrant, 0, -1);
                        }
                        else
                        {
                            LerpControllerImage(teammateControllerIndex, teammate.gameObject, teamQuadrants[newQuadrant].TeamCharacterPositions[0].position, 0.25f);
                        }
                    }
                    else if (!PhotonNetwork.OfflineMode && IsTeammateOnline(newQuadrant))
                    {
                        Debug.Log("teammate is online");

                        // Move teammate
                        int pNumOnOwner = teamQuadrants[newQuadrant].OnlinePlayerNumberOnOwner[0];
                        string clientID = teamQuadrants[newQuadrant].OnlinePlayerClientIDs[0];

                        int index = onlinePlayerNumberLocalIndex[clientID][pNumOnOwner];
                        GameObject toMove = onlinePlayerIcons[index].gameObject;
                        LerpOnlinePlayerIcon(index, toMove, teamQuadrants[newQuadrant].TeamCharacterPositions[0].position, 0.25f);
                        photonView.RPC("SyncObjectAcrossClients", RpcTarget.Others, clientID, pNumOnOwner, teamQuadrants[newQuadrant].TeamCharacterPositions[0].position, 0.25f, newQuadrant, newQuadrant, 1, 0);     // teammate didn't change quadrants
                    }

                    teamQuadrants[newQuadrant].IsOnlinePlayer[1] = false;
                }

                // Handle arrows
                image.LeftArrow.CrossFadeAlpha(0, t, true);
            }
            else
            {
                image.FullController.CrossFadeColor(availableControllerColor, t, true, true);
                image.LeftHalf.CrossFadeAlpha(0, t, true);
                image.RightHalf.CrossFadeAlpha(0, t, true);

                // Handle motion
                if (!PhotonNetwork.OfflineMode)
                {
                    LerpControllerImage(playerNum, image.gameObject, controllerOriginalPositions[playerNum], 0.1f);
                    photonView.RPC("SyncObjectAcrossClients", RpcTarget.Others, PhotonNetwork.LocalPlayer.UserId, playerNum, controllerOriginalPositions[playerNum], 0.1f, oldQuadrant, newQuadrant, 0, -1);
                }
                else
                {
                    LerpControllerImage(playerNum, image.gameObject, controllerOriginalPositions[playerNum], 0.1f);
                }

                // Handle arrows
                image.LeftArrow.CrossFadeAlpha(1, t, true);
                image.RightArrow.CrossFadeAlpha(1, t, true);
            }

            // Teammate of previous quadrant
            if (oldQuadrant != -1)
            {
                Vector3 centerPos = (teamQuadrants[oldQuadrant].TeamCharacterPositions[0].position + teamQuadrants[oldQuadrant].TeamCharacterPositions[1].position) / 2f;

                if (PhotonNetwork.OfflineMode || !IsTeammateOnline(oldQuadrant))
                {
                    int teammateControllerIndex = GetTeammateControllerImageIndex(oldQuadrant, playerNum);

                    if (teammateControllerIndex > -1)
                    {
                        ControllerImage teammate = controllerImages[teammateControllerIndex];
                        Color c1 = image.FullController.canvasRenderer.GetColor();
                        Color c2 = teammate.FullController.canvasRenderer.GetColor();
                        teammate.FullController.CrossFadeAlpha(0, t, true);
                        teammate.LeftHalf.CrossFadeColor(c1, t, true, true);
                        teammate.RightHalf.CrossFadeColor(c2, t, true, true);

                        // Handle teammate motion

                        if (!PhotonNetwork.OfflineMode)
                        {
                            photonView.RPC("SyncObjectAcrossClients", RpcTarget.Others, PhotonNetwork.LocalPlayer.UserId, teammateControllerIndex, centerPos, 0.25f, oldQuadrant, oldQuadrant, -1, 1 - GetPlayerIndexInQuadrant(oldQuadrant, teammateControllerIndex));
                            LerpControllerImage(teammateControllerIndex, teammate.gameObject, centerPos, 0.25f);
                        }
                        else
                        {
                            LerpControllerImage(teammateControllerIndex, teammate.gameObject, centerPos, 0.25f);
                        }
                    }
                }
                else if (!PhotonNetwork.OfflineMode && IsTeammateOnline(oldQuadrant))
                {
                    Debug.Log("previous teammate is online");

                    // Get previous quadrant's teammate
                    int teammatePlayerIndex = GetTeammateOnlinePlayerIndex(oldQuadrant, playerNum);
                    string teammateClientID = GetTeammateClientID(oldQuadrant, playerNum);
                    //Debug.Log("teammate client id: " + teammateClientID);

                    LerpOnlinePlayerIcon(teammatePlayerIndex, onlinePlayerIcons[teammatePlayerIndex].gameObject, centerPos, 0.25f);

                    int sideNum = GetTeammateOnlinePlayerPosInQuadrant(oldQuadrant, playerNum);

                    if (sideNum == 1)
                    {
                        // This if block is added because the RPC call below it only gets called on all OTHER clients, not THIS one.
                        // So we need to do this. Ideally, the RPC call should handle this stuff on EVERY SINGLE CLIENT, including this one.
                        teamQuadrants[oldQuadrant].IsOnlinePlayer[1] = false;
                        teamQuadrants[oldQuadrant].OnlinePlayerClientIDs[1] = "";
                        teamQuadrants[oldQuadrant].OnlinePlayerNumberOnOwner[1] = -1;

                        teamQuadrants[oldQuadrant].IsOnlinePlayer[0] = true;
                        teamQuadrants[oldQuadrant].OnlinePlayerClientIDs[0] = teammateClientID;
                        teamQuadrants[oldQuadrant].OnlinePlayerNumberOnOwner[0] = teammatePlayerIndex;
                    }

                    photonView.RPC("SyncObjectAcrossClients", RpcTarget.Others, teammateClientID, teammatePlayerIndex, centerPos, 0.25f, oldQuadrant, oldQuadrant, -1, sideNum);     // teammate didn't change quadrants
                    string side = sideNum == 0 ? "left" : sideNum == 1 ? "right" : "something is wrong";
                }
            }
            yield return null;
        }
        #endregion

        #region Online
        /// <summary>
        /// Called when the client is connected to an online room
        /// </summary>
        private void JoinedRoom()
        {
            // Unsubscribe because we no longer need to listen to this
            NetworkManager.Instance.JoinedRoom -= JoinedRoom;

            // Reset all clients except for the master client
            if (!PhotonNetwork.IsMasterClient)
            {
                RewiredJoystickAssign.Instance.EndJoystickAssignment(true, true);     // pause joystick assignment
                ActivateState();                                            // Reset current state. ActivateState() also resumes joystick assignment
            }

            // Set proper player locations
            for (int i = 0; i < RewiredJoystickAssign.Instance.UsedIDs.Count; i++)                                         
            {
                int rewiredID = RewiredJoystickAssign.Instance.UsedIDs[i];
                int controllerIndex = RewiredJoystickAssign.Instance.RewiredIDCorrespondingControllerIndices[rewiredID];

                // Debug.Log(i + " " + controllerIndex);
                photonView.RPC("SetThisClientPlayerOnOthers", RpcTarget.OthersBuffered, PhotonNetwork.LocalPlayer.UserId, controllerIndex);

                // Only set locations of player icons on other clients if this is the master client
                // Every other client has its controller images reset to center, so there's no need to update it yet
                if (PhotonNetwork.IsMasterClient)
                {
                    Debug.Log("Setting location on other clients");
                    photonView.RPC("SetPlayerIconLocation", RpcTarget.OthersBuffered, PhotonNetwork.LocalPlayer.UserId, controllerIndex, controllerImages[i].QuadrantIndex, controllerImages[i].transform.position);
                }
            }
        }

        [PunRPC]
        private void SetThisClientPlayerOnOthers(string clientID, int playerNum)
        {
            int onlinePlayerNum = onlinePlayersCount;
            onlinePlayersCount++;

            onlinePlayerIcons[onlinePlayerNum].SetPlayerIcon(clientID, onlinePlayerNum, playerNum);

            if (onlinePlayerNumberLocalIndex.ContainsKey(clientID))
            {
                //Debug.Log("add");
                onlinePlayerNumberLocalIndex[clientID].Add(onlinePlayerNum);
            }
            else
            {
                //Debug.Log("create");
                onlinePlayerNumberLocalIndex.Add(clientID, new List<int> { onlinePlayerNum });
            }

            onlinePlayerIcons[onlinePlayerNum].Icon.canvasRenderer.SetAlpha(1);
        }


        [PunRPC]
        private void SetPlayerIconLocation(string clientID, int playerNum, int quadrant, Vector3 pos)
        {
            StartCoroutine(SetPlayerIconLocationCoroutine(clientID, playerNum, quadrant, pos));
        }

        private IEnumerator SetPlayerIconLocationCoroutine(string clientID, int playerNum, int quadrant, Vector3 pos)
        {
            // In my tests while using a simple function and not having this check below, the RPC call seemed to work every single time.
            // However, I was concerned that if the RPC call somehow manages to happen BEFORE the ActivateState() function is called to reset the variables, this RPC call's change of nPlayersInQuadrant[]
            // will also get reset. So, I added this check and made this function a coroutine just for extra precaution.
            if (!PhotonNetwork.IsMasterClient && !hasResetValues)
            {
                while (!hasResetValues)
                {
                    //Debug.Log("resetting values");
                    yield return null;
                }

                //Debug.Log("done resetting values");
            }

            int index = onlinePlayerNumberLocalIndex[clientID][playerNum];
            if (quadrant != -1)
            {
                if (nPlayersInQuadrant[quadrant] < 2)
                {
                    int pIndex = nPlayersInQuadrant[quadrant];
                    onlinePlayerIcons[index].transform.position = pos;
                    teamQuadrants[quadrant].IsOnlinePlayer[pIndex] = true;
                    teamQuadrants[quadrant].OnlinePlayerNumberOnOwner[pIndex] = playerNum;
                    teamQuadrants[quadrant].OnlinePlayerClientIDs[pIndex] = clientID;
                    nPlayersInQuadrant[quadrant]++;
                }
            }
            else
                onlinePlayerIcons[index].transform.position = onlinePlayerImageOriginalPositions[index];

            yield return null;
        }

        /// <summary>
        /// Moves the player icon of an online client on all clients. Also moves its controller image on its owner client.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="playerNum"></param>
        /// <param name="pos">Final destination of the lerp</param>
        /// <param name="t">Time for the lerp to finish</param>
        /// <param name="leftOrJoinedTeam">Left a team with another player = -1, joined a team with another player = 1, all other cases = 0</param>
        [PunRPC]
        private void SyncObjectAcrossClients(string clientID, int playerNum, Vector3 pos, float timeForLerp, int oldQuadrant, int newQuadrant, int leftOrJoinedTeam, int prevIndex)
        {
            bool isOwnedByThisClient = PhotonNetwork.LocalPlayer.UserId.Equals(clientID);

            if (isOwnedByThisClient) // this is the client which owns the player
            {
                // Move the controller
                GameObject toMove = controllerImages[playerNum].gameObject;

                LerpControllerImage(playerNum, toMove, pos, timeForLerp);
            }
            else
            {
                // Move player icons
                int index = onlinePlayerNumberLocalIndex[clientID][playerNum];
                GameObject toMove = onlinePlayerIcons[index].gameObject;

                LerpOnlinePlayerIcon(index, toMove, pos, timeForLerp);
            }

            if (newQuadrant == oldQuadrant && newQuadrant >= 0 && newQuadrant < 4)
            // if the new and old quadrants are the same, that means a player moved away, and his teammate moved in the same quadrant to place 0, emptying place 2
            {
                Debug.Log("SAME OLD AND NEW BOIS");
                int pIndex = 1 - prevIndex;

                Debug.Log("isOwned" + isOwnedByThisClient);
                if (leftOrJoinedTeam == 1 || leftOrJoinedTeam == -1)
                {
                    Debug.Log(leftOrJoinedTeam == -1?"LEFT TEAM" : "JOINED TEAM");

                    teamQuadrants[newQuadrant].IsOnlinePlayer[0] = !isOwnedByThisClient;
                    teamQuadrants[newQuadrant].OnlinePlayerNumberOnOwner[0] = isOwnedByThisClient ? -1 : playerNum;
                    teamQuadrants[newQuadrant].OnlinePlayerClientIDs[0] = isOwnedByThisClient ? "" : clientID;
                }

                if (leftOrJoinedTeam == -1 && prevIndex == 1)       // only set this to false if p1 LEFT p2, and p2 was on the right half (index 1)
                {
                    Debug.Log("LEFT TEAM");

                    teamQuadrants[newQuadrant].IsOnlinePlayer[prevIndex] = false;
                    teamQuadrants[newQuadrant].OnlinePlayerNumberOnOwner[prevIndex] = -1;
                    teamQuadrants[newQuadrant].OnlinePlayerClientIDs[prevIndex] = "";
                }
            }

            // We can't get rid of the block of code above.
            // I thought the code below would work without having the need of the block above, because in the case the new and old quadrants were the same, the code for both would run
            // It would add one to nPlayersInQuadrant, then subtract one from it, hence making no change, which is what the code above is doing.
            // Yes, the code below WILL work for the case where a player leaves a quadrant which has another player in it.
            //
            // BUT, in the case that a player JOINS a quadrant with another player in it, the add portion will happen first for the joining player, taking nPlayers to 2.
            // Then, it would add AGAIN for the player that was already in the quadrant, and would get clamped back to 2.
            // Then, it would subtract 1 from the 2, going back to 1 - this is the problem. The value here should stay at 2, and that's why we need the code above.
            else
            {
                if (newQuadrant >= 0 && newQuadrant < 4)
                {
                    //Debug.Log("<color=purple>client ID:  " + clientID + " , playerNum: " + playerNum + " , newQuad: " + newQuadrant + "</color>");
                    Debug.Log("new" + newQuadrant);

                    nPlayersInQuadrant[newQuadrant]++;
                    if (nPlayersInQuadrant[newQuadrant] > 2) nPlayersInQuadrant[newQuadrant] = 2;
                    Debug.Log("isOwned" + isOwnedByThisClient);
                    int pIndex = nPlayersInQuadrant[newQuadrant] - 1;
                    teamQuadrants[newQuadrant].IsOnlinePlayer[pIndex] = !isOwnedByThisClient;
                    teamQuadrants[newQuadrant].OnlinePlayerNumberOnOwner[pIndex] = isOwnedByThisClient ? -1 : playerNum;
                    teamQuadrants[newQuadrant].OnlinePlayerClientIDs[pIndex] = isOwnedByThisClient ? "" : clientID;
                }

                if (oldQuadrant >= 0 && oldQuadrant < 4)
                {
                    //Debug.Log("<color=purple>client ID:  " + clientID + " , playerNum: " + playerNum + " , oldQuad: " + oldQuadrant + "</color>");
                    Debug.Log("old" + oldQuadrant);

                    int pIndex = 0;
                    nPlayersInQuadrant[oldQuadrant]--;
                    if (nPlayersInQuadrant[oldQuadrant] < 0) nPlayersInQuadrant[oldQuadrant] = 0;
                    if (teamQuadrants[oldQuadrant].OnlinePlayerClientIDs[0].Equals(clientID))
                    {
                        pIndex = 0;
                    }
                    else if (teamQuadrants[oldQuadrant].OnlinePlayerClientIDs[1].Equals(clientID))
                    {
                        pIndex = 1;
                    }
                    else
                    {
                        pIndex = -1;
                    }

                    if (pIndex != -1)
                    {
                        teamQuadrants[oldQuadrant].OnlinePlayerClientIDs[pIndex] = "";
                        teamQuadrants[oldQuadrant].IsOnlinePlayer[pIndex] = false;
                        teamQuadrants[oldQuadrant].OnlinePlayerNumberOnOwner[pIndex] = -1;
                    }
                }
            }

            Debug.Log("<color=purple>---------------- BOI ----------------</color>");
            Debug.Log(nPlayersInQuadrant[0] + "          ,          " + nPlayersInQuadrant[1]);
            Debug.Log(nPlayersInQuadrant[2] + "          ,          " + nPlayersInQuadrant[3]);
        }

        #endregion

        #region Helper Functions
        /// <summary>
        /// Returns the controller image index of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate is required</param>
        /// <returns></returns>
        private int GetTeammateControllerImageIndex(int quadrant, int controllerImgIndex)
        {
            if (controllerImageIndices[quadrant][0] == controllerImgIndex) return controllerImageIndices[quadrant][1];
            else if (controllerImageIndices[quadrant][1] == controllerImgIndex) return controllerImageIndices[quadrant][0];

            return -1;
        }

        /// <summary>
        /// Returns the controller image index of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate is required</param>
        /// <returns></returns>
        private int GetPlayerIndexInQuadrant(int quadrant, int controllerImgIndex)
        {
            if (controllerImageIndices[quadrant][0] == controllerImgIndex) return 0;
            else if (controllerImageIndices[quadrant][1] == controllerImgIndex) return 1;

            return -1;
        }

        /// <summary>
        /// Returns whether the teammate of a another controller image index is an online player or not
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate's online status is required</param>
        /// <returns></returns>
        private bool IsTeammateOnline(int quadrant)
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
        /// Returns the quadrant of the given controller image index
        /// Returns -1 if the controller image is not at a quadrant
        /// </summary>
        /// <param name="controllerImgIndex">Controller image index of the player whose quadrant is required</param>
        /// <returns></returns>
        private int GetQuadrantOfControllerImageIndex(int controllerImgIndex)
        {
            for (int i = 0; i < 4; i++)
            {
                if (controllerImageIndices[i][0] == controllerImgIndex || controllerImageIndices[i][1] == controllerImgIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the controller image index of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate is required</param>
        /// <returns></returns>
        private int GetTeammateOnlinePlayerIndex(int quadrant, int controllerImgIndex)
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
        /// Returns the controller image index of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate is required</param>
        /// <returns></returns>
        private string GetTeammateClientID(int quadrant, int controllerImgIndex)
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
        /// Returns the controller image index of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate is required</param>
        /// <returns></returns>
        private int GetTeammateOnlinePlayerPosInQuadrant(int quadrant, int controllerImgIndex)
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

        /// <summary>
        /// Removes a controller image index from a quadrant
        /// </summary>
        /// <param name="quadrant"></param>
        /// <param name="controllerImgIndex"></param>
        private void RemoveControllerImageIndexFromQuadrant(int quadrant, int controllerImgIndex)
        {
            if (quadrant >= 0 && quadrant < 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (controllerImageIndices[quadrant][i] == controllerImgIndex)
                    {
                        ControllerImage CI = controllerImages[controllerImgIndex];
                        CI.SetQuadrant(-1);
                        CI.Unready();

                        nPlayersInQuadrant[quadrant]--;
                        if (nPlayersInQuadrant[quadrant] < 0)
                            nPlayersInQuadrant[quadrant] = 0;

                        controllerImageIndices[quadrant][i] = -1;

                        break;
                    }
                }
            }
        }

        private void SavePlayerPrefs()
        {
            int team = 0;

            // Reset player prefs first. This makes sure unused teams have IDs -1.
            for (int i = 0; i < 2; i++)
            {
                PlayerPrefs.SetString("team" + (i + 1).ToString() + "Layout", ControllerLayouts.LayoutStyle.None.ToString());
                PlayerPrefs.SetInt("player_" + (i + 1).ToString() + "1_RewiredID", -1);
                PlayerPrefs.SetInt("player_" + (i + 1).ToString() + "2_RewiredID", -1);
                PlayerPrefs.SetInt("team" + (i + 1).ToString() + "SharedControllerOwnerID", -1);
                PlayerPrefs.SetInt("team" + (i + 1).ToString() + "NumPlayers", -1);
            }

            // Now set player prefs
            for (int i = 0; i < 4; i++)
            {
                if (layouts[i].Layout != ControllerLayouts.LayoutStyle.None)
                {
                    team++;
                    Debug.Log("Team " + team + " is Quadrant " + (i + 1));
                    PlayerPrefs.SetString("team" + (team).ToString() + "Layout", layouts[i].Layout.ToString());
                    PlayerPrefs.SetInt("player_" + (team).ToString() + "1_RewiredID", layouts[i].RewiredIDs[0]);
                    PlayerPrefs.SetInt("player_" + (team).ToString() + "2_RewiredID", layouts[i].RewiredIDs[1]);
                    PlayerPrefs.SetInt("team" + (team).ToString() + "SharedControllerOwnerID", layouts[i].SharedControllerOwnerID);
                    PlayerPrefs.SetInt("team" + (team).ToString() + "NumPlayers", 2);
                    //Debug.Log("<color=blue> Team" + (team).ToString() + "Layout: " + layouts[team - 1].Layout.ToString() + "</color>");
                    //Debug.Log("<color=green> Player" + (team).ToString() + "1_RewiredID: " + layouts[team - 1].RewiredIDs[0].ToString() + "</color>");
                    //Debug.Log("<color=green> Team" + (team).ToString() + "2_RewiredID: " + layouts[team - 1].RewiredIDs[0].ToString() + "</color>");
                    //Debug.Log("<color=green> Team" + (team).ToString() + "SharedControllerOwnerID: " + layouts[team - 1].SharedControllerOwnerID + "</color>");
                    //Debug.Log("<color=green> Team" + (team).ToString() + "Number of players: " + PlayerPrefs.GetInt("team" + (team).ToString() + "NumPlayers") + "</color>");
                }
            }

            PlayerPrefs.SetInt("teamsOnThisClient", team);
            if (team <= 1)
            {
                MenuManager.Instance.SelectedMode = 0;
            }
            else
            {
                Debug.Log("Else for screen select running");
                MenuManager.Instance.SelectedMode = 1;
            }


            if (!Photon.Pun.PhotonNetwork.OfflineMode)
            {
                NetworkManager.Instance.AddNumberOfTeams(MenuManager.Instance.SelectedMode + 1);
            }

            PlayerPrefs.SetInt("screenMode", MenuManager.Instance.SelectedMode);
        }

        private void SetControllerLayout(int quadrantNum, int enumInt)
        {
            if (!PhotonNetwork.OfflineMode)
            {
                // Stuff to do on all clients
                photonView.RPC("SetControllerLayoutRPC", RpcTarget.AllBufferedViaServer, quadrantNum, enumInt);
            }
        }

        [PunRPC]
        private void AddControllerToQuadrant(int quadrant, int rewiredPlayerID, string clientID)
        {
            layouts[quadrant].AddController(rewiredPlayerID, clientID);
        }

        [PunRPC]
        private void RemoveControllerFromQuadrant(int quadrant, int rewiredPlayerID, string clientID)
        {
            layouts[quadrant].RemoveController(rewiredPlayerID, clientID);
        }

        [PunRPC]
        private void SetControllerLayoutRPC(int quadrantNum, int enumInt)
        {

            layouts[quadrantNum].Layout = (ControllerLayouts.LayoutStyle)enumInt;
            Debug.Log(layouts[quadrantNum].Layout);
        }

        #endregion

        private void OnDestroy()
        {
            // Unsubscribe from events
            RewiredJoystickAssign.Instance.OnJoystickConnected -= ControllerConnected;
            RewiredJoystickAssign.Instance.OnJoystickPreDisconnect -= ControllerDisconnected;
            subscribedToControllerEvents = false;
        }

        #region Event Subscriptions
        public void ControllerConnected(int rewiredID, int playerNumber)
        {

            // Activate next available controllerImage
            if (playerNumber < 4)
            {
                controllerImages[playerNumber].gameObject.SetActive(true);
            }
        }

        public void ControllerDisconnected(int rewiredID, int playerNumber)
        {
            // Hide the controller that disconnected
            if (playerNumber < 4)
            {
                // Deactivate the last available controllerImage
                controllerImages[playerNumber].gameObject.SetActive(false);

                controllerImages[playerNumber].transform.position = controllerOriginalPositions[playerNumber];
                controllerImages[playerNumber].FullController.canvasRenderer.SetColor(availableControllerColor);
                controllerImages[playerNumber].LeftHalf.canvasRenderer.SetColor(availableControllerColor);
                controllerImages[playerNumber].RightHalf.canvasRenderer.SetColor(availableControllerColor);
                controllerImages[playerNumber].FullController.canvasRenderer.SetAlpha(1);
                controllerImages[playerNumber].LeftHalf.canvasRenderer.SetAlpha(0);
                controllerImages[playerNumber].RightHalf.canvasRenderer.SetAlpha(0);
                controllerImages[playerNumber].LeftArrow.canvasRenderer.SetAlpha(1f);
                controllerImages[playerNumber].RightArrow.canvasRenderer.SetAlpha(1f);

                controllerImages[playerNumber].Unready();
            }


            //Handle teammate
            int quadrant = GetQuadrantOfControllerImageIndex(playerNumber);
            if (quadrant >= 0 && quadrant < 4)
            {
                int teammate = GetTeammateControllerImageIndex(quadrant, playerNumber);
                //Debug.Log("player num: "  + playerNumber + " , teammate: " + teammate);
                RemoveControllerImageIndexFromQuadrant(quadrant, playerNumber);

                if (teammate != -1)
                {
                    StartCoroutine(HandleVisuals(teammate, quadrant, quadrant, 0.3f));
                }

                // If layout was locked, remove it
                if (layouts[quadrant].RewiredIDs[0] == rewiredID || layouts[quadrant].RewiredIDs[1] == rewiredID)
                {
                    layouts[quadrant].RemoveController(rewiredID, PhotonNetwork.LocalPlayer.UserId);
                }
            }
        }
        #endregion
    }

    [System.Serializable]
    class ControllerLayouts
    {
        #region FIELDS
        public enum LayoutStyle { None, Shared, Separate };             // Enum for types of layout

        /// Holds the layout style for the team.
        /// </summary>
        public LayoutStyle Layout
        {
            get; set;
        }

        private int[] rewiredIDs = new int[] { -1, -1 };       // Array of 2. Holds the rewired IDs of each player in team
        /// <summary>
        /// Array of 2 [arrays of 2 ints]. Holds [player]'s rewired ID.
        /// </summary>
        public int[] RewiredIDs
        {
            get { return rewiredIDs; }
        }

        public int QuadrantIndex;

        private int sharedControllerOwnerID = -1;    //  If a team is using a shared controller, this holds the ID for the "rewired ID" owner of that controller.
        public int SharedControllerOwnerID { get { return sharedControllerOwnerID; } }
        #endregion

        #region ADD_REMOVE_CONTROLLERS
        /// <summary>
        /// Add a controller to a team and player's rewired ID
        /// </summary>
        /// <param name="team"></param>
        /// <param name="rewiredID"></param>
        public void AddController(int rewiredID, string clientID)
        {
            int id = rewiredID;

            if (Layout == LayoutStyle.None) // || ((Layout == LayoutStyle.None) && !PhotonNetwork.OfflineMode && NetworkManager.Instance.TeamClientIDs[QuadrantIndex][0] == null))
            {
                Layout = LayoutStyle.Shared;
                if (!PhotonNetwork.OfflineMode)
                {
                    NetworkManager.Instance.SetTeamNumbersClientId(QuadrantIndex, PhotonNetwork.LocalPlayer.UserId);
                    if (!clientID.Equals(PhotonNetwork.LocalPlayer.UserId))
                    {
                        id = -1;
                    }
                }

                rewiredIDs[0] = id;

                sharedControllerOwnerID = id;
                if (RewiredJoystickAssign.Instance.UnusedIDs.Count > 0)
                {
                    rewiredIDs[1] = RewiredJoystickAssign.Instance.UnusedIDs[0];

                    RewiredJoystickAssign.Instance.AssignControllerToPlayerWithSharedID(sharedControllerOwnerID, rewiredIDs[1]);
                }

                //MenuState_ControllerSetup.assignedRewiredIDs.Add(rewiredID);
                //MenuState_ControllerSetup.unassignedRewiredIDs.Remove(rewiredID);
            }
            else if (Layout == LayoutStyle.Shared) // && (PhotonNetwork.OfflineMode)) || ((Layout == LayoutStyle.None) && (!PhotonNetwork.OfflineMode) && (NetworkManager.Instance.TeamClientIDs[QuadrantIndex][0] != null)))
            {
                Layout = LayoutStyle.Separate;

                if (!PhotonNetwork.OfflineMode)
                {
                    NetworkManager.Instance.SetTeamNumbersClientId(QuadrantIndex, PhotonNetwork.LocalPlayer.UserId);
                    if (!clientID.Equals(PhotonNetwork.LocalPlayer.UserId))
                    {
                        id = -1;
                    }
                }
                
                // Offline mode / code for owner-client
                
                if (rewiredIDs[1] == sharedControllerOwnerID)
                {
                    Debug.Log("a");
                    RewiredJoystickAssign.Instance.RemoveControllerWithSharedID(rewiredIDs[0], rewiredIDs[1], id);

                    rewiredIDs[0] = rewiredIDs[1];      // The old controller is always the LEFT player
                    rewiredIDs[1] = id;                    // The right controller is always the RIGHT player
                }
                else if (rewiredIDs[0] == sharedControllerOwnerID)
                {
                    Debug.Log("b");

                    RewiredJoystickAssign.Instance.RemoveControllerWithSharedID(rewiredIDs[1], sharedControllerOwnerID, id);

                    rewiredIDs[1] = id;
                }

                //MenuState_ControllerSetup.assignedRewiredIDs.Add(rewiredID);
                //MenuState_ControllerSetup.unassignedRewiredIDs.Remove(rewiredID);
                sharedControllerOwnerID = -1;
            }
        }

        /// <summary>
        /// Remove a controller from a team and player's rewired ID
        /// </summary>
        /// <param name="rewiredID"></param>
        public void RemoveController(int rewiredID, string clientID)
        {
            int id = rewiredID;

            if (Layout == LayoutStyle.Separate) // && (PhotonNetwork.OfflineMode)) || ((Layout == LayoutStyle.Separate) && (!PhotonNetwork.OfflineMode) && (NetworkManager.Instance.TeamClientIDs[QuadrantIndex].Contains(PhotonNetwork.LocalPlayer.UserId))))
            {
                Layout = LayoutStyle.Shared;

                if (!PhotonNetwork.OfflineMode)
                {
                    NetworkManager.Instance.RemoveTeamNumbersClientId(QuadrantIndex, PhotonNetwork.LocalPlayer.UserId);

                    if (!clientID.Equals(PhotonNetwork.LocalPlayer.UserId))
                    {
                        id = -1;
                    }
                }

                //MenuState_ControllerSetup.assignedRewiredIDs.Remove(rewiredID);
                //MenuState_ControllerSetup.unassignedRewiredIDs.Add(rewiredID);

                if (rewiredIDs[0] == id)
                {
                    sharedControllerOwnerID = rewiredIDs[1];
                    rewiredIDs[0] = rewiredIDs[1];

                    if (RewiredJoystickAssign.Instance.UnusedIDs.Count > 0)
                    {
                        rewiredIDs[1] = RewiredJoystickAssign.Instance.UnusedIDs[0];

                        RewiredJoystickAssign.Instance.RemoveControllerWithSeparateLayouts(id, sharedControllerOwnerID, rewiredIDs[1]);
                    }

                }
                else if (rewiredIDs[1] == id)
                {
                    sharedControllerOwnerID = rewiredIDs[0];

                    if (RewiredJoystickAssign.Instance.UnusedIDs.Count > 0)
                    {
                        rewiredIDs[1] = RewiredJoystickAssign.Instance.UnusedIDs[0];

                        RewiredJoystickAssign.Instance.RemoveControllerWithSeparateLayouts(id, sharedControllerOwnerID, rewiredIDs[1]);
                    }
                }
            }
            else if (Layout == LayoutStyle.Shared) //  && (PhotonNetwork.OfflineMode)) || ((Layout == LayoutStyle.Shared) && (!PhotonNetwork.OfflineMode) && (NetworkManager.Instance.TeamClientIDs[QuadrantIndex][0] == PhotonNetwork.LocalPlayer.UserId)))
            {
                Layout = LayoutStyle.None;
                if (!PhotonNetwork.OfflineMode)
                {
                    NetworkManager.Instance.RemoveTeamNumbersClientId(QuadrantIndex, PhotonNetwork.LocalPlayer.UserId);

                    if (!clientID.Equals(PhotonNetwork.LocalPlayer.UserId))
                    {
                        id = -1;
                    }
                }

                if (id == rewiredIDs[0])
                {
                    RewiredJoystickAssign.Instance.RemoveAllControllers(rewiredIDs[0], rewiredIDs[1]);
                }
                else if (id == rewiredIDs[1])
                {
                    RewiredJoystickAssign.Instance.RemoveAllControllers(rewiredIDs[1], rewiredIDs[0]);
                }


                rewiredIDs[0] = -1;
                rewiredIDs[1] = -1;

                sharedControllerOwnerID = -1;

                //MenuState_ControllerSetup.assignedRewiredIDs.Remove(rewiredID);
                //MenuState_ControllerSetup.unassignedRewiredIDs.Add(rewiredID);
            }
        }
        #endregion

        public void EndJoystickAssignment()
        {
            if (Layout != LayoutStyle.None)
            {
                for (int i = 0; i < 2; i++)
                {
                    int id = rewiredIDs[i];
                    if (id > 3)
                    {
                        RewiredJoystickAssign.Instance.UsedIDs.Remove(rewiredIDs[i]);
                        RewiredJoystickAssign.Instance.UnusedIDs.Add(rewiredIDs[i]);
                        RewiredJoystickAssign.Instance.UnusedIDs.Sort();

                        rewiredIDs[i] = RewiredJoystickAssign.Instance.UnusedIDs[0];
                        RewiredJoystickAssign.Instance.UnusedIDs.RemoveAt(0);
                        RewiredJoystickAssign.Instance.UsedIDs.Add(rewiredIDs[i]);

                        if (sharedControllerOwnerID == id)
                        {
                            sharedControllerOwnerID = rewiredIDs[i];
                        }
                    }
                }
            }
        }

        #region GETTERS
        /// <summary>
        /// Returns the rewired player ID of the teammate of another rewired ID if the given rewired ID is valid
        /// </summary>
        /// <param name="rewiredID">Rewired ID of the player whose teammate is required</param>
        /// <returns></returns>
        public int GetTeammateRewiredPlayerID(int rewiredID)
        {
            if (rewiredIDs[0] == rewiredID) return rewiredIDs[1];
            else if (rewiredIDs[1] == rewiredID) return rewiredIDs[0];

            return -1;
        }
        #endregion
    }
}