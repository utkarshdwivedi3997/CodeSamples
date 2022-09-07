using Fling.GameModes;
using Fling.Saves;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Fling.Localization;
using FMODUnity;
using ControllerLayout = Menus.ControllerLayout;
using Menus;

public class RaceUIManager : MonoBehaviour
{
    #region Variables

    #region PreRace
    [System.Serializable]
    public struct PreRace
    {
        [SerializeField]
        private CanvasGroup canvasGroup;
        public CanvasGroup CanvasGroup { get { return canvasGroup; } }


        [Header("Single Line Description")]

        [SerializeField]
        private LocalizedText singleModeNameText;
        public LocalizedText SingleModeNameText { get { return singleModeNameText; } }

        [SerializeField]
        private LocalizedText singleModeDescriptionText;
        public LocalizedText SingleModeDescriptionText { get { return singleModeDescriptionText; } }

        [SerializeField]
        private TextMeshProUGUI bestTimeText;
        public TextMeshProUGUI BestTimeText { get { return bestTimeText; } }

        [SerializeField]
        private Image[] imagesToChangeColor;
        public Image[] ImagesToChangeColor { get { return imagesToChangeColor; }}

        [SerializeField]
        private Image[] duckCollected;
        public Image[] DuckCollected { get { return duckCollected; } }

        [SerializeField]
        private GameObject bestTimeParent;
        public GameObject BestTimeParent { get { return bestTimeParent; } }

        [SerializeField]
        private GameObject duckParent;
        public GameObject DuckParent { get { return duckParent; } }
    }
    [SerializeField]
    private PreRace preRace;
    public PreRace PreRaceUI { get { return preRace; } }
    #endregion

    #region DuringRace
    [System.Serializable]
    public struct DuringRace
    {
        [Header("Main variables")]
        [SerializeField]
        private CanvasGroup canvasGroup;
        public CanvasGroup CanvasGroup { get { return canvasGroup; } }

        [SerializeField]
        private GameObject countdownPanel;
        /// <summary>
        /// The panel that holds the animation for 3..2..1..Go!
        /// </summary>
        public GameObject CountdownPanel { get { return countdownPanel; } }

        [SerializeField]
        private GameObject splitline;
        /// <summary>
        /// The line that divides the splitscreen
        /// </summary>
        public GameObject SplitLine { get { return splitline; } }

        [Header("Race Timer")]
        [SerializeField]
        private Animator masterTimerAnimator;
        public Animator MasterTimerAnimator { get { return masterTimerAnimator; } }

        [SerializeField]
        private GameObject timerPanel;
        public GameObject TimerPanel { get { return timerPanel; } }
        [SerializeField] private GameObject timerPanelWithGoals;
        public GameObject TimerPanelWithGoals { get { return timerPanelWithGoals; } }

        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI timerTextForGoalsTimer;
        public TextMeshProUGUI TimerText { get; private set; }

        [SerializeField] private TextMeshProUGUI timeGoalText;
        public TextMeshProUGUI TimeGoalText { get { return timeGoalText; } }

        [SerializeField] private Animator[] timerGoalAnimators;
        public Animator[] TimerGoalAnimators { get { return timerGoalAnimators; } }

        [SerializeField] [EventRef] private string timeObjectiveFailedSFX;
        public string TimeObjectiveFailedSFX { get { return timeObjectiveFailedSFX; } }

        [Header("During Race Rank Placements")]
        [SerializeField]
        private Image rankPlacementBoxLeft;
        [SerializeField]
        private Image rankPlacementBoxRight;
        /// <summary>
        /// Array of [rankPlacementBoxLeft, rankPlacementBoxRight]
        /// </summary>
        public Image[] RankPlacementBoxes { get; private set; }

        [SerializeField]
        private TextMeshProUGUI rankTextLeft;
        [SerializeField]
        private TextMeshProUGUI rankTextRight;
        /// <summary>
        /// Array of [rankTextLeft, rankTextRight]
        /// </summary>
        public TextMeshProUGUI[] RankTexts { get; private set; }

