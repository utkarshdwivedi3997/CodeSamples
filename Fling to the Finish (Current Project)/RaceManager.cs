using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Rewired;
using Fling.Saves;
using Photon.Pun;
using System.Linq;      // for sorting team place ranks
using TMPro;
using Menus.Settings;
using Fling.AbilityManagement;
using Fling.GameModes;
using UnityEngine.Playables;
using UnityEngine.Events;

public class RaceManager : MonoBehaviourPun//PunCallbacks
{
    public static RaceManager Instance { get; set; }

    #region FIELDS
    private PlayableDirector preRacePlayableDirector;

    private Transform[] startingCheckpoint;             // Array of starting point for teams
    private List<Transform> currCheckpoint;             // List of the currently active checkpoints for each team
    private List<Transform> nextCheckpoint;             // List of the next checkpoint for each team

    private List<Transform> allCheckpoints;             // LIST of all checkpoints in the level. Unity's arrays don't have an IndexOf() (yeah, wonderful, right?) function and we need that
                                                        // Technically, we could do System.Array.IndexOf(array, element) but I don't want to import another library for just once line of code
    /// <summary>
    /// Number of checkpoints in the race
    /// </summary>
    public int NCheckpoints
    {
        get; private set;
    }

    private GameObject winPoint;
    private List<bool> hasTeamFinishedRace;
    public int NumberOfTeamsThatHaveFinishedRace { get; private set; }

    public bool isAutumnWorld = false;

    [SerializeField] private RaceUIManager raceUI;
    public RaceUIManager RaceUI { get { return raceUI; } }

    //timer stuff
    public float speedUpTime = 5f, slowDownFactor = 0.35f;

    private float startTime;
    private string minutes, seconds, milliseconds;
    private bool isRaceOver = true;
    private bool hasSingleTeamFinishedRace = false;
    private bool IsRaceUIAnimationOver = false;
    private bool hasRaceStarted = false;
    private float raceTime = 0f;
    public float RaceFinishTime { get; private set; }

    [SerializeField]
    private float respawnDelayTime = 0.5f;
    private List<bool> teamDying;

    private int winningTeam = -1, losingTeamOnClient = -1;
    private bool isSlowingDown = false, isSpeedingUp = false;
    private bool startCameraResize = false;

    public List<List<GameObject>> teamAttachedPlungers;
    // public List<List<GameObject>> TeamAttachedPlungers {get{return teamAttachedPlungers;} set{teamAttachedPlungers = value;}}

    //[Header("--------------------------------------------------------------")]
    //[Header("Place Rank UI (First Place / Second Place etc.)")]
    private List<int> indicesOfTeamsStillRacing;
    private List<int> indicesOfTeamsStillRacingRanked;
    /// <summary>
    /// Indices of teams ordered in the order of their place in the race
    /// </summary>
    public List<int> TeamIndicesOrderedByRank { get; private set; }

    /// <summary>
    /// Indices of teams ordered in the order of their place after they have finished the race
    /// </summary>
    public List<int> FinalTeamIndicesOrderedByRank { get; private set; }

    private List<int> indicesOfTeamsThatQuit;

    private bool rankSwitchCoroutineRunning;

    [Header("AudioPlayers")]
    public GenericSoundPlayer fallSoundPlayer;

    [Header("Pause Menu")]
    public GameObject pauseMenuCanvas;
    private EventSystem eventSystem;
    public Button resumeButton;
    private bool isPaused = false;
    public bool IsPaused {get {return isPaused;}}
    private bool hasWon = false;
    public bool Won
    {
        get
        {
            return hasWon;
        }
        set
        {
            hasWon = value;
        }
    }

    /// <summary>
    /// A container for all GameMode scripts
    /// </summary>
    public RaceReferences.GameModeContainer GameModes { get; private set; }

    private Player systemPlayer; //The Rewired System Player

    #endregion

