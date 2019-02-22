using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// The main manager for the networked portion of Fling to the Finish
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks {

    public static NetworkManager Instance { get; private set; }

    private bool isConnecting = false;                              // is the client attempting to connect to a network?
    public const int MAX_CLIENTS = 2;                              // Max number of players allowed to join a room

    private int playersLoaded = 0;                                  // number of players loaded in a level

    private List<List<string>> teamClientIds = new List<List<string>>(); // Index 0 is team 1, contains a list of client ids for that team [["0", "1"], ["0",null], ["1",null], ["2",null]]
    public List<List<string>> TeamClientIDs
    {
        get { return teamClientIds; }
    }
    /// <summary> 
    /// Have all players loaded in the current level?
    /// </summary>
    public bool AllPlayersLoaded { get; private set; }

    public string UserId { get; set; }

    /// <summary>
    /// Number of clients in this room
    /// </summary>
    public int NumberOfClients { get { return PhotonNetwork.CurrentRoom.PlayerCount; } }

    /// <summary>
    /// This client's number. This can be used to set the team/player numbers in TeamManager
    /// </summary>
    public int ClientNumber { get; private set; }

    /// <summary>
    /// Total number of teams across all clients in the current room, including single screen clients (1 team) and split screen clients (2 teams)
    /// </summary>
    public int NumberOfTeams { get; private set; }

    public List<int> TeamIndicesOrdered { get; private set; }

    /*public List<pair<int>> = { {0,6} {0, -1}, 1, 2 }; // Gets set in Main Menu (assingned here) sync across

        public List<GameObject> teams //Team magager
        public List<int> places*/     // Race Manager

    void Awake () {
        // Singleton Instance
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
	}

    private void Start()
    {
        PhotonNetwork.GameVersion = MetaManager.Instance.GameVersion;       // Set photon network game version
        //TeamIndicesOrdered = new List<int>(new int[] { -1, -1 });
        TeamIndicesOrdered = new List<int>();

        for (int i = 0; i < 8; i++) {
            teamClientIds.Add(new List<string>());
            teamClientIds[i].Add(null);
            teamClientIds[i].Add(null);
        }
    }

    public void Connect()
    {
        isConnecting = true;
        Debug.Log("Connecting...");
        PhotonNetwork.ConnectUsingSettings();
    }

    /// <summary>
    /// Public facing function to be called when loading a new level
    /// </summary>
    public void LoadingNewLevel()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("LoadingNewLevelLogic", RpcTarget.AllBufferedViaServer);
        }
    }

    /// <summary>
    /// The actual logic for the function LoadingNewLevel()
    /// </summary>
    [PunRPC]
    private void LoadingNewLevelLogic()
    {
        AllPlayersLoaded = false;
        playersLoaded = 0;
    }

    #region RPCs
    /// <summary>
    /// Public facing function to be called when a level has finished loading on a client
    /// </summary>
    public void PlayerLoaded()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            photonView.RPC("PlayerLoadedLogic", RpcTarget.AllBufferedViaServer);
        }
    }

    /// <summary>
    /// The actual logic for the function for PlayerLoaded()
    /// </summary>
    [PunRPC]
    private void PlayerLoadedLogic()
    {
        playersLoaded++;
        if (playersLoaded == PhotonNetwork.CurrentRoom.PlayerCount)
        {
            AllPlayersLoaded = true;
            Debug.Log("All players loaded!");
            for (int i = 0; i < 2; i++)
            {
                Debug.Log("[ " + (teamClientIds[i][0] == null ? "null" : teamClientIds[i][0]) + " , " + (teamClientIds[i][1] == null ? "null" : teamClientIds[i][1]) + " ]");
                if (teamClientIds[i].Contains(PhotonNetwork.LocalPlayer.UserId))
                {
                    TeamIndicesOrdered.Add(i);
                }
            }
        }
    }

    /// <summary>
    /// Add 1 or 2 teams to the total number of teams across all clients.
    /// This function calls an RPC call across all clients and adds the numToAdd to NumberOfTeams on all clients
    /// </summary>
    /// <param name="numToAdd"></param>
    public void AddNumberOfTeams(int numToAdd) {

        if (!PhotonNetwork.OfflineMode)
        {
            /* Split screen MP
            // Stuff to do only when this client calls this function
            if (numToAdd >= 1)  // > 1 team
            {
                TeamIndicesOrdered[0] = NumberOfTeams;          // NumberOfTeams starts at 1. TeamIndices start at 0. This is basically (NumberOfTeams + 1) - 1. We need to add one because that addition hasn't happened to NumberOfTeams YET, and then subtract 1 because of the 0 indexing.
                if (numToAdd == 2) // 2 teams
                {
                    TeamIndicesOrdered[1] = NumberOfTeams + 1;  // Same logic as above
                }
            }*/

            // Stuff to do on all clients
            photonView.RPC("AddNumberOfTeamsRPC", RpcTarget.AllBufferedViaServer, numToAdd);
        }
    }

    /// <summary>
    /// RPC for AddNumberOfTeams(), called on all clients
    /// </summary>
    /// <param name="numToAdd"></param>
    [PunRPC]
    private void AddNumberOfTeamsRPC(int numToAdd) {

        NumberOfTeams += numToAdd;
    }

    /// <summary>
    /// Add 1 or 2 teams to the total number of teams across all clients.
    /// This function calls an RPC call across all clients and sets the NumberOfTeams to numToSet on all clients
    /// </summary>
    /// <param name="numToSet"></param>
    public void SetNumberOfTeams(int numToSet) {

        if (!PhotonNetwork.OfflineMode)
        {            
            // Stuff to do only when this client calls this function
            if (numToSet == 0) // if resetting
            {
                TeamIndicesOrdered = new List<int>(new int[] { -1, -1 });
            }

            // Stuff to do on all clients
            photonView.RPC("SetNumberOfTeamsRPC", RpcTarget.AllBufferedViaServer, numToSet);
        }
    }

    /// <summary>
    /// RPC for SetNumberOfTeams(), called on all clients
    /// </summary>
    /// <param name="numToSet"></param>
    [PunRPC]
    private void SetNumberOfTeamsRPC(int numToSet) {

        NumberOfTeams = numToSet;
    }


    public void SetTeamNumbersClientId(int teamNumber, string clientId) {
        if (!PhotonNetwork.OfflineMode)
        {            
            // Stuff to do on all clients
            photonView.RPC("SetTeamNumbersClientIdRPC", RpcTarget.AllBufferedViaServer, teamNumber, clientId);
        }
    }

    [PunRPC]
    private void SetTeamNumbersClientIdRPC(int teamNumber, string clientId) {

        if(teamClientIds[teamNumber][0] == null) {
            teamClientIds[teamNumber][0] = clientId;
        }
        else if(teamClientIds[teamNumber][1] == null){
            teamClientIds[teamNumber][1] = clientId;
        }
        else {
            Debug.LogWarning("somethings not right in controller RPC set");
        }
        Debug.Log(teamClientIds.ToString());
    }

    public void RemoveTeamNumbersClientId(int teamNumber, string clientId) {
        if (!PhotonNetwork.OfflineMode)
        {            
            // Stuff to do on all clients
            photonView.RPC("RemoveTeamNumbersClientIdRPC", RpcTarget.AllBufferedViaServer, teamNumber, clientId);
        }
    }

    [PunRPC]
    private void RemoveTeamNumbersClientIdRPC(int teamNumber, string clientId) {
        
        if((teamClientIds[teamNumber][0] == clientId) && (teamClientIds[teamNumber][1] == clientId)) {
            teamClientIds[teamNumber][1] = null;
        }
        else if(teamClientIds[teamNumber][0] == clientId) {
            teamClientIds[teamNumber][0] = null;
        }
        else if(teamClientIds[teamNumber][1] == clientId){
            teamClientIds[teamNumber][1] = null;
        }
        else {
            Debug.LogWarning("somethings not right in controller RPC REEEmove");
        }
        Debug.Log(teamClientIds.ToString());
    }

    #endregion

    #region PUN Callbacks
    public override void OnConnectedToMaster()
    {
        // we don't want to do anything if we are not attempting to join a room. 
        // this case where isConnecting is false is typically when you lost or quit the game, when this level is loaded, OnConnectedToMaster will be called, in that case
        // we don't want to do anything.
        if (isConnecting)
        {
            Debug.Log("OnConnectedToMaster: Next -> try to Join Random Room");
            Debug.Log("PUN Basics Tutorial/Launcher: OnConnectedToMaster() was called by PUN. Now this client is connected and could join a room.\n Calling: PhotonNetwork.JoinRandomRoom(); Operation will fail if no room found");

            // #Critical: The first we try to do is to join a potential existing room. If there is, good, else, we'll be called back with OnJoinRandomFailed()
            Photon.Pun.PhotonNetwork.JoinRandomRoom();
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("<Color=Red>OnJoinRandomFailed</Color>: Next -> Create a new Room");
        Debug.Log("PUN Basics Tutorial/Launcher:OnJoinRandomFailed() was called by PUN. No random room available, so we create one.\nCalling: PhotonNetwork.CreateRoom");

        // #Critical: we failed to join a random room, maybe none exists or they are all full. No worries, we create a new room.
        Photon.Pun.PhotonNetwork.CreateRoom(null, new Photon.Realtime.RoomOptions { MaxPlayers = MAX_CLIENTS });
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        Debug.Log("<Color=Red>OnDisconnected</Color> " + cause);
        Debug.LogError("PUN Basics Tutorial/Launcher:Disconnected");

        isConnecting = false;
    }

    public delegate void OnJoinedRoomDelegate();
    public event OnJoinedRoomDelegate JoinedRoom;

    public override void OnJoinedRoom()
    {
        Debug.Log("<Color=Green>OnJoinedRoom</Color> with " + Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount + " Player(s)");
        Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.\nFrom here on, your game would be running.");

        ClientNumber = PhotonNetwork.CurrentRoom.PlayerCount - 1;       // Client numbers start from 0

        // #Critical: We only load if we are the first player, else we rely on  PhotonNetwork.AutomaticallySyncScene to sync our instance scene.
        if (Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            //Debug.Log("We load " + CurrentLevel.SceneName);

            // #Critical
            // Load the Room Level. 
            // Photon.Pun.PhotonNetwork.LoadLevel("PunBasics-Room for 1");
            //StartCoroutine(LoadGameLevelCoroutine(CurrentLevel.SceneName));

        }

        if (JoinedRoom != null)
        {
            JoinedRoom();
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player other)
    {
        Debug.Log("OnPlayerEnteredRoom() " + other.NickName); // not seen if you're the player connecting
        if (PhotonNetwork.CurrentRoom.PlayerCount == MAX_CLIENTS)
        {
            Debug.Log("Max players in room. Closing the room.");
            PhotonNetwork.CurrentRoom.IsVisible = true;
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }
        // if (PhotonNetwork.IsMasterClient)
        // {
        // Debug.LogFormat("OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom

        //Restart();
        // }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
    }

    #endregion
}
