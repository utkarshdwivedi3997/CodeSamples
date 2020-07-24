using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Linq;
using Rewired;

namespace Menus
{
    /// <summary>
    /// This class is a subscript of MenuState_LobbyScreen
    /// It is used for the controller setup portion of setting up a private lobby
    /// </summary>
    public class Lobby_ControllerSetup : MonoBehaviourPun
    {
        #region Variables

        MenuState_LobbyScreen lobby;

        #region Quadrant descriptors
        [SerializeField, Tooltip("Threshold angle to make regions in the quadrants that don't count as diagonal input")]
        private float diagonalThreshold = 15f;                          // Threshold angle to make regions in the quadrants that don't count as diagonal input
        private const float QUAD_1_STARTING_ANGLE = 0;                  // Quadrant 1 starts at this angle
        private const float QUAD_2_STARTING_ANGLE = 90;                 // Quadrant 2 starts at this angle
        private const float QUAD_3_STARTING_ANGLE = 180;                // Quadrant 3 starts at this angle
        private const float QUAD_4_STARTING_ANGLE = -90;                // Quadrant 4 starts at this angle
        #endregion

        [SerializeField]
        private Image[] quadrantPointerArrows;                                      // Arrow images pointing to the 4 quadrants. We need these references to turn the arrows on/off depending on where the controllers can go.
        [SerializeField]
        private Color[] colors;                                                     // Colors for all teams
        [SerializeField]
        private Color availableQuadrantColor;                                       // Color for available quadrants / teams
        [SerializeField]
        private Color unavailableQuadrantColor;                                     // Color for unavailable quadrants / teams
        private Vector3[] controllerOriginalPositions = new Vector3[4];             // Original (middle/no team) position for all 4 controller images
        private Vector3 controllerOriginalScale = Vector3.one;                      // Original scales for the controller images
        private Quaternion controllerOriginalRotation;                              // Original rotation for the controller images
        [SerializeField] private float controllerRotationAngle = 3f;
        [SerializeField] private float controllerImgHoverScaleMultiplier = 1.5f;    // scale multiplier for when the controller images are hovering over a quadrant (not selected yet)

        [SerializeField]
        private RectTransform centerPosition;

        public ControllerLayout[] layouts { get; private set; }                     // Array of ControllerLayout() class. Holds the layout of each of the 4 quadrants
        private int[] nPlayersInQuadrant;                                           // Array of 4. Holds the number of controllers/online players in each quadrant
        private int[][] playerQuadrantRewiredIDsBeforeSelection;                    // Rewired IDs of players who are hovering over quadrants and haven't selected teams yet. array[0][0] is quadrant 1's left side (or shared side), and array[0][1] is the right side
        private int[][] controllerImageIndices;                                     // Indices of controller images for each quadrant

        private Coroutine[] controllerImageMoveCoroutines = new Coroutine[4];       // Array to keep track of all coroutines running to move controller UI images
        private Coroutine[] controllerImageScaleCoroutines = new Coroutine[4];      // Array to keep track of all coroutines running to scale controller UI images

        private const int numberOfLocalTeamsAllowed = 2;                                      // The number of local teams that are allowed. This is either 1 for campaign or 2 for race

        #region Online UI Vars
        //[SerializeField]
        //private OnlinePlayerIcon[] onlinePlayerIcons;                               // Array of online player icons
        [SerializeField]
        private GameObject onlinePlayerIconPrefab;
        private Dictionary<string, OnlinePlayerIcon> onlinePlayerIcons;             // Keys = identifying ids of the format "<client ID>|<controller image index on client>", Value = corresponding online player icon
        private Dictionary<string, Coroutine> onlinePlayerIconMoveCoroutines;       // Dictionary of all coroutines running to move online player icon UI images, Keys same as onlinePlayerIcons keys, Values = coroutines
        private Dictionary<string, Coroutine> onlinePlayerIconScaleCoroutines;      // Dictionary of all coroutines running to scale online player icon UI images, Keys same as onlinePlayerIconKeys, Values = coroutines
        private int onlinePlayersCount;
        //private Dictionary<string, List<int>> onlinePlayerNumberLocalIndex;       // Dictionary of ( client ID , local client player numbers of players in those clients )
        private bool hasResetLobbyAfterJoiningRoom = false;                         // Has reset the values required to be reset after joining the room?
        [SerializeField] private Color onlinePlayerColor;                           // Color to display for an online player's quadrant half
        #endregion
        #endregion

        #region Initialization
        /// <summary>
        /// To be called once and only once by MenuState_LobbyScreen.InitState()
        /// </summary>
        public void Init(MenuState_LobbyScreen lobby)
        {
            this.lobby = lobby;

            // Save original locations and rotations of all controller images
            for (int i = 0; i < 4; i++)
            {
                controllerOriginalPositions[i] = lobby.ControllerImages[i].GetComponent<RectTransform>().position;
            }
            controllerOriginalRotation = lobby.ControllerImages[0].GetComponent<RectTransform>().localRotation;

            // Getting rid of this from InitState() breaks ControllerDisconnect and ControllerConnect when they run before this state is activated
            // Reset controller image indices
            controllerImageIndices = new int[4][];
            playerQuadrantRewiredIDsBeforeSelection = new int[4][];
            for (int i = 0; i < 4; i++)
            {
                controllerImageIndices[i] = new int[2];
                controllerImageIndices[i][0] = -1;
                controllerImageIndices[i][1] = -1;

                playerQuadrantRewiredIDsBeforeSelection[i] = new int[2];
                playerQuadrantRewiredIDsBeforeSelection[i][0] = -1;
                playerQuadrantRewiredIDsBeforeSelection[i][1] = -1;
            }
        }

