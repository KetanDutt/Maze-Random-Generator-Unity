namespace Maze.Gameplay
{
    /// <summary>
    /// Lightweight coordination channel between the UI (<c>MazeHud</c>) and the gameplay code.
    /// </summary>
    /// <remarks>
    /// IMGUI and the first person controller would otherwise fight over the mouse: the controller
    /// wants to lock the cursor, the HUD wants to show a cursor for its buttons. The HUD publishes
    /// its state here once per frame and the controller reads it, which keeps both components
    /// independent of each other.
    /// </remarks>
    public static class MazeInputGateway
    {
        /// <summary>Set by the HUD while the pointer is over one of its panels.</summary>
        public static bool PointerOverUi { get; set; }

        /// <summary>Set by the HUD while a panel is open or a text field has keyboard focus.</summary>
        public static bool UiBlocksGameplay { get; set; }
    }
}