        [Space]
        [Header("GameMode HUD")]
        [SerializeField, FormerlySerializedAs("counterCanvasGroup")] private CanvasGroup textCounterCanvasGroup;
        public CanvasGroup TextCounterCanvasGroup => textCounterCanvasGroup;
        [SerializeField, FormerlySerializedAs("counterImage")] private Image textCounterImage;
        public Image TextCounterImage => textCounterImage;
        [SerializeField] private TextMeshProUGUI counterText;
        public TextMeshProUGUI CounterText => counterText;

        [SerializeField] private CanvasGroup imageCounterCanvasGroup;
        public CanvasGroup ImageCounterCanvasGroup => imageCounterCanvasGroup;
        [SerializeField] private Image[] counterImageBGs;
        public Image[] CounterImageBGs => counterImageBGs;
        [SerializeField] private Image[] counterImageFills;
        public Image[] CounterImageFills => counterImageFills;

        [Header("On Team Finish Race Variables")]
        [SerializeField]
        private RaceWinTeamPanel raceWinPanelLeft;
        [SerializeField]
        private RaceWinTeamPanel raceWinPanelRight;
        public RaceWinTeamPanel[] RaceWinPanels { get; private set; }

        [Header("Mouse Joystick Simulation Variables")]
        [SerializeField]
        private MouseJoystickVisualizer mouseJoystickSimulationVisualizer;
        public MouseJoystickVisualizer MouseJoystickSimulationVisualizer { get { return mouseJoystickSimulationVisualizer; } }

        public void Init(GameModeScriptableObject currentGamemodeObject)
        {
            RankPlacementBoxes = new Image[] { rankPlacementBoxLeft, rankPlacementBoxRight };
            RankTexts = new TextMeshProUGUI[] { rankTextLeft, rankTextRight };

            RaceWinPanels = new RaceWinTeamPanel[] { raceWinPanelLeft, raceWinPanelRight };

            TimerText = currentGamemodeObject.ShowTimeGoals ? timerTextForGoalsTimer : timerText;
        }
    }
    [SerializeField]
    private DuringRace duringRace;
    /// <summary>
    /// The UI that is displayed during a race.
    /// This includes race countdown,
    /// </summary>
    public DuringRace DuringRaceUI { get { return duringRace; } }
    #endregion

    #region EndOfRace
    [System.Serializable]
    public struct EndOfRace
    {
        [SerializeField]
        private EndOfLevelScreen[] endOfLevelScreens;
        public Dictionary<EndOfLevelScreenType, EndOfLevelScreen> EndOfLevelScreens { get; private set; }

        [SerializeField] private EndOfLevelScreenType[] endOfLevelScreenOrder;
        public EndOfLevelScreenType[] EndOfLevelScreenOrder { get { return endOfLevelScreenOrder; } }

        [SerializeField] private EndOfLevelScreenType[] endOfLevelScreenOrderForFail;
        public EndOfLevelScreenType[] FailLevelEndScreenOrder { get { return endOfLevelScreenOrderForFail; } }

        public void Init()
        {
            EndOfLevelScreens = new Dictionary<EndOfLevelScreenType, EndOfLevelScreen>();

            foreach (EndOfLevelScreen screen in endOfLevelScreens)
            {
                EndOfLevelScreens[screen.ScreenType] = screen;
                screen.InitializeScreen();
            }
        }

        public void InitializeAfterSaveLoad()
        {
            foreach (EndOfLevelScreen screen in endOfLevelScreens)
            {
                screen.InitializeScreenAfterSaveLoad();
            }
        }
    }
    [SerializeField]
    private EndOfRace endOfRace;
    public EndOfRace EndOfRaceUI { get { return endOfRace; } }
    #endregion

    #region General
    private Rect[] camRects = new Rect[] { new Rect(0, 0, 1f, 1f), new Rect(0.5f, 0, 0.0f, 0.0f) };
    private GameModeScriptableObject currentGameModeObject;

