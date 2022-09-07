using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Battlehub.SplineEditor;
using Photon.Pun;

/// <summary>
/// Base class for making RigidBodies follow a spline.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FollowSpline : MonoBehaviour, IPunObservable
{

    public enum FollowMode { Once, Loop, PingPong };
    public enum WaitMode { Timed, Conditional }

    public enum MoveMode { Speed, Time };
    [Header("Follow spline variables")]
    public SplineBase spline;

    private float timeSinceProgressStarted;

    public bool reverseDir = false;

    [Tooltip("This will offset the starting point of the movement."), Range(0f, 1f)]
    public float startOffset = 0;

    [Tooltip("Check this to rotate the GameObject's forward depending on the spline rotations.")]
    public bool lookForward;
    [Tooltip("Once - Follow the spline once, Loop - loop on the spline, PingPong - Move back and forth on the spline")]
    public FollowMode followMode;

    public bool IsMoving { get; private set; } = true;

    [Tooltip("Timed - wait at spline points for given interval, Conditional - Wait until condition is met")]
    public WaitMode waitMode = WaitMode.Timed;

    public bool StartMovingOnSpawn = true;
    [SerializeField] private bool moveToStartOnInit = true;

    public float progress { get; private set; }             // current position of the GameObject on the spline
    private bool goingForward = true;   // whether the GameObject is going forward or backward on the spline

    private Rigidbody rb;
    private float nextProgress;         // the position of the next control point

    protected SplineControlPoint[] ctrlPoints;    // The ctrlPoint GameObjects attached to this spline.
    protected SplineControlPoint nextCtrlPoint;           // The SplineControlPoint that will be reached next.
    protected SplineControlPoint prevCtrlPoint;
    protected int nextCtrlPtIdx;
    protected int prevCtrlPtIdx;

    private bool hasInitialized = false;

    protected bool finishedOnce = false;

    [Header("(Optional) Online Sync Related")]
    [SerializeField] private bool waitUntilRaceStarts = false;
    [SerializeField] private bool followEvenIfNonHost = false;
    [SerializeField] private bool isPartOfInterestGroup = false;
    [SerializeField] private NetworkManager.InterestGroup interestGroup = NetworkManager.InterestGroup.InterestGroup1;
    [SerializeField] private bool syncProgress = false;
    [SerializeField] private bool alsoExtrapolateProgress = false;
    [SerializeField] private float maxLag = 0.5f;
    private float lag = 0f;
    private float lagCompProgress = 0f;

    private PhotonView pv;
    private PhotonView photonView
    { 
        get
        {
            if (pv == null)
            {
                pv = GetComponent<PhotonView>();
            }

            return pv;
        } 
    }

    [Header("Movement")]
    public MoveMode moveMode;
    [Range(0, Mathf.Infinity), SerializeField, HideInInspector]
    protected float speed;

    [HideInInspector] public float moveTime = 3f;

    protected virtual void Start()
    {
        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        IsMoving = false;
        hasInitialized = false;
        while (spline == null)
        {
            yield return null;
        }

        rb = GetComponent<Rigidbody>();
        ctrlPoints = spline.GetSplineControlPoints();
        GetNextControlPoint(startOffset);
        progress = startOffset;
        timeSinceProgressStarted = moveTime * startOffset;

        if (moveToStartOnInit)
        {
            rb.MovePosition(spline.GetPoint(startOffset));
        }

        if (StartMovingOnSpawn)
        {
            if (!PhotonNetwork.OfflineMode && waitUntilRaceStarts)
            {
                // if online mode, and we should wait until the race starts, then wait until the race has started
                yield return new WaitUntil(() => RaceManager.Instance.HasRaceStarted);
            }
            IsMoving = true;
        }

        hasInitialized = true;
        goingForward = !reverseDir;

        if (!spline.Loop && followMode == FollowMode.Loop)
        {
            Debug.LogWarning("<color=blue>The spline ends are not connected but the follow mode is set to Loop. This might lead to unwanted behaviour.</color>");
        }
    }

    // Update is called once per frame
    protected virtual void FixedUpdate() {
        if (hasInitialized)
        {
            //if (!hasReachedCtrlPoint)
            //{
            // Could divide this into if-else but then will have to make a function for the block,
            // as both if and else will do the same thing
            // This is some weird logic.
            // PLEASE DO NOT CHANGE THIS AT ALL. ANYTHING ELSE WILL BREAK THE MOVEMENT.
            // Because of the override in GetNextControlPoint() for the nextProgress being > 1,
            // only having the last two condition lines made the thing evaluate multiple times
            // because progress>=nextProgress would always be true ( 0.something > 0 == true, always), and progress would always be greater than 0 and less than 1
            // To stop that bug, I had to add these extra conditions.
            // Probably a better way of handling this, but it's 6:29 am and I'm kind of dead.
            if (nextProgress == 0f && progress <= 0)// If you're going backwards, and the t value of your next control point is 0 (the first point)
            {
                ReachedControlPoint();
            }
            else if (nextProgress > 0f && goingForward)
            {
                if (progress >= nextProgress)
                {
                    ReachedControlPoint();
                }
            }
            else if (nextProgress > 0f && !goingForward)
            {
                if (progress <= nextProgress)
                {
                    ReachedControlPoint();
                }
            }

            if (IsMoving)
            {
                Follow();
            }
        }
    }

    protected virtual void ReachedControlPoint()
    {
        // REACHED CTRL POINT
        float waitTime = nextCtrlPoint.WaitTime;
        if (waitMode == WaitMode.Timed && waitTime > 0f && (progress - nextProgress != 0f) )
        {

            if (IsMoving) StartCoroutine(WaitAtPos(waitTime));
        }
        else if (waitMode == WaitMode.Conditional && waitTime > 0f)
        {
            IsMoving = false;
        }
        //hasReachedCtrlPoint = true;
        GetNextControlPoint(progress);
    }

    public delegate void FinishedOnceDelegate();
    public event FinishedOnceDelegate FinishedOnce;
    /// <summary>
    /// Follows the spline.
    /// </summary>
    private void Follow()
    {
        float vel = spline.GetVelocity(progress).magnitude * spline.CurveCount;
        
        if (syncProgress && alsoExtrapolateProgress)
        {
            switch (moveMode)
            {
                case MoveMode.Speed: lagCompProgress = (lag * speed) / vel;
                    break;
                case MoveMode.Time:
                    timeSinceProgressStarted += lag;
                    break;
            }

            // lag needs to be reset to 0 here so that we don't keep adding extra lag compensation until we get another
            // stream from the sender
            lag = 0f;
        }

        if (goingForward)
        {
            switch (moveMode)
            {
                case MoveMode.Speed: progress += ((Time.deltaTime * speed) / vel) + lagCompProgress;
                    break;
                case MoveMode.Time:
                    timeSinceProgressStarted += Time.deltaTime;
                    float percentageComplete = timeSinceProgressStarted / moveTime;
                    progress = Mathf.Lerp(0f, 1f, percentageComplete);
                    break;
            }

            if (progress >= 1f)
            {
                switch (followMode)
                {
                    case FollowMode.Once:
                        if (!finishedOnce)
                        {
                            finishedOnce = true;
                            progress = 1f;
                            if (FinishedOnce != null)
                            {
                                FinishedOnce();
                            }
                        }
                        break;
                    case FollowMode.Loop:
                        progress -= 1f;
                        timeSinceProgressStarted = 0f;
                        break;
                    case FollowMode.PingPong:
                        progress = 2f - progress;
                        timeSinceProgressStarted = 0f;
                        goingForward = false;
                        break;
                }
            }
        }
        else
        {
            switch (moveMode)
            {
                case MoveMode.Speed:
                    progress -= ((Time.deltaTime * speed) / vel) + lagCompProgress;
                    break;
                case MoveMode.Time:
                    timeSinceProgressStarted += Time.deltaTime;
                    float percentageComplete = timeSinceProgressStarted / moveTime;
                    progress = Mathf.Lerp(1f, 0f, percentageComplete);
                    break;
            }

            if (progress <= 0f)
            {
                switch (followMode)
                {
                    case FollowMode.Once:
                        progress = 0f;
                        if (FinishedOnce != null)
                        {
                            FinishedOnce();
                        }
                        break;
                    case FollowMode.Loop:
                        goingForward = !reverseDir;
                        timeSinceProgressStarted = 0f;
                        progress = 1f;
                        break;
                    case FollowMode.PingPong:
                        progress = 0f;
                        timeSinceProgressStarted = 0f;
                        goingForward = true;
                        break;
                }
            }
        }

        // apply the progress only if:
        // 1. offline mode
        // 2. online and this is the master client
        bool shouldApply = PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient || followEvenIfNonHost;
        
        if (!shouldApply && isPartOfInterestGroup)
        {
        // 3. online + this is non-master client + this script is on an interest group + this client is not subscribed to that interest group
            shouldApply = !NetworkManager.Instance.IsSubscribedToInterestGroup(interestGroup);
        }

        if (shouldApply)
        {
            Vector3 newPos = spline.GetPoint(progress);

            rb.MovePosition(newPos);
            //transform.position = newPos;

            // Affect rotations if enabled
            if (lookForward)
            {
                // If you use transform to rotate, the object will stop moving because it's using rigidbody.MovePosition();
                // I think it's because both systems should be consistent as to whether they use physics or transform movements.
                rb.MoveRotation(Quaternion.LookRotation(spline.GetDirection(progress), transform.up));
                //transform.LookAt(newPos + spline.GetDirection(progress));
                //transform.rotation = ;
            }
        }
    }

    /// <summary>
    /// Wait at the given position for t seconds.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    IEnumerator WaitAtPos(float t)
    {
        IsMoving = false;
        Debug.Log("<color=blue>Waiting for " + t + " seconds.</color>");
        yield return new WaitForSeconds(t);
        Debug.Log("<color=blue>Resuming movement.</color>");
        IsMoving = true;
        yield return 0;
    }

    /// <summary>
    /// Wait for t seconds before moving again.
    /// </summary>
    /// <param name="t"></param>
    public void WaitBeforeMoving(float t)
    {
        StartCoroutine(WaitAtPos(t));
    }

    public void StartMoving()
    {
        IsMoving = true;
    }

    public void StartMovingAtControlPoint(int startIndex)
    {
        if (startIndex % 3 != 0)
        {
            Debug.LogError("Starting index for Follow Spline is " + startIndex + " but needs to be a multiple of 3");
            return;
        }

        if ((startIndex != 0))
        {
            int curveIndex = startIndex / 3;
            float deltaTPerCurve = 1.0f / spline.CurveCount;
            float t = curveIndex * deltaTPerCurve;

            progress = t;
            GetNextControlPoint(progress + 0.01f);
        }

        IsMoving = true;
    }

    /// <summary>
    /// Starts moving from a point on the spline that is the closest to the given Vector3 point
    /// </summary>
    /// <param name="testPoint"></param>
    public void StartMovingAtPointClosestTo(Vector3 testPoint)
    {
        // Get the spline's closest delta T (0 <= T <= 1)
        float closestT = spline.ClosestTOnSpline(testPoint);
        StartMovingWithOffset(closestT);
    }

    /// <summary>
    /// Starts moving from a point at the given delta T start offset (0 <= startOffset <= 1)
    /// </summary>
    /// <param name="startOffset"></param>
    public void StartMovingWithOffset(float startOffset)
    {
        if (startOffset < 0f)
        {
            startOffset = 0f;
        }
        else if (startOffset > 1f)
        {
            startOffset = 1f;
        }

        this.startOffset = startOffset;
        progress = startOffset;
        GetNextControlPoint(progress);
        IsMoving = true;
    }

    /// <summary>
    /// Gets the next control point.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    private void GetNextControlPoint(float t)
    {
        int curveIndex = 0;
        UpdateCtrlPtIndices(t, out curveIndex);

        SplineControlPoint ctrlPoint = ctrlPoints[nextCtrlPtIdx];
        nextCtrlPoint = ctrlPoint;
        nextProgress = spline.GetT(curveIndex, nextCtrlPoint.transform.position);

        // Override progress in the case of looped splines
        if (nextProgress > 1f && followMode == FollowMode.Loop)
        {
            nextCtrlPoint = ctrlPoints[0];
            nextProgress = 0f;
        }
        //Debug.Log("next progress = " + nextProgress);
    }

    private void UpdateCtrlPtIndices(float t, out int curveIndex)
    {
        if (t <= 0f) t = 0.01f;
        float deltaTPerCurve = 1.0f / spline.CurveCount;

        // If moving forward, we want to get the NEXT control point, so use CeilToInt.
        // If moving backward (in the case of PingPong, we want to get the PREVIOUS control point, so use FloorToInt.
        curveIndex = goingForward ? Mathf.CeilToInt(t / deltaTPerCurve) : Mathf.FloorToInt(t / deltaTPerCurve);

        prevCtrlPoint = nextCtrlPoint;
        prevCtrlPtIdx = nextCtrlPtIdx;
        nextCtrlPtIdx = curveIndex * 3;

        if (nextCtrlPtIdx >= ctrlPoints.Length)
        {
            nextCtrlPtIdx = ctrlPoints.Length - 1;
        }
    }

    /// <summary>
    /// Change direction of movement
    /// </summary>
    public void SwitchDirection()
    {
        goingForward = !goingForward;
    }

    public void StopMoving()
    {
        IsMoving = false;
    }

    public void ReturnToOrigin(bool alsoMove = true)
    {
        progress = 0;
        nextProgress = 0;
        if (hasInitialized && alsoMove)
            rb.MovePosition(spline.GetPoint(0));
    }

    private void OnValidate()
    {
        goingForward = !reverseDir;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (this.syncProgress)
                stream.SendNext(this.progress);
        }
        else //stream is reading
        {
            if (!syncProgress)
                return;

            lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            if (lag > maxLag)
            {
                lag = maxLag;
            }

            progress = (float)stream.ReceiveNext(); // add lag compensation in Follow() function instead of here
        }
    }

}
