# Script Descriptions

Script files from the game I'm currently working on, Fling to the Finish: https://www.splitsidegames.com/fling-to-the-finish.

## Menu State Machine
- [IMenuState](IMenuState.cs): Interface for all menu-states in the state machine based menu architecture.
- [MenuState_LobbyScreen](MenuState_LobbyScreen.cs): The game's lobby screen where players chose their controller layout and their characters.
- [Lobby_ControllerSetup](Lobby_ControllerSetup.cs): One of the few sub-scripts that communicate to with MenuState_LobbyScreen.cs. Handles team and controller layout selection.

> Portions of the scripts mentioned below were contributed to by my teammates as well.

## Systems
- [MetaManager](MetaManager.cs): The overarching game manager script. Handles systems required by all scenes, such as level loading.
- [NetworkManager](NetworkManager.cs): The overarching network syncing script. Keeps track of all online systems such as client ID and connectivity status.
- [RaceManager](RaceManager.cs): Manages all aspects of a race. Exists only in playable scenes (not menus). Handles things such as starting the race, keeping track of player checkpoints and respawning, winning, etc.
- [RaceUIManager](RaceUIManager.cs): Manages the HUD, pre-race and post-race UI of the game. Responds to race events fired by RaceManager.cs.

## Gameplay and physics
- [PlayerInput](PlayerInput.cs): Responsible for reading input from the player and passing it to PlayerMovement.cs and RopeManager.cs. Each player in the team has their own PlayerInput.cs script.
- [PlayerMovement](PlayerMovement.cs): Responsible for handling player gameplay after receiving input from PlayerInput.cs. Each player in the team has their own PlayerMovement.cs script.
- [RopeManager](RopeManager.cs): Responsible for common gameplay of the rope that is shared by two players. Each team has one RopeManager.cs script that takes input from both its PlayerInput.cs scripts.
