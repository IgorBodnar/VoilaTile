namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Windows.Forms;

    /// <summary>
    /// Provides a system tray icon with actions for the Snapper application.
    /// </summary>
    public sealed class TrayIconService : IDisposable
    {
        private readonly NotifyIcon notifyIcon;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrayIconService"/> class.
        /// </summary>
        public TrayIconService()
        {
            this.notifyIcon = new NotifyIcon
            {
                Icon = this.LoadIconFromResource(),
                Visible = true,
                Text = "VoilaTile.Snapper",
                ContextMenuStrip = this.BuildContextMenu()
            };
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var exitItem = new ToolStripMenuItem("End Task");
            exitItem.Click += this.OnExitClick;

            menu.Items.Add(exitItem);
            return menu;
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            var app = System.Windows.Application.Current;
            if (app is not null)
            {
                if (app.Dispatcher.CheckAccess())
                    app.Shutdown();
                else
                    app.Dispatcher.Invoke(app.Shutdown);
            }
        }


        private Icon LoadIconFromResource()
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "VoilaTile.Snapper.Assets.icon-logo.ico";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            return stream is not null ? new Icon(stream) : SystemIcons.Application;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.notifyIcon.Visible = false;
            this.notifyIcon.Dispose();
        }
    }
}

