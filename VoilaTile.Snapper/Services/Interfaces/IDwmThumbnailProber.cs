namespace VoilaTile.Snapper.Services.Interfaces
{
    using VoilaTile.Snapper.Records;

    internal interface IDwmThumbnailProber
    {
        bool CanRegister(WindowId source);
    }
}
