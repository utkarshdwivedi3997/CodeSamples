using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fling.GameModes;
using TMPro;
using Fling.Saves;
using UnityEngine.EventSystems;

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
        private GameObject singleLineGroup;
        public GameObject SingleLineGroup { get { return singleLineGroup; } }

        [SerializeField]
        private TextMeshProUGUI singleModeNameText;
        public TextMeshProUGUI SingleModeNameText { get { return singleModeNameText; } }

        [SerializeField]
        private TextMeshProUGUI singleModeDescriptionText;
        public TextMeshProUGUI SingleModeDescriptionText { get { return singleModeDescriptionText; } }


        [Header("Double Line Description")]

        [SerializeField]
        private GameObject doubleLineGroup;
        public GameObject DoubleLineGroup { get { return doubleLineGroup; } }

        [SerializeField]
        private TextMeshProUGUI doubleModeNameText;
        public TextMeshProUGUI DoubleModeNameText { get { return doubleModeNameText; } }

        [SerializeField]
        private TextMeshProUGUI doubleModeDescriptionTextOne;
        public TextMeshProUGUI DoubleModeDescriptionTextOne { get { return doubleModeDescriptionTextOne; } }

        [SerializeField]
        private TextMeshProUGUI doubleModeDescriptionTextTwo;
        public TextMeshProUGUI DoubleModeDescriptionTextTwo { get { return doubleModeDescriptionTextTwo; } }
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
        private GameObject timerPanel;
        public GameObject TimerPanel { get { return timerPanel; } }
        public Animator TimerAnimator { get; private set; }

        [SerializeField]
        private TextMeshProUGUI timerText;
        public TextMeshProUGUI TimerText { get { return timerText; } }

        [Header ("During Race Rank Placements")]
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

        public void Init()
        {
            RankPlacementBoxes = new Image[] { rankPlacementBoxLeft, rankPlacementBoxRight };
            RankTexts = new TextMeshProUGUI[] { rankTextLeft, rankTextRight };

            RaceWinPanels = new RaceWinTeamPanel[] { raceWinPanelLeft, raceWinPanelRight };

            TimerAnimator = timerPanel.GetComponent<Animator>();
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
        private CanvasGroup canvasGroup;
        public CanvasGroup CanvasGroup { get { return canvasGroup; } }

        [SerializeField]
        private Transform placementsParent;
        public Transform PlacementsParent { get { return placementsParent; } }

        [SerializeField] private TextMeshProUGUI winTimerText;
        /// <summary>
        /// Text field to show the time taken by the first place team in THIS race
        /// </summary>
        public TextMeshProUGUI WinTimerText { get { return winTimerText; } }

        [SerializeField] private TextMeshProUGUI bestTimerText;
        public TextMeshProUGUI BestTimerText { get { return bestTimerText; } }

        [SerializeField] private GameObject newRecordObject;
        public GameObject NewRecordObject { get { return newRecordObject; } }

        [SerializeField] private GameObject bestTimerRow;
        /// <summary>
        /// 
        /// </summary>
        public GameObject BestTimerRow { get { return bestTimerRow; } }

        [SerializeField]
        private GameObject[] teamRows;
        /// <summary>
        /// Utkarsh, I don't really understand why I'm doing this but I'm falling your good example
        /// </summary>
        public GameObject[] TeamRows { get { return teamRows; } }

        [SerializeField]
        private TextMeshProUGUI[] teamFinishTexts;
        public TextMeshProUGUI[] TeamFinishTexts { get { return teamFinishTexts; } }

        [SerializeField]
        private GameObject[] endButtons;
        public GameObject[] EndButtons { get { return endButtons; } }

        [Header("Trophies")]
        [SerializeField]
        private Animator levelBeatenAnimator;
        public Animator LevelBeatenAnimator { get { return levelBeatenAnimator; } }
        [SerializeField]
        private Animator silverUnlockedAnimator;
        public Animator SilverUnlockedAnimator { get { return silverUnlockedAnimator; } }
        [SerializeField]
        private Animator goldUnlockedAnimator;
        public Animator GoldUnlockedAnimator { get { return goldUnlockedAnimator; } }
    }
    [SerializeField]
    private EndOfRace endOfRace;
    public EndOfRace EndOfRaceUI { get { return endOfRace; } }
    #endregion

    #region General
    private Rect[] camRects = new Rect[] { new Rect(0, 0, 1f, 1f), new Rect(0.5f, 0, 0.0f, 0.0f) };
    private GameModeScriptableObject currentGameModeObject;

    public static string[] RacePlacementStringVals = new string[] { "1<sup>st</sup>", "2<sup>nd</sup>", "3<sup>rd</sup>", "4<sup>th</sup>", "5<sup>th<sup>", "6<sup>th<sup>", "7<sup>th<sup>", "8<sup>th<sup>" };     // 1st, 2nd, 3th,... , 8th with proper superscripts

    private float previousBestTime;

    private int winPanelsActive = 0;

    private bool wasLevelBeatenBefore = false, wasSilverUnlockedBefore = false, wasGoldUnlockedBefore = false;
    #endregion

    #endregion

    private IEnumerator Start()
    {
        #region Immediate Initialization

        // =================== GENERAL =================== //

        winPanelsActive = 0;
        camRects = new Rect[] { new Rect(0, 0, 1f, 1f), new Rect(0.5f, 0, 0.0f, 0.0f) };

        RaceManager.Instance.OnGamePaused += OnPaused;

        // =================== PRE RACE =================== //

        currentGameModeObject = MetaManager.Instance.CurrentGameModeObject;
        RaceManager.Instance.OnPreRaceSetupStarted += OnPreRaceSetupStarted;

        preRace.CanvasGroup.gameObject.SetActive(false);

        // =================== DURING RACE =================== //

        duringRace.Init();

        RaceManager.Instance.OnRaceCountdownStarted += OnRaceCountdownStarted;
        RaceManager.Instance.OnRaceBegin += OnRaceBegin;

        duringRace.CountdownPanel.SetActive(false);
        duringRace.CanvasGroup.gameObject.SetActive(false);

        for (int i = 0; i < duringRace.RaceWinPanels.Length; i++)
        {
            duringRace.RaceWinPanels[i].gameObject.SetActive(false);
        }

        // =================== END OF RACE =================== //

        RaceManager.Instance.OnRaceFinish += OnRaceFinish;
        endOfRace.CanvasGroup.gameObject.SetActive(false);

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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // =================== GENERAL =================== //

        // End of race UI scaling
        camRects = TeamManager.Instance.TeamsOnThisClient == 1 ? camRects : new Rect[] { new Rect(0, 0, 0.5f, 1f), new Rect(0.5f, 0, 0.5f, 1f) };

        // =================== DURING RACE =================== //

        for (int i = 0; i < TeamManager.Instance.TeamsOnThisClient; i++)
        {
            duringRace.RaceWinPanels[i].SetSize(camRects[i]);
        }

        // =================== END OF RACE =================== //

        Fling.Levels.LevelScriptableObject currentLevel = MetaManager.Instance.CurrentLevel;
        wasLevelBeatenBefore = SaveManager.Instance.IsLevelBeaten(currentLevel);
        wasSilverUnlockedBefore = SaveManager.Instance.IsLevelSilverTrophyUnlocked(currentLevel);
        wasGoldUnlockedBefore = SaveManager.Instance.IsLevelGoldTrophyUnlocked(currentLevel);

        //endOfRace.SilverUnlockedAnimator.enabled = false;
        //endOfRace.GoldUnlockedAnimator.enabled = false;

        //endOfRace.LevelBeatenAnimator.gameObject.SetActive(wasLevelBeatenBefore);
        //endOfRace.SilverUnlockedAnimator.gameObject.SetActive(wasSilverUnlockedBefore);
        //endOfRace.GoldUnlockedAnimator.gameObject.SetActive(wasGoldUnlockedBefore);

        #endregion
    }

    #region PreRace
    private void OnPreRaceSetupStarted()
    {
        RaceManager.Instance.OnPreRaceSetupStarted -= OnPreRaceSetupStarted;
        RaceManager.Instance.OnPreRaceSetupOver += OnPreRaceSetupOver;

        previousBestTime = SaveManager.Instance.GetBestTime(MetaManager.Instance.CurrentLevel);

        preRace.CanvasGroup.gameObject.SetActive(true);
        preRace.CanvasGroup.alpha = 1f;

        if (currentGameModeObject != null)
        {
            if (currentGameModeObject.TwoLineDescription)
            {
                preRace.DoubleLineGroup.SetActive(true);
                preRace.DoubleModeNameText.text = currentGameModeObject.ModeName;
                preRace.DoubleModeDescriptionTextOne.text = currentGameModeObject.Description;
                preRace.DoubleModeDescriptionTextTwo.text = currentGameModeObject.DescriptionTwo;
                
            }
            else
            {
                preRace.SingleLineGroup.SetActive(true);
                preRace.SingleModeNameText.text = currentGameModeObject.ModeName;
                preRace.SingleModeDescriptionText.text = currentGameModeObject.Description;
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
    public void UpdateTimerText(float time)
    {
        duringRace.TimerText.text = Utilities.GetTimeFormatted(time);
    }
    #endregion

    #region DuringRace
    #region RaceCountdown
    private void OnRaceCountdownStarted()
    {
        RaceManager.Instance.OnRaceCountdownStarted -= OnRaceCountdownStarted;
        RaceManager.Instance.OnRaceCountdownFinished += OnRaceCountdownFinished;

        duringRace.CanvasGroup.gameObject.SetActive(true);
        duringRace.CountdownPanel.SetActive(true);
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

        RaceManager.Instance.OnTeamWin += OnTeamFinishedRace;
        
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

    private void OnTeamFinishedRace(int teamIndex)
    {
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
                RaceManager.Instance.OnTeamWin -= OnTeamFinishedRace;
            }
        }
    }
    #endregion

    #region RaceOver
    private void OnRaceFinish()
    {
        RaceManager.Instance.OnRaceFinish -= OnRaceFinish;
        RaceManager.Instance.OnTeamWin -= OnTeamFinishedRace;
        RaceManager.Instance.OnGamePaused -= OnPaused;

        // End of race, unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Deactivate extra UI
        for (int i = 0; i < duringRace.RaceWinPanels.Length; i++)
        {
            duringRace.RaceWinPanels[i].gameObject.SetActive(false);
        }
        winPanelsActive = 0;

        // Make all spectating teams see themselves
        for (int i = 0; i < TeamManager.Instance.TeamsOnThisClient; i++)
        {
            int idx = TeamManager.Instance.IndicesOfTeamsOnThisClient[i];

            UpdateSpectate(idx, idx);
        }

        duringRace.TimerAnimator.SetTrigger("Deactivate");

        ShowEndOfRaceUI();
    }

    /// <summary>
    /// UI to show at the end of the race
    /// </summary>
    private void ShowEndOfRaceUI()
    {
        endOfRace.CanvasGroup.gameObject.SetActive(true);
        float finishTime = RaceManager.Instance.RaceFinishTime;
        string formattedTime = Utilities.GetTimeFormatted(finishTime);
        endOfRace.WinTimerText.text = formattedTime;

        if (previousBestTime < 0) //No previous best time recorded
        {
            endOfRace.NewRecordObject.SetActive(true); //NEW RECORD!
        }
        else //There is a real previous best time
        {
            string formattedBestTime = Utilities.GetTimeFormatted(previousBestTime);
            endOfRace.BestTimerText.text = formattedBestTime;

            if (finishTime < previousBestTime) 
            {
                endOfRace.BestTimerRow.transform.SetSiblingIndex(1); //a hard value of 1 may only be correct in local play
                endOfRace.NewRecordObject.SetActive(true); //NEW RECORD!
            }
            else //No one got a better time. BOOO-WHOOO
            {
                endOfRace.WinTimerText.color = Color.white;
                endOfRace.BestTimerRow.transform.SetSiblingIndex(0);
                endOfRace.NewRecordObject.SetActive(false);
            }
        }

        StartCoroutine(ShowPlacements());

        StartCoroutine(ShowTrophies());
    }

    IEnumerator ShowPlacements()
    {
        yield return new WaitForSeconds(2f);

        List<int> teamRanks = RaceManager.Instance.FinalTeamIndicesOrderedByRank;

        int i = 0;
        for (i = 0; i < TeamManager.Instance.TotalNumberOfTeams; i++)
        {
            CharacterContent cc = TeamManager.Instance.TeamInstance[teamRanks[i]].GetComponent<CharacterContent>();
            endOfRace.TeamFinishTexts[i].text = cc.teamName;

            //Set placements active in order of children (since the previous best can be anywhere and we don't want the vertical layout group to snap)
            endOfRace.PlacementsParent.GetChild(i).gameObject.SetActive(true);
            yield return new WaitForSeconds(1.5f);
        }

        if (previousBestTime > 0) //There is a real recorded previous best, so make sure we turn on 1 additional row
        {
            endOfRace.PlacementsParent.GetChild(i).gameObject.SetActive(true);
        }

        StartCoroutine(ShowButtons());
    }

    IEnumerator ShowButtons()
    {
        yield return new WaitForSeconds(1.5f);
        

        for (int i = 0; i < endOfRace.EndButtons.Length; i++)
        {
            endOfRace.EndButtons[i].SetActive(true);

            if (i == 0)
            {
                EventSystem.current.SetSelectedGameObject(endOfRace.EndButtons[0]);
                RaceManager.Instance.EndOfRaceUIAnimationFinished();
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// Updates the spectator UI for spectating team
    /// </summary>
    /// <param name="spectatingTeam"></param>
    /// <param name="spectatedTeam"></param>
    public void UpdateSpectate(int spectatingTeam, int spectatedTeam)
    {
        if (TeamManager.Instance.IndicesOfTeamsOnThisClient.Contains(spectatingTeam))
        {
            int localTeamIdx = TeamManager.Instance.IndicesOfTeamsOnThisClient.IndexOf(spectatingTeam);
            int side = TeamManager.Instance.SpawnSideOfTeamsOnThisClient[localTeamIdx];

            duringRace.RaceWinPanels[side].SpectateTeam(spectatedTeam);
        }
    }
    #endregion

    #region OtherGeneralFunctions
    /// <summary>
    /// Deactivates all race UI
    /// </summary>
    public void DeactivateAllUI()
    {
        endOfRace.CanvasGroup.gameObject.SetActive(false);
    }

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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void SetMouseInput(Vector2 stickInput)
    {
        duringRace.MouseJoystickSimulationVisualizer.SetMouseInput(stickInput);
    }
    #endregion

    private void OnDestroy()
    {
        // Fail safe to display the cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // =================== PRE RACE =================== //

        RaceManager.Instance.OnPreRaceSetupStarted -= OnPreRaceSetupStarted;
        RaceManager.Instance.OnPreRaceSetupOver -= OnPreRaceSetupOver;

        // =================== DURING RACE =================== //

        RaceManager.Instance.OnRaceCountdownStarted -= OnRaceCountdownStarted;
        RaceManager.Instance.OnRaceCountdownFinished -= OnRaceCountdownFinished;

        RaceManager.Instance.OnRaceBegin -= OnRaceBegin;

        RaceManager.Instance.OnTeamWin -= OnTeamFinishedRace;

        // =================== RACE FINISH =================== //
        RaceManager.Instance.OnRaceFinish -= OnRaceFinish;

        // =================== GENERAL =================== //

        RaceManager.Instance.OnGamePaused -= OnPaused;

    }
}