    void Awake()
    {
        // Singelton instance
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    // Use this for initialization
    IEnumerator Start ()
    {
        // Get system player
        systemPlayer = ReInput.players.SystemPlayer;

        eventSystem = EventSystem.current;

        if (NetworkManager.Instance != null)
        {
            OnRaceFinish += NetworkManager.Instance.OnRaceFinishNetworkCleanUp;
        }

        isPaused = false;
        hasWon = false;
        hasRaceStarted = false;
        if (pauseMenuCanvas != null)
        {
            pauseMenuCanvas.SetActive(false);
        }

        // Get all race references from the RaceReferences Instance
        startingCheckpoint = RaceReferences.Instance.StartingCheckpoints;
        allCheckpoints = new List<Transform>();
        allCheckpoints.AddRange(RaceReferences.Instance.AllCheckpoints);
        NCheckpoints = allCheckpoints.Count + 1;
        winPoint = RaceReferences.Instance.WinPoint.gameObject;
        GameModes = RaceReferences.Instance.GameModes;
        preRacePlayableDirector = RaceReferences.Instance.PreRaceCameraPlayableDirector;
        if (preRacePlayableDirector != null) preRacePlayableDirector.Stop();

        // Wait until we know the total number of teams
        while (!TeamManager.Instance.IsNumberOfTeamsSet)
        {
            yield return null;
        }

        int totalTeams = TeamManager.Instance.TotalNumberOfTeams;

        currCheckpoint = new List<Transform>(new Transform[totalTeams]);        // new Transform[startingCheckpoint.Length];
        nextCheckpoint = new List<Transform>(new Transform[totalTeams]);        // new Transform[startingCheckpoint.Length];

        //teamPlaceRanks = new List<int>(new int[totalTeams]);            // can't initialize a list with a specific size, so we have to convert an array to a list
        TeamIndicesOrderedByRank = new List<int>(new int[totalTeams]);  // can't initialize a list with a specific size, so we have to convert an array to a list
        indicesOfTeamsStillRacing = new List<int>(new int[totalTeams]);
        for (int i = 0; i < indicesOfTeamsStillRacing.Count; i++)
        {
            indicesOfTeamsStillRacing[i] = i;
        }
        indicesOfTeamsStillRacingRanked = new List<int>(indicesOfTeamsStillRacing);

        teamAttachedPlungers = new List<List<GameObject>>();            // Team attached plungers

        // Set current checkpoints for each team to their respective starting points
        for (int i = 0; i < totalTeams /*startingCheckpoint.Length*/; i++)
        {
            currCheckpoint[i] = startingCheckpoint[i % 2];
            nextCheckpoint[i] = allCheckpoints[0];

            // Add a list of attached plungers for each team
            teamAttachedPlungers.Add(new List<GameObject>());
        }

        // Instantiate the player characters
        if (PhotonNetwork.OfflineMode)
        {
            TeamManager.Instance.InstantiatePrefabs(startingCheckpoint);
            yield return null;
        }
        else
        {
            yield return WaitForAllClientsToLoad();     // if online mode, wait until everyone has loaded in!
            TeamManager.Instance.InstantiatePrefabs(startingCheckpoint);
        }

        teamDying = new List<bool>(new bool[TeamManager.Instance.TotalNumberOfTeams]);
        for (int i = 0; i < teamDying.Count; i++)
        {
            teamDying[i] = false;
        }

        hasTeamFinishedRace = new List<bool>(new bool[TeamManager.Instance.TotalNumberOfTeams]);
        for (int i = 0; i < hasTeamFinishedRace.Count; i++)
        {
            hasTeamFinishedRace[i] = false;
        }

        FinalTeamIndicesOrderedByRank = new List<int>();
        indicesOfTeamsThatQuit = new List<int>();
        NumberOfTeamsThatHaveFinishedRace = 0;

        if (!PhotonNetwork.OfflineMode)
        {
            NetworkManager.Instance.OnTeamsLeftRoom += OnTeamsLeftRoom;
        }

        systemPlayer.AddInputEventDelegate(SkipPreRaceCutscene, UpdateLoopType.Update, InputActionEventType.ButtonPressed, "SkipCutscene");
        yield return StartCoroutine(PreRaceSetup());
        systemPlayer.RemoveInputEventDelegate(SkipPreRaceCutscene);

        StartCoroutine(BeginRound());
    }

    #region RACE_START

    IEnumerator PreRaceSetup()
    {
        if (OnPreRaceSetupStarted != null)
        {
            OnPreRaceSetupStarted();
        }

        bool done = false;
        if (preRacePlayableDirector != null && MetaManager.Instance)
        {
            preRacePlayableDirector.Play();

            preRacePlayableDirector.stopped += (PlayableDirector dir) => done = true;
        }
        else
        {
            done = true;
        }

        if (MetaManager.Instance.SkipPreRaceCutscene)
        {
            if (preRacePlayableDirector != null)
            {
                preRacePlayableDirector.time = preRacePlayableDirector.duration;
            }
        }

        while (!done)
        {
            yield return null;
        }

        if (OnPreRaceSetupOver != null)
        {
            OnPreRaceSetupOver();
        }
    }

    void SkipPreRaceCutscene(InputActionEventData data)
    {
        if (data.GetButtonDown())
        {
            if (preRacePlayableDirector != null)
            {
                preRacePlayableDirector.time = preRacePlayableDirector.duration;
            }
        }
    }

    /// <summary>
    /// Begins the round. Responsible for the 3..2..1..Go! portion of the start.
    /// </summary>
    IEnumerator BeginRound()
    {
        //only do 3..2..1..Go if devs want
        if (!MetaManager.Instance.SkipPreRaceTimer)
        {
            if (OnRaceCountdownStarted!=null)
            {
                OnRaceCountdownStarted();
            }

            yield return new WaitForSeconds(4f);                // 4 because 3,2,1,Go being 4 seconds worth of pop ups

            if (OnRaceCountdownFinished!=null)
            {
                OnRaceCountdownFinished();
            }
        }

        // enable player inputs again
        TeamManager.Instance.PlayerInputEnabled(true);

        startTime = Time.time;
        RaceFinishTime = 0f;

        isRaceOver = false;
        IsRaceUIAnimationOver = false;

        rankSwitchCoroutineRunning = false;

        if (OnRaceBegin != null)
        {
            OnRaceBegin();
        }

        hasRaceStarted = true;
    }

    IEnumerator WaitForAllClientsToLoad()
    {
        while (!NetworkManager.Instance.AllClientsLoaded)
        {
            yield return null;
        }
    }
    #endregion

    // Update is called once per frame
    void FixedUpdate () {
        if (!isRaceOver)
        {
            raceTime = Time.time - startTime;
            //timerText.text = string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds);
            raceUI.UpdateTimerText(raceTime);
        }

        //if (isSlowingDown)
        //{
        //    timerText.color = Color.Lerp(timerText.color, Color.white, Time.deltaTime * 15f);
        //    //music.GetComponent<AudioSource>().pitch = Time.timeScale;
        //    Time.timeScale -= 3f * Time.unscaledDeltaTime;
        //    Time.timeScale = Mathf.Clamp(Time.timeScale, slowDownFactor, 1f);
        //    Time.fixedDeltaTime = Time.timeScale * 0.02f;
        //    if (Time.timeScale <= slowDownFactor)
        //    {
        //        isSlowingDown = false;
        //        StartCoroutine(WaitBeforeSpeedUp(0.7f));
        //    }
        //    //Debug.Log("slow");
        //}
        //else if (isSpeedingUp)
        //{
        //    //music.GetComponent<AudioSource>().pitch = Time.timeScale;
        //    Time.timeScale += (1 / speedUpTime) * Time.unscaledDeltaTime;
        //    Time.timeScale = Mathf.Clamp(Time.timeScale, 0f, 1f);
        //    Time.fixedDeltaTime = Time.timeScale * 0.02f;
        //    if (Time.timeScale >= 1f)
        //    {
        //        isSpeedingUp = false;
        //    }
        //    //Debug.Log("fast");
        //}
    }

    void Update()
    {
        if (hasRaceStarted && !isRaceOver)
        {
            if (true /*PhotonNetwork.OfflineMode*/ )
            {
                if (TeamManager.Instance.TotalNumberOfTeams > 1)
                {
                    // Team ranking in the race
                    //if (!ranksPanel.activeSelf) ranksPanel.SetActive(true);

                    //int teamsTotal = TeamManager.Instance.TotalNumberOfTeams;
                    List<int> teamPlaceRanks = new List<int>(new int[indicesOfTeamsStillRacing.Count]);
                    List<int> checkpointSortedIndicesOfTeams = new List<int>(new int[indicesOfTeamsStillRacing.Count]);

                    // Get first place
                    for (int i = 0; i < indicesOfTeamsStillRacing.Count; i++)
                    {
                        int idxOfTeam = indicesOfTeamsStillRacing[i];
                        // ======================================= Find which team is at a later checkpoint =======================================
                        if (currCheckpoint[idxOfTeam] == startingCheckpoint[idxOfTeam % 2])
                        {
                            teamPlaceRanks[i] = 0;
                        }
                        else
                        {
                            if (allCheckpoints.Contains(currCheckpoint[idxOfTeam]))
                                teamPlaceRanks[i] = allCheckpoints.IndexOf(currCheckpoint[idxOfTeam]) + 1;
                            else
                            {
                                Debug.LogWarning("Something's wrong with the checkpoints lists. Double click this warning and debug the Update() function");
                                teamPlaceRanks[i] = -1;    // something is wrong, fix this
                            }
                        }
                    }

                    // This uses System.Linq. https://stackoverflow.com/a/53479678
                    // Select() lambdas can take both value and index in the form of (x,i).
                    // Select is like the MAP function in many languages
                    var sorted = teamPlaceRanks
                                .Select((x, i) => new { Checkpoint = x, TeamIndex = indicesOfTeamsStillRacing[i] })         // Map the value and index to a new ANONYMOUS type
                                .OrderByDescending(x => x.Checkpoint)                                // Sort this new data type in the descending order of the value (meaning, teamPlaceRanks) [each checkpoint is at a greater index than its previous one]
                                .ToList();                                                      // Convert this to a list

                    // Now, we can get the indices of each team, ordered by their ranks in the race
                    teamPlaceRanks = sorted.Select(x => x.Checkpoint).ToList();
                    TeamIndicesOrderedByRank = sorted.Select(x => x.TeamIndex).ToList();
                    checkpointSortedIndicesOfTeams = sorted.Select(x => x.TeamIndex).ToList();


                    List<float> distancesRanks = new List<float>(new float[TeamIndicesOrderedByRank.Count]);

                    for (int i = 0; i <= allCheckpoints.Count; i++)
                    {
                        int idxA = teamPlaceRanks.IndexOf(i);            // find the first index of teams that are on this checkpoint
                        int idxB = teamPlaceRanks.LastIndexOf(i);        // find the last index of teams that are on this checkpoint

                        if (idxA >= 0 && idxB >= idxA)
                        {
                            List<int> tmp = teamPlaceRanks.GetRange(idxA, idxB - idxA + 1);
                            List<float> distances = new List<float>(new float[tmp.Count]);

                            for (int j = 0; j < tmp.Count; j++)
                            {
                                // Debug.Log(j + " " + (j + idxA));
                                int teamIdx = checkpointSortedIndicesOfTeams[j + idxA];
                                distances[j] = TeamManager.Instance.SqrMagnitudeBetweenTeamAndVector(teamIdx, nextCheckpoint[teamIdx].position);
                            }

                            var sortedNew = distances
                                    .Select((x, idx) => new { DistanceToNextCheckpoint = x, TeamIndex = checkpointSortedIndicesOfTeams[idx + idxA] })
                                    .OrderBy(x => x.DistanceToNextCheckpoint)
                                    .ToList();

                            distances = sortedNew.Select(x => x.DistanceToNextCheckpoint).ToList();
                            List<int> indices = sortedNew.Select(x => x.TeamIndex).ToList();

                            for (int j = 0; j < distances.Count; j++)
                            {
                                // Debug.Log(j + " + " + idxA + " " + idxB);
                                TeamIndicesOrderedByRank[j + idxA] = indices[j];
                                distancesRanks[j + idxA] = distances[j];
                            }
                        }
                    }

                    // Add any indices that have already won to the front of this list!
                    indicesOfTeamsStillRacingRanked = new List<int>(TeamIndicesOrderedByRank);
                    TeamIndicesOrderedByRank.InsertRange(0, FinalTeamIndicesOrderedByRank);
                    TeamIndicesOrderedByRank.InsertRange(TeamIndicesOrderedByRank.Count, indicesOfTeamsThatQuit);
                    // distances = tmp.Select((x, idx) => x = TeamManager.Instance.SqrMagnitudeBetweenTeamAndVector(idx + idxA, nextCheckpoint[idx + idxA - 1].position));

                    // Display the ranks!
                    for (int j = 0; j < TeamManager.Instance.TeamsOnThisClient; j++)
                    {
                        int teamIdx = TeamManager.Instance.IndicesOfTeamsOnThisClient[j];
                        int rank = TeamIndicesOrderedByRank.IndexOf(teamIdx);

                        int side = TeamManager.Instance.SpawnSideOfTeamsOnThisClient[j]; // == 2? teamIdx % 2 : 0;

                        RaceUI.UpdateTeamRank(side, rank);
                    }
                }
            }
        }

        bool pauseStateChangedThisFrame = false;
        // Change pause status of the game
        if (systemPlayer.GetButtonDown("Pause"))
        {
            bool wasInputFromKeyboard = systemPlayer.controllers.GetLastActiveController().type == ControllerType.Keyboard;

            int idxA = TeamManager.Instance.IndicesOfTeamsOnThisClient[0];

            if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient))
            {
                MetaManager.Instance.LoadNextLevel();
            }
            if (hasTeamFinishedRace[idxA] || (TeamManager.Instance.IndicesOfTeamsOnThisClient.Count > 1 && hasTeamFinishedRace[TeamManager.Instance.IndicesOfTeamsOnThisClient[1]]))
            {
                ForceFinishRace();
            }
            else if (wasInputFromKeyboard)  // was the input from keyboard? // we have to do this extra check because the ESC key is BOTH "Pause" and "Back" on the keyboard. This would lead to an issue where right after pausing the game would unpause
            {
                if (isPaused)
                {
                    CheckSettingsMenuAndUnpause();
                }
                else
                {
                    TogglePauseMenu();
                }
                pauseStateChangedThisFrame = true;
            }
            else    // the input was from a controller
            {
                if (!SettingsMenuManager.Instance.IsActive)
                {
                    TogglePauseMenu();
                    pauseStateChangedThisFrame = true;
                }
            }
        }
        // If player wants to go BACK, and the game is paused AND didn't pause the game this exact frame
        if (systemPlayer.GetButtonDown("Back") && isPaused && !pauseStateChangedThisFrame)
        {
            CheckSettingsMenuAndUnpause();
        }

