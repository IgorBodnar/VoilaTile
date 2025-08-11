namespace VoilaTile.Snapper.Input
{
    using System;

    /// <summary>
    /// Represents the current input mode of the application.
    /// </summary>
    public enum InputMode
    {
        /// <summary>
        /// Listening for global hotkey.
        /// </summary>
        HotKey,

        /// <summary>
        /// Receiving character input for tile selection.
        /// </summary>
        Input
    }

    /// <summary>
    /// Manages the application's current input state and transitions.
    /// </summary>
    public class InputStateManager
    {
        private InputMode currentMode = InputMode.HotKey;

        /// <summary>
        /// Gets the current input mode.
        /// </summary>
        public InputMode CurrentMode => currentMode;

        /// <summary>
        /// Occurs when the input mode changes.
        /// </summary>
        public event Action<InputMode>? ModeChanged;

        /// <summary>
        /// Switches to input mode if not already in it.
        /// </summary>
        public void EnterInputMode()
        {
            if (currentMode != InputMode.Input)
            {
                currentMode = InputMode.Input;
                ModeChanged?.Invoke(currentMode);
            }
        }

        /// <summary>
        /// Switches to hotkey mode if not already in it.
        /// </summary>
        public void ReturnToHotKeyMode()
        {
            if (currentMode != InputMode.HotKey)
            {
                currentMode = InputMode.HotKey;
                ModeChanged?.Invoke(currentMode);
            }
        }
    }
}

