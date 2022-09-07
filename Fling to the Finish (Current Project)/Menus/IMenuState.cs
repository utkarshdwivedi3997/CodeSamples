using System.Collections;
using UnityEngine;

namespace Menus
{
    public interface IMenuState
    {

        /// <summary>
        /// The state of this MenuState
        /// </summary>
        MenuState State { get; }
        /// <summary>
        /// The next menu state
        /// </summary>
        MenuState NextState { get; }
        /// <summary>
        /// The previous menu state
        /// </summary>
        MenuState PreviousState { get; }
        /// <summary>
        /// Location of the camera to look at this menu
        /// </summary>
        Transform CameraLocation { get; }
        /// <summary>
        /// Initializes this state. If there is something that needs to be done in this menu state
        /// before this state is activated, do it in here.
        /// For example, spawning character select characters/world and level select islands
        /// during the splash screen loading
        /// </summary>
        void InitState();
        /// <summary>
        /// Sets this state to be the current active state
        /// </summary>
        void ActivateState();
        /// <summary>
        /// Is called when we know this is no longer the active state
        /// </summary>
        void DeactivateState();
        /// <summary>
        /// Called when the camera starts overshooting beyond the final camera position of this state.
        /// </summary>
        void StateScreenTransitionFinish();
        /// <summary>
        /// Takes horizontal input for moving the snail
        /// </summary>
        /// <param name="horizontalInput">Horizontal input value</param>
        /// <param name="verticalInput">Vertical input value</param>
        /// <param name="rewiredPlayerID">Rewired Player ID of the player whose input this is</param>
        /// <returns></returns>
        bool Select(float horizontalInput, float verticalInput, int rewiredPlayerID = 0, int playerNumber = 0);
        /// <summary>
        /// Select this value.
        /// </summary>
        bool Submit(int rewiredPlayerID = 0, int playerNumber = 0);
        /// <summary>
        /// Makes a selection
        /// </summary>
        bool Pick(int rewiredPlayerID = 0, int playerNumber = 0);
        /// <summary>
        /// Cancel/Go back
        /// </summary>
        bool Cancel(int rewiredPlayerID = 0, int playerNumber = 0);
    }
}
