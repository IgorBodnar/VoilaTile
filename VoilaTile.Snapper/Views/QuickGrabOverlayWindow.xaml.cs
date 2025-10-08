using System;
using System.Windows;
using VoilaTile.Snapper.ViewModels;

namespace VoilaTile.Snapper.Views
{
    /// <summary>
    /// Per-monitor overlay window for Quick Grab (badges only).
    /// </summary>
    public sealed partial class QuickGrabOverlayWindow : Window
    {
        public QuickGrabOverlayWindow(QuickGrabOverlayViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // Ensure the overlay ignores pointer input completely
            IsHitTestVisible = false;
        }
    }
}

