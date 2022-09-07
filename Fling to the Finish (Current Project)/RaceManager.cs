using Fling.Achievements;
using Fling.GameModes;
using Fling.Progression;
using Fling.Saves;
using Menus;
using Menus.Settings;
using Photon.Pun;
using Rewired;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;      // for sorting team place ranks
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

public class RaceManager : MonoBehaviourPunCallbacks
{
    public static RaceManager Instance { get; set; }

    #region FIELDS
    private PlayableDirector preRacePlayableDirector;
    private PlayableDirector raceFinishLoopPlayableDirector;

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
    private List<int> indicesOfTeamsThatLeftRace;
    public int NumberOfTeamsThatHaveFinishedRace { get; private set; }

    [SerializeField] private RaceUIManager raceUI;
    public RaceUIManager RaceUI { get { return raceUI; } }

    [SerializeField]
    private float waitTimeForCountdownAfterIntro = 3f;
    //timer stuff
    public float speedUpTime = 5f, slowDownFactor = 0.35f;

    public bool IsRaceOver { get; private set; } = true;
    private bool hasSingleTeamFinishedRace = false;
    private bool hasAnyLocalTeamFinishedRace = false;
    private bool IsRaceUIAnimationOver = false;
    public bool HasRaceStarted { get; private set; } = false;
    private bool canOpenPauseMenu = true;

    public RaceTimer RaceTimer { get; private set; }
    //public float RaceFinishTime { get; private set; }

    [SerializeField]
    private float respawnDelayTimeBefore = 0.75f;

    [SerializeField]
    private float respawnDelayTimeAfter = 0.75f;
    private List<bool> teamDying;

    private bool isSlowingDown = false, isSpeedingUp = false;
    private bool startCameraResize = false;

    /// <summary>
    /// Ranks of each team
    /// </summary>
    public RaceRanks Rankings { get; private set; }

    private bool rankSwitchCoroutineRunning;

    [Header("AudioPlayers")]
    public GenericSoundPlayer fallSoundPlayer;

    [Header("Pause Menu")]
    public GameObject pauseMenuCanvas;
    private EventSystem eventSystem;
    public Button resumeButton;

    public bool IsPaused { get; private set; } = false;
    private bool isShowingPopup = false;
    private bool newLevelLoadStarted = false;

    /// <summary>
    /// A container for all GameMode scripts
    /// </summary>
    public RaceReferences.GameModeContainer GameModes { get; private set; }

    private Player systemPlayer; //The Rewired System Player

    /// <summary>
    /// To sync race start, we wait until all clients have loaded, then set a start time from master client which is 2 seconds after the master clients receives
    /// the all clients loaded event. This 2s delay is just to make sure everyone receives the race start event before the race actually starts!
    /// </summary>
    private const float RACE_START_DELAY_AFTER_ALL_CLIENTS_LOAD = 2f;
    private const string RACE_START_PROPERTY = "RaceStartTime";
    private float raceStartDelay = 0f;
    private bool canStartOnlineRace = false;
    public bool HasOnlineRaceSyncedTimerBeenHit { get; private set; } = false;
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

        // to sync online stuff
        if (!PhotonNetwork.OfflineMode)
        {
            NetworkManager.OnAllClientsLoaded += OnAllClientsLoadedRace;
        }
        // Deactivate all UI
        //winPanel.SetActive(false);
        //roundStartPanel.SetActive(false);

        IsPaused = false;
        HasRaceStarted = false;
        if (pauseMenuCanvas != null)
        {
            pauseMenuCanvas.SetActive(false);
        }

        PopUpNotification.OnPopUpShown += OnPopUpShown;
        PopUpNotification.OnPopUpDismissed += OnPopUpDismissed;

        // Get all race references from the RaceReferences Instance
        startingCheckpoint = RaceReferences.Instance.StartingCheckpoints;
        allCheckpoints = new List<Transform>();
        allCheckpoints.AddRange(RaceReferences.Instance.AllCheckpoints);
        NCheckpoints = allCheckpoints.Count + 1;
        winPoint = RaceReferences.Instance.WinPoint.gameObject;
        GameModes = RaceReferences.Instance.GameModes;

        preRacePlayableDirector = RaceReferences.Instance.PreRaceCameraPlayableDirector;
        if (preRacePlayableDirector != null) preRacePlayableDirector.Stop();

        raceFinishLoopPlayableDirector = RaceReferences.Instance.RaceFinishLoopPlayableDirector;
        if (raceFinishLoopPlayableDirector != null) raceFinishLoopPlayableDirector.Stop();

        // Wait until MetaManager has initialized
        while (!MetaManager.Instance.Initialized)
        {
            yield return null;
        }

        RaceTimerPropertiesScriptableObject currentTimerProps = MetaManager.Instance.CurrentLevelGameModeTimerProperties;

        // if timer type is None, we can go in this if block
        // otherwise we need currentTimerProps object to exist
        if (MetaManager.Instance.CurrentGameModeObject.TimerType == RaceTimerType.None || currentTimerProps != null)
        {
            switch (MetaManager.Instance.CurrentGameModeObject.TimerType)
            {
                case RaceTimerType.None: RaceTimer = new NoneRaceTimer(); break;
                case RaceTimerType.Countdown:
                    RaceTimer = new CountdownRaceTimer(currentTimerProps.CountdownTimer, currentTimerProps.TimeObjectives);
                    break;
                case RaceTimerType.Stopwatch:
                    RaceTimer = new StopwatchRaceTimer(currentTimerProps.TimeObjectives);
                    break;
            }
        }
        else    // temp check case
        {
            RaceTimer = new StopwatchRaceTimer(new float[] { 120, 240 });
        }

