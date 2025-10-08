namespace VoilaTile.Snapper.Input
{
    using System;

    /// <summary>
    /// Manages the application's current input state and transitions.
    /// </summary>
    public class InputStateManager
    {
        private InputMode currentMode = InputMode.HotKey;

        private InputFeature currentFeature = InputFeature.Snap;

        /// <summary>
        /// Gets the current input mode.
        /// </summary>
        public InputMode CurrentMode => currentMode;

        /// <summary>
        /// Gets the current input feature.
        /// </summary>
        public InputFeature CurrentFeature => currentFeature;

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
        /// Switches to input mode if not already in it.
        /// </summary>
        public void SwitchToPowerGrabFeature()
        {
            this.currentFeature = InputFeature.PowerGrab;
        }

        /// <summary>
        /// Switches to input mode if not already in it.
        /// </summary>
        public void SwitchToQuickGrabFeature()
        {
            this.currentFeature = InputFeature.QuickGrab;
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

