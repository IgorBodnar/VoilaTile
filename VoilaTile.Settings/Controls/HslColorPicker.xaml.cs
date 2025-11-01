namespace VoilaTile.Settings.Controls
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using VoilaTile.Settings.Theming;

    /// <summary>
    /// Minimal HSL color picker with three sliders and a hex field.
    /// Binds two-way via <see cref="SelectedColor"/>.
    /// </summary>
    public partial class HslColorPicker : UserControl
    {
        #region Fields

        /// <summary>
        /// Guard flag to prevent recursive updates between HSL and Color.
        /// </summary>
        private bool updating;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HslColorPicker"/> class.
        /// </summary>
        public HslColorPicker()
        {
            this.InitializeComponent();
            // Initialize HSL from default SelectedColor
            this.SyncHslFromColor((Color)this.GetValue(SelectedColorProperty));
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// Gets or sets the selected color (two-way bindable).
        /// </summary>
        public Color SelectedColor
        {
            get => (Color)this.GetValue(SelectedColorProperty);
            set => this.SetValue(SelectedColorProperty, value);
        }

        /// <summary>
        /// DependencyProperty for <see cref="SelectedColor"/>.
        /// </summary>
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(HslColorPicker),
                new FrameworkPropertyMetadata(Colors.DeepSkyBlue, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

        /// <summary>
        /// Gets or sets the hue (0..360).
        /// </summary>
        public double Hue
        {
            get => (double)this.GetValue(HueProperty);
            set => this.SetValue(HueProperty, value);
        }

        /// <summary>
        /// DependencyProperty for <see cref="Hue"/>.
        /// </summary>
        public static readonly DependencyProperty HueProperty =
            DependencyProperty.Register(
                nameof(Hue),
                typeof(double),
                typeof(HslColorPicker),
                new FrameworkPropertyMetadata(200d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHslChanged, CoerceHue));

        /// <summary>
        /// Gets or sets the saturation (0..100).
        /// </summary>
        public double Saturation
        {
            get => (double)this.GetValue(SaturationProperty);
            set => this.SetValue(SaturationProperty, value);
        }

        /// <summary>
        /// DependencyProperty for <see cref="Saturation"/>.
        /// </summary>
        public static readonly DependencyProperty SaturationProperty =
            DependencyProperty.Register(
                nameof(Saturation),
                typeof(double),
                typeof(HslColorPicker),
                new FrameworkPropertyMetadata(70d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHslChanged, CoercePercent));

        /// <summary>
        /// Gets or sets the lightness (0..100).
        /// </summary>
        public double Lightness
        {
            get => (double)this.GetValue(LightnessProperty);
            set => this.SetValue(LightnessProperty, value);
        }

        /// <summary>
        /// DependencyProperty for <see cref="Lightness"/>.
        /// </summary>
        public static readonly DependencyProperty LightnessProperty =
            DependencyProperty.Register(
                nameof(Lightness),
                typeof(double),
                typeof(HslColorPicker),
                new FrameworkPropertyMetadata(52d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHslChanged, CoercePercent));

        /// <summary>
        /// Gets or sets the hex representation (#RRGGBB).
        /// </summary>
        public string Hex
        {
            get => (string)this.GetValue(HexProperty);
            set => this.SetValue(HexProperty, value);
        }

        /// <summary>
        /// DependencyProperty for <see cref="Hex"/>.
        /// </summary>
        public static readonly DependencyProperty HexProperty =
            DependencyProperty.Register(
                nameof(Hex),
                typeof(string),
                typeof(HslColorPicker),
                new FrameworkPropertyMetadata("#4C8CFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHexChanged));

        #endregion Properties

        #region Methods

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HslColorPicker)d;
            if (ctrl.updating) return;

            ctrl.updating = true;
            try
            {
                var c = (Color)e.NewValue;
                ctrl.SyncHslFromColor(c);
                ctrl.Hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            finally
            {
                ctrl.updating = false;
            }
        }

        private static void OnHslChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HslColorPicker)d;
            if (ctrl.updating) return;

            ctrl.updating = true;
            try
            {
                var rgb = new HslColor(ctrl.Hue / 360.0, ctrl.Saturation / 100.0, ctrl.Lightness / 100.0).ToRgb();
                ctrl.SelectedColor = Color.FromRgb(rgb.R, rgb.G, rgb.B);
                ctrl.Hex = $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}";
            }
            finally
            {
                ctrl.updating = false;
            }
        }

        private static void OnHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HslColorPicker)d;
            if (ctrl.updating) return;

            ctrl.updating = true;
            try
            {
                if (TryParseHex((string?)e.NewValue, out var c))
                {
                    ctrl.SelectedColor = c;
                    ctrl.SyncHslFromColor(c);
                }
            }
            finally
            {
                ctrl.updating = false;
            }
        }

        private static object CoerceHue(DependencyObject d, object baseValue)
        {
            double v = (double)baseValue;
            if (v < 0) return 0d;
            if (v > 360) return 360d;
            return v;
        }

        private static object CoercePercent(DependencyObject d, object baseValue)
        {
            double v = (double)baseValue;
            if (v < 0) return 0d;
            if (v > 100) return 100d;
            return v;
        }

        private static bool TryParseHex(string? s, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(s)) return false;

            s = s.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal)) s = s[1..];
            if (s.Length is not 6) return false;

            if (byte.TryParse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                color = Color.FromRgb(r, g, b);
                return true;
            }
            return false;
        }

        private void SyncHslFromColor(Color c)
        {
            var hsl = HslColor.FromRgb(c);
            this.Hue = Math.Round(hsl.H * 360.0, 0);
            this.Saturation = Math.Round(hsl.S * 100.0, 0);
            this.Lightness = Math.Round(hsl.L * 100.0, 0);
        }

        #endregion Methods
    }
}

