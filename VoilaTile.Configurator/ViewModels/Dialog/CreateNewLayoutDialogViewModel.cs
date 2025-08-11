namespace VoilaTile.Configurator.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class CreateNewLayoutDialogViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string name;

        public CreateNewLayoutDialogViewModel(string currentName)
        {
            Title = "Create New Layout";
            Name = currentName;
            PositiveResponseText = "Create";
            NegativeResponseText = "Cancel";
        }
    }

}
