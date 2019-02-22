using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Rewired;
using Fling.Saves;
using Photon.Pun;

public class RaceManager : MonoBehaviour//PunCallbacks
{
    public static RaceManager Instance { get; set; }

    #region FIELDS
    private Transform[] startingCheckpoint;          // Array of starting point for teams
    private Transform[] currCheckpoint;             // Array of the currently active checkpoints for each team
    private Transform[] nextCheckpoint;             // Array of the next checkpoint for each team

    private List<Transform> allCheckpoints;         // LIST of all checkpoints in the level. Unity's arrays don't have an IndexOf() (yeah, wonderful, right?) function and we need that
                                                    // Technically, we could do System.Array.IndexOf(array, element) but I don't want to import another library for just once line of code
    private GameObject winPoint;

    public GameObject winPanel, timerPanel, roundStartPanel;

    public Text winnerText, restartText, timerText, winTimerText, bestTimeText;
    public Text winnerText2, winTimerText2, bestTimeText2;

    public bool useStartTimer = true;
    public bool isAutumnWorld = false;

    public GameObject splitline;

    //timer stuff
    public float speedUpTime = 5f, slowDownFactor = 0.35f;

    private float startTime;
    private string minutes, seconds, milliseconds;
    private bool isRaceOver = true;
    private float raceTime = 0f;

    private bool teamDying = false;

    private int winningTeam = 0, losingTeam = 0;
    private bool isSlowingDown = false, isSpeedingUp = false;
    private bool startCameraResize = false;

    public List<List<GameObject>> teamAttachedPlungers;
    // public List<List<GameObject>> TeamAttachedPlungers {get{return teamAttachedPlungers;} set{teamAttachedPlungers = value;}}

    [Header("Place Rank UI (First Place / Second Place etc.)")]
    private List<int> teamPlaceRanks;                   // Array to keep track of the ranks of each team in the race (first place/second place/etc.)
    private bool rankSwitchCoroutineRunning;
    [SerializeField]
    private GameObject ranksPanel;
    [SerializeField]
    private float rankSwitchAnimationSpeed = 0.5f;
    [SerializeField]
    private Image firstPlace;
    [SerializeField]
    private Image secondPlace;
    [SerializeField]
    private Transform team1UILocation;
    [SerializeField]
    private Transform team2UILocation;

    [Header("Trophies")]
    [SerializeField]
    private Animator levelBeatenAnimator;
    [SerializeField]
    private Animator silverUnlockedAnimator;
    [SerializeField]
    private Animator goldUnlockedAnimator;
    private bool wasLevelBeatenBefore = false, wasSilverUnlockedBefore = false, wasGoldUnlockedBefore = false;

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


    [Header("Tutorial Temp Win Stuff")]
    [SerializeField]
    private RectTransform[] tutorialWinObjects;
    [SerializeField]
    private Animator tutorialWinAnimator;

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
    IEnumerator Start () {
        // Get system player
        systemPlayer = ReInput.players.SystemPlayer;

        eventSystem = EventSystem.current;

        teamAttachedPlungers = new List<List<GameObject>>();
        teamAttachedPlungers.Add(new List<GameObject>());
        teamAttachedPlungers.Add(new List<GameObject>());

        // Deactivate all UI
        winPanel.SetActive(false);
        roundStartPanel.SetActive(false);
        tutorialWinAnimator.gameObject.SetActive(false);
        isPaused = false;
        hasWon = false;
        if (pauseMenuCanvas != null)
        {
            pauseMenuCanvas.SetActive(false);
        }

        // Get all race references from the RaceReferences Instance
        startingCheckpoint = RaceReferences.Instance.StartingCheckpoints;
        allCheckpoints = new List<Transform>();
        allCheckpoints.AddRange(RaceReferences.Instance.AllCheckpoints);
        winPoint = RaceReferences.Instance.WinPoint.gameObject;

        currCheckpoint = new Transform[startingCheckpoint.Length];
        nextCheckpoint = new Transform[startingCheckpoint.Length];

        teamPlaceRanks = new List<int>(new int[TeamManager.Instance.TeamsOnThisClient]);

        // Set current checkpoints for each team to their respective starting points
        for (int i = 0; i < startingCheckpoint.Length; i++)
        {
            currCheckpoint[i] = startingCheckpoint[i];
            nextCheckpoint[i] = allCheckpoints[0];
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
            //TeamManager.Instance.InstantiatePrefabsNetworked(startingCheckpoint[NetworkManager.Instance.PlayerNumber % 2]);
            TeamManager.Instance.InstantiatePrefabs(startingCheckpoint);
        }

        if(MetaManager.Instance.levelAbilityMode == Fling.Levels.LevelAbilityMode.Anvil) {
            AbilityManager.Instance.AttachAnvilToAllTeams();
        }
        else if(MetaManager.Instance.levelAbilityMode == Fling.Levels.LevelAbilityMode.SpringRope) {
            AbilityManager.Instance.GiveSolidRopeToAllTeams();
        }
        
        StartCoroutine(BeginRound());
    }

