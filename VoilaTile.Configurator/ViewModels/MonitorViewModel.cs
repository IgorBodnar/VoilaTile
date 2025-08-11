namespace VoilaTile.Configurator.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Common.Models;

    public partial class MonitorViewModel : ObservableObject
    {
        public MonitorViewModel(MonitorInfo info, int offsetX, int offsetY)
        {
            MonitorInfo = info;

            MonitorNumber = info.MonitorNumber;

            DeviceName = info.DeviceName;
            DeviceId = info.DeviceID;
            DeviceString = info.DeviceString;

            MonitorX = info.MonitorX;
            MonitorY = info.MonitorY;
            MonitorWidth = info.MonitorWidth;
            MonitorHeight = info.MonitorHeight;

            MonitorSize = $"({info.MonitorWidth} x {info.MonitorHeight})";

            CanvasX = info.MonitorX + offsetX;
            CanvasY = info.MonitorY + offsetY;
        }

        public MonitorInfo MonitorInfo { get; }

        public event EventHandler? Clicked;

        public void RaiseClicked() => Clicked?.Invoke(this, EventArgs.Empty);

        [ObservableProperty] private int monitorNumber;

        [ObservableProperty] private string deviceName;
        [ObservableProperty] private string deviceId;
        [ObservableProperty] private string deviceString;

        [ObservableProperty] private int canvasX;
        [ObservableProperty] private int canvasY;

        [ObservableProperty] private int monitorX;
        [ObservableProperty] private int monitorY;
        [ObservableProperty] private int monitorWidth;
        [ObservableProperty] private int monitorHeight;
        [ObservableProperty] private string monitorSize;

        [ObservableProperty] private bool isSelected;
        [ObservableProperty] private ZoneTemplateViewModel? selectedTemplate;

    }
}
