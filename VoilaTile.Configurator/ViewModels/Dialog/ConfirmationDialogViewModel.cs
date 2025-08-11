namespace VoilaTile.Configurator.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class ConfirmationDialogViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string text;

        public ConfirmationDialogViewModel(string title, string text)
        {
            this.Title = title;
            this.Text = text;
            this.PositiveResponseText = "Yes";
            this.NegativeResponseText = "No";
        }
    }

}
