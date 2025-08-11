namespace VoilaTile.Configurator.Views
{
    using VoilaTile.Configurator.ViewModels;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;

    /// <summary>
    /// Interaction logic for EditorFloatingCard.xaml
    /// </summary>
    public partial class EditorFloatingCard : System.Windows.Controls.UserControl
    {
        private System.Windows.Point dragStart;
        private bool isDragging;

        public event EventHandler? SaveClicked;
        public event EventHandler? CancelClicked;

        public bool IsDragging => isDragging;

        public EditorFloatingCard()
        {
            InitializeComponent();

            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseMove += OnMouseMove;
            this.MouseEnter += OnMouseEnter;
            this.MouseLeave += OnMouseLeave;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.dragStart = e.GetPosition(this);
            this.isDragging = true;
            this.CaptureMouse();

            e.Handled = true; // Prevent the event from bubbling up.
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!this.isDragging || this.Parent is not Canvas canvas)
                return;

            System.Windows.Point position = e.GetPosition(canvas);
            Canvas.SetLeft(this, position.X - this.dragStart.X);
            Canvas.SetTop(this, position.Y - this.dragStart.Y);
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.isDragging = false;
            this.ReleaseMouseCapture();
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (this.DataContext is LayoutEditorViewModel vm)
            {
                vm.OnCardMouseEnter();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (this.DataContext is LayoutEditorViewModel vm)
            {
                vm.OnCardMouseLeave();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            this.SaveClicked?.Invoke(this, EventArgs.Empty);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.CancelClicked?.Invoke(this, EventArgs.Empty);
        }
    }

}