        // Wait until we know the total number of teams
        while (!TeamManager.Instance.IsNumberOfTeamsSet)
        {
            yield return null;
        }

        int totalTeams = TeamManager.Instance.TotalNumberOfTeams;

        currCheckpoint = new List<Transform>(new Transform[totalTeams]);        // new Transform[startingCheckpoint.Length];
        nextCheckpoint = new List<Transform>(new Transform[totalTeams]);        // new Transform[startingCheckpoint.Length];

        // Set current checkpoints for each team to their respective starting points
        for (int i = 0; i < totalTeams /*startingCheckpoint.Length*/; i++)
        {
            currCheckpoint[i] = startingCheckpoint[i];
            nextCheckpoint[i] = allCheckpoints[0];
        }

        teamDying = new List<bool>(new bool[TeamManager.Instance.TotalNumberOfTeams]);
        for (int i = 0; i < teamDying.Count; i++)
        {
            teamDying[i] = false;
        }

        indicesOfTeamsThatLeftRace = new List<int>();
        hasTeamFinishedRace = new List<bool>(new bool[TeamManager.Instance.TotalNumberOfTeams]);
        for (int i = 0; i < hasTeamFinishedRace.Count; i++)
        {
            hasTeamFinishedRace[i] = false;
        }

        Rankings = new DistanceBasedRaceRanks();        // using distance based race ranks
        Rankings.Init(totalTeams, startingCheckpoint, allCheckpoints);

        NumberOfTeamsThatHaveFinishedRace = 0;

        // Instantiate the player characters
        if (PhotonNetwork.OfflineMode)
        {
            TeamManager.Instance.InstantiatePrefabs(startingCheckpoint);
            yield return null;
        }
        else
        {
            yield return WaitUntilOnlineRaceCanStart();     // if online mode, wait until everyone has loaded in!
            TeamManager.Instance.InstantiatePrefabs(startingCheckpoint);
            NetworkManager.Instance.OnTeamsLeftRoom += OnTeamsLeftRoom; // do this before the next waituntil
            yield return new WaitUntil(() => TeamManager.Instance.IsTeamDataSetForAllTeams);
            System.DateTime now = DateTime.UtcNow;
            Debug.Log("RACE START TIME: " + now.ToLongDateString() + " , " + now.ToLongTimeString() + "." + now.Millisecond);
        }

        systemPlayer.AddInputEventDelegate(SkipPreRaceCutscene, UpdateLoopType.Update, InputActionEventType.ButtonPressed, "SkipCutscene");
        yield return StartCoroutine(PreRaceSetup());
        systemPlayer.RemoveInputEventDelegate(SkipPreRaceCutscene);

        StartCoroutine(BeginRound());

