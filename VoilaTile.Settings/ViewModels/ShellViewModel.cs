namespace VoilaTile.Settings.ViewModels
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using VoilaTile.Settings.Models;
    using VoilaTile.Settings.ViewModels.Panels;

    /// <summary>
    /// Root view model hosting the burger menu and page content.
    /// </summary>
    public sealed partial class ShellViewModel : ObservableObject
    {
        #region Fields

        /// <summary>
        /// Backing field for <see cref="Panels"/>.
        /// </summary>
        private readonly List<SettingsPanelViewModel> panels;

        /// <summary>
        /// Gets or sets a value indicating whether the left navigation menu is collapsed (icon-only).
        /// </summary>
        /// <remarks>
        /// The observable property is automatically generated on build.
        /// </remarks>
        [ObservableProperty]
        private bool isMenuCollapsed;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ShellViewModel"/> class.
        /// </summary>
        /// <param name="layoutsPanel">The layouts panel view model.</param>
        /// <param name="inputPanel">The input panel view model.</param>
        /// <param name="themePanel">The theme panel view model.</param>
        public ShellViewModel(LayoutsSettingsPanelViewModel layoutsPanel, InputSettingsPanelViewModel inputPanel, AppearanceSettingsPanelViewModel themePanel)
        {
            this.LayoutsPanel = layoutsPanel;
            this.InputPanel = inputPanel;
            this.AppearancePanel = themePanel;

            this.panels = new List<SettingsPanelViewModel>
            {
                this.LayoutsPanel,
                this.InputPanel,
                this.AppearancePanel,
            };

            this.SetActivePanel(this.LayoutsPanel);
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// Gets the layouts panel.
        /// </summary>
        public LayoutsSettingsPanelViewModel LayoutsPanel { get; }

        /// <summary>
        /// Gets the input panel.
        /// </summary>
        public InputSettingsPanelViewModel InputPanel { get; }

        /// <summary>
        /// Gets the theme panel.
        /// </summary>
        public AppearanceSettingsPanelViewModel AppearancePanel { get; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Selects a panel from the burger menu.
        /// </summary>
        /// <param name="panel">The target panel.</param>
        [RelayCommand]
        private void SelectPanel(SettingsPanelViewModel panel)
        {
            this.SetActivePanel(panel);
        }

        /// <summary>
        /// Sets the specified panel visible and hides all others.
        /// </summary>
        /// <param name="panel">The panel to activate.</param>
        private void SetActivePanel(SettingsPanelViewModel panel)
        {
            foreach (var p in this.panels)
            {
                p.IsVisible = ReferenceEquals(p, panel);
            }
        }

        /// <summary>
        /// Toggles the collapsed state of the left navigation menu.
        /// </summary>
        [RelayCommand]
        private void ToggleMenu() => this.IsMenuCollapsed = !this.IsMenuCollapsed;

        #endregion Methods
    }
}

