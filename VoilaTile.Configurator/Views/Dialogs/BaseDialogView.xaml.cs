namespace VoilaTile.Configurator.Views
{
    using System.Threading.Tasks;
    using System.Windows;
    using VoilaTile.Configurator.Enumerations;

    /// <summary>
    /// Interaction logic for BaseDialogView.xaml.
    /// A dialog shell that auto-sizes to its injected content (via <see cref="Body"/>).
    /// </summary>
    public partial class BaseDialogView : Window
    {
        /// <summary>
        /// Identifies the <see cref="Body"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty BodyProperty =
            DependencyProperty.Register(
                nameof(Body),
                typeof(object),
                typeof(BaseDialogView),
                new PropertyMetadata(null));

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDialogView"/> class.
        /// </summary>
        public BaseDialogView()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Gets the completion source that resolves when the dialog is closed by a user decision.
        /// </summary>
        public TaskCompletionSource<DialogDecision> CompletionSource { get; } = new();

        /// <summary>
        /// Gets or sets the body/content object to render inside the dialog.
        /// Typically a ViewModel that is matched to a View via DataTemplate.
        /// </summary>
        public object? Body
        {
            get => this.GetValue(BodyProperty);
            set => this.SetValue(BodyProperty, value);
        }

        private void OnPositiveClick(object sender, RoutedEventArgs e)
        {
            this.CompletionSource.TrySetResult(DialogDecision.Positive);
            this.Close();
        }

        private void OnNegativeClick(object sender, RoutedEventArgs e)
        {
            this.CompletionSource.TrySetResult(DialogDecision.Negative);
            this.Close();
        }
    }
}