    public static string[] RacePlacementStringVals = new string[] { "1<sup>st</sup>", "2<sup>nd</sup>", "3<sup>rd</sup>", "4<sup>th</sup>", "5<sup>th<sup>", "6<sup>th<sup>", "7<sup>th<sup>", "8<sup>th<sup>" };     // 1st, 2nd, 3th,... , 8th with proper superscripts

    private int winPanelsActive = 0;
    private GameModeBase currentGameModeScript;
    private GamemodeHUDCounter currentHUDCounter;
    private int currentTimeObjectiveIndex = 0;
    private bool isShowingTimeGoals;
    [SerializeField] private bool areYouAGamer = false;
    #endregion

    #endregion

    private IEnumerator Start()
    {
        #region Immediate Initialization

        // =================== GENERAL =================== //

        winPanelsActive = 0;
        camRects = new Rect[] { new Rect(0, 0, 1f, 1f), new Rect(0.5f, 0, 0.0f, 0.0f) };

        RaceManager.Instance.OnGamePaused += OnPaused;
        PopUpNotification.OnPopUpShown += ShowCursor;
        PopUpNotification.OnPopUpDismissed += HideCursor;

        // =================== PRE RACE =================== //

        currentGameModeObject = MetaManager.Instance.CurrentGameModeObject;
        RaceManager.Instance.OnPreRaceSetupStarted += OnPreRaceSetupStarted;

        preRace.CanvasGroup.gameObject.SetActive(false);

        // =================== DURING RACE =================== //

        duringRace.Init(currentGameModeObject);

        RaceManager.Instance.OnRaceCountdownStarted += OnRaceCountdownStarted;
        RaceManager.Instance.OnRaceBegin += OnRaceBegin;

        duringRace.CountdownPanel.SetActive(false);
        duringRace.CanvasGroup.gameObject.SetActive(false);

        for (int i = 0; i < duringRace.RaceWinPanels.Length; i++)
        {
            duringRace.RaceWinPanels[i].gameObject.SetActive(false);
        }

        // =================== END OF RACE =================== //

        endOfRace.Init();

        #endregion

        // Wait until number of teams is set
        while (!TeamManager.Instance.IsNumberOfTeamsSet)
        {
            yield return null;
        }

        // Wait until SaveManager is done
        while (!SaveManager.Instance.HasInitialized)
        {
            yield return null;
        }

        #region Init after team initalization
        // Lock cursor!
        HideCursor();

        // =================== GENERAL =================== //

        currentGameModeScript = RaceReferences.Instance.GameModes.CurrentGameModeScript;

        // End of race UI scaling
        camRects = TeamManager.Instance.TeamsOnThisClient == 1 ? camRects : new Rect[] { new Rect(0, 0, 0.5f, 1f), new Rect(0.5f, 0, 0.5f, 1f) };

        // =================== DURING RACE =================== //

        for (int i = 0; i < TeamManager.Instance.TeamsOnThisClient; i++)
        {
            duringRace.RaceWinPanels[i].SetSize(camRects[i]);
        }

        // =================== END OF RACE =================== //

        Fling.Levels.LevelScriptableObject currentLevel = MetaManager.Instance.CurrentLevel;
        endOfRace.InitializeAfterSaveLoad();
        #endregion
    }