    #region RACE_START

    /// <summary>
    /// Begins the round. Responsible for the 3..2..1..Go! portion of the start.
    /// </summary>
    IEnumerator BeginRound()
    {
        /*if (PhotonNetwork.OfflineMode) {
            useStartTimer = true;
        }
        else {
            useStartTimer = NetworkManager.Instance.AllPlayersLoaded;
            // NetworkManager.Instance.PlayersLoadedIntoScene++;
        }

        while (!useStartTimer) {
            // ExitGames.Client.Photon.Hashtable myHash = new ExitGames.Client.Photon.Hashtable();
            // myHash.Add("load", 0);
            // PhotonNetwork.LocalPlayer.SetCustomProperties(myHash);
            useStartTimer = NetworkManager.Instance.AllPlayersLoaded;

            yield return 0;
        }*/

        //only do 3..2..1..Go if devs want
        if (useStartTimer)
        {
            if (OnRaceCountdown!=null)
            {
                OnRaceCountdown();
            }

            yield return new WaitForSeconds(0.5f);
            roundStartPanel.SetActive(true);
            yield return new WaitForSeconds(3.5f);

            StartCoroutine(DisableRoundStartPanel());
        }

        // enable player inputs again
        TeamManager.Instance.PlayerInputEnabled(true);

        startTime = Time.time;
        isRaceOver = false;

        if (OnRaceBegin != null)
        {
            OnRaceBegin();
        }

        Fling.Levels.LevelScriptableObject currentLevel = MetaManager.Instance.CurrentLevel;
        wasLevelBeatenBefore = SaveManager.Instance.IsLevelBeaten(currentLevel);
        wasSilverUnlockedBefore = SaveManager.Instance.IsLevelSilverTrophyUnlocked(currentLevel);
        wasGoldUnlockedBefore = SaveManager.Instance.IsLevelGoldTrophyUnlocked(currentLevel);

        silverUnlockedAnimator.enabled = false;
        goldUnlockedAnimator.enabled = false;

        levelBeatenAnimator.gameObject.SetActive(wasLevelBeatenBefore);
        silverUnlockedAnimator.gameObject.SetActive(wasSilverUnlockedBefore);
        goldUnlockedAnimator.gameObject.SetActive(wasGoldUnlockedBefore);

        rankSwitchCoroutineRunning = false;

        if (PlayerPrefs.GetInt("screenMode") == 0)
        {
            ranksPanel.SetActive(false);
        }
        else
        {
            ranksPanel.SetActive(true);
        }
    }

    IEnumerator DisableRoundStartPanel()
    {
        yield return new WaitForSeconds(2f);
        roundStartPanel.SetActive(false);
    }

    IEnumerator WaitForAllClientsToLoad()
    {
        while (!NetworkManager.Instance.AllPlayersLoaded)
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
            timerText.text = GetTimeFormatted(raceTime);
        }

