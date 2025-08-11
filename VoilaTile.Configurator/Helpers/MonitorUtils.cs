namespace VoilaTile.Configurator.Helpers
{
    using System.Windows;
    using VoilaTile.Common.Models;

    public static class MonitorUtils
    {
        public static bool IsSameMonitor(Window window, MonitorInfo monitor)
        {
            var left = window.Left;
            var top = window.Top;

            return left >= monitor.WorkX &&
                   left < monitor.WorkX + monitor.WorkWidth &&
                   top >= monitor.WorkY &&
                   top < monitor.WorkY + monitor.WorkHeight;
        }
    }
}