        public void ActivateState()
        {
            // Do this at the start of this function!
            if (!PhotonNetwork.OfflineMode)
            {
                hasResetLobbyAfterJoiningRoom = false;
            }

            // Deactivate all the prompts
            for (int i = 0; i < lobby.TeamQuadrants.Length; i++)
            {
                lobby.TeamQuadrants[i].PromptChangeDeactivateAllPrompts();

                if (MenuData.HasBeenToMainMenuBefore)
                {
                    lobby.TeamQuadrants[i].SetQuadrantReady(false);
                }
            }

            // Grab layouts from MenuData (null if no data, not null if we've been through this phase before)
            layouts = MenuData.LobbyScreenData.ControllerSetup.TeamLayouts;
            nPlayersInQuadrant = new int[4];

            bool keyboardPlayerAccountedFor = false;
            // Reset color and positions of controller icons
            for (int i = 0; i < lobby.ControllerImages.Length; i++)
            {
                Color c = colors[i];
                lobby.ControllerImages[i].SetColorAll(c);
                lobby.ControllerImages[i].Init();

                // Activate only as many controller images as controllers are connected
                if (i >= RewiredJoystickAssign.Instance.ConnectedControllers)
                {
                    if (!keyboardPlayerAccountedFor && MenuData.LobbyScreenData.ControllerSetup.HasKeyboardPlayerRegistered)
                    {
                        keyboardPlayerAccountedFor = true;
                        lobby.ControllerImages[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        lobby.ControllerImages[i].gameObject.SetActive(false);
                    }
                }
                else
                {
                    lobby.ControllerImages[i].gameObject.SetActive(true);
                }
            }

            // Reset controller image indices
            controllerImageIndices = new int[4][];
            playerQuadrantRewiredIDsBeforeSelection = new int[4][];

            // Get my online information
            string myClientID = "";
            if (!PhotonNetwork.OfflineMode)
            {
                myClientID = PhotonNetwork.LocalPlayer.UserId;
            }

            // Position controllers at respective places
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                if (layouts[quadrant] == null)
                {
                    layouts[quadrant] = new ControllerLayout();
                }

                layouts[quadrant].QuadrantIndex = quadrant;               // each layout belongs to one of the 4 quadrants

                controllerImageIndices[quadrant] = new int[2];
                controllerImageIndices[quadrant][0] = -1;
                controllerImageIndices[quadrant][1] = -1;

                playerQuadrantRewiredIDsBeforeSelection[quadrant] = new int[2];
                playerQuadrantRewiredIDsBeforeSelection[quadrant][0] = -1;
                playerQuadrantRewiredIDsBeforeSelection[quadrant][1] = -1;

                Color leftCol = Color.white, rightCol = Color.white;

                switch (layouts[quadrant].Layout)
                {
                    case ControllerLayout.LayoutStyle.None:
                        nPlayersInQuadrant[quadrant] = 0;
                        break;

                    case ControllerLayout.LayoutStyle.Shared:
                        nPlayersInQuadrant[quadrant] = 1;

                        // Offline or owned by this client?
                        if (PhotonNetwork.OfflineMode || NetworkManager.Instance.TeamClientIDs[quadrant][0] == myClientID)
                        {
                            int controllerIndex = RewiredJoystickAssign.Instance.RewiredIDCorrespondingControllerIndices[layouts[quadrant].SharedControllerOwnerID];
                            ControllerImage cImg = lobby.ControllerImages[controllerIndex];

                            Vector3 centerPos = (lobby.TeamQuadrants[quadrant].TeamCharacterPositions[0].position + lobby.TeamQuadrants[quadrant].TeamCharacterPositions[1].position) / 2f;
                            cImg.transform.position = centerPos;
                            cImg.transform.localScale = controllerOriginalScale * controllerImgHoverScaleMultiplier;
                            cImg.transform.localRotation = controllerOriginalRotation;

                            cImg.SetQuadrant(quadrant);
                            cImg.Ready();

                            cImg.NumberOfPlayerPromptUI.OverrideNumberOfPlayers(MenuData.LobbyScreenData.ControllerSetup.TeamNumPlayers[quadrant]);
                            cImg.NumberOfPlayerPromptUI.Pick();

                            cImg.quadrantMode = ControllerImage.QuadrantMode.Ready;
                            cImg.SetControllerImage(layouts[quadrant].SharedControllerOwnerID);

                            controllerImageIndices[quadrant][0] = controllerIndex;
                            playerQuadrantRewiredIDsBeforeSelection[quadrant][0] = layouts[quadrant].SharedControllerOwnerID;

                            // Reset prompts
                            lobby.TeamQuadrants[quadrant].PromptChangeAddControllerNoneToShared(0, layouts[quadrant].RewiredIDs, layouts[quadrant].SharedControllerOwnerID);
                            leftCol = rightCol = colors[controllerIndex];
                        }
                        else
                        {

                        }

                        lobby.TeamQuadrants[quadrant].SetQuadrantColor(leftCol, rightCol);
                        break;

                    case ControllerLayout.LayoutStyle.Separate:
                        nPlayersInQuadrant[quadrant] = 2;

                        if (PhotonNetwork.OfflineMode || NetworkManager.Instance.TeamClientIDs[quadrant][0] == myClientID)
                        {
                            int controllerIndex1 = RewiredJoystickAssign.Instance.RewiredIDCorrespondingControllerIndices[layouts[quadrant].RewiredIDs[0]];
                            ControllerImage cImg1 = lobby.ControllerImages[controllerIndex1];

                            cImg1.transform.position = lobby.TeamQuadrants[quadrant].TeamCharacterPositions[0].position;
                            cImg1.transform.localScale = controllerOriginalScale * controllerImgHoverScaleMultiplier;
                            Quaternion rot = Quaternion.AngleAxis(-controllerRotationAngle, Vector3.forward);
                            cImg1.transform.localRotation = rot;

                            cImg1.LeftHalf.canvasRenderer.SetAlpha(1f);
                            cImg1.RightArrow.canvasRenderer.SetAlpha(1f);
                            cImg1.SetQuadrant(quadrant);
                            cImg1.Ready();

                            cImg1.NumberOfPlayerPromptUI.HidePrompt();

                            cImg1.quadrantMode = ControllerImage.QuadrantMode.Ready;
                            cImg1.SetControllerImage(layouts[quadrant].RewiredIDs[0]);

                            controllerImageIndices[quadrant][0] = controllerIndex1;
                            playerQuadrantRewiredIDsBeforeSelection[quadrant][0] = layouts[quadrant].RewiredIDs[0];

                            // Reset prompts
                            lobby.TeamQuadrants[quadrant].PromptChangeAddControllerNoneToShared(0, layouts[quadrant].RewiredIDs, layouts[quadrant].RewiredIDs[0]);
                            leftCol = colors[controllerIndex1];
                        }
                        else
                        {

                        }

                        if (PhotonNetwork.OfflineMode || NetworkManager.Instance.TeamClientIDs[quadrant][1] == myClientID)
                        {
                            int controllerIndex2 = RewiredJoystickAssign.Instance.RewiredIDCorrespondingControllerIndices[layouts[quadrant].RewiredIDs[1]];
                            ControllerImage cImg2 = lobby.ControllerImages[controllerIndex2];

                            cImg2.transform.position = lobby.TeamQuadrants[quadrant].TeamCharacterPositions[1].position;
                            cImg2.transform.localScale = controllerOriginalScale * controllerImgHoverScaleMultiplier;
                            Quaternion rot = Quaternion.AngleAxis(controllerRotationAngle, Vector3.forward);
                            cImg2.transform.localRotation = rot;

                            cImg2.LeftHalf.canvasRenderer.SetAlpha(1f);
                            cImg2.RightArrow.canvasRenderer.SetAlpha(1f);
                            cImg2.SetQuadrant(quadrant);
                            cImg2.Ready();

                            cImg2.NumberOfPlayerPromptUI.HidePrompt();

                            cImg2.quadrantMode = ControllerImage.QuadrantMode.Ready;
                            cImg2.SetControllerImage(layouts[quadrant].RewiredIDs[1]);

                            controllerImageIndices[quadrant][1] = controllerIndex2;
                            playerQuadrantRewiredIDsBeforeSelection[quadrant][1] = layouts[quadrant].RewiredIDs[1];

                            // Reset prompts
                            lobby.TeamQuadrants[quadrant].PromptChangeAddControllerSharedToSeperate(1, layouts[quadrant].RewiredIDs, layouts[quadrant].RewiredIDs[1]);
                            rightCol = colors[controllerIndex2];
                        }
                        else
                        {

                        }

                        lobby.TeamQuadrants[quadrant].SetQuadrantColor(leftCol, rightCol);
                        break;

                    default: break;
                }

                lobby.TeamQuadrants[quadrant].SetQuadrantSelected(layouts[quadrant].Layout != ControllerLayout.LayoutStyle.None); // set the "selected" property of all quadrants
                lobby.TeamQuadrants[quadrant].SetQuadrantSelectedVisuals(layouts[quadrant].Layout != ControllerLayout.LayoutStyle.None); // set the "selected" property of all quadrants
                // make all quadrants available initially
                SetQuadrantAvailable(quadrant, true);
            }

            // Put the remaining controllers in the center
            for (int i = 0; i < 4; i++)
            {
                if (lobby.ControllerImages[i].QuadrantIndex == -1)
                {
                    lobby.ControllerImages[i].transform.position = controllerOriginalPositions[i];
                    lobby.ControllerImages[i].transform.localScale = controllerOriginalScale;
                    lobby.ControllerImages[i].transform.localRotation = controllerOriginalRotation;
                }
            }

            // Reset quadrant pointing arrows
            for (int i = 0; i < 4; i++)
            {
                quadrantPointerArrows[i].CrossFadeAlpha(1f, 0f, true);
            }

            // Reset controller image movement coroutines
            controllerImageMoveCoroutines = new Coroutine[4];
            controllerImageScaleCoroutines = new Coroutine[4];

            SetQuadrantAvailabilities();

            RewiredJoystickAssign.Instance.BeginJoystickAssignment(MenuData.LobbyScreenData.ResetLobby);


            NetworkManager.Instance.ResetTeamClientIDs();

            // Clear up onlinePlayerIcons if they exist
            if (onlinePlayerIcons != null && onlinePlayerIcons.Keys.Count > 0)
            {
                foreach (string id in onlinePlayerIcons.Keys)
                {
                    Destroy(onlinePlayerIcons[id].gameObject);
                }
                onlinePlayerIcons.Clear();
            }

            // Reset online variables
            onlinePlayersCount = 0;
            onlinePlayerIconMoveCoroutines = new Dictionary<string, Coroutine>(); //[8 /*onlinePlayerIcons.Length*/];
            onlinePlayerIconScaleCoroutines = new Dictionary<string, Coroutine>();
            onlinePlayerIcons = new Dictionary<string, OnlinePlayerIcon>();

            // If online, and has been to the menu earlier
            if (!PhotonNetwork.OfflineMode && MenuData.HasBeenToMainMenuBefore)
            {
                if (MenuData.LobbyScreenData.Online.IdentifyingKeys != null)
                {
                    if (MenuData.LobbyScreenData.Online.IdentifyingKeys.Count > 0)
                    {
                        for (int i = 0; i < MenuData.LobbyScreenData.Online.IdentifyingKeys.Count; i++)
                        {
                            string identifyingKey = MenuData.LobbyScreenData.Online.IdentifyingKeys[i];
                            string clientID = MenuData.GetClientIDFromIdentifyingKey(identifyingKey);
                            //Debug.Log("trying c id: " + clientID);
                            int playerNum = MenuData.GetPlayerNumFromIdentifyingKey(identifyingKey);
                            //Debug.Log("trying p num: " + playerNum);

                            GameObject OPIGO = Instantiate(onlinePlayerIconPrefab, centerPosition.position, centerPosition.rotation, centerPosition);
                            OnlinePlayerIcon OPI = OPIGO.GetComponent<OnlinePlayerIcon>();
                            OPI.SetPlayerIcon(clientID, onlinePlayersCount, playerNum);
                            onlinePlayerIcons.Add(identifyingKey, OPI);
                            OPI.Icon.canvasRenderer.SetAlpha(1);

                            // Image movement coroutine
                            onlinePlayerIconMoveCoroutines.Add(identifyingKey, null);
                            onlinePlayerIconScaleCoroutines.Add(identifyingKey, null);

                            onlinePlayersCount++;
                        }
                    }
                }
            }

            // !! DO THIS AT THE END !!
            if (!PhotonNetwork.OfflineMode)
            {
                hasResetLobbyAfterJoiningRoom = true;
            }
            // !! DO NOT ADD ANYTHING BELOW THIS POINT
        }

        #endregion

