namespace VoilaTile.Configurator.Views
{
    using VoilaTile.Configurator.ViewModels;
    using System.Windows.Input;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for Page1.xaml
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