        if (isSlowingDown)
        {
            timerText.color = Color.Lerp(timerText.color, Color.white, Time.deltaTime * 15f);
            //music.GetComponent<AudioSource>().pitch = Time.timeScale;
            Time.timeScale -= 3f * Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Clamp(Time.timeScale, slowDownFactor, 1f);
            Time.fixedDeltaTime = Time.timeScale * 0.02f;
            if (Time.timeScale <= slowDownFactor)
            {
                isSlowingDown = false;
                StartCoroutine(WaitBeforeSpeedUp(0.7f));
            }
            //Debug.Log("slow");
        }
        else if (isSpeedingUp)
        {
            //music.GetComponent<AudioSource>().pitch = Time.timeScale;
            Time.timeScale += (1 / speedUpTime) * Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Clamp(Time.timeScale, 0f, 1f);
            Time.fixedDeltaTime = Time.timeScale * 0.02f;
            if (Time.timeScale >= 1f)
            {
                isSpeedingUp = false;
            }
            //Debug.Log("fast");
        }
    }

    // EVERYTHING IN HERE NEEDS TO BE REWORKED
    void Update()
    {
        if (!isRaceOver)
        {
            if (PhotonNetwork.OfflineMode)
            {
                if (PlayerPrefs.GetInt("screenMode") == 1)
                {
                    if (!ranksPanel.activeSelf) ranksPanel.SetActive(true);

                    List<int> initTeamRanks = new List<int>(teamPlaceRanks);        // don't copy the old list because that will just be a reference!

                    // Get first place
                    for (int i = 0; i < 2 /*TeamManager.Instance.TeamsOnThisClient*/; i++)
                    {
                        // ======================================= Find which team is at a later checkpoint =======================================

                        if (currCheckpoint[i] == startingCheckpoint[i])
                        {
                            teamPlaceRanks[i] = 0;
                        }
                        else
                        {
                            if (allCheckpoints.Contains(currCheckpoint[i]))
                                teamPlaceRanks[i] = allCheckpoints.IndexOf(currCheckpoint[i]) + 1;
                            else
                            {
                                Debug.LogWarning("Something's wrong with the checkpoints lists. Double click this warning and debug the Update() function");
                                teamPlaceRanks[i] = -1;    // something is wrong, fix this
                            }
                        }
                    }

                    // =============== If they are both at the same checkpoint, get their relative distance to both checkpoints ==================
                    // This isn't scalable. We might have to find a different algorithm if we intend to use more than two teams (in case of networked multiplayer).
                    // Here's a potential algorithm that could be useful but is untested:
                    //  1. Find all players that share the same currCheckpoint
                    //  2. For each of those players, find the distance from their last checkpoint and sort them out accordingly
                    //  3. Do this for each list of players that share the same checkpoint (player 1 and 3 might be on checkpoint 5, player 2, 4 and 6 might be on checkpoint 4, etc.)
                    //
                    // Anyway, the current algorithm is much simpler and works for 2 teams.

                    if (teamPlaceRanks[0] == teamPlaceRanks[1])         // if both players have the same checkpoint
                    {
                        float team1sqrMag = TeamManager.Instance.SqrMagnitudeBetweenTeamAndVector(1, nextCheckpoint[0].position);
                        float team2sqrMag = TeamManager.Instance.SqrMagnitudeBetweenTeamAndVector(2, nextCheckpoint[1].position);

                        if (team1sqrMag < team2sqrMag)
                        {
                            teamPlaceRanks[0] = 1;
                            teamPlaceRanks[1] = 0;
                        }
                        else
                        {
                            teamPlaceRanks[0] = 0;
                            teamPlaceRanks[1] = 1;
                        }
                    }

                    if (Mathf.Sign(teamPlaceRanks[0] - teamPlaceRanks[1]) != Mathf.Sign(initTeamRanks[0] - initTeamRanks[1]))      // Have the team place ranks changed in this frame?
                    {
                        Transform first = team1UILocation, second = team2UILocation;
                        if (teamPlaceRanks[0] < teamPlaceRanks[1])
                        {
                            first = team2UILocation;
                            second = team1UILocation;
                        }
                        else
                        {
                            first = team1UILocation;
                            second = team2UILocation;
                        }

                        /*if (!rankSwitchCoroutineRunning)*/
                        StartCoroutine(SwitchRanks(first, second));
                    }
                }
                else if (ranksPanel.activeSelf)
                {
                    ranksPanel.SetActive(false);
                }
            }
        }

        // Change pause status of the game
        if (systemPlayer.GetButtonDown("Start"))
        {
            if (hasWon)
            {
                MetaManager.Instance.LoadNextLevel();
            }
            else if (!OptionsMenuManager.Instance.IsActive)
            {
                TogglePauseMenu();
            }
        }

        // If paused, return to main menu
        if (systemPlayer.GetButtonDown("Select"))
        {
            if (hasWon)
            {
                // MetaManager.Instance.LoadMainMenu();
                PauseMenuLevelSelect();
            }
            else if (isPaused)
            {
                // TogglePauseMenu();                  // get rid of the pause panel and change timescale to 1
                // MetaManager.Instance.LoadMainMenu();
            }
        }

        if (systemPlayer.GetButtonDown("Restart"))
        {
            if (hasWon)
            {
                RestartLevel();
            }
        }
        
        // If player wants to go BACK, and the game is paused
        if (systemPlayer.GetButtonDown("Back") && isPaused)
        {
            // If the options menu is open
            if (OptionsMenuManager.Instance.IsActive)
            {
                DeactivateOptionsMenu();
            }
            else // get out of paused state
            {
                TogglePauseMenu();
            }
        }

        /* ================ REMOVE WHEN NOT USING DEVMODE SCREEN MODE CHANGE ================ */
        if (systemPlayer.GetButtonDown("ChangeSplitScreenStatus"))
        {
            Rect[] camRects;
            if (PlayerPrefs.GetInt("screenMode") == 0)
            {
                PlayerPrefs.SetInt("screenMode", 1);
                camRects = new Rect[] { new Rect(0, 0, 0.5f, 1f), new Rect(0.5f, 0, 0.5f, 1f) };
                ChangeSplitLineStatus();
            }
            else
            {
                PlayerPrefs.SetInt("screenMode", 0);
                camRects = new Rect[] { new Rect(0, 0, 1f, 1f), new Rect(0.5f, 0, 0.0f, 0.0f) };
                ChangeSplitLineStatus();
            }

            for (int i = 0; i < 2; i++)
            {
                TeamManager.Instance.TeamInstance[i].GetComponentInChildren<Camera>().rect = camRects[i];
            }
        }
    }
    
    IEnumerator SwitchRanks(Transform first, Transform second)
    {
        rankSwitchCoroutineRunning = true;

        float percentComplete = 0f;
        float timeSinceStarted = 0f;

        while (percentComplete < 1)
        {
            timeSinceStarted += Time.deltaTime;
            percentComplete = timeSinceStarted / rankSwitchAnimationSpeed;

            firstPlace.transform.position = Vector3.Slerp(second.position, first.position, percentComplete);
            secondPlace.transform.position = Vector3.Slerp(first.position, second.position, percentComplete);

            yield return null;
        }

        rankSwitchCoroutineRunning = false;
    }

    #region CHECKPOINT_AND_RESPAWNS
    /// <summary>
    /// Sets the checkpoint of a given team.
    /// </summary>
    /// <param name="checkpoint">Transform of the checkpoint</param>
    /// <param name="teamNumber">Team number of the team whose checkpoint is to be set</param>
    public void SetCheckpoint(Transform checkpoint, int teamNumber)
    {
        if (checkpoint.gameObject != currCheckpoint[teamNumber - 1].gameObject)
        {
            currCheckpoint[teamNumber - 1] = checkpoint;

            if (checkpoint == startingCheckpoint[teamNumber - 1])
            {
                nextCheckpoint[teamNumber - 1] = allCheckpoints[0];
            }
            else if (checkpoint == winPoint.transform)
            {
                nextCheckpoint[teamNumber - 1] = winPoint.transform;
            }
            else if(allCheckpoints.IndexOf(checkpoint) == allCheckpoints.Count - 1)
            {
                nextCheckpoint[teamNumber - 1] = winPoint.transform;
            }
            else
            {
                nextCheckpoint[teamNumber - 1] = allCheckpoints[allCheckpoints.IndexOf(checkpoint) + 1];
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
        teamDying = true;
        yield return new WaitForSeconds(time);
        DoRespawn(team);
        teamDying = false;
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
    /// <param name="teamNumber"></param>
    public void Respawn(int teamNumber)
    {
        if (teamDying)
            return;     // don't respawn team if already in the respawn sequence

        DoRespawn(teamNumber);
    }

    /// <summary>
    /// Respawns a team at a given checkpoint (indexed checkpoints).
    /// </summary>
    /// <param name="checkpoint">Index of checkpoint (indexed from 0)</param>
    /// <param name="teamNumber">Team number (not indexed from 0)</param>
    public void RespawnAtCheckpoint(int checkpoint, int teamNumber)
    {
        if (teamNumber > 0 && teamNumber <= TeamManager.Instance.TeamsOnThisClient) // team number is valid
        {
            if (checkpoint == 0)        // if respawning at starting checkpoint
            {
                SetCheckpoint(startingCheckpoint[teamNumber - 1], teamNumber);
                Respawn(teamNumber);
            }
            else if (checkpoint > 0 && checkpoint <= allCheckpoints.Count)     // all other checkpoints
            {
                SetCheckpoint(allCheckpoints[checkpoint - 1], teamNumber);
                Respawn(teamNumber);
            }
            else {
                Debug.Log("Something's wrong in RespawnAtCheckpoint");
            }

            // Technically, the Respawn(teamNumber) line can be moved out of all the if conditions and it would still respawn.
            // Say I want to respawn at checkpoint 4 but levels only have 3 checkpoints.
            // If I had the line outside of if conditions, I would still Respawn, but the currentCheckpoint wouldn't get updated.
            // This would be confusing.
            // One way of fixing that is adding another if condition checking if the entered checkpoint is valid,
            // and then Respawning.
            // We're already doing that check above, so I added the Respawn code in there.
            //
            // This is more for myself if I return here in the future and can't figure out why I did this. So please don't delete these comments.
        }
    }

    /// <summary>
    /// Respawns all active teams on indexed checkpoint.
    /// </summary>
    /// <param name="checkpoint">Index of checkpoint (indexed from 0).</param>
    public void RespawnAllTeamsAtCheckpoint(int checkpoint)
    {
        for (int i = 1; i <= TeamManager.Instance.TeamsOnThisClient; i++)
        {
            RespawnAtCheckpoint(checkpoint, i);
        }
    }

    /// <summary>
    /// Subscribe to this for all events that need to happen when a team dies (right before respawning).
    /// </summary>
    /// <param name="teamNumber"></param>
    public delegate void OnTeamDeathDelegate(int teamNumber);
    public event OnTeamDeathDelegate OnTeamDeath;

    /// <summary>
    /// Subscribe to this for all events that need to happen when a team respawns.
    /// </summary>
    /// <param name="teamNumber"></param>
    public delegate void OnTeamRespawnDelegate(int teamNumber);
    public event OnTeamRespawnDelegate OnTeamRespawn;

    /// <summary>
    /// Does the actual respawning.
    /// </summary>
    /// <param name="teamNumber"></param>
    private void DoRespawn(int teamNumber)
    {
        // Call any methods that might have subscribed to this method.
        if (OnTeamDeath != null)
        {
            OnTeamDeath(teamNumber);
        }

        List<GameObject> copyPlungerList = new List<GameObject>(teamAttachedPlungers[teamNumber - 1]);
        foreach (GameObject plunger in copyPlungerList) {
            if (plunger != null) {
                // Debug.Log(teamAttachedPlungers[teamNumber - 1].Count);
                teamAttachedPlungers[teamNumber - 1].Remove(plunger);
                // Debug.Log(teamAttachedPlungers[teamNumber - 1].Count);

                Debug.Log(plunger.name);
                FixedJoint fj = plunger.GetComponentInChildren<FixedJoint>();
                Destroy(fj);
                Destroy(plunger);
            }
        }
        teamAttachedPlungers[teamNumber - 1].Clear();
        // teamAttachedPlungers[teamNumber - 1] = null;
        // teamAttachedPlungers[teamNumber - 1] = new List<GameObject>();

        fallSoundPlayer.EmitSound();

        GameObject currCamLocator = null;
        List<Rigidbody> RBs = new List<Rigidbody>();

        //Get all rigidbodies in the team in the order: Player 1 > Rope links > Player 2

        CharacterContent characterInfo = TeamManager.Instance.TeamInstance[teamNumber - 1].GetComponent<CharacterContent>();
        RBs.Add(characterInfo.GetPlayer(1));
        RBs.AddRange(characterInfo.GetLinkRigidbodies());
        RBs.Add(characterInfo.GetPlayer(2));

        //PoofPooler.Instance.SpawnFromPool ("SpherePoof", RBs[0].position, 80);
		//PoofPooler.Instance.SpawnFromPool ("SpherePoof", RBs[RBs.Count - 1].position, 80);

        currCamLocator = characterInfo.GetCameraLocator();
        currCamLocator.transform.position = currCheckpoint[teamNumber - 1].transform.position;

        Vector3 offset = new Vector3(-2.5f, 2f, -1.5f * (teamNumber-1));
        Quaternion linkRotation = Quaternion.Euler (new Vector3(0f, 0f, 90f));

        Vector3 checkPointPosition = currCheckpoint[teamNumber - 1].transform.position;
        Quaternion checkPointRotation = currCheckpoint[teamNumber - 1].transform.rotation;


        for (int i = 0; i < RBs.Count; i++)
        {
            RBs[i].velocity = Vector3.zero;
            RBs[i].angularVelocity = Vector3.zero;
            RBs[i].useGravity = false;
            RBs[i].isKinematic = true;
            RBs[i].transform.position = checkPointRotation*(offset) + checkPointPosition;
            if (i > 0 && i < (RBs.Count - 1))
                RBs[i].transform.rotation = checkPointRotation * linkRotation;
            RBs[i].useGravity = true;
            RBs[i].isKinematic = false;
            offset.x = offset.x + 0.275f;
        }

		//PoofPooler.Instance.SpawnFromPool ("SpherePoof", RBs[0].position, 80);
		//PoofPooler.Instance.SpawnFromPool ("SpherePoof", RBs[RBs.Count - 1].position, 80);

        Rigidbody temp = characterInfo.GetTempBody();
        temp.transform.position = currCheckpoint[teamNumber - 1].transform.position + new Vector3(0f, 2f, 0f);
        temp.velocity = Vector3.zero;

        // Join the rope on respawn
        //AbilityManager.Instance.JoinRope(teamNumber);

        // Reset rope burn status on respawn
        //AbilityManager.Instance.ResetRopeBurnStatus(teamNumber);

        // Call any methods that might have subscribed to this method.
        if (OnTeamRespawn != null)
        {
            OnTeamRespawn(teamNumber);
        }
    }
    #endregion

    #region WINNING
    /// <summary>
    /// Race won event delegate. Subscribe to this for all events that need to happen when a team has won.
    /// </summary>
    /// <param name="teamNumber"></param>
    public delegate void OnTeamWinDelegate(int teamNumber);
    public event OnTeamWinDelegate OnTeamWin;
    /// <summary>
    /// Sets a team as the winner of a race.
    /// </summary>
    /// <param name="teamNumber">Team that has won</param>
    public void SetWinner(int teamNumber)
    {
        // If tutorial level
        if (MetaManager.Instance.isInTutorialLevel && PlayerPrefs.GetInt("screenMode") == 1)
        {
            if (winningTeam != 1 && winningTeam != 2)
            {
                winningTeam = teamNumber;
                losingTeam = winningTeam == 1 ? 2 : 1;
                //isSlowingDown = true;

                SaveManager.Instance.LevelBeaten(MetaManager.Instance.CurrentLevel, raceTime);
                SaveManager.Instance.UnlockNextLevel();

                tutorialWinAnimator.gameObject.SetActive(true);

                float xMovePos = winningTeam == 1 ? -197 : 197;
                foreach (RectTransform rT in tutorialWinObjects)
                {
                    Vector3 pos = rT.localPosition;
                    pos.x = xMovePos;
                    rT.localPosition = pos;
                }
            }
            else if (teamNumber == losingTeam)
            {
                tutorialWinAnimator.SetTrigger("Deactivate");
                tutorialWinAnimator.gameObject.SetActive(false);
                isSlowingDown = true;
                isRaceOver = true;
                winPoint.GetComponent<BoxCollider>().enabled = false;
                splitline.gameObject.SetActive(false);

                hasWon = true;

                if (OnTeamWin != null)
                {
                    OnTeamWin(winningTeam);
                }

                if (OnRaceFinish != null)
                {
                    OnRaceFinish();
                }
            }

            return;
        }

        // All other levels

        if (OnTeamWin != null)
        {
            OnTeamWin(teamNumber);
        }

        if (OnRaceFinish!=null)
        {
            OnRaceFinish();
        }

        SaveManager.Instance.LevelBeaten(MetaManager.Instance.CurrentLevel, raceTime);
        SaveManager.Instance.UnlockNextLevel();

        if (winningTeam != 1 || winningTeam != 2)
        {
            winningTeam = teamNumber;
            losingTeam = winningTeam == 1 ? 2 : 1;
            isSlowingDown = true;
            isRaceOver = true;
            winPoint.GetComponent<BoxCollider>().enabled = false;
            splitline.gameObject.SetActive(false);

            hasWon = true;
        }
    }

    // Waits before speeding time back up after wins.
    IEnumerator WaitBeforeSpeedUp(float time)
    {
        yield return new WaitForSeconds(time);
        isSpeedingUp = true;
        if (isRaceOver)
        {
            timerPanel.GetComponent<Animator>().SetTrigger("Deactivate");
            ActivateUI();
        }
    }

    // Activates the winner UI after wins.
    void ActivateUI()
    {
        StartCoroutine(Wait(0.5f));
        winPanel.SetActive(true);
        winTimerText.text = "Time: " + timerText.text;
        winTimerText2.text = "Time: " + timerText.text;

        float bestTime = SaveManager.Instance.GetBestTime(MetaManager.Instance.CurrentLevel);
        string formattedBestTime = GetTimeFormatted(bestTime);

        bestTimeText.text = "Best Time: " + formattedBestTime;
        bestTimeText2.text = "Best Time: " + formattedBestTime;

        CharacterContent winningCharacterContent = TeamManager.Instance.GetTeamCharacterContent(winningTeam);
        winnerText.text = winningCharacterContent.teamName;
        winnerText2.text = winningCharacterContent.teamName;

        StartCoroutine(ShowTrophies());
        // if (winningTeam == 1)
        // {
        //     winnerText.text = "RoboSnail";
        //     winnerText2.text = "RoboSnail";
        // }
        // else
        // {
        //     winnerText.text = "SheepToad";
        //     winnerText2.text = "SheepToad";
        // }
    }

    IEnumerator ShowTrophies()
    {
        yield return new WaitForSeconds(2f);

        Fling.Levels.LevelScriptableObject currentLevel = MetaManager.Instance.CurrentLevel;
        levelBeatenAnimator.enabled = true;

        if (!wasLevelBeatenBefore)
        {
            levelBeatenAnimator.gameObject.SetActive(true);
            levelBeatenAnimator.SetTrigger("Unlock");
            yield return new WaitForSeconds(0.5f);
        }

        levelBeatenAnimator.enabled = false;

        if (SaveManager.Instance.IsLevelSilverTrophyUnlocked(currentLevel))
        {
            silverUnlockedAnimator.gameObject.SetActive(true);
            silverUnlockedAnimator.enabled = true;
            if (!wasSilverUnlockedBefore)
            {
                silverUnlockedAnimator.SetTrigger("Unlock");
                yield return new WaitForSeconds(0.5f);
            }
        }
        silverUnlockedAnimator.enabled = false;

        if (SaveManager.Instance.IsLevelGoldTrophyUnlocked(currentLevel))
        {
            goldUnlockedAnimator.gameObject.SetActive(true);
            goldUnlockedAnimator.enabled = true;
            if (!wasGoldUnlockedBefore)
            {
                goldUnlockedAnimator.SetTrigger("Unlock");
                yield return new WaitForSeconds(0.5f);
            }
        }
        goldUnlockedAnimator.enabled = false;
    }

    IEnumerator Wait(float time)
    {
        yield return new WaitForSeconds(time);
    }
    #endregion

    #region PAUSE_MENU
    public void ChangeSplitLineStatus()
    {
        splitline.SetActive(!splitline.activeSelf);
    }

    public void ChangeLevelAbilityMode(Fling.Levels.LevelAbilityMode levelAbilityMode) {
        if(MetaManager.Instance.levelAbilityMode == Fling.Levels.LevelAbilityMode.Anvil) {
            AbilityManager.Instance.DetachAnvilFromAllTeams();
        }
        else if(MetaManager.Instance.levelAbilityMode == Fling.Levels.LevelAbilityMode.SpringRope) {
            AbilityManager.Instance.DeactivateSolidRopeForAllTeams();
        }

        MetaManager.Instance.levelAbilityMode = levelAbilityMode;

        if(levelAbilityMode == Fling.Levels.LevelAbilityMode.Anvil) {
            AbilityManager.Instance.AttachAnvilToAllTeams();
        }
        else if(levelAbilityMode == Fling.Levels.LevelAbilityMode.SpringRope) {
            AbilityManager.Instance.GiveSolidRopeToAllTeams();
        }
    }

    /// <summary>
    /// Pause/unpause the current game.
    /// </summary>
    public void TogglePauseMenu() {
        isPaused = !isPaused;
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

    public void ActivateOptionsMenu()
    {
        if (isPaused)
        {
            pauseMenuCanvas.SetActive(false);
        }

        OptionsMenuManager.Instance.ActivateOptionsMenu();
    }

    public void DeactivateOptionsMenu()
    {
        OptionsMenuManager.Instance.DeactivateOptionsMenu();

        if (isPaused)
        {
            pauseMenuCanvas.SetActive(true);
            eventSystem.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    /// <summary>
    /// Load the main menu and update to level select through the pause menu.
    /// </summary>
    public void PauseMenuLevelSelect() {
        if (isPaused) {
            TogglePauseMenu();
        }
        MetaManager.Instance.LoadMainMenu(Menus.MenuState.WORLD_SELECT);
    }

    /// <summary>
    /// Restarts the current level.
    /// </summary>
    public void RestartLevel()
    {
        MetaManager.Instance.Restart();
    }
    #endregion

    /* ============ RACE EVENTS ============ */
    #region RACE_EVENTS
    public delegate void OnRaceCountdownDelegate();
    public event OnRaceCountdownDelegate OnRaceCountdown;

    public delegate void OnRaceStartDelegate();
    public event OnRaceStartDelegate OnRaceBegin;

    public delegate void OnRaceFinishDelegate();
    public event OnRaceFinishDelegate OnRaceFinish;
    #endregion

    #region HELPER_FUNCTIONS
    private string GetTimeFormatted(float t)
    {
        string minutes = ((int)t / 60).ToString("d2");
        string seconds = ((int)(t % 60)).ToString("d2");
        string milliseconds = ((int)(((t % 60) % 1) * 100)).ToString("d2");

        return (minutes + ":" + seconds + ":" + milliseconds);
    }
    #endregion
}