
namespace VoilaTile.Configurator.Helpers
{
    using System.Windows;
    using VoilaTile.Configurator.Models;
    using VoilaTile.Configurator.ViewModels;
    using VoilaTile.Configurator.Views;
    using VoilaTile.Common.Models;

    public static class LayoutEditorManager
    {
        public static void OpenEditor(
            ZoneTemplate template,
            MonitorInfo monitorInfo,
            Action<LayoutEditorViewModel> onCloseCallback)
        {
            var viewModel = new LayoutEditorViewModel(template);

            var editor = new LayoutEditorWindow(viewModel)
            {
                Left = monitorInfo.WorkX,
                Top = monitorInfo.WorkY,
                Width = monitorInfo.WorkWidth,
                Height = monitorInfo.WorkHeight,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };

            var mainWindow = Application.Current.MainWindow;

            mainWindow.Hide();

            editor.Closed += (_, __) =>
            {
                mainWindow.Show();

                onCloseCallback(viewModel);
            };

            editor.Show();
        }
    }
}

