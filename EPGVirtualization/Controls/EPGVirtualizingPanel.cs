using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EPGVirtualization
{
    /// <summary>
    /// A more performant EPG control that uses standard WPF panels with virtualization
    /// </summary>
    public class EPGCanvas : Control
    {
        #region Dependency Properties

        public static readonly DependencyProperty ProgramsProperty =
            DependencyProperty.Register(
                "Programs",
                typeof(List<ProgramInfo>),
                typeof(EPGCanvas),
                new PropertyMetadata(null, OnProgramsChanged));

        public static readonly DependencyProperty ViewStartTimeProperty =
            DependencyProperty.Register(
                "ViewStartTime",
                typeof(DateTime),
                typeof(EPGCanvas),
                new PropertyMetadata(DateTime.Today));

        public static readonly DependencyProperty PixelsPerMinuteProperty =
            DependencyProperty.Register(
                "PixelsPerMinute",
                typeof(double),
                typeof(EPGCanvas),
                new PropertyMetadata(3.0));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(
                "Zoom",
                typeof(double),
                typeof(EPGCanvas),
                new PropertyMetadata(1.0, OnZoomChanged));

        public static readonly DependencyProperty ChannelHeightProperty =
            DependencyProperty.Register(
                "ChannelHeight",
                typeof(double),
                typeof(EPGCanvas),
                new PropertyMetadata(40.0));

        public static readonly DependencyProperty ChannelLabelWidthProperty =
            DependencyProperty.Register(
                "ChannelLabelWidth",
                typeof(double),
                typeof(EPGCanvas),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty TimelineHeightProperty =
            DependencyProperty.Register(
                "TimelineHeight",
                typeof(double),
                typeof(EPGCanvas),
                new PropertyMetadata(40.0));

        public List<ProgramInfo> Programs
        {
            get => (List<ProgramInfo>)GetValue(ProgramsProperty);
            set => SetValue(ProgramsProperty, value);
        }

        public DateTime ViewStartTime
        {
            get => (DateTime)GetValue(ViewStartTimeProperty);
            set => SetValue(ViewStartTimeProperty, value);
        }

        public double PixelsPerMinute
        {
            get => (double)GetValue(PixelsPerMinuteProperty);
            set => SetValue(PixelsPerMinuteProperty, value);
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public double ChannelHeight
        {
            get => (double)GetValue(ChannelHeightProperty);
            set => SetValue(ChannelHeightProperty, value);
        }

        public double ChannelLabelWidth
        {
            get => (double)GetValue(ChannelLabelWidthProperty);
            set => SetValue(ChannelLabelWidthProperty, value);
        }

        public double TimelineHeight
        {
            get => (double)GetValue(TimelineHeightProperty);
            set => SetValue(TimelineHeightProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<ProgramInfo> ProgramSelected;

        #endregion

        #region Template parts

        private ScrollViewer _programGridScrollViewer;
        private Canvas _programGrid;
        private Canvas _timelineCanvas;
        private StackPanel _channelPanel;
        private List<ProgramControl> _programControls = new List<ProgramControl>();
        private Dictionary<int, List<ProgramInfo>> _programsByChannel = new Dictionary<int, List<ProgramInfo>>();
        private List<ChannelRow> _channelRows = new List<ChannelRow>();
        private double _horizontalOffset = 0;
        private double _verticalOffset = 0;
        private Point _lastDragPosition;
        private bool _isDragging = false;
        private ProgramInfo _selectedProgram;

        static EPGCanvas()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(EPGCanvas),
                new FrameworkPropertyMetadata(typeof(EPGCanvas)));
        }

        public EPGCanvas()
        {
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Programs != null)
            {
                UpdateChannelRows();

                // Wait for template to be applied
                Dispatcher.BeginInvoke(new Action(() => {
                    UpdateLayout();
                }), DispatcherPriority.Loaded);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Get main scroll viewer that contains the program grid
            _programGridScrollViewer = GetTemplateChild("PART_MainScrollViewer") as ScrollViewer;
            _programGrid = GetTemplateChild("PART_ProgramGrid") as Canvas;
            _timelineCanvas = GetTemplateChild("PART_TimelineCanvas") as Canvas;

            // Get the timeline scroll viewer
            var timelineScrollViewer = GetTemplateChild("PART_TimelineScrollViewer") as ScrollViewer;

            // Get channel scroll viewer
            var channelScrollViewer = GetTemplateChild("PART_ChannelScrollViewer") as ScrollViewer;
            _channelPanel = GetTemplateChild("PART_ChannelPanel") as StackPanel;

            if (_programGridScrollViewer != null)
            {
                // Sync scrolling between main scroll viewer, timeline, and channel list
                _programGridScrollViewer.ScrollChanged += (s, e) =>
                {
                    _horizontalOffset = e.HorizontalOffset;
                    _verticalOffset = e.VerticalOffset;

                    // Sync timeline horizontal scroll
                    if (timelineScrollViewer != null)
                    {
                        timelineScrollViewer.ScrollToHorizontalOffset(_horizontalOffset);
                    }

                    // Sync channel list vertical scroll
                    if (channelScrollViewer != null)
                    {
                        channelScrollViewer.ScrollToVerticalOffset(_verticalOffset);
                    }

                    // Update program visibility
                    UpdateVisibility();
                };

                // Add mouse events to program grid for dragging
                if (_programGrid != null)
                {
                    _programGrid.MouseLeftButtonDown += OnProgramGridMouseLeftButtonDown;
                    _programGrid.MouseMove += OnProgramGridMouseMove;
                    _programGrid.MouseLeftButtonUp += OnProgramGridMouseLeftButtonUp;
                    _programGrid.MouseWheel += OnProgramGridMouseWheel;
                }
            }

            // Start timer for current time marker
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            timer.Tick += (s, e) => DrawCurrentTimeMarker();
            timer.Start();

            // If we already have data, update the layout
            if (Programs != null && Programs.Count > 0)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    UpdateChannelRows();
                    UpdateLayout();
                }), DispatcherPriority.Render);
            }
        }

        // Alternative implementation for more efficient scrolling

        #endregion

        #region Event Handlers

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _horizontalOffset = e.HorizontalOffset;
            _verticalOffset = e.VerticalOffset;

            // Synchronize timeline horizontal scrolling
            UpdateTimelinePosition();

            // Synchronize channel list vertical scrolling
            UpdateChannelListPosition();

            // Update visibility of program items
            UpdateVisibility();
        }
        

        private void OnProgramGridMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastDragPosition = e.GetPosition(_programGrid);
            _isDragging = true;
            _programGrid.CaptureMouse();
            e.Handled = true;
        }

        private void OnProgramGridMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _programGridScrollViewer != null)
            {
                var currentPos = e.GetPosition(_programGrid);
                var delta = _lastDragPosition - currentPos;

                _programGridScrollViewer.ScrollToHorizontalOffset(_programGridScrollViewer.HorizontalOffset + delta.X);
                _programGridScrollViewer.ScrollToVerticalOffset(_programGridScrollViewer.VerticalOffset + delta.Y);

                _lastDragPosition = currentPos;
                e.Handled = true;
            }
        }

        private void OnProgramGridMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _programGrid.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void OnProgramGridMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Get position before zoom
                var mousePos = e.GetPosition(_programGrid);
                var timeOffset = (_horizontalOffset + mousePos.X - ChannelLabelWidth) / (PixelsPerMinute * Zoom);

                // Adjust zoom level
                var oldZoom = Zoom;
                Zoom += e.Delta > 0 ? 0.1 : -0.1;
                Zoom = Math.Max(0.5, Math.Min(2.0, Zoom));

                if (oldZoom != Zoom)
                {
                    // Calculate new offset to keep mouse over same time point
                    var newOffset = (timeOffset * PixelsPerMinute * Zoom) - (mousePos.X - ChannelLabelWidth);
                    _programGridScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newOffset));
                }

                e.Handled = true;
            }
        }

        private void OnProgramClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is ProgramControl programControl)
            {
                var program = programControl.DataContext as ProgramInfo;
                if (program != null)
                {
                    SelectProgram(program);
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Public Methods

        public void SetPrograms(List<ProgramInfo> programs)
        {
            Programs = programs;
        }

        public void SelectProgram(ProgramInfo program)
        {
            if (_selectedProgram != null)
            {
                _selectedProgram.IsSelected = false;
            }

            _selectedProgram = program;

            if (_selectedProgram != null)
            {
                _selectedProgram.IsSelected = true;
            }

            // Update program control styles
            foreach (var control in _programControls)
            {
                if (control.DataContext is ProgramInfo p)
                {
                    control.UpdateStyle(p == _selectedProgram);
                }
            }

            // Raise event
            ProgramSelected?.Invoke(this, program);
        }

        #endregion

        #region Private Methods

        private static void OnProgramsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EPGCanvas epg && epg.IsLoaded)
            {
                epg.Dispatcher.BeginInvoke(new Action(() => {
                    epg.UpdateChannelRows();
                    epg.UpdateLayout();
                }), DispatcherPriority.Render);
            }
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EPGCanvas epg)
            {
                epg.UpdateLayout();
            }
        }

        private void UpdateChannelRows()
        {
            if (Programs == null || Programs.Count == 0)
                return;

            // Group programs by channel
            _programsByChannel.Clear();
            foreach (var program in Programs)
            {
                if (!_programsByChannel.TryGetValue(program.ChannelIndex, out var channelPrograms))
                {
                    channelPrograms = new List<ProgramInfo>();
                    _programsByChannel[program.ChannelIndex] = channelPrograms;
                }
                channelPrograms.Add(program);
            }

            // Create channel rows
            _channelRows.Clear();
            foreach (var channelIndex in _programsByChannel.Keys.OrderBy(x => x))
            {
                _channelRows.Add(new ChannelRow
                {
                    ChannelIndex = channelIndex,
                    ChannelName = $"Channel {channelIndex + 1}",
                    Programs = _programsByChannel[channelIndex].OrderBy(p => p.StartTime).ToList()
                });
            }
        }

        private void UpdateLayout()
        {
            if (_programGrid == null || _timelineCanvas == null || _channelPanel == null || _channelRows.Count == 0)
                return;

            // Clear existing controls
            _programGrid.Children.Clear();
            _timelineCanvas.Children.Clear();
            _channelPanel.Children.Clear();
            _programControls.Clear();

            // Set program grid size
            double gridWidth = 24 * 60 * PixelsPerMinute * Zoom; // 24 hours
            _programGrid.Width = gridWidth;
            _programGrid.Height = _channelRows.Count * ChannelHeight * Zoom;

            // Set timeline width to match program grid width
            _timelineCanvas.Width = gridWidth;

            // Draw timeline
            DrawTimeline();

            // Draw channel labels
            DrawChannelLabels();

            // First, draw alternating row backgrounds
            DrawRowBackgrounds();

            // Then draw programs on top
            DrawPrograms();

            // Draw current time marker
            DrawCurrentTimeMarker();

            // Update what's visible
            UpdateVisibility();

            // Force layout update
            _programGrid.UpdateLayout();
            _timelineCanvas.UpdateLayout();
            _channelPanel.UpdateLayout();
        }

        private void DrawRowBackgrounds()
        {
            // Define alternating background colors for channel rows
            var evenRowBrush = new SolidColorBrush(Color.FromRgb(245, 245, 250)); // Light blue-gray
            var oddRowBrush = new SolidColorBrush(Color.FromRgb(235, 235, 255));  // Slightly darker blue-gray

            for (int i = 0; i < _channelRows.Count; i++)
            {
                var rowHeight = Math.Floor(ChannelHeight * Zoom); // Ensure whole pixel values
                var y = i * rowHeight;

                // Determine if this is an even or odd row
                var isEvenRow = i % 2 == 0;
                var rowBrush = isEvenRow ? evenRowBrush : oddRowBrush;

                // Draw row background
                var rowBackground = new System.Windows.Shapes.Rectangle
                {
                    Width = _programGrid.Width,
                    Height = rowHeight,
                    Fill = rowBrush,
                    Tag = "RowBackground"
                };
                Canvas.SetLeft(rowBackground, 0);
                Canvas.SetTop(rowBackground, y);
                Canvas.SetZIndex(rowBackground, -10);
                _programGrid.Children.Add(rowBackground);

                // Draw horizontal grid line at the bottom of each row
                var line = new System.Windows.Shapes.Line
                {
                    X1 = 0,
                    Y1 = y + rowHeight,
                    X2 = _programGrid.Width,
                    Y2 = y + rowHeight,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1,
                    Tag = "GridLine"
                };
                Canvas.SetZIndex(line, -5);
                _programGrid.Children.Add(line);
            }
        }

        private void DrawPrograms()
        {
            for (int i = 0; i < _channelRows.Count; i++)
            {
                var channel = _channelRows[i];
                var rowHeight = Math.Floor(ChannelHeight * Zoom); // Ensure whole pixel values
                var y = i * rowHeight;

                // Sort programs by start time to ensure proper alternating
                var sortedPrograms = channel.Programs.OrderBy(p => p.StartTime).ToList();

                // Track alternating state within the row
                bool isAlternating = false;

                // Draw programs
                foreach (var program in sortedPrograms)
                {
                    double x = Math.Floor((program.StartTime - ViewStartTime).TotalMinutes * PixelsPerMinute * Zoom);
                    double width = Math.Floor(program.Duration.TotalMinutes * PixelsPerMinute * Zoom);

                    // Only create controls for programs within a reasonable range
                    if (x < -2000 || x > _programGrid.Width + 2000)
                        continue;

                    var programControl = new ProgramControl
                    {
                        DataContext = program,
                        Width = Math.Max(2, width),
                        Height = rowHeight - 1, // Small gap between rows
                        Tag = "Program",
                        IsAlternatingProgram = isAlternating // Set alternating flag
                    };
                    programControl.MouseLeftButtonDown += OnProgramClicked;
                    programControl.UpdateStyle(program == _selectedProgram);

                    Canvas.SetLeft(programControl, x);
                    Canvas.SetTop(programControl, y);
                    Canvas.SetZIndex(programControl, 0);

                    _programGrid.Children.Add(programControl);
                    _programControls.Add(programControl);

                    // Toggle alternating state for the next program in this row
                    isAlternating = !isAlternating;
                }
            }
        }

        private void DrawTimeline()
        {
            _timelineCanvas.Children.Clear();
            _timelineCanvas.Width = _programGrid.Width;
            _timelineCanvas.Height = TimelineHeight;

            // Draw hour labels
            for (int hour = 0; hour < 24; hour++)
            {
                var time = ViewStartTime.AddHours(hour);
                var x = hour * 60 * PixelsPerMinute * Zoom;

                // Hour background
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = 60 * PixelsPerMinute * Zoom,
                    Height = TimelineHeight,
                    Fill = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, x);
                _timelineCanvas.Children.Add(rect);

                // Hour text
                var text = new TextBlock
                {
                    Text = time.ToString("HH:00"),
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center                    
                };
                Canvas.SetLeft(text, x + 5);
                Canvas.SetTop(text, 10);
                _timelineCanvas.Children.Add(text);

                // 30-minute mark
                if (PixelsPerMinute * Zoom > 1)
                {
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = 0,
                        Y1 = 30,
                        X2 = 0,
                        Y2 = TimelineHeight,
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(line, x + 30 * PixelsPerMinute * Zoom);
                    _timelineCanvas.Children.Add(line);
                }
            }
        }

        private void DrawChannelLabels()
        {
            _channelPanel.Children.Clear();

            // Define alternating background colors for channel rows
            var evenRowBrush = new SolidColorBrush(Color.FromRgb(245, 245, 250)); // Light blue-gray
            var oddRowBrush = new SolidColorBrush(Color.FromRgb(235, 235, 255));  // Slightly darker blue-gray

            for (int i = 0; i < _channelRows.Count; i++)
            {
                var channel = _channelRows[i];

                // Determine if this is an even or odd row
                var isEvenRow = i % 2 == 0;
                var rowBrush = isEvenRow ? evenRowBrush : oddRowBrush;

                var rowHeight = Math.Floor(ChannelHeight * Zoom); // Ensure whole pixel values

                var label = new Border
                {
                    Width = Math.Floor(ChannelLabelWidth), // Ensure whole pixel values
                    Height = rowHeight,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Background = rowBrush
                };

                var text = new TextBlock
                {
                    Text = channel.ChannelName,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(5, 0, 0, 0)
                };

                label.Child = text;
                _channelPanel.Children.Add(label);
            }
        }
        private void DrawCurrentTimeMarker()
        {
            // Remove existing marker
            foreach (var child in _programGrid.Children.OfType<System.Windows.Shapes.Line>().ToList())
            {
                if (child.Tag as string == "CurrentTime")
                {
                    _programGrid.Children.Remove(child);
                }
            }

            // Check if current time is in view
            var now = DateTime.Now;
            if (now.Date == ViewStartTime.Date)
            {
                var x = (now - ViewStartTime).TotalMinutes * PixelsPerMinute * Zoom;

                // Draw current time line
                var line = new System.Windows.Shapes.Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = _programGrid.Height,
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    Tag = "CurrentTime"
                };

                _programGrid.Children.Add(line);
            }
        }
        private void UpdateTimelinePosition()
        {
            if (_timelineCanvas != null)
            {
                // Always reset the timeline to the top-left corner relative to the horizontal scroll
                Canvas.SetLeft(_timelineCanvas, -_horizontalOffset);
                Canvas.SetTop(_timelineCanvas, 0); // Explicitly set top to 0
            }
        }
        private void UpdateChannelListPosition()
        {
            if (_channelPanel != null)
            {
                // Get the parent ScrollViewer of the channel panel
                var channelScrollViewer = GetTemplateChild("PART_ChannelScrollViewer") as ScrollViewer;
                if (channelScrollViewer != null)
                {
                    // Synchronize the vertical scroll position
                    channelScrollViewer.ScrollToVerticalOffset(_verticalOffset);
                }
            }
        }
        private void UpdateVisibility()
        {
            // Optimize by only showing controls that are potentially visible
            var visibleRect = new Rect(
                _horizontalOffset - 500,
                _verticalOffset - 200,
                _programGridScrollViewer.ViewportWidth + 1000,
                _programGridScrollViewer.ViewportHeight + 400);

            foreach (var control in _programControls)
            {
                var left = Canvas.GetLeft(control);
                var top = Canvas.GetTop(control);
                var bounds = new Rect(left, top, control.Width, control.Height);

                // Set visibility based on whether it intersects with visible area
                control.Visibility = visibleRect.IntersectsWith(bounds) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        #endregion

        #region Helper Classes

        private class ChannelRow
        {
            public int ChannelIndex { get; set; }
            public string ChannelName { get; set; }
            public List<ProgramInfo> Programs { get; set; } = new List<ProgramInfo>();
        }

        #endregion
    }

    /// <summary>
    /// Custom control for a program in the EPG
    /// </summary>
    public class ProgramControl : Control
    {
        // First program color set
        private static readonly SolidColorBrush Color1Selected = new SolidColorBrush(Color.FromRgb(100, 149, 237)); // Cornflower Blue
        private static readonly SolidColorBrush Color1Normal = new SolidColorBrush(Color.FromRgb(202, 225, 255));   // Light blue

        // Second program color set (alternating)
        private static readonly SolidColorBrush Color2Selected = new SolidColorBrush(Color.FromRgb(70, 130, 180));   // Steel Blue
        private static readonly SolidColorBrush Color2Normal = new SolidColorBrush(Color.FromRgb(176, 196, 222));   // Light Steel Blue

        // Property to determine whether this is an "odd" program in the sequence
        public bool IsAlternatingProgram { get; set; }

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

        private ControlTemplate CreateDefaultTemplate()
        {
            // Create a template with TextBlocks for title and time
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "Border";
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));
            factory.SetValue(Border.BorderBrushProperty, Brushes.DarkBlue);
            factory.SetValue(SnapsToDevicePixelsProperty, true);
            factory.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

            var backgroundBinding = new System.Windows.Data.Binding("Background");
            backgroundBinding.RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent);
            factory.SetBinding(Border.BackgroundProperty, backgroundBinding);

            var panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.MarginProperty, new Thickness(2));
            factory.AppendChild(panel);

            var titleBlock = new FrameworkElementFactory(typeof(TextBlock));
            titleBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            titleBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            titleBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Title"));
            panel.AppendChild(titleBlock);

            var timeBlock = new FrameworkElementFactory(typeof(TextBlock));
            timeBlock.SetValue(TextBlock.FontSizeProperty, 10.0);
            timeBlock.SetValue(TextBlock.ForegroundProperty, Brushes.DarkGray);
            timeBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("StartTime") { StringFormat = "{0:HH:mm}" });
            panel.AppendChild(timeBlock);

            return new ControlTemplate(typeof(ProgramControl)) { VisualTree = factory };
        }

        public void UpdateStyle(bool isSelected)
        {
            BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            BorderBrush = isSelected ? Brushes.DarkBlue : Brushes.SlateGray;

            // Set background based on alternating flag and selection state
            if (isSelected)
            {
                Background = IsAlternatingProgram ? Color2Selected : Color1Selected;
            }
            else
            {
                Background = IsAlternatingProgram ? Color2Normal : Color1Normal;
            }
        }
    }

    /// <summary>
    /// Helper converter for program background based on selection
    /// </summary>
    public class ProgramBackgroundConverter : System.Windows.Data.IValueConverter
    {
        // First program color set
        private static readonly SolidColorBrush Color1Selected = new SolidColorBrush(Color.FromRgb(100, 149, 237)); // Cornflower Blue
        private static readonly SolidColorBrush Color1Normal = new SolidColorBrush(Color.FromRgb(202, 225, 255));   // Light blue

        // Second program color set (alternating)
        private static readonly SolidColorBrush Color2Selected = new SolidColorBrush(Color.FromRgb(70, 130, 180));   // Steel Blue
        private static readonly SolidColorBrush Color2Normal = new SolidColorBrush(Color.FromRgb(176, 196, 222));   // Light Steel Blue

        // Dictionary to track program alternation by channel
        private static Dictionary<int, Dictionary<DateTime, bool>> _alternatingState = new Dictionary<int, Dictionary<DateTime, bool>>();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isSelected = value is bool boolValue && boolValue;

            // Check if we have the alternating flag from the ProgramControl
            if (parameter is bool isAlternating)
            {
                if (isSelected)
                {
                    return isAlternating ? Color2Selected : Color1Selected;
                }
                else
                {
                    return isAlternating ? Color2Normal : Color1Normal;
                }
            }
            // Default fallback if no alternating info
            else if (parameter is ProgramInfo program)
            {
                // Get or create alternating tracking for this channel
                if (!_alternatingState.TryGetValue(program.ChannelIndex, out var channelState))
                {
                    channelState = new Dictionary<DateTime, bool>();
                    _alternatingState[program.ChannelIndex] = channelState;
                }

                // Get or determine alternating state for this program
                if (!channelState.TryGetValue(program.StartTime, out bool isAlt))
                {
                    // Determine based on position in the sequence
                    var programsList = _alternatingState.Keys
                        .Where(k => k == program.ChannelIndex)
                        .SelectMany(k => _alternatingState[k].Keys)
                        .OrderBy(dt => dt)
                        .ToList();

                    isAlt = programsList.Count % 2 == 1;
                    channelState[program.StartTime] = isAlt;
                }

                if (isSelected)
                {
                    return isAlt ? Color2Selected : Color1Selected;
                }
                else
                {
                    return isAlt ? Color2Normal : Color1Normal;
                }
            }

            // Default fallback
            return isSelected ? Color1Selected : Color1Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to create a Thickness from a TimelineHeight value
    /// </summary>
    public class TimelineHeightToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double height)
            {
                return new Thickness(0, height, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Thickness thickness)
            {
                return thickness.Top;
            }
            return 0.0;
        }
    }
}