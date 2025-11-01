namespace VoilaTile.Settings.Models
{
    using System.IO;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Models;
    using VoilaTile.Settings.DTO;

    /// <summary>
    /// The model for the input settings panel.
    /// </summary>
    public class InputSettingsPanelModel
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="InputSettingsPanelModel"/> class.
        /// </summary>
        public InputSettingsPanelModel()
        {
            this.InputSettings = InputSettingsStorage.LoadSettings();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The input settings.
        /// </summary>
        public SettingsDTO InputSettings { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Saves the input settings asynchronously.
        /// </summary>
        /// <remarks>
        /// Called on application shutdown to persist settings.
        /// </remarks>
        /// <param name="settingsDTO">The input settings dto.</param>
        /// <returns>An instance of <see cref="Task"/> representing asynchronous operation.</returns>
        public async Task SaveSettingsAsync(SettingsDTO settingsDTO)
        {
            await Task.Run(() => InputSettingsStorage.SaveSettings(settingsDTO)).ConfigureAwait(false);
        }

        #endregion
    }
}