    #region PreRace
    private void OnPreRaceSetupStarted()
    {
        RaceManager.Instance.OnPreRaceSetupStarted -= OnPreRaceSetupStarted;
        RaceManager.Instance.OnPreRaceSetupOver += OnPreRaceSetupOver;

        LevelData levelData = SaveManager.Instance.GetSavedDataForLevel(MetaManager.Instance.CurrentLevel);

        preRace.CanvasGroup.gameObject.SetActive(true);
        preRace.CanvasGroup.alpha = 1f;

        if (currentGameModeObject != null)
        {
            

            foreach(Image image in preRace.ImagesToChangeColor)
            {
                image.color = currentGameModeObject.ModeUIColor;
            }

            if (levelData != null)
            {
                float bestTime = -1f;
                //preRace.BestTimeText.GetComponent<LocalizedText>().UpdateWithoutKey();

                bool trophy1Unlocked = false, trophy2Unlocked = false, trophy3Unlocked = false;
                if (levelData.ModeSaveData.ContainsKey(currentGameModeObject.Mode))
                {
                    ModeSaveData mData = levelData.ModeSaveData[currentGameModeObject.Mode];
                    trophy1Unlocked = mData.Trophy1Unlocked;
                    trophy2Unlocked = mData.Trophy2Unlocked;
                    trophy3Unlocked = mData.Trophy3Unlocked;

                    bestTime = mData.BestTime;
                }
                preRace.DuckCollected[0].enabled = trophy1Unlocked;
                preRace.DuckCollected[1].enabled = trophy2Unlocked;
                preRace.DuckCollected[2].enabled = trophy3Unlocked;

                preRace.BestTimeText.text = LocalizationManager.Instance.GetLocalizedValue("best time") + " " + Utilities.GetTimeFormatted(bestTime);
            }

            if (!MetaManager.Instance.isInTutorialLevel )
            {
                preRace.SingleModeDescriptionText.OverrideKeyAndUpdate(currentGameModeObject.Description);
                preRace.SingleModeNameText.OverrideKeyAndUpdate(currentGameModeObject.ModeName);

                if (currentGameModeObject.Mode == GameMode.Race)
                {
                    preRace.DuckParent.SetActive(false);
                }
            }
            else //is in Tutorial
            {
                preRace.SingleModeDescriptionText.OverrideKeyAndUpdate(currentGameModeObject.DescriptionTwo);
                preRace.SingleModeNameText.OverrideKeyAndUpdate("DesertTutorial");
                preRace.DuckParent.SetActive(false);
                preRace.BestTimeParent.SetActive(false);
            }
        }
    }

