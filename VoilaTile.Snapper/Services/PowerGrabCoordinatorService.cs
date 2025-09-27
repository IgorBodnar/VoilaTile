namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using VoilaTile.Snapper.Input;

    /// <summary>
    /// Coordinates Power Mode lifecycle and input handling.
    /// Step 3: skeleton; no window enumeration or thumbnails yet.
    /// </summary>
    internal sealed class PowerGrabCoordinatorService
    {
        #region Fields

        private readonly InputStateManager inputState;

        // TODO (Step 4+): inject real services here
        // private readonly IWindowCacheService cache;
        // private readonly IWindowEnumerator enumerator;
        // private readonly IWindowIconService icons;
        // private readonly IWindowFocusService focus;
        // private readonly IHintService hints;

        private bool isActive;

        // Very temporary placeholder window for Step 3 verification.
        private Window? placeholderWindow;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerGrabCoordinatorService"/> class.
        /// </summary>
        /// <param name="inputState">The shared input state manager.</param>
        public PowerGrabCoordinatorService(InputStateManager inputState)
        {
            this.inputState = inputState ?? throw new ArgumentNullException(nameof(inputState));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether Power Mode is currently active.
        /// </summary>
        public bool IsActive => this.isActive;

        #endregion

        #region Methods

        /// <summary>
        /// Begins Power Mode: switches input routing and shows a temporary placeholder.
        /// </summary>
        public void Begin()
        {
            if (this.isActive)
            {
                return;
            }

            this.isActive = true;

            // Switch global input routing to Power Mode.
            this.inputState.EnterInputMode();
            this.inputState.SwitchToPowerGrabFeature();

            Debug.WriteLine("[PowerMode] Begin");

            // TEMP visual proof for Step 3 only.
            this.placeholderWindow = new Window
            {
                Title = "VoilaTile — Power Mode (placeholder)",
                Width = 560,
                Height = 360,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false,
                Topmost = true,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Power Mode is active.\nType letters / Space / Backspace / Esc.\n(Placeholder — real overlay arrives in Step 5+)",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(24),
                },
            };

            this.placeholderWindow.Closed += (_, __) => this.placeholderWindow = null;
            this.placeholderWindow.Show();
        }

        /// <summary>
        /// Cancels Power Mode and restores global input to HotKey mode.
        /// </summary>
        public void Cancel()
        {
            if (!this.isActive)
            {
                return;
            }

            Debug.WriteLine("[PowerMode] Cancel");

            try
            {
                this.placeholderWindow?.Close();
            }
            catch
            {
                // ignore
            }
            finally
            {
                this.placeholderWindow = null;
            }

            this.inputState.ReturnToHotKeyMode();
            this.isActive = false;
        }

        /// <summary>
        /// Handles a character typed in Power Mode (hint buffer in future steps).
        /// </summary>
        /// <param name="c">The input character.</param>
        public void ForwardCharacter(char c)
        {
            if (!this.isActive)
            {
                return;
            }

            Debug.WriteLine($"[PowerMode] Char: {c}");
            // TODO: Step 5 — update VM hint buffer / highlight selection
        }

        /// <summary>
        /// Handles Backspace during Power Mode.
        /// </summary>
        public void Backspace()
        {
            if (!this.isActive)
            {
                return;
            }

            Debug.WriteLine("[PowerMode] Backspace");
            // TODO: Step 5 — update buffer
        }

        /// <summary>
        /// Accepts the current selection (Space/Enter). For now, just closes.
        /// </summary>
        public void AcceptSelection()
        {
            if (!this.isActive)
            {
                return;
            }

            Debug.WriteLine("[PowerMode] Accept (placeholder: close)");
            // TODO: Step 6 — focus selected window; optionally chain to Snapper
            this.Cancel();
        }

        #endregion
    }
}
