namespace VoilaTile.Common.DTO
{
    /// <summary>
    /// DTO for saving and loading user settings.
    /// </summary>
    public class SettingsDTO
    {
        public string Seed { get; set; } = string.Empty;
        public string SelectedShortcutKey { get; set; } = string.Empty;
    }
}

