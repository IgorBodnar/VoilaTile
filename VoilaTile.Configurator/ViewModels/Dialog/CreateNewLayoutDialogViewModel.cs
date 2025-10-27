namespace VoilaTile.Configurator.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class CreateNewLayoutDialogViewModel : DialogViewModelBase
    {
        #region Fields

        /// <summary>
        /// The name of the new layout.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks>
        [ObservableProperty]
        private string name;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateNewLayoutDialogViewModel"/> class.
        /// </summary>
        /// <param name="currentName">The current name.</param>
        public CreateNewLayoutDialogViewModel(string currentName)
        {
            this.Title = "Create New Layout";
            this.Name = currentName;
            this.PositiveResponseText = "Create";
            this.NegativeResponseText = "Cancel";
        }

        #endregion
    }
}