        /* ================ REMOVE WHEN NOT USING DEVMODE SCREEN MODE CHANGE ================ */
        if (DevScript.Instance.DevMode)
        {
            if (systemPlayer.GetButtonDown("ChangeSplitScreenStatus") && TeamManager.Instance.TeamsOnThisClient == 2)
            {
                Rect[] camRects;
                //if (PlayerPrefs.GetInt("screenMode") == 0)
                if (MenuData.LobbyScreenData.ScreenMode == 0)
                {
                    //PlayerPrefs.SetInt("screenMode", 1);
                    MenuData.LobbyScreenData.ScreenMode = 1;
                    RaceUI.SetSplitScreenActive(true);
                }
                else
                {
                    //PlayerPrefs.SetInt("screenMode", 0);
                    MenuData.LobbyScreenData.ScreenMode = 0;
                    camRects = new Rect[] { new Rect(0, 0, 1f, 1f), new Rect(0.5f, 0, 0.0f, 0.0f) };
                    RaceUI.SetSplitScreenActive(false);
                }
            }

            // If paused, return to main menu
            if (systemPlayer.GetButtonDown("Select"))
            {
               if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient))
               {
                   // MetaManager.Instance.LoadMainMenu();
                   ReturnToLevelSelect();
               }
               else if (isPaused)
               {
                   TogglePauseMenu();                  // get rid of the pause panel and change timescale to 1
                   // MetaManager.Instance.LoadMainMenu();
               }
            }

            if (systemPlayer.GetButtonDown("Restart"))
            {
               if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode) /* || PhotonNetwork.IsMasterClient)*/)
               {
                   RestartLevel();
               }
            }
        }
    }

    #region CHECKPOINT_AND_RESPAWNS
    /// <summary>
    /// Sets the checkpoint of a given team.
    /// </summary>
    /// <param name="checkpoint">Transform of the checkpoint</param>
    /// <param name="teamIndex">Team index of the team whose checkpoint is to be set</param>
    public void SetCheckpoint(Transform checkpoint, int teamIndex)
    {
        int checkpointNum = 0;      // assume we were given the starting checkpoint
        if (allCheckpoints.Contains(checkpoint))
        {
            checkpointNum = allCheckpoints.IndexOf(checkpoint) + 1;
        }
        else if (!startingCheckpoint.Contains(checkpoint))
        {
            Debug.LogWarning("Something's wrong in SetCheckpoint(Transform checkpoint, int teamNumber)");
            return;
        }

        SetCheckpoint(checkpointNum, teamIndex);
        
    }

    /// <summary>
    /// Overload for SetCheckpoint that takes the int index of the checkpoint
    /// 0 - starting checkpoint
    /// 1 to end - index of checkpoint in allCheckpoints + 1
    /// </summary>
    /// <param name="checkpoint"></param>
    /// <param name="teamIndex"></param>
    private void SetCheckpoint(int checkpoint, int teamIndex)
    {
        if (PhotonNetwork.OfflineMode)
        {
            SetCheckpointRPC(checkpoint, teamIndex);
        }
        else
        {
            photonView.RPC("SetCheckpointRPC", RpcTarget.All, checkpoint, teamIndex);
        }
    }

    /// <summary>
    /// RPC for the SetCheckpoint() method
    /// </summary>
    /// <param name="checkpoint"></param>
    /// <param name="teamIndex"></param>
    [PunRPC]
    private void SetCheckpointRPC(int checkpoint, int teamIndex)
    {
        Transform chkPt;

        if (checkpoint == 0)        // if respawning at starting checkpoint
        {
            chkPt = startingCheckpoint[(teamIndex) % 2];
        }
        else if (checkpoint > 0 && checkpoint <= allCheckpoints.Count)     // all other checkpoints
        {
            chkPt = allCheckpoints[checkpoint - 1];
        }
        else
        {
            Debug.LogWarning("Something's wrong in SetCheckpointRPC(int checkpoint, int teamNumber)");
            return;
        }

        if (chkPt.gameObject != currCheckpoint[teamIndex].gameObject)
        {
            currCheckpoint[teamIndex] = chkPt;

            if (chkPt == startingCheckpoint[(teamIndex) % 2])
            {
                nextCheckpoint[teamIndex] = allCheckpoints[0];
            }
            else if (chkPt == winPoint.transform)
            {
                nextCheckpoint[teamIndex] = winPoint.transform;
            }
            else if (allCheckpoints.IndexOf(chkPt) == allCheckpoints.Count - 1)
            {
                nextCheckpoint[teamIndex] = winPoint.transform;
            }
            else
            {
                nextCheckpoint[teamIndex] = allCheckpoints[allCheckpoints.IndexOf(chkPt) + 1];
            }
        }
    }

    /// <summary>
    /// Respawns the given team in a certain time.
    /// </summary>
    /// <param name="time">Time to respawn the team after.</param>
    /// <param name="team">Team number</param>
    public void RespawnInTime(float time, int team)
    {
        StartCoroutine(DoRespawnInTime(time, team));
    }
    /// <summary>
    /// Does the actual respawning in time.
    /// DO NOT access this coroutine from outside this class. Ever.
    /// </summary>
    /// <param name="time">Time to respawn the team after.</param>
    /// <param name="team">Team number</param>
    IEnumerator DoRespawnInTime(float time, int team)
    {
        teamDying[team] = true;
        yield return new WaitForSeconds(time);
        DoRespawn(team);
        teamDying[team] = false;
    }

    /// <summary>
    /// Responsible for respawning teams.
    /// </summary>
    /// <remarks>
    /// Respawn is public and meant to be called from scripts OUTSIDE of the GameManager.
    /// DoRespawn is private and is meant to do the actual respawning (it doesn't check for whether the team is already set to be respawned in some time, this function does that check).
    /// If in the future we need outside scripts to respawn the team in time, make the RespawnInTime coroutine public, or better, make a public function that starts that coroutine (just so we don't have to write lengthy syntaxes everywhere else).
    /// I'd prefer to have one messy script and everything else clean than have everything be messy.
    /// </remarks>
    /// <param name="teamIndex"></param>
    public void Respawn(int teamIndex)
    {
        if (teamDying[teamIndex])
            return;     // don't respawn team if already in the respawn sequence

        if (TeamManager.Instance.IsTeamSharedOnline(teamIndex))
        {
            photonView.RPC("RespawnRPC", TeamManager.Instance.GetSharedTeammatePhotonPlayer(teamIndex), teamIndex);
        }

        // Debug.Log("Respawned my client team");
        DoRespawn(teamIndex);
    }

    [PunRPC]
    private void RespawnRPC(int teamIndex)
    {
        // Debug.Log("other teammate of " + teamIndex + " respawned");
        if (teamDying[teamIndex])
            return;

        DoRespawn(teamIndex);
    }

    /// <summary>
    /// Respawns a team at a given checkpoint (indexed checkpoints).
    /// </summary>
    /// <param name="checkpoint">Index of checkpoint (indexed from 0)</param>
    /// <param name="teamIndex">Team number (not indexed from 0)</param>
    public void RespawnAtCheckpoint(int checkpoint, int teamIndex)
    {
        if (teamIndex >= 0 && teamIndex < TeamManager.Instance.TotalNumberOfTeams) // team number is valid
        {
            if (checkpoint >= 0 && checkpoint <= allCheckpoints.Count)
            {
                SetCheckpoint(checkpoint, teamIndex);
                Respawn(teamIndex);
            }
            else
            {
                Debug.LogWarning("Something's wrong in RespawnAtCheckpoint");
            }
        }
    }

    /// <summary>
    /// Respawns all active teams on indexed checkpoint.
    /// </summary>
    /// <param name="checkpoint">Index of checkpoint (indexed from 0).</param>
    public void RespawnAllTeamsAtCheckpoint(int checkpoint)
    {
        for (int i = 0; i < TeamManager.Instance.TotalNumberOfTeams; i++)
        {
            RespawnAtCheckpoint(checkpoint, i);
        }
    }

    /// <summary>
    /// Subscribe to this for all events that need to happen when a team dies (right before respawning).
    /// </summary>
    /// <param name="teamIndex"></param>
    public delegate void OnTeamDeathDelegate(int teamIndex);
    public event OnTeamDeathDelegate OnTeamDeath;

    /// <summary>
    /// Subscribe to this for all events that need to happen when a team respawns.
    /// </summary>
    /// <param name="teamIndex"></param>
    public delegate void OnTeamRespawnDelegate(int teamIndex);
    public event OnTeamRespawnDelegate OnTeamRespawn;

    /// <summary>
    /// Does the actual respawning.
    /// </summary>
    /// <param name="teamIndex"></param>
    private void DoRespawn(int teamIndex)
    {
        StartCoroutine(DoRepsawnCoroutine(teamIndex));
    }

    private IEnumerator DoRepsawnCoroutine(int teamIndex)
    {
        if (teamIndex >= 0 && teamIndex < TeamManager.Instance.TotalNumberOfTeams && TeamManager.Instance.TeamInstance[teamIndex] != null)
        {
            // Call any methods that might have subscribed to this method.
            if (OnTeamDeath != null)
            {
                OnTeamDeath(teamIndex);
            }

            List<GameObject> copyPlungerList = new List<GameObject>(teamAttachedPlungers[teamIndex]);
            foreach (GameObject plunger in copyPlungerList)
            {
                if (plunger != null)
                {
                    teamAttachedPlungers[teamIndex].Remove(plunger);

                    FixedJoint fj = plunger.GetComponentInChildren<FixedJoint>();
                    Destroy(fj);
                    Destroy(plunger);
                }
            }
            teamAttachedPlungers[teamIndex].Clear();
            // teamAttachedPlungers[teamIndex] = null;
            // teamAttachedPlungers[teamIndex] = new List<GameObject>();

            fallSoundPlayer.EmitSound();

            GameObject currCamLocator = null;
            List<Rigidbody> RBs = new List<Rigidbody>();

            //Get all rigidbodies in the team in the order: Player 1 > Rope links > Player 2

            CharacterContent characterInfo = TeamManager.Instance.TeamInstance[teamIndex].GetComponent<CharacterContent>();
            RBs.Add(characterInfo.GetPlayerRigidbody(1));
            RBs.AddRange(characterInfo.GetLinkRigidbodies());
            RBs.Add(characterInfo.GetPlayerRigidbody(2));

            currCamLocator = characterInfo.GetCameraLocator();

            if (currCamLocator != null) currCamLocator.transform.position = currCheckpoint[teamIndex].transform.position;

            float timeSinceStarted = 0f;
            bool firstFrameOfDelay = true;

            for (int i = 0; i < RBs.Count; i++)
            {
                RBs[i].velocity = Vector3.zero;
                RBs[i].angularVelocity = Vector3.zero;
                RBs[i].useGravity = false;
                RBs[i].isKinematic = true;
            }

            while (timeSinceStarted < respawnDelayTime)
            {
                Vector3 offset = new Vector3(-2f, 1.5f + (1.1f * teamIndex), 0);
                Quaternion linkRotation = Quaternion.Euler(new Vector3(0f, 0f, 90f));

                Vector3 checkPointPosition = currCheckpoint[teamIndex].transform.position;
                Quaternion checkPointRotation = currCheckpoint[teamIndex].transform.rotation;

                for (int i = 0; i < RBs.Count; i++)
                {
                    RBs[i].velocity = Vector3.zero;
                    RBs[i].angularVelocity = Vector3.zero;
                    //RBs[i].useGravity = false;
                    //RBs[i].isKinematic = true;
                    RBs[i].transform.position = checkPointRotation * (offset) + checkPointPosition;
                    if (i > 0 && i < (RBs.Count - 1))
                        RBs[i].transform.rotation = checkPointRotation * linkRotation;
                    //RBs[i].useGravity = true;
                    //RBs[i].isKinematic = false;
                    offset.x = offset.x + 0.275f;
                }

                Rigidbody temp = characterInfo.GetTempBody();
                temp.transform.position = currCheckpoint[teamIndex].transform.position + new Vector3(0f, 2f, 0f);
                temp.velocity = Vector3.zero;

                if (firstFrameOfDelay)
                {
                    // Anything that needs to happen only during the FIRST frame when the team is teleported goes here

                    // Call any methods that might have subscribed to this method.
                    if (OnTeamRespawn != null)
                    {
                        OnTeamRespawn(teamIndex);
                        // Debug.Log("here");
                    }

                    firstFrameOfDelay = false;
                }

                timeSinceStarted += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < RBs.Count; i++)
            {
                RBs[i].useGravity = true;
                RBs[i].isKinematic = false;
            }

            //PoofPooler.Instance.SpawnFromPool ("SpherePoof", RBs[0].position, 80);
            //PoofPooler.Instance.SpawnFromPool ("SpherePoof", RBs[RBs.Count - 1].position, 80);
        }

    }
    #endregion

    #region WINNING
    [ContextMenu("Force race finish")]
    public void ForceFinishRace()
    {
        int count = indicesOfTeamsStillRacingRanked.Count - 1;
        if (MetaManager.Instance.isInTutorialLevel)
        {
            count = indicesOfTeamsStillRacingRanked.Count;
        }

        for (int i = 0; i < count; i++)
        {
            SetWinner(indicesOfTeamsStillRacingRanked[i]);
        }
    }


    /// <summary>
    /// Race won event delegate. Subscribe to this for all events that need to happen when a team has won.
    /// </summary>
    /// <param name="teamIndex"></param>
    public delegate void OnTeamWinDelegate(int teamIndex);
    public event OnTeamWinDelegate OnTeamWin;
    /* Functions subscribed to OnTeamWin():
     * 
     * ScreenManager.SetWinner();
    */
    /// <summary>
    /// Sets a team as the winner of a race.
    /// </summary>
    /// <param name="teamIndex">Team that has won</param>
    public void SetWinner(int teamIndex)
    {
        if (PhotonNetwork.OfflineMode)
        {
            SetWinnerLogic(teamIndex);
        }
        else /*if (PhotonNetwork.IsMasterClient)*/
        {
            photonView.RPC("SetWinnerLogic", RpcTarget.AllViaServer, teamIndex);
        }
    }

    /// <summary>
    /// Actual logic for the SetWinner() function
    /// </summary>
    /// <param name="teamIndex"></param>
    [PunRPC]
    private void SetWinnerLogic(int teamIndex)
    {
        if (!hasTeamFinishedRace[teamIndex])
        {
            // Debug.Log("finishhhh");
            hasTeamFinishedRace[teamIndex] = true;
            indicesOfTeamsStillRacing.Remove(teamIndex);
            FinalTeamIndicesOrderedByRank.Add(teamIndex);
            currCheckpoint[teamIndex] = winPoint.transform;

            if (OnTeamWin != null)
            {
                OnTeamWin(teamIndex);
            }

            if (!hasSingleTeamFinishedRace)
            {
                hasSingleTeamFinishedRace = true;
                RaceFinishTime = raceTime;
                SaveManager.Instance.LevelBeaten(MetaManager.Instance.CurrentLevel, raceTime);
                SaveManager.Instance.UnlockNextLevel();
            }

            if (winningTeam < 0)
            {
                winningTeam = teamIndex;
                if (TeamManager.Instance.TeamsOnThisClient == 2 && TeamManager.Instance.IndicesOfTeamsOnThisClient.Contains(winningTeam))
                {
                    int idxOfWinner = TeamManager.Instance.IndicesOfTeamsOnThisClient.IndexOf(winningTeam);
                    losingTeamOnClient = TeamManager.Instance.IndicesOfTeamsOnThisClient[1 - idxOfWinner];
                    //splitline.gameObject.SetActive(false);
                }
                //isSlowingDown = true;
                //winPoint.GetComponent<BoxCollider>().enabled = false;

                hasWon = true;
            }

            NumberOfTeamsThatHaveFinishedRace++;
            
            // If not tutorial, finish when second to last place team finishes race
            if (!MetaManager.Instance.isInTutorialLevel && NumberOfTeamsThatHaveFinishedRace >= TeamManager.Instance.TotalNumberOfTeams - 1)
            {
                int idxOfLastTeam = hasTeamFinishedRace.IndexOf(false);
                NumberOfTeamsThatHaveFinishedRace = TeamManager.Instance.TotalNumberOfTeams;
                FinalTeamIndicesOrderedByRank.Add(idxOfLastTeam); // = NumberOfTeamsThatHaveFinishedRace - 1;
            }

            CheckForRaceFinish();
        }
    }

    /// <summary>
    /// Check whether the race is over or not, and if it is, call the OnRaceFinish() event and show the race finish UI.
    /// </summary>
    private void CheckForRaceFinish()
    {
        if (NumberOfTeamsThatHaveFinishedRace >= TeamManager.Instance.TotalNumberOfTeams)
        {
            if (FinalTeamIndicesOrderedByRank.Count != NumberOfTeamsThatHaveFinishedRace)
            {
                FinalTeamIndicesOrderedByRank.AddRange(indicesOfTeamsThatQuit);
            }

            isRaceOver = true;
            winPoint.GetComponent<BoxCollider>().enabled = false;

            if (OnRaceFinish != null)
            {
                OnRaceFinish();
                OnRaceFinish -= NetworkManager.Instance.OnRaceFinishNetworkCleanUp;
            }
        }
    }

    // Waits before speeding time back up after wins.
    IEnumerator WaitBeforeSpeedUp(float time)
    {
        yield return new WaitForSeconds(time);
        isSpeedingUp = true;
    }

    IEnumerator Wait(float time)
    {
        yield return new WaitForSeconds(time);
    }
    #endregion

    public void EndOfRaceUIAnimationFinished()
    {
        IsRaceUIAnimationOver = true;
    }

    #region PAUSE_MENU
    public UnityAction<bool> OnGamePaused;

    private void CheckSettingsMenuAndUnpause()
    {
        // If the options menu is open
        if (SettingsMenuManager.Instance.IsActive)
        {
            if (SettingsMenuManager.Instance.CanExitSettingsMenu)
            {
                DeactivateSettingsMenu();
            }
        }
        else // get out of paused state
        {
            TogglePauseMenu();
        }
    }

    /// <summary>
    /// Pause/unpause the current game.
    /// </summary>
    public void TogglePauseMenu() {
        isPaused = !isPaused;
        if (OnGamePaused != null)
        {
            OnGamePaused(isPaused);
        }

        if (PhotonNetwork.OfflineMode) {
            Time.timeScale = isPaused == true ? 0 : 1;
        }

        if (!isRaceOver) {
            TeamManager.Instance.PlayerInputEnabled(!isPaused);
        }
        if (pauseMenuCanvas != null)
        {
            pauseMenuCanvas.SetActive(isPaused);
            if (isPaused) {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(resumeButton.gameObject);
            }
        }
    }

    public void ActivateSettingsMenu()
    {
        if (isPaused)
        {
            pauseMenuCanvas.SetActive(false);
        }

        SettingsMenuManager.Instance.ActivateMenu();
    }

    public void DeactivateSettingsMenu()
    {
        SettingsMenuManager.Instance.DeactivateMenu();

        if (isPaused)
        {
            pauseMenuCanvas.SetActive(true);
            eventSystem.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    /// <summary>
    /// Load the main menu and update to level select through the pause menu.
    /// </summary>
    public void ReturnToLevelSelect() {
        if (isPaused) {
            TogglePauseMenu();
        }
        MetaManager.Instance.LoadMainMenu(Menus.MenuState.WORLD_SELECT);
    }

    /// <summary>
    /// Load the main menu through the pause menu.
    /// </summary>
    public void PauseMenuSplashScreen() {
        if (isPaused) {
            TogglePauseMenu();
        }

        if (PhotonNetwork.OfflineMode) {
            MetaManager.Instance.LoadMainMenu();
        }
        else {
            NetworkManager.Instance.Disconnect();
            MetaManager.Instance.LoadMainMenu();
        }
    }

    /// <summary>
    /// Quit and close the application.
    /// </summary>
    public void PauseMenuQuitGame() {
        Application.Quit();
    }

    /// <summary>
    /// Restarts the current level.
    /// </summary>
    public void RestartLevel()
    {
        MetaManager.Instance.Restart();
    }
    #endregion

    #region END_OF_RACE
    public void ContinueButton()
    {
        if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient))
        {
            MetaManager.Instance.LoadNextLevel();
        }
    }

    public void LevelSelectButton()
    {
        if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient))
        {
            // MetaManager.Instance.LoadMainMenu();
            ReturnToLevelSelect();
        }
    }
    public void RestartButton()
    {
        if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode) /* || PhotonNetwork.IsMasterClient)*/)
        {
            RestartLevel();
        }
    }
    #endregion

    #region PUN Callbacks

    /// <summary>
    /// Triggered when a client disconnects from the room
    /// </summary>
    /// <param name="clientID"></param>
    /// <param name="indicesOfTeamsThatLeft"></param>
    private void OnTeamsLeftRoom(Photon.Realtime.Player player, List<int> indicesOfTeamsThatLeft, List<int> sharedTeamIndices)
    {
        for (int i = 0; i < indicesOfTeamsThatLeft.Count; i++)
        {
            hasTeamFinishedRace[indicesOfTeamsThatLeft[i]] = true;
            indicesOfTeamsStillRacing.Remove(indicesOfTeamsThatLeft[i]);
        }

        indicesOfTeamsThatQuit.InsertRange(0, indicesOfTeamsThatLeft);
        NumberOfTeamsThatHaveFinishedRace += indicesOfTeamsThatQuit.Count;
        CheckForRaceFinish();
    }
    #endregion
    /* ============ RACE EVENTS ============ */
    #region RACE_EVENTS
    public UnityAction OnPreRaceSetupStarted;
    public UnityAction OnPreRaceSetupOver;

    /* Functions subscribed
     * 
     * SoundManager.OnRaceCountdown
     */
    public UnityAction OnRaceCountdownStarted;
    public UnityAction OnRaceCountdownFinished;

    public UnityAction OnRaceBegin;

    public UnityAction OnRaceFinish;
    /* Functions subscribed to OnRaceFinish():
     * 
     * SoundManager.OnRaceFinish();
    */
    #endregion

    private void OnDestroy()
    {
        NetworkManager.Instance.OnTeamsLeftRoom -= OnTeamsLeftRoom;
        OnRaceFinish -= NetworkManager.Instance.OnRaceFinishNetworkCleanUp;
    }
}