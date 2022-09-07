# Script Descriptions

Script files from the game I'm currently working on, Fling to the Finish: [Steam](https://store.steampowered.com/app/1054430/Fling_to_the_Finish/).

## Menu State Machine
- [IMenuState](Menus/IMenuState.cs): Interface for all menu-states in the state machine based menu architecture.
- [MenuState_LobbyScreen3D](Menus/MenuState_LobbyScreen3D.cs): The new 3D menu where players choose their teams. Better, stable and player-friendly UX version of the 2D menu that served the same purpose.
- **Deprecated** [MenuState_LobbyScreen](Menus/MenuState_LobbyScreen.cs): The game's old lobby screen where players chose their controller layout and their characters.
- [MenusControllerAssignmentManager](Menus/MenusControllerAssignmentManager.cs): The system which lets players spawn new characters to control after connecting a controller (or using the mouse and keyboard) and swap the layout of their controller character to be two characters controlled by the same controller, and vice versa.

## Networked Multiplayer & Matchmaking
- [NetworkManager](Network/NetworkManager.cs): The overarching network syncing script. Keeps track of all online systems such as client ID and connectivity status. Responsible for connecting the player to the network and creating / joining rooms. Also handles "Interest Groups", which is basically network culling.
- [NetworkManager_Matchmaking](Network/NetworkManager_Matchmaking.cs): The portion of NetworkManager that handles global match finding, match search timeouts and re-search, etc.

## Systems that require porting to different platforms
- [LeaderboardManager](Leaderboards/LeaderboardManager.cs): The master system that the game directly communicates with for getting and storing leaderboard data.
- [PlatformSpecificLeaderboardManager](Leaderboards/PlatformSpecificLeaderboardManager.cs): An abstract class that lays down instructions on how each platform (Steam/Switch etc.) should implement their own leaderboards while maintaining Fling's consistency and not affecting code for storing or retreiving leaderboard data. **Why an abstract class instead of interface?**: Because there is some event invoking functionality implented in this class that wouldn't change per platform.
- [AchievementsManager](Acheivements/AchievementsManager.cs): The master system that the game directly communicates with for storing player stats and unlocking achievements.
- [IPlatformSpecificAchievementsManager](Achievements/PlatformSpecificAchievementsManager.cs): An interface that defines how each platform (Steam/Switch etc.) should implement their own stats and achievements while maintaining Fling's consistency and not affecting code for setting stats and achievements.

## General Systems & Architecture
- [MetaManager](MetaManager.cs): The overarching game manager script. Handles systems required by all scenes, such as level loading.
- [RaceManager](RaceManager.cs): Manages all aspects of a race. Exists only in playable scenes (not menus). Handles things such as starting the race, keeping track of player checkpoints and respawning, winning, etc.
- [RaceUIManager](RaceUIManager.cs): Manages the HUD, pre-race and post-race UI of the game. Responds to race events fired by RaceManager.cs.

## Gameplay and physics (offline & network synced)
- [PlayerInput](PlayerInput.cs): Responsible for reading input from the player and passing it to PlayerMovement.cs and RopeManager.cs. Each player in the team has their own PlayerInput.cs script.
- [PlayerMovement](PlayerMovement.cs): Responsible for handling player gameplay after receiving input from PlayerInput.cs. Each player in the team has their own PlayerMovement.cs script.
- [RopeManager](RopeManager.cs): Responsible for common gameplay of the rope that is shared by two players. Each team has one RopeManager.cs script that takes input from both its PlayerInput.cs scripts.
- [FollowSpline](FollowSpline.cs): A generic script that takes makes an object follow a given spline using one of multiple methods. Has different options for syncing the movement of the object over the network.

## Unity Tools
- [FlingMenu](UnityTools/FlingMenu.cs): A collection of helper functions that appear as a toolbar to automate and make certain things easier when working on the game.
