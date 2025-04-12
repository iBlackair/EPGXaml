using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;

namespace EPGVirtualization.Controls
{
    /// <summary>
    /// Custom control for a program in the EPG
    /// </summary>
    public class ProgramControl : Control
    {
        // First program color set
        private static readonly SolidColorBrush Color1Selected = new(Color.FromRgb(85, 85, 85));
        private static readonly SolidColorBrush Color1Normal = new(Color.FromRgb(60, 60, 60));

        // Second program color set (alternating)
        private static readonly SolidColorBrush Color2Normal = new(Color.FromRgb(48, 0, 104));
        private static readonly SolidColorBrush Color2Selected = new(Color.FromRgb(102, 0, 170));

        // New brush for currently airing programs
        private static readonly SolidColorBrush CurrentlyAiringBrush = new(Color.FromRgb(58, 120, 69)); // Green color

        // Property to determine whether this is an "odd" program in the sequence
        public bool IsAlternatingProgram { get; set; }

        // UI elements
        private TextBlock _titleBlock;
        private TextBlock _timeBlock;
        private Border _border;

        static ProgramControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ProgramControl),
                new FrameworkPropertyMetadata(typeof(ProgramControl)));
        }

        public ProgramControl()
        {
            // Default style properties
            BorderThickness = new Thickness(1);
            Margin = new Thickness(0);
            Padding = new Thickness(3);
            Cursor = Cursors.Hand;

            // Ensure pixel-perfect rendering
            SnapsToDevicePixels = true;
            SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

            // Set default template
            Template = CreateDefaultTemplate();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Get template parts
            _border = GetTemplateChild("Border") as Border;
            _titleBlock = GetTemplateChild("TitleBlock") as TextBlock;
            _timeBlock = GetTemplateChild("TimeBlock") as TextBlock;
        }

        private ControlTemplate CreateDefaultTemplate()
        {
            // Create a template with TextBlocks for title and time
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "Border";
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));
            factory.SetValue(Border.BorderBrushProperty, Brushes.DarkBlue);
            factory.SetValue(SnapsToDevicePixelsProperty, true);
            factory.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

            var backgroundBinding = new Binding("Background");
            backgroundBinding.RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent);
            factory.SetBinding(Border.BackgroundProperty, backgroundBinding);

            var panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.MarginProperty, new Thickness(2));
            factory.AppendChild(panel);

            var titleBlock = new FrameworkElementFactory(typeof(TextBlock));
            titleBlock.Name = "TitleBlock";
            titleBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            titleBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            titleBlock.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(194, 194, 194)));
            titleBlock.SetBinding(TextBlock.TextProperty, new Binding("Title"));
            panel.AppendChild(titleBlock);

            var timeBlock = new FrameworkElementFactory(typeof(TextBlock));
            timeBlock.Name = "TimeBlock";
            timeBlock.SetValue(TextBlock.FontSizeProperty, 10.0);
            timeBlock.SetValue(TextBlock.ForegroundProperty, Brushes.DarkGray);
            timeBlock.SetBinding(TextBlock.TextProperty, new Binding("StartTime") { StringFormat = "{0:HH:mm}" });
            panel.AppendChild(timeBlock);

            return new ControlTemplate(typeof(ProgramControl)) { VisualTree = factory };
        }

        public void UpdateStyle(bool isSelected)
        {
            // Check if program is currently airing
            bool isCurrentlyAiring = IsCurrentlyAiring();

            // Set border based on selection state
            BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            BorderBrush = isSelected ? Brushes.DarkBlue : Brushes.SlateGray;

            // Prioritize currently airing status over other styling
            if (isCurrentlyAiring)
            {
                // Use the currently airing brush
                Background = CurrentlyAiringBrush;

                // Make border more noticeable for currently airing programs
                BorderBrush = Brushes.LightGreen;

                // Update text style if we have access to the TextBlocks
                if (_titleBlock != null)
                {
                    _titleBlock.FontWeight = FontWeights.Bold;
                    _titleBlock.Foreground = Brushes.White;
                }
            }
            else
            {
                // Regular styling based on selection and alternation
                if (isSelected)
                {
                    Background = IsAlternatingProgram ? Color2Selected : Color1Selected;
                }
                else
                {
                    Background = IsAlternatingProgram ? Color2Normal : Color1Normal;
                }

                // Reset title style if not currently airing
                if (_titleBlock != null)
                {
                    _titleBlock.FontWeight = FontWeights.SemiBold;
                    _titleBlock.Foreground = new SolidColorBrush(Color.FromRgb(194, 194, 194));
                }
            }
        }

        public bool IsCurrentlyAiring()
        {
            if (DataContext is ProgramInfo program)
            {
                var now = DateTime.Now;
                return now >= program.StartTime && now < program.StopTime;
            }
            return false;
        }
    }
}