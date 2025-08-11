namespace VoilaTile.Configurator.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class InformationDialogViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string info;

        public InformationDialogViewModel(string title, string info)
        {
            this.Title = title;
            this.Info = info;
            this.PositiveResponseText = "OK";
            this.ShowNegativeResponse = false;
        }
    }

}
