namespace VoilaTile.Configurator.ViewModels
{
    using System;
    using System.Windows;
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Configurator.Models;

    /// <summary>
    /// ViewModel for an editable divider that defines a splitting line and its bounds.
    /// </summary>
    public partial class DividerViewModel : ObservableObject, ICloneable
    {
        #region Fields

        /// <summary>
        /// Gets or sets the ending bound of the divider (in normalized coordinates).
        /// </summary>
        [ObservableProperty]
        private double boundEnd;

        /// <summary>
        /// Gets or sets the starting bound of the divider (in normalized coordinates).
        /// </summary>
        [ObservableProperty]
        private double boundStart;

        /// <summary>
        /// Gets or sets the position of the divider (in normalized coordinates).
        /// </summary>
        [ObservableProperty]
        private double position;

        /// <summary>
        /// Gets or sets a value indicating whether the divider is vertical.
        /// </summary>
        [ObservableProperty]
        private bool isVertical;

        /// <summary>
        /// Gets or sets a value indicating whether this is a placeholder divider.
        /// </summary>
        [ObservableProperty]
        private bool isPlaceholder;

        /// <summary>
        /// Gets or sets a value indicating whether this divider is selected.
        /// </summary>
        [ObservableProperty]
        private bool isSelected;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DividerViewModel"/> class.
        /// </summary>
        public DividerViewModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DividerViewModel"/> class using a model.
        /// </summary>
        /// <param name="model">The data model used to initialize the view model.</param>
        public DividerViewModel(DividerModel model)
        {
            this.IsVertical = model.IsVertical;
            this.Position = model.Position;
            this.BoundStart = model.BoundStart;
            this.BoundEnd = model.BoundEnd;
        }

        #endregion Constructors

        #region Events

        /// <summary>
        /// Occurs when the divider is selected.
        /// </summary>
        public event Action<DividerViewModel>? Selected;

        /// <summary>
        /// Occurs when the mouse enters the divider handle.
        /// </summary>
        public event Action? MouseEntered;

        /// <summary>
        /// Occurs when the mouse leaves the divider handle.
        /// </summary>
        public event Action? MouseLeft;

        #endregion Events

        #region Properties

        /// <summary>
        /// Gets the normalized center point of the handle for dragging visuals.
        /// </summary>
        public Point HandleCenter =>
            this.IsVertical
                ? new Point(this.Position, (this.BoundStart + this.BoundEnd) / 2)
                : new Point((this.BoundStart + this.BoundEnd) / 2, this.Position);

        #endregion Properties

        #region Methods

        /// <summary>
        /// Triggers the <see cref="Selected"/> event.
        /// </summary>
        public void RaiseSelected()
        {
            this.Selected?.Invoke(this);
        }

        /// <summary>
        /// Triggers the <see cref="MouseEntered"/> event.
        /// </summary>
        public void RaiseMouseEnter()
        {
            this.MouseEntered?.Invoke();
        }

        /// <summary>
        /// Triggers the <see cref="MouseLeft"/> event.
        /// </summary>
        public void RaiseMouseLeave()
        {
            this.MouseLeft?.Invoke();
        }

        /// <inheritdoc/>
        public object Clone()
        {
            return new DividerViewModel
            {
                IsVertical = this.IsVertical,
                Position = this.Position,
                BoundStart = this.BoundStart,
                BoundEnd = this.BoundEnd,
            };
        }

        /// <summary>
        /// Produces a <see cref="DividerModel"/> containing relevant view model data.
        /// </summary>
        /// <returns>A <see cref="DividerModel"/> containing relevant view model data.</returns>
        public DividerModel ToModel()
        {
            return new DividerModel
            {
                IsVertical = this.IsVertical,
                Position = this.Position,
                BoundStart = this.BoundStart,
                BoundEnd = this.BoundEnd,
            };
        }

        #endregion Methods
    }
}
