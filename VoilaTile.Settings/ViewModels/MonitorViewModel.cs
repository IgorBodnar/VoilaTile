namespace VoilaTile.Settings.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Common.Models;

    /// <summary>
    /// The view model for a monitor.
    /// </summary>
    public partial class MonitorViewModel : ObservableObject
    {
        #region Fields

        /// <summary>
        /// The backing field for a MonitorNumber observable property generated on build.
        /// Represents the monitor number.
        /// </summary>
        [ObservableProperty] private int monitorNumber;

        /// <summary>
        /// The backing field for a DeviceName observable property generated on build.
        /// Represents the name of the device.
        /// </summary>
        [ObservableProperty] private string deviceName;

        /// <summary>
        /// The backing field for a DeviceId observable property generated on build.
        /// Represents the ID of the device.
        /// </summary>
        [ObservableProperty] private string deviceId;

        /// <summary>
        /// The backing field for a DeviceString observable property generated on build.
        /// Represents the string representation of the device.
        /// </summary>
        [ObservableProperty] private string deviceString;

        /// <summary>
        /// The backing field for a CanvasX observable property generated on build.
        /// Represents the X position on the canvas.
        /// </summary>
        [ObservableProperty] private int canvasX;

        /// <summary>
        /// The backing field for a CanvasY observable property generated on build.
        /// Represents the Y position on the canvas.
        /// </summary>
        [ObservableProperty] private int canvasY;

        /// <summary>
        /// The backing field for a MonitorX observable property generated on build.
        /// Represents the X position of the monitor in pixels.
        /// </summary>
        [ObservableProperty] private int monitorX;

        /// <summary>
        /// The backing field for a MonitorY observable property generated on build.
        /// Represents the Y position of the monitor in pixels.
        /// </summary>
        [ObservableProperty] private int monitorY;

        /// <summary>
        /// The backing field for a MonitorWidth observable property generated on build.
        /// Represents the width of the monitor in pixels.
        /// </summary>
        [ObservableProperty] private int monitorWidth;

        /// <summary>
        /// The backing field for a MonitorHeight observable property generated on build.
        /// Represents the height of the monitor in pixels.
        /// </summary>
        [ObservableProperty] private int monitorHeight;

        /// <summary>
        /// The backing field for a MonitorSize observable property generated on build.
        /// Represents the size of the monitor as a formatted string.
        /// </summary>
        [ObservableProperty] private string monitorSize;

        /// <summary>
        /// The backing field for a IsSelected observable property generated on build.
        /// Represents whether the monitor is currently selected.
        /// </summary>
        [ObservableProperty] private bool isSelected;

        /// <summary>
        /// The backing field for a SelectedTemplate observable property generated on build.
        /// Represents the currently selected zone template for the monitor.
        /// </summary>
        [ObservableProperty] private ZoneTemplateViewModel? selectedTemplate;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorViewModel"/> class.
        /// </summary>
        /// <param name="info">The monitor information.</param>
        /// <param name="offsetX">The monitor offset X required for proper positioning on a transformed canvas.</param>
        /// <param name="offsetY">The monitor offset Y required for proper positioning on a transformed canvas.</param>
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

        #endregion

        #region Events

        /// <summary>
        /// The event that is raised when the monitor is clicked.
        /// </summary>
        public event EventHandler? Clicked;

        #endregion

        #region Properties

        /// <summary>
        /// The monitor information.
        /// </summary>
        public MonitorInfo MonitorInfo { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Raises the <see cref="Clicked"/> event.
        /// </summary>
        public void RaiseClicked() => Clicked?.Invoke(this, EventArgs.Empty);

        #endregion
    }
}
