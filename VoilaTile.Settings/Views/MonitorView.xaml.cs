namespace VoilaTile.Settings.Views
{
    using System.Windows.Controls;
    using System.Windows.Input;
    using VoilaTile.Settings.ViewModels;

    /// <summary>
    /// Interaction logic for Page1.xaml.
    /// </summary>
    public partial class MonitorView : UserControl
    {
        public MonitorView()
        {
            InitializeComponent();
        }

        private void OnClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MonitorViewModel vm)
            {
                vm.RaiseClicked();
            }
        }
    }
}