        // Update number of attempts in this level
        SaveManager.Instance.LevelAttempted(MetaManager.Instance.CurrentLevel, MenuData.LevelSelectData.GameMode);
        SaveManager.Instance.SaveGame();
    }

    #region RACE_START

    IEnumerator PreRaceSetup()
    {
        if (OnPreRaceSetupStarted != null)
        {
            OnPreRaceSetupStarted();
        }

        bool done = false;
        if (preRacePlayableDirector != null)
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

        TeamManager.Instance.EnableOwnedTeamCameras();

        if (OnPreRaceSetupOver != null)
        {
            OnPreRaceSetupOver();
        }
    }

    void SkipPreRaceCutscene(InputActionEventData data)
    {
        return; // not allowing players to skip pre race cutscene
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
            // Wait after intro cutscene before starting the countdown
            yield return new WaitForSeconds(waitTimeForCountdownAfterIntro);

            if (OnRaceCountdownStarted != null)
            {
                OnRaceCountdownStarted();
            }

            yield return new WaitForSeconds(4f);                // 4 because 3,2,1,Go being 4 seconds worth of pop ups

            if (OnRaceCountdownFinished != null)
            {
                OnRaceCountdownFinished();
            }
        }
        else
        {
            // fire the race countdown started and finished events regardless of whether we are actually showing the countdown or not
            // because game events depend on these events
            if (OnRaceCountdownStarted != null)
            {
                OnRaceCountdownStarted();
            }

            if (OnRaceCountdownFinished != null)
            {
                OnRaceCountdownFinished();
            }
        }

        IsRaceOver = false;
        IsRaceUIAnimationOver = false;

        rankSwitchCoroutineRunning = false;

        RaceTimer.OnTimerOver += LevelFailedNoSync;
        RaceTimer.BeginTimer();

        if (OnRaceBegin != null)
        {
            OnRaceBegin();
        }

        HasRaceStarted = true;
    }

    //IEnumerator DisableRoundStartPanel()
    //{
    //    yield return new WaitForSeconds(2f);
    //    roundStartPanel.SetActive(false);
    //}

    IEnumerator WaitUntilOnlineRaceCanStart()
    {
        while (!canStartOnlineRace)
        {
            yield return null;
        }
        //while (!NetworkManager.Instance.AllClientsLoaded)
        //{
        //    yield return null;
        //}

        yield return new WaitForSeconds(raceStartDelay);
        HasOnlineRaceSyncedTimerBeenHit = true;
    }
    #endregion

    // Update is called once per frame
    void FixedUpdate () {
        if (!IsRaceOver)
        {
            RaceTimer.UpdateTimer(Time.deltaTime);
            raceUI.UpdateTimer(RaceTimer);
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
        if (HasRaceStarted && !IsRaceOver)
        {
            if (!isShowingPopup)
            {
                CheckPauseInput();
            }
            Rankings.CalculateTeamRanks(currCheckpoint, nextCheckpoint);
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
    /// Event fired when a team activates a checkpoint
    /// </summary>
    public static event Action<int, Transform> OnTeamCrossedCheckpoint;

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
            chkPt = startingCheckpoint[teamIndex];
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

            if (chkPt == startingCheckpoint[teamIndex])
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

            OnTeamCrossedCheckpoint?.Invoke(teamIndex, chkPt);

            // only track the data offline
            if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
            {
                int attemptNumber = SaveManager.Instance.GetNumberOfAttemptsForLevelGameMode(MetaManager.Instance.CurrentLevel, MenuData.LevelSelectData.GameMode);
                AnalyticsManager.TrackCheckpointData(MetaManager.Instance.CurrentLevel, MenuData.LevelSelectData.GameMode, checkpoint, attemptNumber);
            }
        }
    }

    /// <summary>
    /// Respawns the given team in a certain time.
    /// DO NOT sync this using RPCs, because this function is ALREADY called from a synced function (rope cutting)
    /// </summary>
    /// <param name="time">Time to respawn the team after.</param>
    /// <param name="team">Team number</param>
    public void RespawnInTime(float time, int team)
    {
        if (teamDying[team])
        {
            return;
        }

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

        //if (TeamManager.Instance.IsTeamSharedOnline(teamIndex))
        if (!PhotonNetwork.OfflineMode)
        {
            //photonView.RPC("RespawnRPC", TeamManager.Instance.GetSharedTeammatePhotonPlayer(teamIndex), teamIndex);
            photonView.RPC("RespawnRPC", RpcTarget.All, teamIndex);

            if (TeamManager.Instance.IsTeamSharedOnline(teamIndex))
            {
                // stop syncing rigidbodies until we have received confirmation from the teammate that they received the message
                SetSyncPauseDuringRespawn(teamIndex, true);
            }
        }
        else
        {
            DoRespawn(teamIndex);
        }
    }

    [PunRPC]
    private void RespawnRPC(int teamIndex)
    {
        // Send confirmation that this message was received
        SendRespawnMessageReceivedConfirmation(teamIndex);

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
    /// Subscribe to this for events that need to happen AFTER the respawn delay is over
    /// </summary>
    /// <param name="teamIndex"></param>
    public delegate void OnTeamRespawnDelayFinishDelegate(int teamIndex);
    public event OnTeamRespawnDelayFinishDelegate OnTeamRespawnDelayFinished;

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
            teamDying[teamIndex] = true;            
                
            // Call any methods that might have subscribed to this method.
            if (OnTeamDeath != null)
            {
                OnTeamDeath(teamIndex);
            }

            fallSoundPlayer.EmitSound();

            GameObject currCamLocator = null;
            List<Rigidbody> RBs = new List<Rigidbody>();

            //Get all rigidbodies in the team in the order: Player 1 > Rope links > Player 2

            CharacterContent characterInfo = TeamManager.Instance.TeamInstance[teamIndex].GetComponent<CharacterContent>();
            RBs.Add(characterInfo.GetPlayerRigidbody(1));
            RBs.AddRange(characterInfo.GetLinkRigidbodies());
            RBs.Add(characterInfo.GetPlayerRigidbody(2));

            characterInfo.GetCharacterDelayMover(0).SetRespawning(true);
            characterInfo.GetCharacterDelayMover(1).SetRespawning(true);
            characterInfo.GetTeamRopeManager().SetRespawning(true);

            currCamLocator = characterInfo.GetCameraLocator().gameObject;
            float timeSinceStarted = 0f;
            while (timeSinceStarted < respawnDelayTimeBefore)
            {
                for (int i = 0; i < RBs.Count; i++)
                {
                    RBs[i].velocity = Vector3.zero;
                    RBs[i].angularVelocity = Vector3.zero;
                }

                Rigidbody temp = characterInfo.GetTempBody();
                temp.velocity = Vector3.zero;

                timeSinceStarted += Time.deltaTime;
                yield return null;
            }

            if (currCamLocator != null) currCamLocator.transform.position = currCheckpoint[teamIndex].transform.position;

            timeSinceStarted = 0f;
            bool firstFrameOfDelay = true;

            for (int i = 0; i < RBs.Count; i++)
            {
                RBs[i].velocity = Vector3.zero;
                RBs[i].angularVelocity = Vector3.zero;
                RBs[i].useGravity = false;
                //RBs[i].isKinematic = true;
            }

            while (timeSinceStarted < respawnDelayTimeAfter)
            {
                Vector3 offset = new Vector3(-2f, 1.5f + (1.1f * teamIndex), 0);
                Quaternion linkRotation = Quaternion.Euler(new Vector3(0f, 0f, 90f));

                Vector3 checkPointPosition = currCheckpoint[teamIndex].transform.position;
                Quaternion checkPointRotation = currCheckpoint[teamIndex].transform.rotation;

                for (int i = 0; i < RBs.Count; i++)
                {
                    RBs[i].velocity = Vector3.zero;
                    RBs[i].angularVelocity = Vector3.zero;
                    RBs[i].transform.position = checkPointRotation * (offset) + checkPointPosition;
                    if (i > 0 && i < (RBs.Count - 1))
                        RBs[i].transform.rotation = checkPointRotation * linkRotation;
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
                    }

                    firstFrameOfDelay = false;
                }

                timeSinceStarted += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < RBs.Count; i++)
            {
                RBs[i].useGravity = true;
                //RBs[i].isKinematic = false;
            }

            teamDying[teamIndex] = false;

            characterInfo.GetCharacterDelayMover(0).SetRespawning(false);
            characterInfo.GetCharacterDelayMover(1).SetRespawning(false);
            characterInfo.GetTeamRopeManager().SetRespawning(false);

            if (OnTeamRespawnDelayFinished != null)
            {
                OnTeamRespawnDelayFinished(teamIndex);
            }
        }

    }

    #region RespawnMessageConfirmationAndSyncPausing
    /// <summary>
    /// Sends respawn message received confirmation to the shared teammate of this client
    /// </summary>
    /// <param name="teamIndex"></param>
    private void SendRespawnMessageReceivedConfirmation(int teamIndex)
    {
        if (TeamManager.Instance.IsTeamSharedOnline(teamIndex))
        {
            photonView.RPC(nameof(RespawnMessageReceivedConfirmationRPC), TeamManager.Instance.GetSharedTeammatePhotonPlayer(teamIndex), teamIndex);
        }
    }

    [PunRPC]
    private void RespawnMessageReceivedConfirmationRPC(int teamIndex)
    {
        // resume team syncing
        StartCoroutine(WaitAfterReceivingMessage(teamIndex));
    }

    IEnumerator WaitAfterReceivingMessage(int teamIndex)
    {
        yield return new WaitForSeconds(0.5f);
        SetSyncPauseDuringRespawn(teamIndex, false);
    }

    /// <summary>
    /// Pauses/resumes the syncing of player and rope rigidbodies on the given teamindex
    /// </summary>
    /// <param name="teamIndex"></param>
    /// <param name="pause"></param>
    private void SetSyncPauseDuringRespawn(int teamIndex, bool pause)
    {
        CharacterContent cc = TeamManager.Instance.GetTeamCharacterContent(teamIndex);

        RopeSyncer[] ropeSyncers = cc.GetRopeSyncers();

        //cc.GetPlayer(1).transform.GetChild(0).GetComponent<CharacterDelayMover>().SetRespawning(pause);
        //cc.GetPlayer(2).transform.GetChild(0).GetComponent<CharacterDelayMover>().SetRespawning(pause);

        foreach (RopeSyncer syncer in ropeSyncers)
        {
            syncer.SetSyncPauseStatusForRespawn(pause);
        }
    }
    #endregion
    #endregion

    #region WINNING_AND_LOSING
    private void ForceFinishRace()
    {
        int count = Rankings.IndicesOfTeamsStillRacingRanked.Count - 1;
        if (MetaManager.Instance.isInTutorialLevel)
        {
            count = Rankings.IndicesOfTeamsStillRacingRanked.Count;
        }

        for (int i = 0; i < count; i++)
        {
            SetTeamHasFinishedLevel(Rankings.IndicesOfTeamsStillRacingRanked[i]);
        }
    }

    /// <summary>
    /// Race won event delegate. Subscribe to this for all events that need to happen when a team has won.
    /// </summary>
    /// <param name="teamIndex"></param>
    public delegate void OnTeamFinishedLevelDelegate(int teamIndex);
    public event OnTeamFinishedLevelDelegate OnTeamFinishedLevel;
    public static event Action<int> OnLocalTeamFinishedTournamentRace;

    /// <summary>
    /// Called to inidicate that a team has crossed the "win point" or finished the race in some other manner
    /// </summary>
    /// <param name="teamIndex">Team that has finished the level</param>
    public void SetTeamHasFinishedLevel(int teamIndex)
    {
        float finishTime = RaceTimer.CurrentTime; // get the local finish time then send it so it's synced across all clients

        if (PhotonNetwork.OfflineMode)
        {
            SetTeamHasFinishedLevelLogic(teamIndex, finishTime);
        }
        else /*if (PhotonNetwork.IsMasterClient)*/
        {
            photonView.RPC("SetTeamHasFinishedLevelLogic", RpcTarget.AllViaServer, teamIndex, finishTime);
        }
    }

    /// <summary>
    /// Actual logic for the SetTeamHasFinishedLevel() function
    /// </summary>
    /// <param name="teamIndex"></param>
    [PunRPC]
    private void SetTeamHasFinishedLevelLogic(int teamIndex, float finishTime)
    {
        if (!hasTeamFinishedRace[teamIndex])
        {
            hasTeamFinishedRace[teamIndex] = true;
            Rankings.OnTeamHasFinishedRace(teamIndex, finishTime);
            currCheckpoint[teamIndex] = winPoint.transform;

            if (!hasSingleTeamFinishedRace)
            {
                hasSingleTeamFinishedRace = true;

                if (MenuData.LevelSelectData.GameMode == GameMode.Race)
                {
                    CheckAndUnlockNextLevel();
                }
            }

            if (PhotonNetwork.OfflineMode || TeamManager.Instance.IsTeamOwnedByThisClient(teamIndex))
            {
                RaceTimer.TimerBeaten(finishTime);

                if (!hasAnyLocalTeamFinishedRace)
                {
                    hasAnyLocalTeamFinishedRace = true;
                    if (MenuData.LevelSelectData.GameMode == GameMode.Race)
                    {
                        if (MetaManager.Instance.isInTutorialLevel)
                        {
                            // tutorial completion XP
                            bool tutorialBeatenOverall = SaveManager.Instance.IsLevelBeatenInAnyMode(MetaManager.Instance.CurrentLevel);
                            if (!tutorialBeatenOverall)
                            {
                                ProgressionManager.Instance.GainXP(ProgressionManager.XP_AWARD_TUTORIAL);
                            }
                        }

                        // the last 3 bools are true because race mode has no trophies
                        SaveManager.Instance.LevelBeaten(MetaManager.Instance.CurrentLevel, MenuData.LevelSelectData.GameMode, RaceTimer, true, true, true);

                        if (!PhotonNetwork.OfflineMode && NetworkManager.Instance.RoomType == RoomType.Matchmake)
                        {
                            AchievementsManager.Instance?.IncrementStat(StatType.NumWins_MM, 1);
                        }
                    }
                }

                if (MenuData.LevelSelectData.GameMode == GameMode.Race)
                {
                    if (TournamentManager.IsPlayingTournament())
                    {
                        int pos = NumberOfTeamsThatHaveFinishedRace;
                        if (Rankings.FinalTeamIndicesOrderedByRank.Count == 1)
                        {
                            // only 1 team in race, NERF points!
                            pos = TournamentManager.TOURNAMENT_RANK_POINTS.Length - 1;
                        }
                        OnLocalTeamFinishedTournamentRace?.Invoke(pos);
                    }
                    else
                    {
                        // only gain xp in private race. Tournament xp is given separately
                        int index = NetworkManager.MAX_TEAMS_ALLOWED - TeamManager.Instance.TotalNumberOfTeams + NumberOfTeamsThatHaveFinishedRace;
                        int xpGained = ProgressionManager.XP_AWARDS_PRIVATE_RACE[index];                            
                        ProgressionManager.Instance.GainXP(xpGained);
                    }
                }
            }

            NumberOfTeamsThatHaveFinishedRace++;

            // Reworking this script to move race specific out of here is going to be a huge pain
            // So we'll make sure this script is generic enough for all other gamemodes
            if (MenuData.LevelSelectData.GameMode == GameMode.Race)
            {
                CheckForRaceFinish();
            }

            if (OnTeamFinishedLevel != null)
            {
                OnTeamFinishedLevel(teamIndex);
            }
        }
    }

    /// <summary>
    /// Check whether the race is over or not, and if it is, call the OnRaceFinish() event and show the race finish UI.
    /// </summary>
    private void CheckForRaceFinish(bool checkIfBeatenLocally = false, bool onlyCheckDontFinish = false)
    {
        // If tutorial, finish when all teams have finished the race
        if ((NumberOfTeamsThatHaveFinishedRace >= TeamManager.Instance.TotalNumberOfTeams) ||
            // If not tutorial, finish when second to last place team finishes race
            (!MetaManager.Instance.isInTutorialLevel &&
            NumberOfTeamsThatHaveFinishedRace >= TeamManager.Instance.TotalNumberOfTeams - 1))
        {
            bool beaten = true;

            if (checkIfBeatenLocally)
            {
                beaten = hasAnyLocalTeamFinishedRace;
            }

            if (onlyCheckDontFinish)
            {
                //if (NetworkManager.Instance.RoomType == RoomType.Private || !NetworkManager.Instance.IsMatchmakingMatchOver)
                //{
                //    MetaManager.Instance.NotificationControl.Show2ButtonNotification("pop all other teams disconnected",
                //    "continue", null, "exit", ExitLevel, null);
                //}
            }
            else
            {
                LevelFinished(beaten);
            }
        }
    }

    /// <summary>
    /// Call this when a multiplayer race is over
    /// or when a single player mode is successfully completed
    /// </summary>
    /// <param name="syncToAllPlayers">Optional parameter. Usually this function is called from a synced function so we don't need to sync this.</param>
    public void LevelBeaten(bool syncToAllPlayers = false)
    {
        if (!syncToAllPlayers)
        {
            LevelFinished(true);
        }
        else
        {
            if (PhotonNetwork.OfflineMode)
            {
                LevelFinished(true);
            }
            else
            {
                photonView.RPC(nameof(LevelFinished), RpcTarget.All, true);
            }
        }
    }

    private void LevelFailedNoSync()
    {
        LevelFailed();
    }

    /// <summary>
    /// Called when a single player mode fails (for various reasons: ran out of time, lost all lives, etc.)
    /// </summary>
    /// <param name="syncToAllPlayers">Optional parameter. Usually this function is called from a synced function so we don't need to sync this.</param>
    public void LevelFailed(bool syncToAllPlayers = false)
    {
        if (!syncToAllPlayers)
        {
            LevelFinished(false);
        }
        else
        {
            if (PhotonNetwork.OfflineMode)
            {
                LevelFinished(false);
            }
            else
            {
                photonView.RPC(nameof(LevelFinished), RpcTarget.All, false);
            }
        }
    }

    /// <summary>
    /// Common code for when a level finishes
    /// Pass true if race is finished or game mode is beaten
    /// Pass false if game mode fails
    /// </summary>
    /// <param name="completed"></param>
    [PunRPC]
    private void LevelFinished(bool completed)
    {
        IsRaceOver = true;
        Rankings.RaceFinished();
        RaceTimer.TimerBeaten((int)RaceTimer.CurrentTime);    // call this again as a failsafe! we have a check in place that won't override the time if it was already beaten

        if (completed)
        {
            if (MenuData.LevelSelectData.GameMode != GameMode.Race)
            {
                GameModeBase gmScript = GameModes.AllGameModeScripts[MenuData.LevelSelectData.GameMode];
                SaveManager.Instance.LevelBeaten(MetaManager.Instance.CurrentLevel, MenuData.LevelSelectData.GameMode, RaceTimer,
                                                gmScript.HasUnlockedTrophy1, gmScript.HasUnlockedTrophy2, gmScript.HasUnlockedTrophy3);

                CheckAndUnlockNextGameModes();
                CheckAndUnlockNextLevel();
            }
        }

        if (MenuData.LevelSelectData.GameMode == GameMode.Race)
        {
            // if level finishes successfully in race mode and it is not counted as beaten for this team (only happens when this team is the last team) then count it as beaten
            if (!SaveManager.Instance.IsLevelModeBeaten(MetaManager.Instance.CurrentLevel, GameMode.Race))
            {
                SaveManager.Instance.LevelBeaten(MetaManager.Instance.CurrentLevel, MenuData.LevelSelectData.GameMode, RaceTimer, true, true, true);
                if (MetaManager.Instance.isInTutorialLevel)
                {
                    // tutorial completion XP
                    bool tutorialBeatenOverall = SaveManager.Instance.IsLevelModeBeaten(MetaManager.Instance.CurrentLevel, GameMode.Normal);
                    if (!tutorialBeatenOverall)
                    {
                        ProgressionManager.Instance.GainXP(ProgressionManager.XP_AWARD_TUTORIAL);
                    }
                }
            }

            CheckAndSetDNFTeamsInRace();
        }

        SaveManager.Instance.SaveGame();        // save the game

        winPoint.GetComponent<BoxCollider>().enabled = false;

        CheckAndPlayRaceFinishLoopAnimation();

        if (OnRaceFinish != null)
        {
            OnRaceFinish(completed);
        }
    }

    private void CheckAndSetDNFTeamsInRace()
    {
        List<int> teamRanks = Instance.Rankings.FinalTeamIndicesOrderedByRank;
        int total = TeamManager.Instance.TotalNumberOfTeams;

        for (int i = 0; i < total; i++)
        {
            int teamIdx = teamRanks[i];
            float time = Rankings.RaceFinishTimesInOrder[teamIdx];
            if (time < 0)   // DNF
            {
                if (PhotonNetwork.OfflineMode || TeamManager.Instance.IsTeamOwnedByThisClient(teamIdx))   // in matchmaking, count this as finished matchmaking race
                {
                    if (TournamentManager.IsPlayingTournament())
                    {
                        OnLocalTeamFinishedTournamentRace?.Invoke(TournamentManager.TOURNAMENT_RANK_POINTS.Length - 1);  // DNF
                    }
                    else
                    {
                        // in race, gain XP for DNF (last position)
                        int xpGained = ProgressionManager.XP_AWARDS_PRIVATE_RACE[3];
                        ProgressionManager.Instance.GainXP(xpGained);
                    }
                }
            }
        }
    }
    private void CheckAndUnlockNextLevel()
    {
        // Check and unlock the next level
        SaveManager.Instance.CheckAndUnlockNextLevel(playType: MenuData.MainMenuData.PlayType);

        if (MetaManager.Instance.isInTutorialLevel)
        {
            // also unlock next level in the other play type if we just beat tutorial
            MenuData.PlayType otherPlayType = MenuData.MainMenuData.PlayType == MenuData.PlayType.Race ? MenuData.PlayType.Campaign : MenuData.PlayType.Race;
            SaveManager.Instance.CheckAndUnlockNextLevel(playType: otherPlayType);
        }
    }

    private void CheckAndUnlockNextGameModes()
    {
        if (!MetaManager.Instance.isInTutorialLevel)
        {
            SaveManager.Instance.CheckAndUnlockNextGameModes();
        }
    }

    /// <summary>
    /// Plays the end of race loop animation at the appropriate time
    /// </summary>
    private void CheckAndPlayRaceFinishLoopAnimation()
    {
        for (int i = 0; i < TeamManager.Instance.IndicesOfTeamsOnThisClient.Count; i++)
        {
            int index = TeamManager.Instance.IndicesOfTeamsOnThisClient[i]; // we need the overall TEAM INDEX of this local team

            FinishlineAnimationContent content = TeamManager.Instance.FinishlineAnimationContents[i];   // finishline animation contents are only spawned for local teams, so use the value of i directly
            content.OnAnimationFinished -= CheckAndPlayRaceFinishLoopAnimation;

            // if this team has finished the race (not a DNF team) but has not finished its race finish animation, wait until that race finish animation completes
            // then re-run this function
            if (hasTeamFinishedRace[index] && !content.HasAnimationFinished)
            {
                content.OnAnimationFinished += CheckAndPlayRaceFinishLoopAnimation;
                return;
            }
        }

        // if all teams have finished their animation, start the race end loop animation
        if (raceFinishLoopPlayableDirector != null)
        {
            raceFinishLoopPlayableDirector.Play();
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
    private void CheckPauseInput()
    {
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
            if ((hasTeamFinishedRace[idxA] && !indicesOfTeamsThatLeftRace.Contains(idxA)) || 
                (TeamManager.Instance.IndicesOfTeamsOnThisClient.Count > 1 && hasTeamFinishedRace[TeamManager.Instance.IndicesOfTeamsOnThisClient[1]] 
                && !indicesOfTeamsThatLeftRace.Contains(TeamManager.Instance.IndicesOfTeamsOnThisClient[1])))
            {
                if (NetworkManager.Instance.RoomType != RoomType.Matchmake)
                {
                    ForceFinishRace();
                }
            }
            else if (wasInputFromKeyboard)  // was the input from keyboard? // we have to do this extra check because the ESC key is BOTH "Pause" and "Back" on the keyboard. This would lead to an issue where right after pausing the game would unpause
            {
                if (IsPaused)
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
        if (systemPlayer.GetButtonDown("Back") && IsPaused && !pauseStateChangedThisFrame)
        {
            CheckSettingsMenuAndUnpause();
        }
    }

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
        if (!canOpenPauseMenu && !IsPaused)
        {
            return;
        }

        if (newLevelLoadStarted && !IsPaused)
        {
            // don't open pause menu once a new level has started loading
            return;
        }

        IsPaused = !IsPaused;
        if (OnGamePaused != null)
        {
            OnGamePaused(IsPaused);
        }

        if (PhotonNetwork.OfflineMode) {
            Time.timeScale = IsPaused == true ? 0 : 1;
        }

        if (!IsRaceOver) {
            TeamManager.Instance.PlayerInputEnabled(!IsPaused);
        }
        if (pauseMenuCanvas != null)
        {
            pauseMenuCanvas.SetActive(IsPaused);
            if (IsPaused) {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(resumeButton.gameObject);
            }
        }
    }

    public void ActivateSettingsMenu()
    {
        if (IsPaused)
        {
            pauseMenuCanvas.SetActive(false);
        }

        SettingsMenuManager.Instance.ActivateMenu();
    }

    public void DeactivateSettingsMenu()
    {
        SettingsMenuManager.Instance.DeactivateMenu();

        if (IsPaused)
        {
            pauseMenuCanvas.SetActive(true);
            eventSystem.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    /// <summary>
    /// Load the main menu and update to level select through the pause menu.
    /// </summary>
    public void ReturnToLevelSelect() {
        if (IsPaused) {
            TogglePauseMenu();
        }

        if (newLevelLoadStarted)
        {
            return;
        }

        MenuState stateToLoad = MenuState.LEVEL_SELECT; // by default (if we're in campaign)
        if (MenuData.MainMenuData.PlayType == MenuData.PlayType.Race)
        {
            // in race we will return to grid level select
            stateToLoad = MenuState.GRID_LEVEL_SELECT;
        }
        MetaManager.Instance.LoadMainMenu(stateToLoad);

        newLevelLoadStarted = true;
    }

    /// <summary>
    /// Load the main menu through the pause menu.
    /// </summary>
    public void PauseMenuSplashScreen() {
        if (IsPaused) {
            TogglePauseMenu();
        }

        ExitLevel();
    }

    private void ExitLevel()
    {
        if (newLevelLoadStarted)
        {
            return;
        }

        if (PhotonNetwork.OfflineMode)
        {
            MetaManager.Instance.LoadMainMenu();
        }
        else
        {
            canOpenPauseMenu = false;
            NetworkManager.Instance.Disconnect(MetaManager.Instance.LoadMainMenu);
        }

        newLevelLoadStarted = true;
    }

    private void GoToPedestalScreen()
    {
        if (newLevelLoadStarted)
        {
            return;
        }

        if (!TournamentManager.IsPlayingTournament())
        {
            return;
        }

        if (!PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        canOpenPauseMenu = false;
        MetaManager.Instance.LoadMainMenu(MenuState.PEDESTAL_3D);
        newLevelLoadStarted = true;
    }

    /// <summary>
    /// Quit and close the application.
    /// </summary>
    public void PauseMenuQuitGame() {
        Application.Quit();
    }

    public void RestartFromCheckpointButtonClicked()
    {
        if (IsPaused)
        {
            TogglePauseMenu();
        }

        RestartLocalTeamsFromLastCheckpoint();
    }

    /// <summary>
    /// Respawns all local teams at their last checkpoint
    /// </summary>
    private void RestartLocalTeamsFromLastCheckpoint()
    {
        foreach (int team in TeamManager.Instance.IndicesOfTeamsOnThisClient)
        {
            Respawn(team);
        }
    }

    /// <summary>
    /// Restarts the current level.
    /// </summary>
    public void RestartLevel()
    {
        if (newLevelLoadStarted)
        {
            return;
        }

        MetaManager.Instance.Restart();

        newLevelLoadStarted = true;
    }
    #endregion

    #region END_OF_RACE
    public void ContinueButton()
    {
        if (newLevelLoadStarted)
        {
            return;
        }

        if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient))
        {
            if (MenuData.MainMenuData.PlayType == MenuData.PlayType.Race)
            {
                MetaManager.Instance.LoadNextLevel();
                newLevelLoadStarted = true;
            }
            else
            {
                ReturnToLevelSelect();
            }
        }
    }

    public void LevelSelectButton()
    {
        if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient))
        {
            ReturnToLevelSelect();
        }
    }
    public void RestartButton()
    {
        if (IsRaceUIAnimationOver && (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient))
        {
            RestartLevel();
        }
    }

    public void ReadyButton()
    {
        // ready button doesn't do anything
    }

    public void ExitButton()
    {
        if (IsRaceUIAnimationOver)
        {
            ExitLevel();
        }
    }

    public void LoadNextLevelInTournament()
    {
        if (newLevelLoadStarted)
        {
            return;
        }

        if (TournamentManager.Instance.RacesPlayedInTournament < TournamentManager.MAX_ROUNDS_IN_TOURNAMENT)
        {
            if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
            {
                MetaManager.Instance.LoadNextTournamentRace();
                newLevelLoadStarted = true;
            }
        }
        else
        {
            GoToPedestalScreen();
            //MetaManager.Instance.NotificationControl.ShowOKNotification(Fling.Localization.LocalizationKeys.POPUP_MM_FINISHED, ExitLevel, null);
        }
    }
    #endregion

    #region POPUPS
    private void OnPopUpShown()
    {
        isShowingPopup = true;
    }

    private void OnPopUpDismissed()
    {
        isShowingPopup = false;
    }
    #endregion

    #region PUN Callbacks

    /// <summary>
    /// Triggered when a client disconnects from the room
    /// </summary>
    /// <param name="clientID"></param>
    /// <param name="indicesOfTeamsThatLeft"></param>
    private void OnTeamsLeftRoom(List<int> indicesOfTeamsThatLeft, List<int> sharedTeamIndices)
    {
        int leftTeamsCount = indicesOfTeamsThatLeft.Count;
        if (indicesOfTeamsThatLeft == null || leftTeamsCount <= 0)
        {
            return;
        }

        indicesOfTeamsThatLeftRace.AddRange(indicesOfTeamsThatLeft);

        for (int i = 0; i < leftTeamsCount; i++)
        {
            hasTeamFinishedRace[indicesOfTeamsThatLeft[i]] = true;
        }

        Rankings.OnTeamsLeftRoom(indicesOfTeamsThatLeft);
        NumberOfTeamsThatHaveFinishedRace += leftTeamsCount;
        CheckForRaceFinish(true, true);
    }

    private void OnAllClientsLoadedRace()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                // only the master client should do this
                double raceStartTime = PhotonNetwork.Time + RACE_START_DELAY_AFTER_ALL_CLIENTS_LOAD;
                ExitGames.Client.Photon.Hashtable ht = new ExitGames.Client.Photon.Hashtable() { { RACE_START_PROPERTY, raceStartTime } };
                PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
            }

            // force start the race after all clients load anyway, in case the master disconnects before setting the room props!
            Invoke(nameof(ForceStartOnlineRace), RACE_START_DELAY_AFTER_ALL_CLIENTS_LOAD);
        }
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        raceStartDelay = RACE_START_DELAY_AFTER_ALL_CLIENTS_LOAD;
        object raceStartTimeObj;

        if (propertiesThatChanged.TryGetValue(RACE_START_PROPERTY, out raceStartTimeObj))
        {
            if (IsInvoking(nameof(ForceStartOnlineRace)))
            {
                CancelInvoke(nameof(ForceStartOnlineRace));
            }
            
            double startTime = (double)raceStartTimeObj;
            Debug.Log("The race will start at " + startTime);

            raceStartDelay = (float)(startTime - PhotonNetwork.Time);
            if (raceStartDelay < 0f)
            {
                raceStartDelay = 0f;
            }
            else if (raceStartDelay > RACE_START_DELAY_AFTER_ALL_CLIENTS_LOAD)
            {
                raceStartDelay = RACE_START_DELAY_AFTER_ALL_CLIENTS_LOAD;
            }
            canStartOnlineRace = true;
        }
    }

    private void ForceStartOnlineRace()
    {
        canStartOnlineRace = true;
        raceStartDelay = 0f;
        Debug.Log("Force starting the race now!");
    }
    #endregion
    /* ============ RACE EVENTS ============ */
    #region RACE_EVENTS
    public UnityAction OnPreRaceSetupStarted;
    public UnityAction OnPreRaceSetupOver;

    public UnityAction OnRaceCountdownStarted;
    public UnityAction OnRaceCountdownFinished;

    public UnityAction OnRaceBegin;

    public UnityAction<bool /*mode beaten*/> OnRaceFinish;
    #endregion

    public void Dev_ChangeSplitScreenStatus()
    {
        if (TeamManager.Instance.TeamsOnThisClient == 2)
        {
            Rect[] camRects;
            if (MenuData.LobbyScreenData.ScreenMode == 0)
            {
                MenuData.LobbyScreenData.ScreenMode = 1;
                RaceUI.SetSplitScreenActive(true);
            }
            else
            {
                MenuData.LobbyScreenData.ScreenMode = 0;
                camRects = new Rect[] { new Rect(0, 0, 1f, 1f), new Rect(0.5f, 0, 0.0f, 0.0f) };
                RaceUI.SetSplitScreenActive(false);
            }
        }
    }
    private void OnDestroy()
    {
        NetworkManager.Instance.OnTeamsLeftRoom -= OnTeamsLeftRoom;

        if (RaceTimer != null)
        {
            RaceTimer.OnTimerOver -= LevelFailedNoSync;
        }

        NetworkManager.OnAllClientsLoaded -= OnAllClientsLoadedRace;
        PopUpNotification.OnPopUpShown -= OnPopUpShown;
        PopUpNotification.OnPopUpDismissed -= OnPopUpDismissed;
    }
}