    private void OnPreRaceSetupOver()
    {
        RaceManager.Instance.OnPreRaceSetupOver -= OnPreRaceSetupOver;
        preRace.CanvasGroup.gameObject.SetActive(false);
        preRace.CanvasGroup.alpha = 0f;

        preRace.CanvasGroup.gameObject.SetActive(false);
        preRace.CanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Updates the race timer text with formatted time
    /// </summary>
    /// <param name="time"></param>
    public void UpdateTimer(RaceTimer timer)
    {
        if (timer.TimerType != RaceTimerType.None)
        {
            duringRace.TimerText.text = Utilities.GetTimeFormatted(timer.CurrentTime);
        }
    }
    #endregion

    #region DuringRace
    #region RaceCountdown
    private void OnRaceCountdownStarted()
    {
        RaceManager.Instance.OnRaceCountdownStarted -= OnRaceCountdownStarted;
        RaceManager.Instance.OnRaceCountdownFinished += OnRaceCountdownFinished;

        switch (MetaManager.Instance.CurrentGameModeObject.GameModeHUDCounterType)
        {
            case GameModeHUDCounterType.NumericText:
                currentHUDCounter = gameObject.AddComponent<GamemodeHUDNumericCounter>();
                break;
            case GameModeHUDCounterType.SameImageForEachCounter:
                currentHUDCounter = gameObject.AddComponent<GameModeHUDSingleImageCounter>();
                break;
            case GameModeHUDCounterType.DifferentImageForEachCounter:
                currentHUDCounter = gameObject.AddComponent<GameModeHUDDifferentImageCounter>();
                break;
            case GameModeHUDCounterType.None:
                currentHUDCounter = gameObject.AddComponent<GameModeHUDNoCounter>();
                break;
        }

        isShowingTimeGoals = false;
        if (!MetaManager.Instance.isInTutorialLevel)
        {
            currentHUDCounter.Setup(duringRace, currentGameModeScript);
            currentHUDCounter.UpdateCounter();
            currentGameModeScript.OnCollectableCountUpdated += currentHUDCounter.UpdateCounter;

            isShowingTimeGoals = currentGameModeObject.ShowTimeGoals;
        }

        duringRace.TimerPanelWithGoals.SetActive(isShowingTimeGoals);
        duringRace.TimerPanel.SetActive(!isShowingTimeGoals);
        if (isShowingTimeGoals)
        {
            
            float firstTimeToBeat = RaceManager.Instance.RaceTimer.GetCurrentTimeObjective();
            RaceManager.Instance.RaceTimer.OnTimeObjectiveFailed += OnTimeObjectiveFailed;
            RaceManager.Instance.RaceTimer.OnAllTimeObjectivesFailed += OnAllTimeObjectivesFailed;
            currentTimeObjectiveIndex = 0;
            duringRace.TimeGoalText.text = Utilities.GetTimeFormatted(firstTimeToBeat);
        }

        duringRace.CanvasGroup.gameObject.SetActive(true);
        duringRace.CountdownPanel.SetActive(true);

        DisplayMouseUIIfNeeded();
        SaveManager.Instance.OnMouseAndKeyboardButtonsChanged += DisplayMouseUIIfNeeded;
    }

    private void OnRaceCountdownFinished()
    {
        RaceManager.Instance.OnRaceCountdownFinished -= OnRaceCountdownFinished;
        StartCoroutine(DisableCountdownPanel());                        // we wait for the animation to complete before disabling the panel
    }

    private IEnumerator DisableCountdownPanel()
    {
        yield return new WaitForSeconds(2f);

        duringRace.CountdownPanel.SetActive(false);
    }
    #endregion

    private void OnRaceBegin()
    {
        RaceManager.Instance.OnRaceBegin -= OnRaceBegin;
        RaceManager.Instance.OnRaceFinish += OnRaceFinish;

        RaceManager.Instance.OnTeamFinishedLevel += OnTeamFinishedRace;

        if (!duringRace.CanvasGroup.gameObject.activeSelf)
        {
            duringRace.CanvasGroup.gameObject.SetActive(true);
        }

        // Race ranks boxes
        if (TeamManager.Instance.TotalNumberOfTeams > 1)
        {
            for (int i = 0; i < 2; i++)
            {
                duringRace.RankPlacementBoxes[i].canvasRenderer.SetAlpha(0);
                duringRace.RankPlacementBoxes[i].gameObject.SetActive(true);
                duringRace.RankPlacementBoxes[i].CrossFadeAlpha(1f, 0.3f, true);
                duringRace.RankTexts[i].canvasRenderer.SetAlpha(0);
                duringRace.RankTexts[i].CrossFadeAlpha(1f, 0.3f, true);

                if (TeamManager.Instance.TeamsOnThisClient <= 1)
                {
                    break;
                }
            }
        }
        // If there's only 1 team, turn all place showing UI off
        else
        {
            for (int i = 0; i < 2; i++)
            {
                duringRace.RankPlacementBoxes[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Updates display of team placement ranks in their respective boxes
    /// </summary>
    /// <param name="teamSideOnClient"></param>
    /// <param name="rank"></param>
    public void UpdateTeamRank(int teamSideOnClient, int rank)
    {
        TextMeshProUGUI txtToChng = duringRace.RankTexts[teamSideOnClient];

        // Debug.Log(rank);
        txtToChng.text = RacePlacementStringVals[rank];
    }

    private void OnTimeObjectiveFailed(float failedTimeObjective, float nextTimeObjective)
    {
        if (currentTimeObjectiveIndex < 3)
        {
            currentTimeObjectiveIndex++;
            duringRace.TimeGoalText.text = Utilities.GetTimeFormatted(nextTimeObjective);
            int failedObjectiveIndex = 3 - currentTimeObjectiveIndex;
            if (duringRace.TimerGoalAnimators.Length > failedObjectiveIndex)
            {
                duringRace.TimerGoalAnimators[failedObjectiveIndex].SetTrigger("DuckLoss");
            }
            RuntimeManager.PlayOneShot(duringRace.TimeObjectiveFailedSFX);
        }
    }

    private void OnAllTimeObjectivesFailed()
    {
        duringRace.TimeGoalText.text = "-- : -- : --";
    }

    private void OnTeamFinishedRace(int teamIndex)
    {
        /*if (winningTeam < 0)
        {
            winningTeam = teamIndex;
            //losingTeamOnClient = winningTeam == 1 ? 2 : 1;

            // Only do the camera resize stuff if winner's camera is owned by this team
            if (TeamManager.Instance.IndicesOfTeamsOnThisClient.Contains(winningTeam))
            {
                int idxOfWinner = TeamManager.Instance.IndicesOfTeamsOnThisClient.IndexOf(winningTeam);
                winningTeamSide = TeamManager.Instance.SpawnSideOfTeamsOnThisClient[idxOfWinner];

                if (TeamManager.Instance.TeamsOnThisClient == 2)
                {
                    losingTeamOnClient = TeamManager.Instance.IndicesOfTeamsOnThisClient[1 - idxOfWinner];
                    losingTeamOnThisClientSide = TeamManager.Instance.SpawnSideOfTeamsOnThisClient[1 - idxOfWinner];
                }

                winningTeamCamera = TeamManager.Instance.GetTeamCamera(winningTeam);
                losingTeamCamera = TeamManager.Instance.GetTeamCamera(losingTeamOnClient);

                startCameraResize = true;
            }
        }*/

        if (RaceManager.Instance.IsRaceOver)
        {
            RaceManager.Instance.OnTeamFinishedLevel -= OnTeamFinishedRace;
            return;
        }

        if (TeamManager.Instance.IndicesOfTeamsOnThisClient.Contains(teamIndex))
        {
            int localTeamIdx = TeamManager.Instance.IndicesOfTeamsOnThisClient.IndexOf(teamIndex);
            int side = TeamManager.Instance.SpawnSideOfTeamsOnThisClient[localTeamIdx];

            duringRace.RaceWinPanels[side].gameObject.SetActive(true);
            duringRace.RaceWinPanels[side].ActivateForTeam(teamIndex);

            GameObject rankPlacementObj = duringRace.RankPlacementBoxes[side].gameObject;
            rankPlacementObj.SetActive(false);

            winPanelsActive++;

            if (winPanelsActive >= TeamManager.Instance.TeamsOnThisClient)
            {
                // Unsubscribe from RaceManager's win event
                RaceManager.Instance.OnTeamFinishedLevel -= OnTeamFinishedRace;
            }
        }
    }
    #endregion

    #region RaceOver
    private void OnRaceFinish(bool beaten)
    {
        RaceManager.Instance.OnRaceFinish -= OnRaceFinish;
        RaceManager.Instance.OnTeamFinishedLevel -= OnTeamFinishedRace;
        RaceManager.Instance.OnGamePaused -= OnPaused;
        currentGameModeScript.OnCollectableCountUpdated += currentHUDCounter.UpdateCounter;

        // End of race, unlock cursor
        MetaManager.Instance.CursorHandler.SetToShowCursorWhenPossible();

        // Make all spectating teams see themselves
        for (int i = 0; i < TeamManager.Instance.TeamsOnThisClient; i++)
        {
            int idx = TeamManager.Instance.IndicesOfTeamsOnThisClient[i];

            UpdateSpectate(idx, idx);
        }

        // Deactivate extra UI
        for (int i = 0; i < duringRace.RaceWinPanels.Length; i++)
        {
            duringRace.RaceWinPanels[i].Deactivate();
            duringRace.RaceWinPanels[i].gameObject.SetActive(false);
        }
        winPanelsActive = 0;

        duringRace.MasterTimerAnimator.SetTrigger("Deactivate");

        // show the fail UI if level was not beaten and this is a campaign type gamemode
        isShowingFailUI = !beaten && MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign;
        ShowEndOfRaceUI();
    }

    private int currEndScreenIdx;
    private bool isShowingFailUI = false;

    /// <summary>
    /// UI to show at the end of the race
    /// </summary>
    private void ShowEndOfRaceUI()
    {
        EndOfLevelScreenType[] screenOrder = endOfRace.EndOfLevelScreenOrder;
        if (isShowingFailUI)
        {
            screenOrder = endOfRace.FailLevelEndScreenOrder;
        }

        // Main UI for winning
        currEndScreenIdx = 0;

        if (screenOrder != null
            && screenOrder != null
            && screenOrder.Length > currEndScreenIdx
            && endOfRace.EndOfLevelScreens.ContainsKey(screenOrder[currEndScreenIdx]))
        {
            EndOfLevelScreen screen = endOfRace.EndOfLevelScreens[screenOrder[currEndScreenIdx]];
            screen.OnScreenFinished += OnCurrentEndScreenFinished;
            screen.StartScreen(isShowingFailUI);
        }
        else
        {
            Debug.LogError("Error in RaceUIManager: Current screen index has passed the number of available screens!");
        }
    }

    private void OnCurrentEndScreenFinished(EndOfLevelScreen prevScreen)
    {
        EndOfLevelScreenType[] screenOrder = endOfRace.EndOfLevelScreenOrder;
        if (isShowingFailUI)
        {
            screenOrder = endOfRace.FailLevelEndScreenOrder;
        }

        prevScreen.OnScreenFinished -= OnCurrentEndScreenFinished;

        currEndScreenIdx++;

        if (currEndScreenIdx < screenOrder.Length)
        {
            EndOfLevelScreenType nextTypeToShow = screenOrder[currEndScreenIdx];

            if (endOfRace.EndOfLevelScreens.ContainsKey(nextTypeToShow))
            {
                EndOfLevelScreen screen = endOfRace.EndOfLevelScreens[nextTypeToShow];
                screen.OnScreenFinished += OnCurrentEndScreenFinished;
                screen.StartScreen(isShowingFailUI);
            }
            else
            {
                Debug.LogError("Error in RaceUIManager: End of level screens list does not contain current screen to show!");
            }
        }
        else
        {
            Debug.LogError("Error in RaceUIManager: Current screen index has passed the number of available screens!");
        }
    }

    /// <summary>
    /// Updates the spectator UI for spectating team
    /// </summary>
    /// <param name="spectatingTeam"></param>
    /// <param name="spectatedTeam"></param>
    public void UpdateSpectate(int spectatingTeam, int spectatedTeam)
    {
        if (RaceManager.Instance.IsRaceOver)
        {
            return;
        }

        if (TeamManager.Instance.IndicesOfTeamsOnThisClient.Contains(spectatingTeam))
        {
            int localTeamIdx = TeamManager.Instance.IndicesOfTeamsOnThisClient.IndexOf(spectatingTeam);
            int side = TeamManager.Instance.SpawnSideOfTeamsOnThisClient[localTeamIdx];

            duringRace.RaceWinPanels[side].SpectateTeam(spectatedTeam);
        }
    }
    #endregion

    #region OtherGeneralFunctions
    public void SetSplitScreenActive(bool val)
    {
        SetSplitLineActive(val);

        if (val)
        {
            camRects = new Rect[] { new Rect(0, 0, 0.5f, 1f), new Rect(0.5f, 0, 0.5f, 1f) };
        }
        else
        {
            camRects = new Rect[] { new Rect(0, 0, 1f, 1f), new Rect(0.5f, 0, 0.0f, 0.0f) };
        }

        for (int i = 0; i < TeamManager.Instance.TeamsOnThisClient; i++)
        {
            int idx = TeamManager.Instance.IndicesOfTeamsOnThisClient[i];
            TeamManager.Instance.TeamInstance[idx].GetComponentInChildren<Camera>().rect = camRects[i];
        }

        duringRace.RankPlacementBoxes[0].gameObject.SetActive(val);
        duringRace.RankPlacementBoxes[1].gameObject.SetActive(val);

        if (TeamManager.Instance.TotalNumberOfTeams == 1)
            duringRace.RankPlacementBoxes[0].gameObject.SetActive(false);
    }

    public void SetSplitLineActive(bool val)
    {
        duringRace.SplitLine.SetActive(val);
    }
    #endregion

    #region Mouse UI
    private void DisplayMouseUIIfNeeded()
    {
        int mouseUiTeam = 0;
        int mouseUiPlayer = 1;
        bool displayMouseUi = false;

        if (MenuData.LobbyScreenData.ControllerSetup.LocalMenuTeams != null)
        {
            int localTeams = TeamManager.Instance.TeamsOnThisClient;
            for (int index = 0; index < localTeams; index++)
            {
                MenuTeam team = MenuData.LobbyScreenData.ControllerSetup.LocalMenuTeams[index];
                int teamIndex = TeamManager.Instance.IndicesOfTeamsOnThisClient[index];

                if (team == null)
                {
                    continue;
                }

                ControllerLayout.LayoutStyle layout = team.Layout;
                CharacterContent cc = TeamManager.Instance.GetTeamCharacterContent(teamIndex);
                PlayerInput[] PIs = cc.GetPlayerInputs();

                for (int i = 0; i < 2; i++) // for both players
                {
                    int playerNumber = i + 1;
                    PlayerInput pi = PIs[i];
                    bool isUsingMouse = false;
                    // Should the mouse be displayed?
                    if (!displayMouseUi && pi.Player != null)   // if mouse ui player has not already been found only then continue
                    {
                        if (pi.Player.controllers.hasMouse)
                        {
                            // Is this player a separate layout?
                            if (layout == ControllerLayout.LayoutStyle.Separate)
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
                            {
                                Debug.Log("using mouse: player " + playerNumber + " of team " + teamIndex);
                                displayMouseUi = true;
                                mouseUiTeam = teamIndex;
                                mouseUiPlayer = playerNumber;
                            }
                        }
                    }

                    pi.SetIsUsingMouse(isUsingMouse);
                }
            }

        }

        UseMouseJoystickUI(displayMouseUi, mouseUiTeam, mouseUiPlayer);
    }
    public void UseMouseJoystickUI(bool isActive, int team, int player)
    {
        if (team < 0 || player <= 0) return;

        if (isActive)
        {
            duringRace.MouseJoystickSimulationVisualizer.Activate(team, player);
        }
        else
        {
            duringRace.MouseJoystickSimulationVisualizer.Deactivate();
        }
    }

    private void OnPaused(bool paused)
    {
        if (paused)
        {
            ShowCursor();
        }
        else
        {
            HideCursor();
        }
    }

    private void ShowCursor()
    {
        MetaManager.Instance.CursorHandler.SetToShowCursorWhenPossible();
    }

    private void HideCursor()
    {
        if (RaceManager.Instance.IsPaused)
        {
            return;
        }

        MetaManager.Instance.CursorHandler.SetToHideCursor();
    }

    public void SetMouseInput(Vector2 stickInput)
    {
        duringRace.MouseJoystickSimulationVisualizer.SetMouseInput(stickInput);
    }
    #endregion

    private void OnDestroy()
    {
        // Fail safe to display the cursor
        ShowCursor();

        // =================== PRE RACE =================== //

        RaceManager.Instance.OnPreRaceSetupStarted -= OnPreRaceSetupStarted;
        RaceManager.Instance.OnPreRaceSetupOver -= OnPreRaceSetupOver;

        // =================== DURING RACE =================== //

        RaceManager.Instance.OnRaceCountdownStarted -= OnRaceCountdownStarted;
        RaceManager.Instance.OnRaceCountdownFinished -= OnRaceCountdownFinished;

        RaceManager.Instance.OnRaceBegin -= OnRaceBegin;

        if (currentGameModeScript != null && currentHUDCounter != null)
        {
            currentGameModeScript.OnCollectableCountUpdated -= currentHUDCounter.UpdateCounter;
        }

        RaceManager.Instance.OnTeamFinishedLevel -= OnTeamFinishedRace;

        // =================== RACE FINISH =================== //
        RaceManager.Instance.OnRaceFinish -= OnRaceFinish;

        // =================== GENERAL =================== //

        RaceManager.Instance.OnGamePaused -= OnPaused;
        PopUpNotification.OnPopUpShown -= ShowCursor;
        PopUpNotification.OnPopUpDismissed -= HideCursor;
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnMouseAndKeyboardButtonsChanged -= DisplayMouseUIIfNeeded;
        }
    }
}