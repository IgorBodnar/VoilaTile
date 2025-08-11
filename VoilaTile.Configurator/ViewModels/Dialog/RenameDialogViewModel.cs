namespace VoilaTile.Configurator.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class RenameDialogViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string name;

        public RenameDialogViewModel(string currentName)
        {
            Title = "Rename Template";
            Name = currentName;
            PositiveResponseText = "Save";
            NegativeResponseText = "Cancel";
        }
    }

}