        #region Input
        public bool Select(float horizontalInput, float verticalInput, int rewiredPlayerID, int playerNumber)
        {
            if (lobby.ControllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Controller)
            {
                // Debug.Log("Select with player ID " + rewiredPlayerID);

                Vector2 dir = new Vector2(horizontalInput, verticalInput);
                float angle = Vector2.SignedAngle(dir, Vector2.left);

                //Debug.Log(playerNumber);
                ControllerImage thisCtrlImage = lobby.ControllerImages[playerNumber];
                int oldQuadrant = thisCtrlImage.QuadrantIndex;
                int quadrant = oldQuadrant;

                // Deadzone. While in deadzone, move the controllers inside the center circle. Don't do any actual layout selection.
                if (Mathf.Abs(verticalInput) <= 0.7f && Mathf.Abs(horizontalInput) <= 0.7f)
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
                    // Nothing happens here
                    #endregion

                    if (quadrant != oldQuadrant)                                // Going to a new quadrant, otherwise there's no point in doing the movement :)
                    {
                        if ((quadrant == -1) || (nPlayersInQuadrant[quadrant] < 2))     // we need this check here
                        {
                            MovePlayerToQuadrant(playerNumber, oldQuadrant, quadrant, rewiredPlayerID);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool Pick(int rewiredPlayerID, int playerNumber)
        {
            if (lobby.ControllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Controller)
            {
                ControllerImage thisCtrlImage = lobby.ControllerImages[playerNumber];
                int quadrant = thisCtrlImage.QuadrantIndex;

                if (quadrant != -1 && !thisCtrlImage.HasLockedOnQuadrant && thisCtrlImage.IsInQuadrant && layouts[quadrant].Layout != ControllerLayout.LayoutStyle.Separate && lobby.TeamQuadrants[quadrant].AvailableForClientSelection)
                {
                    thisCtrlImage.Ready();

                    int playerSide = GetPlayerSideInQuadrantFromRewiredID(quadrant, rewiredPlayerID);
                    // Debug.Log("PLAYER SIDE: " + playerSide);
                    if (!PhotonNetwork.OfflineMode)
                    {
                        photonView.RPC("AddControllerToQuadrant", RpcTarget.AllBufferedViaServer, quadrant, rewiredPlayerID, PhotonNetwork.LocalPlayer.UserId, playerSide);
                        photonView.RPC("OnPickExtras", RpcTarget.AllBufferedViaServer, playerNumber, playerSide, rewiredPlayerID, quadrant, PhotonNetwork.LocalPlayer.UserId);      // An RPC call to SELF
                    }
                    else
                    {
                        layouts[quadrant].AddController(rewiredPlayerID, PhotonNetwork.LocalPlayer.UserId, lobby.TeamQuadrants[quadrant], playerSide);
                        OnPickExtras(playerNumber, playerSide, rewiredPlayerID, quadrant);
                    }
                    Debug.Log("<color=green>Controller added to player, side " + playerSide + ", with rewired ID " + rewiredPlayerID + " for quadrant " + quadrant + ", which now has layout type " + layouts[quadrant].Layout + ".</color>");
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Additional code for Pick()
        /// This is the code that would have been fine in Pick() itself, but since Pick has an RPC call to "AddControllerToQuadrant" in online modes, and it is all via server,
        /// we need to make sure that that RPC call function runs BEFORE we execute this code. So, we make this an additional RPC call to SELF.
        /// </summary>
        /// <param name="playerNumber"></param>
        /// <param name="playerSide"></param>
        /// <param name="rewiredPlayerID"></param>
        /// <param name="quadrant"></param>
        /// <param name="clientId"></param>
        [PunRPC]
        private void OnPickExtras(int playerNumber, int playerSide, int rewiredPlayerID, int quadrant, string clientId = "")
        {
            if (!PhotonNetwork.OfflineMode && clientId != PhotonNetwork.LocalPlayer.UserId) return;     // only executed on SELF

            //SetControllerLayout(quadrant);

            // This hack makes sure that the numTeamsReady is set properly when a second controller joins a team that had a single controller with both characters selected
            // Also, it deselects the character for THIS controller to be able to choose a character
            if (lobby.CharacterSelect.IsCharacterSelected((lobby.ControllerImages[playerNumber].QuadrantIndex * 2) + playerSide))
            {
                lobby.ControllerImages[playerNumber].quadrantMode = ControllerImage.QuadrantMode.Ready;     // this override is necessary (in case that other player is in number of players select mode)
                lobby.Cancel(rewiredPlayerID, playerNumber);
            }

            Color leftQuadrantCol = Color.white;
            Color rightQuadrantCol = Color.white;

            if (layouts[quadrant].Layout == ControllerLayout.LayoutStyle.Separate)
            {
                if (PhotonNetwork.OfflineMode || !lobby.IsTeamOnline(quadrant))     // if the whole team is offline
                {
                    int teammate = GetTeammateControllerImageIndex(quadrant, playerNumber);

                    // hide the number of players prompt because it's automatically 2 players now!
                    NumberOfPlayersPromptUI promptUI = lobby.ControllerImages[teammate].NumberOfPlayerPromptUI;
                    if (promptUI.Active)
                    {
                        promptUI.HidePrompt();
                        lobby.ControllerImages[teammate].quadrantMode = ControllerImage.QuadrantMode.Ready;
                    }

                    if (playerSide == 0)
                    {
                        leftQuadrantCol = colors[playerNumber];
                        rightQuadrantCol = colors[teammate];
                    }
                    else
                    {
                        leftQuadrantCol = colors[teammate];
                        rightQuadrantCol = colors[playerNumber];
                    }
                }
                else
                {
                    int teammate = lobby.GetTeammateOnlinePlayerIndex(quadrant);

                    // this RPC call will go to ALL people
                    // this is in the Lobby_NumberOfPlayersSelect.cs script
                    photonView.RPC("UpdateNumberOfPlayersSelectUIOnTeammate", RpcTarget.AllBuffered, teammate, lobby.GetTeammateClientID(quadrant));

                    if (playerSide == 0)
                    {
                        leftQuadrantCol = colors[playerNumber];
                        rightQuadrantCol = onlinePlayerColor;
                    }
                    else
                    {
                        rightQuadrantCol = colors[playerNumber];
                        leftQuadrantCol = onlinePlayerColor;
                    }
                }
            }
            else
            {
                leftQuadrantCol = colors[playerNumber];
                rightQuadrantCol = leftQuadrantCol;
            }

            lobby.TeamQuadrants[quadrant].SetQuadrantColor(leftQuadrantCol, rightQuadrantCol);

            lobby.ControllerImages[playerNumber].quadrantMode = ControllerImage.QuadrantMode.Character;

            // lobby.CharacterSelect.ActivateState(lobby.HasBeenHereAlready);
            // lobby.TeamQuadrants[quadrant].isQuadrantUsed = true;

            if (!lobby.TeamQuadrants[quadrant].IsQuadrantSelectedOnThisClient)
            {
                lobby.TeamQuadrants[quadrant].SetQuadrantSelected(true);
                SetQuadrantAvailabilities();
            }

            if (PhotonNetwork.OfflineMode)
            {
                OnPickQuadrantVisuals(playerNumber, quadrant, playerSide);
            }
            else
            {
                photonView.RPC("OnPickQuadrantVisuals", RpcTarget.AllBuffered, playerNumber, quadrant, playerSide, PhotonNetwork.LocalPlayer.UserId);
            }
        }

        public bool Cancel(int rewiredPlayerID, int playerNumber)
        {
            if (lobby.ControllerImages[playerNumber].quadrantMode == ControllerImage.QuadrantMode.Controller)
            {

                //Debug.Log("Cancel with id " + rewiredPlayerID);
                ControllerImage thisCtrlImage = lobby.ControllerImages[playerNumber];
                int quadrant = thisCtrlImage.QuadrantIndex;

                if (quadrant != -1 && thisCtrlImage.HasLockedOnQuadrant && layouts[quadrant].Layout != ControllerLayout.LayoutStyle.None)
                {
                    thisCtrlImage.Unready();

                    if (PhotonNetwork.OfflineMode)
                    {
                        OnCancelQuadrantVisuals(playerNumber);
                    }
                    else
                    {
                        photonView.RPC("OnCancelQuadrantVisuals", RpcTarget.AllBuffered, playerNumber, PhotonNetwork.LocalPlayer.UserId);
                    }
                    RemoveControllerFromQuadrant(quadrant, rewiredPlayerID);
                    return false;
                }
                // Are all controllers unready? If yes, move to previous screen
                else
                {
                    int numUnready = 0;
                    for (int i = 0; i < lobby.ControllerImages.Length; i++)
                    {
                        if (!lobby.ControllerImages[i].HasLockedOnQuadrant) numUnready++;
                    }

                    if (numUnready >= lobby.ControllerImages.Length)
                    {
                        for (int i = 0; i < lobby.TeamQuadrants.Length; i++)
                        {
                            lobby.TeamQuadrants[i].SetQuadrantSelected(false);
                            lobby.TeamQuadrants[i].SetQuadrantSelectedVisuals(false);
                        }
                        RewiredJoystickAssign.Instance.EndJoystickAssignment(false);
                        NetworkManager.Instance.Disconnect();
                        // ###@@@ hasBeenHereAlready = false;
                        return true;
                    }
                }
            }

            return false;
        }
        #endregion

        #region Controller Select Visuals
        /// <summary>
        /// Additional visuals code for when a controller successfully "picks" a quadrant
        /// </summary>
        /// <param name="playerNumber"></param>
        /// <param name="quadrant"></param>
        /// <param name="clientId"></param>
        [PunRPC]
        private void OnPickQuadrantVisuals(int playerNumber, int quadrant, int playerSide, string clientId = "")
        {
            if (PhotonNetwork.OfflineMode || clientId == PhotonNetwork.LocalPlayer.UserId)
            {
                ScaleControllerImage(playerNumber, lobby.ControllerImages[playerNumber].gameObject, controllerOriginalScale, 0.1f);
            }
            else
            {
                string identifyingKey = clientId + "|" + playerNumber;
                Color leftColor = onlinePlayerColor;
                Color rightColor = onlinePlayerColor;

                if (onlinePlayerIcons.ContainsKey(identifyingKey))
                {
                    ScaleOnlinePlayerIcon(identifyingKey, onlinePlayerIcons[identifyingKey].gameObject, controllerOriginalScale, 0.1f);
                }

                if (layouts[quadrant].Layout == ControllerLayout.LayoutStyle.Separate)
                {
                    if (playerSide == 0)
                    {
                        rightColor = lobby.TeamQuadrants[quadrant].RightColor;
                    }
                    else
                    {
                        leftColor = lobby.TeamQuadrants[quadrant].LeftColor;
                    }    
                }

                // Only set colors if not owned by this client, because otherwise, this is handled in Pick() itself
                lobby.TeamQuadrants[quadrant].SetQuadrantColor(leftColor, rightColor);
            }

            lobby.TeamQuadrants[quadrant].SetQuadrantSelectedVisuals(true);
            lobby.CharacterSelect.SetActiveUsedQuadrants(true);
        }

        /// <summary>
        /// Visuals code for when a controller successfully "cancels" out of a quadrant
        /// </summary>
        /// <param name="playerNumber"></param>
        /// <param name="clientId"></param>
        [PunRPC]
        private void OnCancelQuadrantVisuals(int playerNumber, string clientId = "")
        {
            if (PhotonNetwork.OfflineMode || clientId == PhotonNetwork.LocalPlayer.UserId)
            {
                    ScaleControllerImage(playerNumber, lobby.ControllerImages[playerNumber].gameObject, controllerOriginalScale * controllerImgHoverScaleMultiplier, 0.1f);
            }
            else
            {
                string identifyingKey = clientId + "|" + playerNumber;

                if (onlinePlayerIcons.ContainsKey(identifyingKey))
                {
                    ScaleOnlinePlayerIcon(identifyingKey, onlinePlayerIcons[identifyingKey].gameObject, controllerOriginalScale * controllerImgHoverScaleMultiplier, 0.1f);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="playerNum"></param>
        /// <param name="toMove"></param>
        /// <param name="finalPos"></param>
        /// <param name="rotateDir">0 for left side, -1 for center, 1 for right side</param>
        /// <param name="t"></param>
        private void LerpControllerImage(int playerNum, GameObject toMove, Vector3 finalPos, int rotateDir, float t)
        {
            if (controllerImageMoveCoroutines[playerNum] != null)
            {
                StopCoroutine(controllerImageMoveCoroutines[playerNum]);
            }

            Quaternion rot = controllerOriginalRotation;
            if (rotateDir == 0)
            {
                rot = Quaternion.AngleAxis(-controllerRotationAngle, Vector3.forward);
            }
            else if (rotateDir == 1)
            {
                rot = Quaternion.AngleAxis(controllerRotationAngle, Vector3.forward);
            }

            controllerImageMoveCoroutines[playerNum] = StartCoroutine(LerpUIGameObjectCoroutine(toMove, finalPos, rot, t));

            //Simple Move to without lerping for debugging:
            //toMove.transform.position = new Vector3(finalPos.x, toMove.transform.position.y, finalPos.z);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="identifyingKey"></param>
        /// <param name="toMove"></param>
        /// <param name="finalPos"></param>
        /// <param name="rotateDir">0 for left side, -1 for center, 1 for right side</param>
        /// <param name="t"></param>
        private void LerpOnlinePlayerIcon(string identifyingKey, GameObject toMove, Vector3 finalPos, int rotateDir, float t)
        {
            if (onlinePlayerIconMoveCoroutines[identifyingKey] != null)
            {
                StopCoroutine(onlinePlayerIconMoveCoroutines[identifyingKey]);
            }

            Quaternion rot = controllerOriginalRotation;
            if (rotateDir == 0)
            {
                rot = Quaternion.AngleAxis(-controllerRotationAngle, Vector3.forward);
            }
            else if (rotateDir == 1)
            {
                rot = Quaternion.AngleAxis(controllerRotationAngle, Vector3.forward);
            }
            onlinePlayerIconMoveCoroutines[identifyingKey] = StartCoroutine(LerpUIGameObjectCoroutine(toMove, finalPos, rot, t));
        }

        IEnumerator LerpUIGameObjectCoroutine(GameObject toMove, Vector3 finalPos, Quaternion finalRot, float t)
        {
            Vector3 startPos = toMove.transform.position;
            Quaternion startRot = toMove.transform.localRotation;
            float timeSinceStarted = 0f;
            float percentComplete = 0f;
            while (percentComplete < 1f)
            {
                timeSinceStarted += Time.deltaTime;
                percentComplete = timeSinceStarted / t;
                Vector3 pos = Vector3.Lerp(startPos, finalPos, percentComplete);
                Quaternion rot = Quaternion.Lerp(startRot, finalRot, percentComplete);
                toMove.transform.position = pos;
                toMove.transform.localRotation = rot;
                //Debug.Log(pos);

                yield return null;
            }

            yield return 0;
        }

        /// <summary>
        /// Scales a controller image back to hover scale
        /// </summary>
        /// <param name="playerNum"></param>
        public void ScaleControllerImageToHoverScale(int playerNum)
        {
            ScaleControllerImage(playerNum, lobby.ControllerImages[playerNum].gameObject, controllerOriginalScale * controllerImgHoverScaleMultiplier, 0.1f);
        }

        /// <summary>
        /// Scales an online player icon back to hover scale
        /// </summary>
        /// <param name="identifyingKey"></param>
        public void ScaleOnlinePlayerIconToHoverScale(string identifyingKey)
        {
            ScaleOnlinePlayerIcon(identifyingKey, onlinePlayerIcons[identifyingKey].gameObject, controllerOriginalScale * controllerImgHoverScaleMultiplier, 0.1f);
        }

        private void ScaleControllerImage(int playerNum, GameObject toScale, Vector3 finalScale, float t)
        {
            if (controllerImageScaleCoroutines[playerNum] != null)
            {
                StopCoroutine(controllerImageScaleCoroutines[playerNum]);
            }
            controllerImageScaleCoroutines[playerNum] = StartCoroutine(ScaleUIGameObjectCoroutine(toScale, finalScale, t));
        }

        private void ScaleOnlinePlayerIcon(string identifyingKey, GameObject toScale, Vector3 finalScale, float t)
        {
            if (onlinePlayerIconScaleCoroutines[identifyingKey] != null)
            {
                StopCoroutine(onlinePlayerIconScaleCoroutines[identifyingKey]);
            }
            onlinePlayerIconScaleCoroutines[identifyingKey] = StartCoroutine(ScaleUIGameObjectCoroutine(toScale, finalScale, t));
        }

        IEnumerator ScaleUIGameObjectCoroutine(GameObject toScale, Vector3 finalScale, float t)
        {
            Vector3 startPos = toScale.transform.position;
            Vector3 startScale = toScale.transform.localScale;
            float timeSinceStarted = 0f;
            float percentComplete = 0f;
            while (percentComplete < 1f)
            {
                timeSinceStarted += Time.deltaTime;
                percentComplete = timeSinceStarted / t;
                Vector3 scale = Vector3.Lerp(startScale, finalScale, percentComplete);
                toScale.transform.localScale = scale;

                yield return null;
            }

            yield return 0;
        }

        /// <summary>
        /// Sets the availability status of all quadrants
        /// </summary>
        private void SetQuadrantAvailabilities()
        {
            int quadrantsSelected = lobby.TeamQuadrants.Count(x => x.IsQuadrantSelectedOnThisClient);

            List<int> idxOfQuadrantsToMakeUnavailable = new List<int>();
            List<int> idxOfQuadrantsToMakeAvailable = new List<int>();

            if (quadrantsSelected == numberOfLocalTeamsAllowed)
            {
                idxOfQuadrantsToMakeUnavailable = lobby.TeamQuadrants.FindAllIndexOfConditionMet(x => !x.IsQuadrantSelectedOnThisClient);
            }
            else if (quadrantsSelected >= 0 && quadrantsSelected < numberOfLocalTeamsAllowed)
            {
                idxOfQuadrantsToMakeAvailable = lobby.TeamQuadrants.FindAllIndexOfConditionMet(x => !x.IsQuadrantSelectedOnThisClient);
            }


            foreach (int i in idxOfQuadrantsToMakeUnavailable)
            {
                // Debug.Log("Setting " + teamQuadrants[i].name + " unavailable.");
                SetQuadrantAvailable(i, false);
            }
            foreach (int i in idxOfQuadrantsToMakeAvailable)
            {
                // Debug.Log("Setting " + teamQuadrants[i].name + " available.");
                SetQuadrantAvailable(i, true);
            }
        }

        private void SetQuadrantAvailable(int quadrant, bool available)
        {
            Color c = available ? availableQuadrantColor : unavailableQuadrantColor;
            lobby.TeamQuadrants[quadrant].SetAvailable(available, c);

            if (available)
            {
                quadrantPointerArrows[quadrant].CrossFadeAlpha(1f, 0.2f, true);
            }
            else
            {
                //if (nPlayersInQuadrant[quadrant] < 2)
                //{
                //    quadrantPointerArrows[quadrant].CrossFadeAlpha(1f, 0.2f, true);
                //}
                //else
                //{
                    quadrantPointerArrows[quadrant].CrossFadeAlpha(0f, 0.2f, true);
                //}
            }
        }
        #endregion

        #region Online
        public bool CheckAllPlayersReady(out int quadrantsWithPlayers)
        {
            quadrantsWithPlayers = 0;
            for (int i = 0; i < layouts.Length; i++)
            {
                if (nPlayersInQuadrant[i] > 0)
                {
                    quadrantsWithPlayers++;

                    if (layouts[i].Layout == ControllerLayout.LayoutStyle.None ||
                        (layouts[i].Layout == ControllerLayout.LayoutStyle.Shared && nPlayersInQuadrant[i] == 2))
                    {
                        Debug.Log("a team isn't ready");
                        return false;
                    }
                }
            }

            return true;
        }
        /// <summary>
        /// Syncs the layouts and locations of players and their icons across all clients
        /// </summary>
        public void SyncPlayersAcrossClients()
        {
            // For shared controllers, there are two rewired IDs. We only want to use one of those, the SHARED ID. To ignore the other one, we make a list and add all ignorable IDs to it.
            List<int> idsToIgnore = new List<int>();

            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i].Layout == ControllerLayout.LayoutStyle.Shared)
                {
                    int sharedRewiredID = layouts[i].SharedControllerOwnerID;
                    int otherID = layouts[i].RewiredIDs[0] == sharedRewiredID ? layouts[i].RewiredIDs[1] : layouts[i].RewiredIDs[0];
                    idsToIgnore.Add(otherID);

                    // Sync layouts if master client
                    // Remove the layouts LOCALLY, then SET the layouts through the NETWORK on ALL clients, INCLUDING this one
                    if (PhotonNetwork.IsMasterClient)
                    {
                        layouts[i].RemoveController(sharedRewiredID, PhotonNetwork.LocalPlayer.UserId, lobby.TeamQuadrants[i], 0);
                        photonView.RPC("AddControllerToQuadrant", RpcTarget.AllBufferedViaServer, i, sharedRewiredID, PhotonNetwork.LocalPlayer.UserId, 0);
                        // Debug.Log("Setting master's layout on other clients for quadrant " + i);
                    }
                }
                else if (layouts[i].Layout == ControllerLayout.LayoutStyle.Separate)
                {
                    // Set layouts if master client
                    // Remove the layouts LOCALLY, then SET the layouts through the NETWORK on ALL clients, INCLUDING this one
                    if (PhotonNetwork.IsMasterClient)
                    {
                        int rewiredID1 = layouts[i].RewiredIDs[0];
                        int rewiredID2 = layouts[i].RewiredIDs[1];

                        layouts[i].RemoveController(rewiredID1, PhotonNetwork.LocalPlayer.UserId, lobby.TeamQuadrants[i], 0);
                        layouts[i].RemoveController(rewiredID2, PhotonNetwork.LocalPlayer.UserId, lobby.TeamQuadrants[i], 1);
                        photonView.RPC("AddControllerToQuadrant", RpcTarget.AllBufferedViaServer, i, rewiredID1, PhotonNetwork.LocalPlayer.UserId, 0);
                        photonView.RPC("AddControllerToQuadrant", RpcTarget.AllBufferedViaServer, i, rewiredID2, PhotonNetwork.LocalPlayer.UserId, 1);
                        // Debug.Log("Setting master's layout on other clients for quadrant " + i);
                    }
                }
            }

            // Now, we get the IDs that we need to iterate on for setting proper player icon locations
            List<int> idsToIterateOn = RewiredJoystickAssign.Instance.UsedIDs.Except(idsToIgnore).ToList();

            // Set proper player icon locations
            for (int i = 0; i < idsToIterateOn.Count; i++)
            {
                int rewiredID = idsToIterateOn[i];
                int controllerIndex = RewiredJoystickAssign.Instance.RewiredIDCorrespondingControllerIndices[rewiredID];

                // Debug.Log("setting rewired ID " + rewiredID + " for controllerIndex " + controllerIndex);
                photonView.RPC("SetThisClientPlayerOnOthers", RpcTarget.OthersBuffered, PhotonNetwork.LocalPlayer.UserId, controllerIndex);

                // Only set locations of player icons on other clients if this is the master client
                // Every other client has its controller images reset to center, so there's no need to update it yet
                if (PhotonNetwork.IsMasterClient)
                {
                    //Debug.Log("Setting location on other clients");
                    photonView.RPC("SetPlayerIconLocation", RpcTarget.OthersBuffered, PhotonNetwork.LocalPlayer.UserId, controllerIndex, lobby.ControllerImages[i].QuadrantIndex, lobby.ControllerImages[i].transform.position);
                }
            }

            if (PhotonNetwork.IsMasterClient)
            {
                for (int i = 0; i < lobby.TeamQuadrants.Length; i++)
                {
                    lobby.SetTeamReady(i, lobby.TeamQuadrants[i].TeamReady);
                }
            }
        }

        /// <summary>
        /// PunRPC: Sets the player data for this client's controller on other clients
        /// Only use RPCTarget.OthersBuffered with this method. Never call this on the owner client!
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="playerNum"></param>
        [PunRPC]
        private void SetThisClientPlayerOnOthers(string clientID, int playerNum)
        {
            // Increment online player
            int onlinePlayerNum = onlinePlayersCount;
            onlinePlayersCount++;

            string identifyingKey = clientID + "|" + playerNum.ToString();
            if (!onlinePlayerIcons.ContainsKey(identifyingKey))
            {
                MenuData.AddIdentifyingKey(identifyingKey);

                GameObject OPIGO = Instantiate(onlinePlayerIconPrefab, centerPosition.position, centerPosition.rotation, centerPosition);
                OnlinePlayerIcon OPI = OPIGO.GetComponent<OnlinePlayerIcon>();
                OPI.SetPlayerIcon(clientID, onlinePlayerNum, playerNum);
                onlinePlayerIcons.Add(identifyingKey, OPI);
                OPI.Icon.canvasRenderer.SetAlpha(1);

                // Image movement coroutine
                onlinePlayerIconMoveCoroutines.Add(identifyingKey, null);
                onlinePlayerIconScaleCoroutines.Add(identifyingKey, null);

                Debug.Log("New player joined. Adding " + identifyingKey + " to dictionary.");
            }
            else
            {
                Debug.Log("<color=red>Something's wrong. A player with identifying ID " + identifyingKey + " already exists.");
            }
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
            if (!PhotonNetwork.IsMasterClient && !hasResetLobbyAfterJoiningRoom)
            {
                while (!hasResetLobbyAfterJoiningRoom)
                {
                    //Debug.Log("resetting values");
                    yield return null;
                }

                //Debug.Log("done resetting values");
            }

            // Debug.Log("<color=green>Setting location of player " + clientID + "|" + playerNum + " to quadrant " + quadrant + "</color>");

            // REFACTOR!
            string identifyingKey = clientID + "|" + playerNum.ToString();
            if (quadrant != -1)
            {
                if (nPlayersInQuadrant[quadrant] < 2)
                {
                    int pIndex = nPlayersInQuadrant[quadrant];
                    onlinePlayerIcons[identifyingKey].transform.position = pos;
                    lobby.TeamQuadrants[quadrant].IsOnlinePlayer[pIndex] = true;
                    lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[pIndex] = playerNum;
                    lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[pIndex] = clientID;
                    IncrementPlayersInQuadrant(1, quadrant);
                }
            }
            else
                onlinePlayerIcons[identifyingKey].transform.position = centerPosition.position;

            yield return null;
        }

        [PunRPC]
        private void AddControllerToQuadrant(int quadrant, int rewiredPlayerID, string clientID, int playerSide)
        {
            layouts[quadrant].AddController(rewiredPlayerID, clientID, lobby.TeamQuadrants[quadrant], playerSide);
        }

        [PunRPC]
        private void RemoveControllerFromQuadrantOnlineRPC(int quadrant, int rewiredPlayerID, string clientID, int playerSide)
        {
            layouts[quadrant].RemoveController(rewiredPlayerID, clientID, lobby.TeamQuadrants[quadrant], playerSide);
        }

        [PunRPC]
        private void SetControllerLayoutRPC(int quadrantNum, int enumInt)
        {

            layouts[quadrantNum].Layout = (ControllerLayout.LayoutStyle)enumInt;
            // Debug.Log(layouts[quadrantNum].Layout);
        }

        private void RemoveOnlinePlayer(string clientID, int playerNum, int quadrant)
        {
            string identifyingKey = clientID + "|" + playerNum.ToString();
            if (onlinePlayerIcons.ContainsKey(identifyingKey))
            {
                onlinePlayersCount--;
                if (onlinePlayersCount < 0) onlinePlayersCount = 0;

                MenuData.RemoveIdentifyingKey(identifyingKey);

                OnlinePlayerIcon OPI = onlinePlayerIcons[identifyingKey];
                GameObject OPIGO = OPI.gameObject;
                onlinePlayerIcons.Remove(identifyingKey);
                Destroy(OPIGO);

                //Image movement coroutine
                onlinePlayerIconMoveCoroutines.Remove(identifyingKey);
                onlinePlayerIconScaleCoroutines.Remove(identifyingKey);

                Debug.Log("Player disconnected on other client. Removing " + identifyingKey + " from dictionary.");
            }
            else
            {
                Debug.Log("<color=red>Something's wrong. A player with identifying ID " + identifyingKey + " does not exist, but it should.");
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Moves a player to a quadrant
        /// </summary>
        /// <param name="playerNum">Player number of the player being moved</param>
        /// <param name="oldQuadrant">Quadrant from which the player is moving</param>
        /// <param name="newQuadrant">Quadrant where the player is going (pass -2 if the player controller got disconnected)</param>
        /// <param name="rewiredID">Rewired ID of the player controller that is moving</param>
        private void MovePlayerToQuadrant(int playerNum, int oldQuadrant, int newQuadrant, int rewiredID)
        {
            if (PhotonNetwork.OfflineMode)
            {
                MovePlayerToQuadrantLogic(playerNum, oldQuadrant, newQuadrant, rewiredID);
            }
            else
            {
                string myClientID = PhotonNetwork.LocalPlayer.UserId;
                photonView.RPC("MovePlayerToQuadrantLogic", RpcTarget.AllBufferedViaServer, playerNum, oldQuadrant, newQuadrant, rewiredID, myClientID);
            }                          
        }

        [PunRPC]
        private void MovePlayerToQuadrantLogic(int playerNum, int oldQuadrant, int newQuadrant, int rewiredID, string clientID = "")
        {

            // First, check if the player being moved is a local player or a player on another client
            bool isOnline = !PhotonNetwork.OfflineMode;
            bool isOwnedByThisClient = true;

            if (isOnline)
            {
                isOwnedByThisClient = PhotonNetwork.LocalPlayer.UserId.Equals(clientID);
            }

            ControllerImage ctrlImage = null;
            OnlinePlayerIcon onlinePlayerIcon = null;
            string identifyingKey = "";

            float t = 0.3f;

            if (isOwnedByThisClient) // this is the client which owns the player
            {
                ctrlImage = lobby.ControllerImages[playerNum];
            }
            else
            {
                identifyingKey = clientID + "|" + playerNum.ToString();
                onlinePlayerIcon = onlinePlayerIcons[identifyingKey];
            }

            if (newQuadrant != oldQuadrant)
            {
                // Decrease number of players from oldQuadrant if it wasn't center
                if (oldQuadrant >= 0 && oldQuadrant < 4)
                {
                    DecrementPlayersFromQuadrant(1, oldQuadrant);
                }
                // Increase number of players in newQuadrant if it isn't center
                if (newQuadrant >= 0 && newQuadrant < 4)
                {
                    IncrementPlayersInQuadrant(1, newQuadrant);
                }

                if (isOwnedByThisClient)
                {
                    int quadrantToSet = newQuadrant;
                    if (newQuadrant == -2) quadrantToSet = -1;

                    ctrlImage.SetQuadrant(quadrantToSet);
                }

                #region New Quadrant
                // If new quadrant is -2, that means the controller got disconnected
                if (newQuadrant == -2)
                {
                    if (isOwnedByThisClient)
                    {
                        ctrlImage.gameObject.SetActive(false);

                        ctrlImage.transform.position = controllerOriginalPositions[playerNum];
                        ctrlImage.transform.localScale = controllerOriginalScale;
                        ctrlImage.transform.localRotation = controllerOriginalRotation;
                        //lobby.ControllerImages[playerNumber].FullController.canvasRenderer.SetColor(availableControllerColor);
                        //lobby.ControllerImages[playerNumber].LeftHalf.canvasRenderer.SetColor(availableControllerColor);
                        //lobby.ControllerImages[playerNumber].RightHalf.canvasRenderer.SetColor(availableControllerColor);
                        ctrlImage.FullController.canvasRenderer.SetAlpha(1);
                        ctrlImage.LeftHalf.canvasRenderer.SetAlpha(0);
                        ctrlImage.RightHalf.canvasRenderer.SetAlpha(0);
                        ctrlImage.LeftArrow.canvasRenderer.SetAlpha(1f);
                        ctrlImage.RightArrow.canvasRenderer.SetAlpha(1f);

                        ctrlImage.Unready();

                        if (oldQuadrant != -1) // if wasn't in the center
                        {
                            bool removeControllerFromQuadrant = true;

                            // Was the layout shared?
                            if (layouts[oldQuadrant].Layout == ControllerLayout.LayoutStyle.Shared)
                            {
                                // Were there 2 controllers in the quadrant (1 locked and 1 hovering)
                                //if (nPlayersInQuadrant[oldQuadrant] == 2)
                                //{
                                    // Was the disconnected controller the locked one or not?
                                    if (layouts[oldQuadrant].SharedControllerOwnerID != rewiredID)
                                    {
                                        removeControllerFromQuadrant = false;       // if it wasn't locked, we don't need to remove it from the quadrant!
                                    }
                                //}
                            }

                            if (removeControllerFromQuadrant)
                            {
                                int playerSide = GetPlayerSideInQuadrantFromRewiredID(oldQuadrant, rewiredID);
                                RemoveControllerFromQuadrant(oldQuadrant, rewiredID, playerSide);
                            }
                        }
                    }
                    else
                    {
                        RemoveOnlinePlayer(clientID, playerNum, oldQuadrant);
                    }
                }
                // if new quadrant is center
                else if (newQuadrant == -1)
                {
                    #region Visuals
                    if (isOwnedByThisClient)
                    {
                        ctrlImage.FullController.CrossFadeAlpha(1, t, true);
                        ctrlImage.LeftHalf.CrossFadeAlpha(0, t, true);
                        ctrlImage.RightHalf.CrossFadeAlpha(0, t, true);

                        LerpControllerImage(playerNum, ctrlImage.gameObject, controllerOriginalPositions[playerNum], -1, 0.1f);
                        ScaleControllerImage(playerNum, ctrlImage.gameObject, controllerOriginalScale, 0.1f);
                    }
                    else
                    {
                        LerpOnlinePlayerIcon(identifyingKey, onlinePlayerIcon.gameObject, controllerOriginalPositions[0], -1, 0.1f);
                        ScaleOnlinePlayerIcon(identifyingKey, onlinePlayerIcon.gameObject, controllerOriginalScale, 0.1f);
                    }
                    #endregion
                }
                // If the new quadrant has space left and is available for client selection
                else if (nPlayersInQuadrant[newQuadrant] <= 2 && lobby.TeamQuadrants[newQuadrant].AvailableForClientSelection)
                {
                    int playerSide = 0;     // the side of the team where the player is moving: 0 for left, 1 for right

                    if (nPlayersInQuadrant[newQuadrant] == 1)
                    {
                        playerSide = 0;
                    }
                    else if (nPlayersInQuadrant[newQuadrant] == 2)
                    {
                        playerSide = 1;
                    }

                    // Update online player data
                    lobby.TeamQuadrants[newQuadrant].IsOnlinePlayer[playerSide] = !isOwnedByThisClient;
                    lobby.TeamQuadrants[newQuadrant].OnlinePlayerNumberOnOwner[playerSide] = isOwnedByThisClient ? -1 : playerNum;
                    lobby.TeamQuadrants[newQuadrant].OnlinePlayerClientIDs[playerSide] = isOwnedByThisClient ? "" : clientID;

                    // Update local data
                    controllerImageIndices[newQuadrant][playerSide] = isOwnedByThisClient ? playerNum : -1;
                    playerQuadrantRewiredIDsBeforeSelection[newQuadrant][playerSide] = isOwnedByThisClient ? rewiredID : -1;

                    #region Visuals
                    // Update hover prompts (only if owned by this client)
                    if (isOwnedByThisClient)
                    {
                        lobby.TeamQuadrants[newQuadrant].PromptOnHover(playerSide, rewiredID);
                    }

                    // nPlayersInQuadrant is either 1 or 2 (it has been increased before we get to this point in the code)
                    if (nPlayersInQuadrant[newQuadrant] == 1)
                    {
                        Vector3 centerPos = (lobby.TeamQuadrants[newQuadrant].TeamCharacterPositions[0].position + lobby.TeamQuadrants[newQuadrant].TeamCharacterPositions[1].position) / 2;

                        if (isOwnedByThisClient)
                        {
                            if (ctrlImage.ControllerType != ControllerType.Keyboard && ctrlImage.ControllerType != ControllerType.Mouse)
                            {
                                ctrlImage.FullController.CrossFadeAlpha(0, t, true);
                                ctrlImage.LeftHalf.CrossFadeAlpha(1, t, true);
                                ctrlImage.RightHalf.CrossFadeAlpha(1, t, true);
                            }
                            
                            // Handle motion
                            LerpControllerImage(playerNum, ctrlImage.gameObject, centerPos, -1, 0.1f);
                            ScaleControllerImage(playerNum, ctrlImage.gameObject, controllerOriginalScale * controllerImgHoverScaleMultiplier, 0.1f);
                            
                        }
                        else
                        {
                            LerpOnlinePlayerIcon(identifyingKey, onlinePlayerIcon.gameObject, centerPos, -1, 0.1f);
                            ScaleOnlinePlayerIcon(identifyingKey, onlinePlayerIcon.gameObject, controllerOriginalScale * controllerImgHoverScaleMultiplier, 0.1f);
                        }
                    }
                    else if (nPlayersInQuadrant[newQuadrant] == 2)
                    {
                        if (isOwnedByThisClient)
                        {
                            ctrlImage.FullController.CrossFadeAlpha(1, t, true);
                            ctrlImage.LeftHalf.CrossFadeAlpha(0, t, true);
                            ctrlImage.RightHalf.CrossFadeAlpha(0, t, true);

                            // Handle motion
                            LerpControllerImage(playerNum, ctrlImage.gameObject, lobby.TeamQuadrants[newQuadrant].TeamCharacterPositions[1].position, playerSide, 0.1f);
                            ScaleControllerImage(playerNum, ctrlImage.gameObject, controllerOriginalScale * controllerImgHoverScaleMultiplier, 0.1f);
                            
                        }
                        else
                        {
                            LerpOnlinePlayerIcon(identifyingKey, onlinePlayerIcon.gameObject, lobby.TeamQuadrants[newQuadrant].TeamCharacterPositions[1].position, playerSide, 0.1f);
                            ScaleOnlinePlayerIcon(identifyingKey, onlinePlayerIcon.gameObject, controllerOriginalScale * controllerImgHoverScaleMultiplier, 0.1f);
                        }

                        #region Teammate Visuals
                        // If the current teammate is on this client
                        if (!lobby.IsPlayerOnSideOnline(newQuadrant, 0))        // the side is 0 because the teammate in this case is always the player on the left
                        {
                            int teammateControllerIndex = controllerImageIndices[newQuadrant][0];
                            
                            ControllerImage teammate = lobby.ControllerImages[teammateControllerIndex];
                            teammate.FullController.CrossFadeAlpha(1, t, true);
                            teammate.LeftHalf.CrossFadeAlpha(0, t, true);
                            teammate.RightHalf.CrossFadeAlpha(0, t, true);

                            LerpControllerImage(teammateControllerIndex, teammate.gameObject, lobby.TeamQuadrants[newQuadrant].TeamCharacterPositions[0].position,  1 - playerSide, 0.25f);
                        }
                        // If the teammate is on a different client and this is online mode
                        else if (isOnline && lobby.IsPlayerOnSideOnline(newQuadrant, 0))
                        {
                            // Debug.Log("teammate is online");

                            // Move teammate
                            int pNumOnOwner = lobby.TeamQuadrants[newQuadrant].OnlinePlayerNumberOnOwner[0];
                            string teammateClientID = lobby.TeamQuadrants[newQuadrant].OnlinePlayerClientIDs[0];

                            string teammateIdentifyingKey = teammateClientID + "|" + pNumOnOwner.ToString();
                            GameObject toMove = onlinePlayerIcons[teammateIdentifyingKey].gameObject;

                            LerpOnlinePlayerIcon(teammateIdentifyingKey, toMove, lobby.TeamQuadrants[newQuadrant].TeamCharacterPositions[0].position, 1 - playerSide, 0.25f);
                        }
                        #endregion
                    }
                    #endregion
                }
                #endregion

                #region Old Quadrant
                // If old quadrant is not center
                if (oldQuadrant != -1)
                {
                    // nPlayersInQuadrant is either 0 or 1 in old quadrant (it has been decreased before we get to this point in the code)
                    if (nPlayersInQuadrant[oldQuadrant] == 1)
                    {
                        #region Visuals
                        Vector3 centerPos = (lobby.TeamQuadrants[oldQuadrant].TeamCharacterPositions[0].position + lobby.TeamQuadrants[oldQuadrant].TeamCharacterPositions[1].position) / 2f;

                        int teammateSide = 0;
                        int teammateControllerIndex = -1;

                        string teammateIdentifyingKey = string.Empty;

                        if (isOwnedByThisClient)
                        {
                            teammateSide = GetTeammatePlayerSideInQuadrantFromControllerIndex(oldQuadrant, playerNum);
                        }
                        else
                        {
                            teammateSide = GetTeammatePlayerSideInQuadrantFromClientID(oldQuadrant, clientID, playerNum);
                        }

                        if (lobby.TeamQuadrants[oldQuadrant].IsOnlinePlayer[teammateSide])
                        {
                            teammateIdentifyingKey = lobby.TeamQuadrants[oldQuadrant].OnlinePlayerClientIDs[teammateSide] + "|" + lobby.TeamQuadrants[oldQuadrant].OnlinePlayerNumberOnOwner[teammateSide];
                            //Debug.Log(teammateIdentifyingKey);
                        }
                        else
                        {
                            teammateControllerIndex = controllerImageIndices[oldQuadrant][teammateSide];
                            //Debug.Log(teammateControllerIndex);
                        }
                        

                        if (teammateControllerIndex > -1)
                        {
                            ControllerImage teammate = lobby.ControllerImages[teammateControllerIndex];

                            if (teammate.ControllerType != ControllerType.Keyboard && teammate.ControllerType != ControllerType.Mouse)
                            {
                                teammate.FullController.CrossFadeAlpha(0, t, true);
                                teammate.LeftHalf.CrossFadeAlpha(1, t, true);
                                teammate.RightHalf.CrossFadeAlpha(1, t, true);
                            }

                            LerpControllerImage(teammateControllerIndex, teammate.gameObject, centerPos, -1, 0.25f);
                        }

                        else if (!string.IsNullOrEmpty(teammateIdentifyingKey))
                        {
                            LerpOnlinePlayerIcon(teammateIdentifyingKey, onlinePlayerIcons[teammateIdentifyingKey].gameObject, centerPos, -1, 0.25f);
                        }
                        #endregion
                    }

                    // Update data after figuring out which player left the old quadrant
                    bool leftPlayerLeft = false;
                    bool rightPlayerLeft = false;

                    if (isOwnedByThisClient)
                    {
                        if (controllerImageIndices[oldQuadrant][0] == playerNum) leftPlayerLeft = true;
                        else if (controllerImageIndices[oldQuadrant][1] == playerNum) rightPlayerLeft = true;
                    }
                    else
                    {
                        int playerSide = GetPlayerSideInQuadrantFromClientID(oldQuadrant, clientID, playerNum);

                        if (playerSide == 0) leftPlayerLeft = true;
                        else if (playerSide == 1) rightPlayerLeft = true;
                    }
                    // Push right data to the left (left teammate leaves, right one becomes left teammate)
                    // Empty data on the right
                    if (leftPlayerLeft)
                    {
                        controllerImageIndices[oldQuadrant][0] = controllerImageIndices[oldQuadrant][1];
                        controllerImageIndices[oldQuadrant][1] = -1;

                        playerQuadrantRewiredIDsBeforeSelection[oldQuadrant][0] = playerQuadrantRewiredIDsBeforeSelection[oldQuadrant][1];
                        playerQuadrantRewiredIDsBeforeSelection[oldQuadrant][1] = -1;

                        lobby.TeamQuadrants[oldQuadrant].IsOnlinePlayer[0] = lobby.TeamQuadrants[oldQuadrant].IsOnlinePlayer[1];
                        lobby.TeamQuadrants[oldQuadrant].OnlinePlayerClientIDs[0] = lobby.TeamQuadrants[oldQuadrant].OnlinePlayerClientIDs[1];
                        lobby.TeamQuadrants[oldQuadrant].OnlinePlayerNumberOnOwner[0] = lobby.TeamQuadrants[oldQuadrant].OnlinePlayerNumberOnOwner[1];

                        lobby.TeamQuadrants[oldQuadrant].IsOnlinePlayer[1] = false;
                        lobby.TeamQuadrants[oldQuadrant].OnlinePlayerClientIDs[1] = "";
                        lobby.TeamQuadrants[oldQuadrant].OnlinePlayerNumberOnOwner[1] = -1;

                        #region Visuals
                        bool lockedIn = false, anyPlayerStillInTeam = false;
                        if (nPlayersInQuadrant[oldQuadrant] == 1)       // still another player in the quadrant?
                        {
                            if (layouts[oldQuadrant].Layout == ControllerLayout.LayoutStyle.Shared)
                            {
                                lockedIn = true;
                            }

                            anyPlayerStillInTeam = true;
                        }

                        lobby.TeamQuadrants[oldQuadrant].PromptOnLeaveHover(0, rewiredID, anyPlayerStillInTeam, lockedIn);
                        #endregion
                    }
                    // Empty data on the right (right teammate leaves)
                    else if (rightPlayerLeft)
                    {
                        controllerImageIndices[oldQuadrant][1] = -1;
                        playerQuadrantRewiredIDsBeforeSelection[oldQuadrant][1] = -1;

                        lobby.TeamQuadrants[oldQuadrant].IsOnlinePlayer[1] = false;
                        lobby.TeamQuadrants[oldQuadrant].OnlinePlayerClientIDs[1] = "";
                        lobby.TeamQuadrants[oldQuadrant].OnlinePlayerNumberOnOwner[1] = -1;

                        #region Visuals
                        lobby.TeamQuadrants[oldQuadrant].PromptOnLeaveHover(1, rewiredID);
                        #endregion
                    }

                }
                #endregion

                Debug.Log("<color=green>=============== nPlayersInQuadrant ===============</color>");
                Debug.Log(nPlayersInQuadrant[0] + "          ,          " + nPlayersInQuadrant[1]);
                Debug.Log(nPlayersInQuadrant[2] + "          ,          " + nPlayersInQuadrant[3]);

                Debug.Log("<color=green>=============== playerQuadrantRewiredIDsBeforeSelection ===============</color>");
                Debug.Log("[" + playerQuadrantRewiredIDsBeforeSelection[0][0] + " , " + playerQuadrantRewiredIDsBeforeSelection[0][1] + "]          ,          [" + playerQuadrantRewiredIDsBeforeSelection[1][0] + " , " + playerQuadrantRewiredIDsBeforeSelection[1][1] + "]");
                Debug.Log("[" + playerQuadrantRewiredIDsBeforeSelection[2][0] + " , " + playerQuadrantRewiredIDsBeforeSelection[2][1] + "]          ,          [" + playerQuadrantRewiredIDsBeforeSelection[3][0] + " , " + playerQuadrantRewiredIDsBeforeSelection[3][1] + "]");

                Debug.Log("<color=green>=============== controllerImageIndices ===============</color>");
                Debug.Log("[" + controllerImageIndices[0][0] + " , " + controllerImageIndices[0][1] + "]          ,          [" + controllerImageIndices[1][0] + " , " + controllerImageIndices[1][1] + "]");
                Debug.Log("[" + controllerImageIndices[2][0] + " , " + controllerImageIndices[2][1] + "]          ,          [" + controllerImageIndices[3][0] + " , " + controllerImageIndices[3][1] + "]");
            }
        }

        /// <summary>
        /// Used to remove a controller from a quadrant layout.
        /// The optional parameter optionalPlayerSide is only passed when a controller is disconnected, because getting player side in that case is not possible.
        /// </summary>
        /// <param name="quadrant"></param>
        /// <param name="rewiredPlayerID"></param>
        /// <param name="optionalPlayerSide"></param>
        public void RemoveControllerFromQuadrant(int quadrant, int rewiredPlayerID, int optionalPlayerSide = -1)
        {
            int playerSide = GetPlayerSideInQuadrantFromRewiredID(quadrant, rewiredPlayerID);
            if (optionalPlayerSide != -1) playerSide = optionalPlayerSide;

            // This happens when the ID that hits CANCEL/BACK is the ID not in the playerQuadrantRewiredIDsBeforeSelection list - which is because the other half of the controller (the other rewired ID) is in that list
            if (playerSide == -1 && layouts[quadrant].Layout == ControllerLayout.LayoutStyle.Shared)
            {
                int idx = -1;
                if (layouts[quadrant].RewiredIDs[0] == rewiredPlayerID)
                {
                    idx = 1;
                }
                else if (layouts[quadrant].RewiredIDs[1] == rewiredPlayerID)
                {
                    idx = 0;
                }
                int actualPlayerSide = GetPlayerSideInQuadrantFromRewiredID(quadrant, layouts[quadrant].RewiredIDs[idx]);
                playerSide = actualPlayerSide;

                Debug.Log("ACTUAL PLAYER SIDE: " + playerSide);
                int rewiredIDSide = layouts[quadrant].RewiredIDs[playerSide] == rewiredPlayerID ? playerSide : 1 - playerSide;
                playerQuadrantRewiredIDsBeforeSelection[quadrant][playerSide] = layouts[quadrant].RewiredIDs[rewiredIDSide];

                Debug.Log("Actual player side = " + actualPlayerSide + " , playerQuadrantRewiredIDsBeforeSelection[" + quadrant + "][" + playerSide + "] = " + playerQuadrantRewiredIDsBeforeSelection[quadrant][playerSide]);
            }

            if (playerSide != -1)
            {
                bool onlineTeammate = layouts[quadrant].Layout == ControllerLayout.LayoutStyle.Separate && !PhotonNetwork.OfflineMode && lobby.IsTeamOnline(quadrant);

                if (layouts[quadrant].Layout == ControllerLayout.LayoutStyle.Shared)
                {                    
                    lobby.SetTeamReady(quadrant, false);
                }

                if (!PhotonNetwork.OfflineMode)
                {
                    photonView.RPC("RemoveControllerFromQuadrantOnlineRPC", RpcTarget.AllBufferedViaServer, quadrant, rewiredPlayerID, PhotonNetwork.LocalPlayer.UserId, playerSide);
                    photonView.RPC("RemoveControllerFromQuadrantExtras", RpcTarget.AllBufferedViaServer, quadrant, onlineTeammate, PhotonNetwork.LocalPlayer.UserId);       // execute on self AFTER the previous RPC
                }
                else
                {
                    layouts[quadrant].RemoveController(rewiredPlayerID, PhotonNetwork.LocalPlayer.UserId, lobby.TeamQuadrants[quadrant], playerSide);
                    RemoveControllerFromQuadrantExtras(quadrant, onlineTeammate);
                }

                Debug.Log("<color=green>Controller for player with rewired ID, " + rewiredPlayerID + ", side " + playerSide + " removed from quadrant " + quadrant + ", which now has layout type " + layouts[quadrant].Layout + ".</color>");
            }
        }

        /// <summary>
        /// Extra code for "RemoveControllerFromQuadrant" that would have been fine, but since the code right before this function's code is "ViaServer", we need to wait until we're sure all of that has executed
        /// </summary>
        /// <param name="quadrant"></param>
        [PunRPC]
        private void RemoveControllerFromQuadrantExtras(int quadrant, bool onlineTeammate, string clientId = "")
        {
            if (!PhotonNetwork.OfflineMode && clientId != PhotonNetwork.LocalPlayer.UserId) return;

            if (lobby.TeamQuadrants[quadrant].IsQuadrantSelectedOnThisClient &&
                (layouts[quadrant].Layout == ControllerLayout.LayoutStyle.None || onlineTeammate))
            {
                lobby.TeamQuadrants[quadrant].SetQuadrantSelected(false);
                SetQuadrantAvailabilities();
            }
            if (layouts[quadrant].Layout == ControllerLayout.LayoutStyle.None)
            {
                lobby.TeamQuadrants[quadrant].SetQuadrantSelectedVisuals(false);
            }
        }

        /// <summary>
        /// Use this to add numToAdd players to nPlayersInQuadrant[quadrant]
        /// </summary>
        /// <param name="numToAdd"></param>
        /// <param name="quadrant"></param>
        private void IncrementPlayersInQuadrant(int numToAdd, int quadrant)
        {
            nPlayersInQuadrant[quadrant] += numToAdd;
            if (nPlayersInQuadrant[quadrant] >= 2)
            {
                nPlayersInQuadrant[quadrant] = 2;
                quadrantPointerArrows[quadrant].CrossFadeAlpha(0f, 0.2f, true);
            }
        }

        /// <summary>
        /// Use this to subtract numToSubtract players from nPlayersInQuadrant[quadrant]
        /// </summary>
        /// <param name="numToSubtract"></param>
        /// <param name="quadrant"></param>
        private void DecrementPlayersFromQuadrant(int numToSubtract, int quadrant)
        {
            if (nPlayersInQuadrant[quadrant] == 2 && numToSubtract > 0)
            {
                quadrantPointerArrows[quadrant].CrossFadeAlpha(1f, 0.2f, true);
            }
            nPlayersInQuadrant[quadrant] -= numToSubtract;
            if (nPlayersInQuadrant[quadrant] < 0)
            {
                nPlayersInQuadrant[quadrant] = 0;
            }
        }

        /// <summary>
        /// Returns the controller image index of the teammate of another controller image index if the given index is valid
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="controllerImgIndex">Controller image index of the player whose teammate is required</param>
        /// <returns></returns>
        public int GetTeammateControllerImageIndex(int quadrant, int controllerImgIndex)
        {
            if (controllerImageIndices[quadrant][0] == controllerImgIndex) return controllerImageIndices[quadrant][1];
            else if (controllerImageIndices[quadrant][1] == controllerImgIndex) return controllerImageIndices[quadrant][0];

            return -1;
        }

        /// <summary>
        /// Returns the side of the player (index 0 or 1) for a given rewired ID.
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="rewiredID">Rewired ID for which side is required</param>
        /// <returns></returns>
        public int GetPlayerSideInQuadrantFromRewiredID(int quadrant, int rewiredID)
        {
            if (quadrant < 0 || quadrant > 4)
            {
                Debug.LogWarning("Quadrant is -1, returing -1 player side");
                return -1;
            }
            if (playerQuadrantRewiredIDsBeforeSelection[quadrant][0] == rewiredID) return 0;
            else if (playerQuadrantRewiredIDsBeforeSelection[quadrant][1] == rewiredID) return 1;

            //Debug.LogWarning("we broke bois");
            //Debug.LogWarning("quadrant: " + quadrant);
            //Debug.LogWarning("rewiredID: " + rewiredID);

            return -1;
        }

        /// <summary>
        /// Returns the side of the player (index 0 or 1) for a given rewired ID.
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="clientID">Client ID of the player whose side is required</param>
        /// <returns></returns>
        public int GetPlayerSideInQuadrantFromClientID(int quadrant, string clientID, int playerNumOnOwner)
        {
            string identifyingKey = clientID + "|" + playerNumOnOwner.ToString();

            string p0IdentifyingKey = lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[0] + "|" + lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[0];
            string p1IdentifyingKey = lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[1] + "|" + lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[1];

            if (p0IdentifyingKey.Equals(identifyingKey)) return 0;
            else if (p1IdentifyingKey.Equals(identifyingKey)) return 1;

            return -1;
        }

        /// <summary>
        /// Returns the side of the teammate of the player (index 0 or 1) for a given client ID.
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="clientID">Client ID of the player whose teammate's player side is required</param>
        /// <returns></returns>
        private int GetTeammatePlayerSideInQuadrantFromClientID(int quadrant, string clientID, int playerNumOnOwner)
        {
            string identifyingKey = clientID + "|" + playerNumOnOwner.ToString();

            string p0IdentifyingKey = lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[0] + "|" + lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[0];
            string p1IdentifyingKey = lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[1] + "|" + lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[1];

            if (p0IdentifyingKey.Equals(identifyingKey)) return 1;
            else if (p1IdentifyingKey.Equals(identifyingKey)) return 0;

            return -1;
        }

        /// <summary>
        /// Returns the side of the player (index 0 or 1) for a given controller image index.
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="clientID">Controller image index of the player whose side is required</param>
        /// <returns></returns>
        public int GetPlayerSideInQuadrantFromControllerIndex(int quadrant, int controllerImageIndex)
        {
            if (controllerImageIndices[quadrant][0] == controllerImageIndex) return 0;
            else if (controllerImageIndices[quadrant][1] == controllerImageIndex) return 1;

            return -1;
        }

        /// <summary>
        /// Returns the controller image index of the controller in a given quadrant. Player side: 0 for the left player, and 1 for the right player
        /// </summary>
        /// <param name="quadrant">Quadrant number</param>
        /// <param name="playerSide">Side of the player whose controller image index is requested</param>
        /// <returns></returns>
        public int GetControllerImageIndexInQuadrantFromPlayerSide(int quadrant, int playerSide)
        {
            if (quadrant >= 0 && quadrant < controllerImageIndices.Length && playerSide >= 0 && playerSide < 2)
            {
                return controllerImageIndices[quadrant][playerSide];
            }

            return -1;
        }

        /// <summary>
        /// Returns the side of the teammate of the player (index 0 or 1) for a given controller image index.
        /// </summary>
        /// <param name="quadrant">Quadrant of the team</param>
        /// <param name="clientID">Controller image index of the player whose teammate's player side is required</param>
        /// <returns></returns>
        private int GetTeammatePlayerSideInQuadrantFromControllerIndex(int quadrant, int controllerImageIndex)
        {
            if (controllerImageIndices[quadrant][0] == controllerImageIndex) return 1;
            else if (controllerImageIndices[quadrant][1] == controllerImageIndex) return 0;

            return -1;
        }

        /// <summary>
        /// Returns the quadrant of the given controller image index
        /// Returns -1 if the controller image is not at a quadrant
        /// </summary>
        /// <param name="controllerImgIndex">Controller image index of the player whose quadrant is required</param>
        /// <returns></returns>
        public int GetQuadrantOfControllerImageIndex(int controllerImgIndex)
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
                        ControllerImage CI = lobby.ControllerImages[controllerImgIndex];
                        CI.SetQuadrant(-1);
                        CI.Unready();

                        DecrementPlayersFromQuadrant(1, quadrant);

                        /*if (nPlayersInQuadrant[quadrant] == 2)
                        {
                            quadrantPointerArrows[quadrant].CrossFadeAlpha(1f, 0.2f, true);
                        }
                        nPlayersInQuadrant[quadrant]--;
                        if (nPlayersInQuadrant[quadrant] < 0)
                            nPlayersInQuadrant[quadrant] = 0;*/

            if (i == 0)
                        {
                            controllerImageIndices[quadrant][0] = controllerImageIndices[quadrant][1];
                            playerQuadrantRewiredIDsBeforeSelection[quadrant][0] = playerQuadrantRewiredIDsBeforeSelection[quadrant][1];
                        }
                        controllerImageIndices[quadrant][1] = -1;
                        playerQuadrantRewiredIDsBeforeSelection[quadrant][1] = -1;

                        Debug.Log("<color=green>=============== BOI ===============</color>");
                        Debug.Log("[" + playerQuadrantRewiredIDsBeforeSelection[0][0] + " , " + playerQuadrantRewiredIDsBeforeSelection[0][1] + "]          ,          [" + playerQuadrantRewiredIDsBeforeSelection[1][0] + " , " + playerQuadrantRewiredIDsBeforeSelection[1][1] + "]");
                        Debug.Log("[" + playerQuadrantRewiredIDsBeforeSelection[2][0] + " , " + playerQuadrantRewiredIDsBeforeSelection[2][1] + "]          ,          [" + playerQuadrantRewiredIDsBeforeSelection[3][0] + " , " + playerQuadrantRewiredIDsBeforeSelection[3][1] + "]");

                        /*Debug.Log("<color=green>=============== controlle img indices ===============</color>");
                        Debug.Log("[" + controllerImageIndices[0][0] + " , " + controllerImageIndices[0][1] + "]          ,          [" + controllerImageIndices[1][0] + " , " + controllerImageIndices[1][1] + "]");
                        Debug.Log("[" + controllerImageIndices[2][0] + " , " + controllerImageIndices[2][1] + "]          ,          [" + controllerImageIndices[3][0] + " , " + controllerImageIndices[3][1] + "]");
                        */
                        break;
                    }
                }
            }
        }

        public void SavePlayerPrefs()
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
            for (int i = 0; i < 4; i++)
            {
                if (layouts[i].Layout != ControllerLayout.LayoutStyle.None)
                {
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

                        MenuData.LobbyScreenData.ControllerSetup.LocalTeamLayouts[prevLocalTeam] = layouts[i];

                        int numPlayers = 2;
                        if (layouts[i].Layout == ControllerLayout.LayoutStyle.Shared)
                        {
                            if (controllerImageIndices[i][0] != -1 && lobby.ControllerImages[controllerImageIndices[i][0]].quadrantMode == ControllerImage.QuadrantMode.Ready)
                            {
                                numPlayers = lobby.ControllerImages[controllerImageIndices[i][0]].NumberOfPlayerPromptUI.NumberOfPlayers;
                            }
                            else if (controllerImageIndices[i][1] != -1 && lobby.ControllerImages[controllerImageIndices[i][1]].quadrantMode == ControllerImage.QuadrantMode.Ready)
                            {
                                numPlayers = lobby.ControllerImages[controllerImageIndices[i][1]].NumberOfPlayerPromptUI.NumberOfPlayers;
                            }
                        }

                        MenuData.LobbyScreenData.ControllerSetup.LocalTeamNumPlayers[prevLocalTeam] = numPlayers;
                        MenuData.LobbyScreenData.ControllerSetup.TeamNumPlayers[i] = numPlayers;

                        //PlayerPrefs.SetInt("team" + (prevLocalTeam).ToString() + "NumPlayers", numPlayers);

                        Debug.Log("<color=blue> Team" + (prevLocalTeam).ToString() + "Layout: " + layouts[i].Layout.ToString() + "</color>");
                        Debug.Log("<color=green> Player" + (prevLocalTeam).ToString() + "1_RewiredID: " + layouts[i].RewiredIDs[0].ToString() + "</color>");
                        Debug.Log("<color=green> Team" + (prevLocalTeam).ToString() + "2_RewiredID: " + layouts[i].RewiredIDs[1].ToString() + "</color>");
                        Debug.Log("<color=green> Team" + (prevLocalTeam).ToString() + "SharedControllerOwnerID: " + layouts[i].SharedControllerOwnerID + "</color>");
                        Debug.Log("<color=green> Team" + (prevLocalTeam).ToString() + "Number of players: " + MenuData.LobbyScreenData.ControllerSetup.TeamNumPlayers[i] + "</color>");

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

            MenuData.LobbyScreenData.ControllerSetup.TeamLayouts = layouts;

            // Also need to set this in offline mode, because in the case that people are offline in lobby then go to matchmake, NetworkManager needs to have the correct number of teams.
            NetworkManager.Instance.SetNumberOfTeams(team);

            MenuData.LobbyScreenData.ScreenMode = MenuManager.Instance.SelectedMode;
            //PlayerPrefs.SetInt("screenMode", MenuManager.Instance.SelectedMode);
        }

        private void SetControllerLayout(int quadrantNum, int enumInt)
        {
            if (!PhotonNetwork.OfflineMode)
            {
                // Stuff to do on all clients
                photonView.RPC("SetControllerLayoutRPC", RpcTarget.AllBufferedViaServer, quadrantNum, enumInt);
            }
        }

        public void OtherClientDisconnectedFromRoom(string clientID, Photon.Realtime.Player otherPlayer)
        {
            PhotonNetwork.OpRemoveCompleteCacheOfPlayer(otherPlayer.ActorNumber);

            List<string> idsToRemove = new List<string>();

            // First, look at all layouts and remove any layouts that were set by the disconnected client
            for (int i = 0; i < 4; i++)
            {
                bool wasRightPOnline = false;

                if (lobby.TeamQuadrants[i].OnlinePlayerClientIDs[1].Equals(clientID))
                {
                    wasRightPOnline = true;

                    string identifyingKey = clientID + "|" + lobby.TeamQuadrants[i].OnlinePlayerNumberOnOwner[1];

                    MenuData.RemoveIdentifyingKey(identifyingKey);

                    Destroy(onlinePlayerIcons[identifyingKey].gameObject);
                    onlinePlayerIcons.Remove(identifyingKey);
                    onlinePlayerIconMoveCoroutines.Remove(identifyingKey);
                    onlinePlayerIconScaleCoroutines.Remove(identifyingKey);

                    lobby.TeamQuadrants[i].IsOnlinePlayer[1] = false;
                    lobby.TeamQuadrants[i].OnlinePlayerClientIDs[1] = "";
                    lobby.TeamQuadrants[i].OnlinePlayerNumberOnOwner[1] = -1;

                    DecrementPlayersFromQuadrant(1, i);

                    // Also, remove layout if it was set
                    if (NetworkManager.Instance.TeamClientIDs[i][1] != "" && NetworkManager.Instance.TeamClientIDs[i][1].Equals(clientID)) layouts[i].RemoveController(-1, clientID, lobby.TeamQuadrants[i], 1);
                }
                if (lobby.TeamQuadrants[i].OnlinePlayerClientIDs[0].Equals(clientID))
                {
                    string identifyingKey = clientID + "|" + lobby.TeamQuadrants[i].OnlinePlayerNumberOnOwner[0];

                    MenuData.RemoveIdentifyingKey(identifyingKey);

                    Destroy(onlinePlayerIcons[identifyingKey].gameObject);
                    onlinePlayerIcons.Remove(identifyingKey);
                    onlinePlayerIconMoveCoroutines.Remove(identifyingKey);
                    onlinePlayerIconScaleCoroutines.Remove(identifyingKey);

                    lobby.TeamQuadrants[i].IsOnlinePlayer[0] = false;
                    lobby.TeamQuadrants[i].OnlinePlayerClientIDs[0] = "";
                    lobby.TeamQuadrants[i].OnlinePlayerNumberOnOwner[0] = -1;

                    DecrementPlayersFromQuadrant(1, i);

                    // Also, remove layout if it was set
                    if (NetworkManager.Instance.TeamClientIDs[i][0] != "" && NetworkManager.Instance.TeamClientIDs[i][0].Equals(clientID)) layouts[i].RemoveController(-1, clientID, lobby.TeamQuadrants[i], 0);

                    if (!wasRightPOnline)
                    {
                        controllerImageIndices[i][0] = controllerImageIndices[i][1];
                        controllerImageIndices[i][1] = -1;

                        playerQuadrantRewiredIDsBeforeSelection[i][0] = playerQuadrantRewiredIDsBeforeSelection[i][1];
                        playerQuadrantRewiredIDsBeforeSelection[i][1] = -1;
                    }
                }
            }

            // Destroy all remaining player icons that exist in the center and belong to the disconnected client
            List<string> keys = new List<string>(onlinePlayerIcons.Keys.ToList());      // since we can't directly use onlinePlayerIcons.Keys in the foreach as foreach needs the iterable its iterating on to NOT change
            foreach (string id in keys)
            {
                if (id.Split('|')[0].Equals(clientID))
                {
                    Destroy(onlinePlayerIcons[id].gameObject);
                    onlinePlayerIcons.Remove(id);
                    onlinePlayerIconMoveCoroutines.Remove(id);
                    onlinePlayerIconScaleCoroutines.Remove(id);
                }
            }
        }

        #endregion

        #region EventSubscriptions
        public void ControllerConnected(int rewiredID, int playerNumber, ControllerType type)
        {
            // Activate next available controllerImage
            if (playerNumber < 4)
            {
                lobby.ControllerImages[playerNumber].gameObject.SetActive(true);
                lobby.ControllerImages[playerNumber].FullController.color = colors[playerNumber];
                lobby.ControllerImages[playerNumber].LeftHalf.color = colors[playerNumber];
                lobby.ControllerImages[playerNumber].RightHalf.color = colors[playerNumber];
                lobby.ControllerImages[playerNumber].Init();
                lobby.ControllerImages[playerNumber].SetControllerImage(rewiredID);

                if (!PhotonNetwork.OfflineMode)
                {
                    // int controllerIndex = RewiredJoystickAssign.Instance.RewiredIDCorrespondingControllerIndices[rewiredID];
                    // Debug.Log("Player number " + playerNumber + "controllerIndex " + controllerIndex);
                    photonView.RPC("SetThisClientPlayerOnOthers", RpcTarget.OthersBuffered, PhotonNetwork.LocalPlayer.UserId, playerNumber);
                }
            }
        }

        public void ControllerDisconnected(int rewiredID, int playerNumber)
        {
            // Hide the controller that disconnected
            if (playerNumber < 4)
            {
                int quadrant = GetQuadrantOfControllerImageIndex(playerNumber);

                MovePlayerToQuadrant(playerNumber, quadrant, -2, rewiredID);

                #region remove
                //if (!PhotonNetwork.OfflineMode)
                //{
                //    photonView.RPC("RemoveThisClientPlayerFromOthers", RpcTarget.OthersBuffered, PhotonNetwork.LocalPlayer.UserId, playerNumber, quadrant, playerSide);
                //}

                //if (quadrant >= 0 && quadrant < 4)
                //{
                //    // This int value has to be initialized before the next function call as it modifies the values
                //    int teammate = GetTeammateControllerImageIndex(quadrant, playerNumber);

                //    RemoveControllerImageIndexFromQuadrant(quadrant, playerNumber);

                //    //Handle teammate
                //    if (!lobby.IsTeamOnline(quadrant))
                //    {
                //        // Debug.Log("player num: " + playerNumber + " , teammate: " + teammate);

                //        if (teammate != -1)
                //        {
                //            MovePlayerToQuadrant
                //            // StartCoroutine(HandleVisuals(teammate, quadrant, quadrant, 0.3f));
                //        }
                //    }
                //    else if (!PhotonNetwork.OfflineMode)
                //    {
                //        int teammateSide = lobby.TeamQuadrants[quadrant].IsOnlinePlayer[0] ? 0 : lobby.TeamQuadrants[quadrant].IsOnlinePlayer[1] ? 1 : -1;

                //        if (teammateSide == 1)
                //        {
                //            lobby.TeamQuadrants[quadrant].IsOnlinePlayer[0] = lobby.TeamQuadrants[quadrant].IsOnlinePlayer[1];
                //            lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[0] = lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[1];
                //            lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[0] = lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[1];

                //            lobby.TeamQuadrants[quadrant].IsOnlinePlayer[1] = false;
                //            lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[1] = "";
                //            lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[1] = -1;

                //            teammateSide = 0;
                //        }

                //        if (teammateSide != -1)
                //        {
                //            // Debug.Log("Quadrant " + quadrant + " teammate is on " + (teammateSide == 0 ? "left" : "right"));

                //            int pNumOnOwner = lobby.TeamQuadrants[quadrant].OnlinePlayerNumberOnOwner[teammateSide];
                //            string clientID = lobby.TeamQuadrants[quadrant].OnlinePlayerClientIDs[teammateSide];

                //            // photonView.RPC("HandleVisualsOnOtherClient", RpcTarget.Others, clientID, pNumOnOwner, quadrant);
                //        }
                //    }

                //    // If layout was locked, remove it
                //    RemoveControllerFromQuadrant(quadrant, playerNumber, rewiredID, playerSide);
                //}
                #endregion

            }
        }
        #endregion

    }
}