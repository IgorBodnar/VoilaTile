namespace VoilaTile.Settings.ViewModels.Panels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// Base class for all settings panels hosted by the Shell.
    /// </summary>
    public abstract partial class SettingsPanelViewModel : ObservableObject
    {
        #region Fields

        /// <summary>
        /// Backing field for <see cref="IsVisible"/>.
        /// </summary>
        [ObservableProperty]
        private bool isVisible;

        /// <summary>
        /// Backing field for <see cref="Title"/>.
        /// </summary>
        [ObservableProperty]
        private string title = string.Empty;

        #endregion Fields
    }
}

