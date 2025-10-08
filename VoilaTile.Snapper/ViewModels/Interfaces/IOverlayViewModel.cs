namespace VoilaTile.Snapper.ViewModels
{
    using VoilaTile.Common.Models;

    /// <summary>
    /// Marker interface for per-monitor overlay VMs.
    /// </summary>
    public interface IOverlayViewModel
    {
        /// <summary>
        /// Monitor this overlay is bound to.
        /// </summary>
        MonitorInfo Monitor { get; }
    }
}

