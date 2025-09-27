namespace VoilaTile.Snapper.Services
{
    using System;
    using VoilaTile.Snapper.Models;

    /// <summary>
    /// Observes system window events and raises deltas describing changes.
    /// </summary>
    public interface IWindowEventMonitor : IDisposable
    {
        #region Events

        /// <summary>
        /// Occurs when a window change is observed.
        /// </summary>
        event Action<WindowDelta> OnChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Starts monitoring window events.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops monitoring window events.
        /// </summary>
        void Stop();

        #endregion
    }
}
