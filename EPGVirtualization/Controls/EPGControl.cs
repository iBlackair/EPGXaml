using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;


namespace EPGVirtualization.Controls
{
    /// <summary>
    /// A high-performance Electronic Program Guide (EPG) control using UI virtualization
    /// for optimal handling of large datasets.
    /// </summary>
    [TemplatePart(Name = PART_MainScrollViewer, Type = typeof(ScrollViewer))]
    [TemplatePart(Name = PART_ProgramGrid, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_TimelineCanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_ChannelPanel, Type = typeof(VirtualizingStackPanel))]
    [TemplatePart(Name = PART_TimelineScrollViewer, Type = typeof(ScrollViewer))]
    [TemplatePart(Name = PART_ChannelScrollViewer, Type = typeof(ScrollViewer))]
    [TemplatePart(Name = PART_DateButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_DatePickerPopup, Type = typeof(Popup))]
    [TemplatePart(Name = PART_Calendar, Type = typeof(Calendar))]
    public class EPGControl : Control
    {
        #region Constants
        private const string PART_MainScrollViewer = "PART_MainScrollViewer";
        private const string PART_ProgramGrid = "PART_ProgramGrid";
        private const string PART_TimelineCanvas = "PART_TimelineCanvas";
        private const string PART_ChannelPanel = "PART_ChannelPanel";
        private const string PART_TimelineScrollViewer = "PART_TimelineScrollViewer";
        private const string PART_ChannelScrollViewer = "PART_ChannelScrollViewer";
        private const string PART_DateButton = "PART_DateButton";
        private const string PART_DatePickerPopup = "PART_DatePickerPopup";
        private const string PART_Calendar = "PART_Calendar";
        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty ProgramsProperty =
            DependencyProperty.Register(
                "Programs",
                typeof(IList<ProgramInfo>),
                typeof(EPGControl),
                new PropertyMetadata(null, OnProgramsChanged));

        public static readonly DependencyProperty ViewStartTimeProperty =
            DependencyProperty.Register(
                "ViewStartTime",
                typeof(DateTime),
                typeof(EPGControl),
                new PropertyMetadata(DateTime.Today, OnViewStartTimeChanged));

        public static readonly DependencyProperty PixelsPerMinuteProperty =
            DependencyProperty.Register(
                "PixelsPerMinute",
                typeof(double),
                typeof(EPGControl),
                new PropertyMetadata(3.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(
                "Zoom",
                typeof(double),
                typeof(EPGControl),
                new PropertyMetadata(1.0, OnZoomChanged));

        public static readonly DependencyProperty ChannelHeightProperty =
            DependencyProperty.Register(
                "ChannelHeight",
                typeof(double),
                typeof(EPGControl),
                new PropertyMetadata(40.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty ChannelLabelWidthProperty =
            DependencyProperty.Register(
                "ChannelLabelWidth",
                typeof(double),
                typeof(EPGControl),
                new PropertyMetadata(100.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty TimelineHeightProperty =
            DependencyProperty.Register(
                "TimelineHeight",
                typeof(double),
                typeof(EPGControl),
                new PropertyMetadata(40.0, OnLayoutPropertyChanged));

        public static readonly DependencyProperty SelectedProgramProperty =
            DependencyProperty.Register(
                "SelectedProgram",
                typeof(ProgramInfo),
                typeof(EPGControl),
                new PropertyMetadata(null, OnSelectedProgramChanged));

        public static readonly DependencyProperty AlternatingProgram1BrushProperty =
            DependencyProperty.Register(
                "AlternatingProgram1Brush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(38, 38, 38))));

        public static readonly DependencyProperty AlternatingProgram2BrushProperty =
            DependencyProperty.Register(
                "AlternatingProgram2Brush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(85, 85, 85))));

        public static readonly DependencyProperty SelectedProgram1BrushProperty =
            DependencyProperty.Register(
                "SelectedProgram1Brush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(18, 18, 20))));

        public static readonly DependencyProperty SelectedProgram2BrushProperty =
            DependencyProperty.Register(
                "SelectedProgram2Brush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(63, 63, 65))));

        public static readonly DependencyProperty TimelineBrushProperty =
            DependencyProperty.Register(
                "TimelineBrush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(7, 7, 9))));

        public static readonly DependencyProperty ChannelLabelBrushProperty =
            DependencyProperty.Register(
                "ChannelLabelBrush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(7, 7, 9))));

        public static readonly DependencyProperty EvenRowBackgroundProperty =
            DependencyProperty.Register(
                "EvenRowBackground",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(25, 25, 28))));

        public static readonly DependencyProperty OddRowBackgroundProperty =
            DependencyProperty.Register(
                "OddRowBackground",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(19, 20, 22))));

        public static readonly DependencyProperty CurrentTimeMarkerBrushProperty =
            DependencyProperty.Register(
                "CurrentTimeMarkerBrush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(Brushes.Red));

        public static readonly DependencyProperty ProgramTextBrushProperty =
            DependencyProperty.Register(
                "ProgramTextBrush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(194, 194, 194))));

        public static readonly DependencyProperty TimeTextBrushProperty =
            DependencyProperty.Register(
                "TimeTextBrush",
                typeof(Brush),
                typeof(EPGControl),
                new PropertyMetadata(Brushes.DarkGray));

        public IList<ProgramInfo> Programs
        {
            get => (IList<ProgramInfo>)GetValue(ProgramsProperty);
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

        public ProgramInfo SelectedProgram
        {
            get => (ProgramInfo)GetValue(SelectedProgramProperty);
            set => SetValue(SelectedProgramProperty, value);
        }

        public Brush AlternatingProgram1Brush
        {
            get => (Brush)GetValue(AlternatingProgram1BrushProperty);
            set => SetValue(AlternatingProgram1BrushProperty, value);
        }

        public Brush AlternatingProgram2Brush
        {
            get => (Brush)GetValue(AlternatingProgram2BrushProperty);
            set => SetValue(AlternatingProgram2BrushProperty, value);
        }

        public Brush SelectedProgram1Brush
        {
            get => (Brush)GetValue(SelectedProgram1BrushProperty);
            set => SetValue(SelectedProgram1BrushProperty, value);
        }

        public Brush SelectedProgram2Brush
        {
            get => (Brush)GetValue(SelectedProgram2BrushProperty);
            set => SetValue(SelectedProgram2BrushProperty, value);
        }

        public Brush TimelineBrush
        {
            get => (Brush)GetValue(TimelineBrushProperty);
            set => SetValue(TimelineBrushProperty, value);
        }

        public Brush ChannelLabelBrush
        {
            get => (Brush)GetValue(ChannelLabelBrushProperty);
            set => SetValue(ChannelLabelBrushProperty, value);
        }

        public Brush EvenRowBackground
        {
            get => (Brush)GetValue(EvenRowBackgroundProperty);
            set => SetValue(EvenRowBackgroundProperty, value);
        }

        public Brush OddRowBackground
        {
            get => (Brush)GetValue(OddRowBackgroundProperty);
            set => SetValue(OddRowBackgroundProperty, value);
        }

        public Brush CurrentTimeMarkerBrush
        {
            get => (Brush)GetValue(CurrentTimeMarkerBrushProperty);
            set => SetValue(CurrentTimeMarkerBrushProperty, value);
        }

        public Brush ProgramTextBrush
        {
            get => (Brush)GetValue(ProgramTextBrushProperty);
            set => SetValue(ProgramTextBrushProperty, value);
        }

        public Brush TimeTextBrush
        {
            get => (Brush)GetValue(TimeTextBrushProperty);
            set => SetValue(TimeTextBrushProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a program is selected
        /// </summary>
        public event EventHandler<ProgramSelectionEventArgs> ProgramSelected;

        /// <summary>
        /// Event raised when the view changes
        /// </summary>
        public event EventHandler<ViewChangedEventArgs> ViewChanged;

        /// <summary>
        /// Event raised when a program action is executed (e.g. double-click)
        /// </summary>
        public event EventHandler<ProgramActionEventArgs> ProgramAction;

        #endregion

        #region Private Fields

        private ScrollViewer _programGridScrollViewer;
        private Canvas _programGrid;
        private Canvas _timelineCanvas;
        private Panel _channelPanel;
        private ScrollViewer _timelineScrollViewer;
        private ScrollViewer _channelScrollViewer;

        // UI Virtualization fields
        private Dictionary<int, ChannelRow> _channelRows = new Dictionary<int, ChannelRow>();
        private Dictionary<string, ProgramControl> _programControls = new Dictionary<string, ProgramControl>();
        private HashSet<string> _visibleProgramKeys = new HashSet<string>();

        // Scrolling and interaction fields
        private double _horizontalOffset = 0;
        private double _verticalOffset = 0;
        private Point _lastDragPosition;
        private bool _isDragging = false;
        private bool _isViewChanging = false;

        // Drag acceleration support
        private double _scrollVelocityX = 0;
        private DispatcherTimer _momentumTimer;

        // Recycling pool for program controls
        private Stack<ProgramControl> _recycledProgramControls = new Stack<ProgramControl>();

        // Current time marker timer
        private DispatcherTimer _currentTimeTimer;
        private UIElement _currentTimeMarker;

        #endregion

        #region Constructor and Initialization

        static EPGControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(EPGControl),
                new FrameworkPropertyMetadata(typeof(EPGControl)));
        }

        public EPGControl()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // Set default options for optimal performance
            SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // Initialize timers
            InitializeMomentumTimer();
            InitializeCurrentTimeTimer();
        }

        private void InitializeMomentumTimer()
        {
            _momentumTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };
            _momentumTimer.Tick += (s, e) =>
            {
                if (Math.Abs(_scrollVelocityX) < 0.1)
                {
                    _momentumTimer.Stop();
                    return;
                }

                if (_programGridScrollViewer != null)
                {
                    _horizontalOffset = Math.Max(0, _horizontalOffset + _scrollVelocityX);
                    _programGridScrollViewer.ScrollToHorizontalOffset(_horizontalOffset);
                }

                // Apply friction
                _scrollVelocityX *= 0.95;
            };
        }

        private void InitializeCurrentTimeTimer()
        {
            _currentTimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _currentTimeTimer.Tick += (s, e) => UpdateCurrentTimeMarker();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Programs != null && Programs.Count > 0)
            {
                ProcessProgramData();

                // Wait for template to be applied
                Dispatcher.BeginInvoke(new Action(() => {
                    ApplyInitialView();
                }), DispatcherPriority.Loaded);
            }

            _currentTimeTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _currentTimeTimer.Stop();
            _momentumTimer.Stop();

            // Clean up event handlers
            if (_programGrid != null)
            {
                _programGrid.MouseMove -= OnProgramGridMouseMove;
                _programGrid.MouseLeftButtonUp -= OnProgramGridMouseLeftButtonUp;
                _programGrid.PreviewMouseLeftButtonDown -= OnProgramGridPreviewMouseDown;
                _programGrid.MouseLeave -= OnProgramGridMouseLeave;
                _programGrid.MouseWheel -= OnProgramGridMouseWheel;
            }

            if (_programGridScrollViewer != null)
            {
                _programGridScrollViewer.ScrollChanged -= OnScrollChanged;
            }

            // Clear collections
            ClearControls();
        }

        private void ClearControls()
        {
            if (_programGrid != null)
            {
                _programGrid.Children.Clear();
            }

            if (_channelPanel != null)
            {
                _channelPanel.Children.Clear();
            }

            if (_timelineCanvas != null)
            {
                _timelineCanvas.Children.Clear();
            }

            _programControls.Clear();
            _visibleProgramKeys.Clear();
            _recycledProgramControls.Clear();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Get template parts
            _programGridScrollViewer = GetTemplateChild(PART_MainScrollViewer) as ScrollViewer;
            _programGrid = GetTemplateChild(PART_ProgramGrid) as Canvas;
            _timelineCanvas = GetTemplateChild(PART_TimelineCanvas) as Canvas;
            _channelPanel = GetTemplateChild(PART_ChannelPanel) as Panel;
            _timelineScrollViewer = GetTemplateChild(PART_TimelineScrollViewer) as ScrollViewer;
            _channelScrollViewer = GetTemplateChild(PART_ChannelScrollViewer) as ScrollViewer;

            // Setup synchronizing scrolling
            if (_programGridScrollViewer != null)
            {
                _programGridScrollViewer.ScrollChanged += OnScrollChanged;
            }

            // Setup mouse handlers for program grid
            if (_programGrid != null)
            {
                _programGrid.MouseMove += OnProgramGridMouseMove;
                _programGrid.MouseLeftButtonUp += OnProgramGridMouseLeftButtonUp;
                _programGrid.PreviewMouseLeftButtonDown += OnProgramGridPreviewMouseDown;
                _programGrid.MouseLeave += OnProgramGridMouseLeave;
                _programGrid.MouseWheel += OnProgramGridMouseWheel;
            }

            // Setup date picker
            SetupDatePicker();

            // If we already have data, update the layout
            if (Programs != null && Programs.Count > 0)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    ProcessProgramData();
                    ApplyInitialView();
                }), DispatcherPriority.Render);
            }
        }

        private void SetupDatePicker()
        {
            // Wire up date picker button
            Button dateButton = GetTemplateChild(PART_DateButton) as Button;
            Popup datePickerPopup = GetTemplateChild(PART_DatePickerPopup) as Popup;
            Calendar calendar = GetTemplateChild(PART_Calendar) as Calendar;

            if (dateButton != null && datePickerPopup != null)
            {
                dateButton.Click += (s, e) =>
                {
                    datePickerPopup.IsOpen = !datePickerPopup.IsOpen;
                    e.Handled = true;
                };
            }

            if (calendar != null)
            {
                calendar.SelectedDatesChanged += (s, e) =>
                {
                    if (datePickerPopup != null)
                    {
                        datePickerPopup.IsOpen = false;
                    }

                    if (calendar.SelectedDate.HasValue)
                    {
                        ViewStartTime = calendar.SelectedDate.Value.Date;
                    }
                };
            }
        }

        #endregion

        #region Data Processing

        private void ProcessProgramData()
        {
            if (Programs == null || Programs.Count == 0)
                return;

            // Group programs by channel
            _channelRows.Clear();
            var programsByChannel = Programs
                .GroupBy(p => p.ChannelIndex)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.StartTime).ToList());

            // Create channel rows dictionary
            foreach (var channelIndex in programsByChannel.Keys.OrderBy(x => x))
            {
                _channelRows[channelIndex] = new ChannelRow
                {
                    ChannelIndex = channelIndex,
                    ChannelName = $"Channel {channelIndex + 1}",
                    ChannelLogo = null,
                    Programs = programsByChannel[channelIndex]
                };
            }
        }

        private void ApplyInitialView()
        {
            if (_programGrid == null || _timelineCanvas == null || _channelPanel == null || _channelRows.Count == 0)
                return;

            SetupContainerSizes();
            DrawTimeline();
            DrawChannelLabels();
            DrawRowBackgrounds();
            DrawInitialPrograms();
            UpdateCurrentTimeMarker();
        }

        private void SetupContainerSizes()
        {
            // Set program grid size
            double gridWidth = 24 * 60 * PixelsPerMinute * Zoom; // 24 hours
            _programGrid.Width = gridWidth;
            _programGrid.Height = _channelRows.Count * ChannelHeight * Zoom;

            // Set timeline width to match program grid width
            _timelineCanvas.Width = gridWidth;
        }

        #endregion

        #region Drawing Methods

        private void DrawTimeline()
        {
            _timelineCanvas.Children.Clear();

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
                    Fill = TimelineBrush,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(rect, x);
                _timelineCanvas.Children.Add(rect);

                // Hour text
                var text = new TextBlock
                {
                    Text = time.ToString("HH:00"),
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = ProgramTextBrush
                };
                Canvas.SetLeft(text, x + rect.Width / 2 - 17);
                Canvas.SetTop(text, 10);
                _timelineCanvas.Children.Add(text);

                // 30-minute mark if there's enough space
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

            foreach (var channelRow in _channelRows.Values.OrderBy(c => c.ChannelIndex))
            {
                var rowHeight = Math.Floor(ChannelHeight * Zoom); // Ensure whole pixel values

                var label = new Border
                {
                    Width = Math.Floor(ChannelLabelWidth),
                    Height = rowHeight,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Background = ChannelLabelBrush
                };

                // Create a StackPanel to hold both the image and text horizontally
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Create an image box
                var imageBox = new Border
                {
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(5, 0, 5, 0),
                    Background = Brushes.Transparent
                };

                // Create an Image control
                var image = new Image
                {
                    Stretch = Stretch.Uniform
                };
                image.Source = channelRow.ChannelLogo;
                imageBox.Child = image;

                // Create the text block for the channel name
                var text = new TextBlock
                {
                    Text = channelRow.ChannelName,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ProgramTextBrush,
                };

                // Add both to the stack panel
                stackPanel.Children.Add(imageBox);
                stackPanel.Children.Add(text);

                // Set the stack panel as the content of the border
                label.Child = stackPanel;

                _channelPanel.Children.Add(label);
            }
        }

        private void DrawRowBackgrounds()
        {
            // First remove any existing row backgrounds
            var backgroundElements = _programGrid.Children
                .OfType<UIElement>()
                .Where(e => e.GetValue(TagProperty) as string == "RowBackground")
                .ToList();

            foreach (var element in backgroundElements)
            {
                _programGrid.Children.Remove(element);
            }

            // Create new row backgrounds
            int i = 0;
            foreach (var channelRow in _channelRows.Values.OrderBy(c => c.ChannelIndex))
            {
                var rowHeight = Math.Floor(ChannelHeight * Zoom); // Ensure whole pixel values
                var y = i * rowHeight;

                // Determine if this is an even or odd row
                var isEvenRow = i % 2 == 0;
                var rowBrush = isEvenRow ? EvenRowBackground : OddRowBackground;

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
                Panel.SetZIndex(rowBackground, -10);
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
                Panel.SetZIndex(line, -5);
                _programGrid.Children.Add(line);

                i++;
            }
        }

        private void DrawInitialPrograms()
        {
            // Clear old controls and recycling pool
            _programControls.Clear();
            _recycledProgramControls.Clear();
            _visibleProgramKeys.Clear();

            // Remove program controls from the grid
            var programElements = _programGrid.Children
                .OfType<ProgramControl>()
                .ToList();

            foreach (var element in programElements)
            {
                _programGrid.Children.Remove(element);
            }

            // Calculate the visible viewport
            UpdateVisiblePrograms();
        }

        private void UpdateCurrentTimeMarker()
        {
            // Remove existing marker
            if (_currentTimeMarker != null)
            {
                _programGrid.Children.Remove(_currentTimeMarker);
                _currentTimeMarker = null;
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
                    Stroke = CurrentTimeMarkerBrush,
                    StrokeThickness = 2,
                    Tag = "CurrentTime"
                };

                Panel.SetZIndex(line, 10); // Ensure it's on top
                _programGrid.Children.Add(line);
                _currentTimeMarker = line;
            }
        }

        #endregion

        #region UI Virtualization

        private void UpdateVisiblePrograms()
        {
            if (_programGrid == null || _programGridScrollViewer == null || _channelRows.Count == 0)
                return;

            // Calculate visible area with buffer zone
            var visibleRect = new Rect(
                _horizontalOffset - 500, // Buffer before
                _verticalOffset - 100,   // Buffer above
                _programGridScrollViewer.ViewportWidth + 1000, // Buffer after
                _programGridScrollViewer.ViewportHeight + 200  // Buffer below
            );

            // Track which programs should be visible
            var newVisibleKeys = new HashSet<string>();

            // For each channel in view
            int startChannel = Math.Max(0, (int)(_verticalOffset / (ChannelHeight * Zoom)));
            int endChannel = Math.Min(_channelRows.Count - 1,
                (int)((_verticalOffset + _programGridScrollViewer.ViewportHeight) / (ChannelHeight * Zoom)) + 1);

            for (int i = startChannel; i <= endChannel; i++)
            {
                int channelIndex = _channelRows.Keys.OrderBy(k => k).ElementAt(i);
                var channelRow = _channelRows[channelIndex];
                var rowHeight = Math.Floor(ChannelHeight * Zoom);
                var y = i * rowHeight;

                // Calculate time range in view (with buffer)
                double viewStartMinutes = (_horizontalOffset - 500) / (PixelsPerMinute * Zoom);
                double viewEndMinutes = (_horizontalOffset + _programGridScrollViewer.ViewportWidth + 500) / (PixelsPerMinute * Zoom);

                DateTime rangeStart = ViewStartTime.AddMinutes(viewStartMinutes);
                DateTime rangeEnd = ViewStartTime.AddMinutes(viewEndMinutes);

                // Find programs in this channel that overlap with the visible time range
                var visiblePrograms = channelRow.Programs
                    .Where(p => p.StartTime < rangeEnd && p.StartTime.Add(p.Duration) > rangeStart)
                    .OrderBy(p => p.StartTime);

                // Alternate coloring state
                bool isAlternate = false;

                foreach (var program in visiblePrograms)
                {
                    // Generate a unique key for this program
                    string programKey = $"{program.ChannelIndex}_{program.Id}";
                    newVisibleKeys.Add(programKey);

                    double x = Math.Floor((program.StartTime - ViewStartTime).TotalMinutes * PixelsPerMinute * Zoom);
                    double width = Math.Floor(program.Duration.TotalMinutes * PixelsPerMinute * Zoom);

                    // Skip extremely small or out-of-view programs
                    if (width < 1 || x + width < visibleRect.Left || x > visibleRect.Right)
                        continue;

                    // If we already have this program visible, update its position
                    if (_programControls.TryGetValue(programKey, out var existingControl))
                    {
                        Canvas.SetLeft(existingControl, x);
                        Canvas.SetTop(existingControl, y);
                        existingControl.Width = Math.Max(2, width);
                        existingControl.Height = rowHeight - 1;
                        existingControl.IsAlternate = isAlternate;
                        existingControl.UpdateSelection(program == SelectedProgram);
                    }
                    else
                    {
                        // Create a new program control or get one from recycling pool
                        ProgramControl programControl;
                        if (_recycledProgramControls.Count > 0)
                        {
                            programControl = _recycledProgramControls.Pop();
                            programControl.DataContext = program;
                        }
                        else
                        {
                            programControl = new ProgramControl { DataContext = program };
                            programControl.ProgramClicked += OnProgramControlClicked;
                            programControl.ProgramDoubleClicked += OnProgramControlDoubleClicked;
                        }

                        // Configure control
                        programControl.Width = Math.Max(2, width);
                        programControl.Height = rowHeight - 1;
                        programControl.Tag = programKey;
                        programControl.IsAlternate = isAlternate;
                        programControl.UpdateSelection(program == SelectedProgram);

                        // Position on canvas
                        Canvas.SetLeft(programControl, x);
                        Canvas.SetTop(programControl, y);
                        Panel.SetZIndex(programControl, 0);

                        // Add to controls dictionary and canvas
                        _programControls[programKey] = programControl;
                        _programGrid.Children.Add(programControl);
                    }

                    // Toggle alternating state for next program
                    isAlternate = !isAlternate;
                }
            }

            // Remove programs that are no longer visible
            var keysToRemove = _visibleProgramKeys.Except(newVisibleKeys).ToList();
            foreach (var key in keysToRemove)
            {
                if (_programControls.TryGetValue(key, out var control))
                {
                    // Recycle the control
                    _programGrid.Children.Remove(control);
                    _recycledProgramControls.Push(control);
                    _programControls.Remove(key);
                }
            }

            // Update visible programs set
            _visibleProgramKeys = newVisibleKeys;
        }

        #endregion

        #region Scrolling and Interaction

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _horizontalOffset = e.HorizontalOffset;
            _verticalOffset = e.VerticalOffset;

            // Sync timeline horizontal scroll
            if (_timelineScrollViewer != null)
            {
                _timelineScrollViewer.ScrollToHorizontalOffset(_horizontalOffset);
            }

            // Sync channel list vertical scroll
            if (_channelScrollViewer != null)
            {
                _channelScrollViewer.ScrollToVerticalOffset(_verticalOffset);
            }

            // Update program visibility
            UpdateVisiblePrograms();

            // Raise view changed event
            if (!_isViewChanging)
            {
                _isViewChanging = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ViewChanged?.Invoke(this, new ViewChangedEventArgs
                    {
                        HorizontalOffset = _horizontalOffset,
                        VerticalOffset = _verticalOffset,
                        ViewportWidth = _programGridScrollViewer.ViewportWidth,
                        ViewportHeight = _programGridScrollViewer.ViewportHeight
                    });
                    _isViewChanging = false;
                }), DispatcherPriority.Input);
            }
        }

        private void OnProgramGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Only capture mouse if not clicking on a program directly
                if (!(e.OriginalSource is ProgramControl ||
                     e.OriginalSource is TextBlock && ((FrameworkElement)e.OriginalSource).TemplatedParent is ProgramControl))
                {
                    // Start drag operation
                    _programGrid.CaptureMouse();
                    _lastDragPosition = e.GetPosition(_programGrid);
                    _isDragging = true;

                    // Stop momentum
                    _scrollVelocityX = 0;
                    _momentumTimer.Stop();

                    e.Handled = true;
                }
            }
        }

        private void OnProgramGridMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _programGridScrollViewer != null)
            {
                var currentPos = e.GetPosition(_programGrid);

                // Calculate delta and update position for next frame
                double deltaX = currentPos.X - _lastDragPosition.X;

                // Scroll the main panel, horizontal only
                _horizontalOffset = Math.Max(0, _horizontalOffset - deltaX);
                _programGridScrollViewer.ScrollToHorizontalOffset(_horizontalOffset);

                // Store delta for momentum
                _scrollVelocityX = deltaX * 0.8; // Scale down a bit

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

                // Start momentum scrolling if velocity is high enough
                if (Math.Abs(_scrollVelocityX) > 1)
                {
                    _momentumTimer.Start();
                }

                e.Handled = true;
            }
        }

        private void OnProgramGridMouseLeave(object sender, MouseEventArgs e)
        {
            // End drag operation if mouse leaves the grid
            if (_isDragging)
            {
                _isDragging = false;
                _programGrid.ReleaseMouseCapture();

                // Start momentum scrolling if velocity is high enough
                if (Math.Abs(_scrollVelocityX) > 1)
                {
                    _momentumTimer.Start();
                }
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
                Zoom = Math.Max(0.5, Math.Min(5.0, Zoom));

                if (oldZoom != Zoom)
                {
                    // Apply new zoom in property changed handler
                }

                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Horizontal scrolling with mouse wheel when shift is pressed
                if (_programGridScrollViewer != null)
                {
                    _horizontalOffset = Math.Max(0, _horizontalOffset - e.Delta);
                    _programGridScrollViewer.ScrollToHorizontalOffset(_horizontalOffset);
                }

                e.Handled = true;
            }
        }

        private void OnProgramControlClicked(object sender, MouseButtonEventArgs e)
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

        private void OnProgramControlDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is ProgramControl programControl)
            {
                var program = programControl.DataContext as ProgramInfo;
                if (program != null)
                {
                    ProgramAction?.Invoke(this, new ProgramActionEventArgs
                    {
                        Program = program,
                        Action = ProgramActionType.Execute
                    });
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the list of program data to display
        /// </summary>
        public void SetPrograms(IList<ProgramInfo> programs)
        {
            Programs = programs;
        }

        /// <summary>
        /// Selects a specific program in the EPG
        /// </summary>
        public void SelectProgram(ProgramInfo program)
        {
            SelectedProgram = program;

            // Ensure program is visible
            EnsureProgramVisible(program);
        }

        /// <summary>
        /// Scrolls to make sure the given program is visible
        /// </summary>
        public void EnsureProgramVisible(ProgramInfo program)
        {
            if (program == null || _programGridScrollViewer == null)
                return;

            // Find the channel index
            int channelRow = _channelRows.Values.OrderBy(c => c.ChannelIndex)
                .ToList().FindIndex(c => c.ChannelIndex == program.ChannelIndex);

            if (channelRow < 0)
                return;

            // Calculate program position
            var y = channelRow * ChannelHeight * Zoom;
            var x = (program.StartTime - ViewStartTime).TotalMinutes * PixelsPerMinute * Zoom;
            var width = program.Duration.TotalMinutes * PixelsPerMinute * Zoom;

            // Calculate current viewport
            double viewportLeft = _horizontalOffset;
            double viewportRight = _horizontalOffset + _programGridScrollViewer.ViewportWidth;
            double viewportTop = _verticalOffset;
            double viewportBottom = _verticalOffset + _programGridScrollViewer.ViewportHeight;

            // Check if program is outside viewport horizontally
            if (x < viewportLeft || x + width > viewportRight)
            {
                // Center program horizontally
                _horizontalOffset = Math.Max(0, x - (_programGridScrollViewer.ViewportWidth - width) / 2);
                _programGridScrollViewer.ScrollToHorizontalOffset(_horizontalOffset);
            }

            // Check if program is outside viewport vertically
            if (y < viewportTop || y + ChannelHeight * Zoom > viewportBottom)
            {
                // Scroll to program vertically
                _programGridScrollViewer.ScrollToVerticalOffset(y);
            }
        }

        /// <summary>
        /// Navigate to a specific date
        /// </summary>
        public void NavigateToDate(DateTime date)
        {
            ViewStartTime = date.Date;
        }

        /// <summary>
        /// Navigate to a specific time on the current date
        /// </summary>
        public void NavigateToTime(TimeSpan time)
        {
            if (_programGridScrollViewer != null)
            {
                var x = time.TotalMinutes * PixelsPerMinute * Zoom;
                _programGridScrollViewer.ScrollToHorizontalOffset(x);
            }
        }

        /// <summary>
        /// Navigate to current time
        /// </summary>
        public void NavigateToNow()
        {
            var now = DateTime.Now;
            ViewStartTime = now.Date;

            Dispatcher.BeginInvoke(new Action(() => {
                NavigateToTime(now.TimeOfDay);
            }), DispatcherPriority.Loaded);
        }

        #endregion

        #region Property Change Handlers

        private static void OnProgramsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EPGControl epg && epg.IsLoaded)
            {
                epg.Dispatcher.BeginInvoke(new Action(() => {
                    epg.ProcessProgramData();
                    epg.ApplyInitialView();
                }), DispatcherPriority.Render);
            }
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EPGControl epg)
            {
                var oldZoom = (double)e.OldValue;
                var newZoom = (double)e.NewValue;

                // Get current viewport center before zoom
                if (epg._programGridScrollViewer != null)
                {
                    double centerTime = (epg._horizontalOffset + epg._programGridScrollViewer.ViewportWidth / 2) /
                                      (epg.PixelsPerMinute * oldZoom);

                    epg.Dispatcher.BeginInvoke(new Action(() => {
                        epg.SetupContainerSizes();
                        epg.DrawTimeline();
                        epg.DrawChannelLabels();
                        epg.DrawRowBackgrounds();

                        // Calculate new scroll position to keep the same time at the center
                        var newCenterX = centerTime * epg.PixelsPerMinute * newZoom;
                        epg._horizontalOffset = Math.Max(0, newCenterX - epg._programGridScrollViewer.ViewportWidth / 2);
                        epg._programGridScrollViewer.ScrollToHorizontalOffset(epg._horizontalOffset);

                        // Update visible programs
                        epg.UpdateVisiblePrograms();
                        epg.UpdateCurrentTimeMarker();
                    }), DispatcherPriority.Render);
                }
            }
        }

        private static void OnViewStartTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EPGControl epg)
            {
                epg.Dispatcher.BeginInvoke(new Action(() => {
                    epg.DrawTimeline();
                    epg.UpdateVisiblePrograms();
                    epg.UpdateCurrentTimeMarker();
                }), DispatcherPriority.Render);
            }
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EPGControl epg && epg.IsLoaded)
            {
                epg.Dispatcher.BeginInvoke(new Action(() => {
                    epg.SetupContainerSizes();
                    epg.DrawTimeline();
                    epg.DrawChannelLabels();
                    epg.DrawRowBackgrounds();
                    epg.UpdateVisiblePrograms();
                }), DispatcherPriority.Render);
            }
        }

        private static void OnSelectedProgramChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EPGControl epg)
            {
                var oldProgram = e.OldValue as ProgramInfo;
                var newProgram = e.NewValue as ProgramInfo;

                // Update program controls
                foreach (var programControl in epg._programControls.Values)
                {
                    if (programControl.DataContext is ProgramInfo p)
                    {
                        programControl.UpdateSelection(p == newProgram);
                    }
                }

                // Raise event
                if (newProgram != null)
                {
                    epg.ProgramSelected?.Invoke(epg, new ProgramSelectionEventArgs { Program = newProgram });
                }
            }
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Custom control to display a program in the EPG
        /// </summary>
        private class ProgramControl : Control
        {
            private TextBlock _titleBlock;
            private TextBlock _timeBlock;
            private Border _border;

            public event EventHandler<MouseButtonEventArgs> ProgramClicked;
            public event EventHandler<MouseButtonEventArgs> ProgramDoubleClicked;

            public bool IsAlternate { get; set; }

            static ProgramControl()
            {
                DefaultStyleKeyProperty.OverrideMetadata(
                    typeof(ProgramControl),
                    new FrameworkPropertyMetadata(typeof(ProgramControl)));
            }

            public ProgramControl()
            {
                // Set default properties
                BorderThickness = new Thickness(1);
                Margin = new Thickness(0);
                Padding = new Thickness(3);
                Cursor = Cursors.Hand;

                // Ensure pixel-perfect rendering
                SnapsToDevicePixels = true;
                SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

                // Set default template
                Template = CreateTemplate();

                // Handle mouse events
                MouseLeftButtonDown += (s, e) => ProgramClicked?.Invoke(this, e);
                MouseDoubleClick += (s, e) => ProgramDoubleClicked?.Invoke(this, e);
            }

            public override void OnApplyTemplate()
            {
                base.OnApplyTemplate();

                // Get template parts
                _border = GetTemplateChild("Border") as Border;
                _titleBlock = GetTemplateChild("TitleBlock") as TextBlock;
                _timeBlock = GetTemplateChild("TimeBlock") as TextBlock;

                // Set initial style
                UpdateSelection(false);
            }

            private ControlTemplate CreateTemplate()
            {
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
                panel.SetValue(MarginProperty, new Thickness(2));
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

            public void UpdateSelection(bool isSelected)
            {
                if (_border == null) return;

                EPGControl parent = TemplatedParent as EPGControl;
                if (parent == null) return;

                // Set border thickness based on selection
                BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
                BorderBrush = isSelected ? Brushes.LightBlue : Brushes.SlateGray;

                // Set background based on alternating flag and selection state
                if (isSelected)
                {
                    Background = IsAlternate ?
                        parent.SelectedProgram2Brush :
                        parent.SelectedProgram1Brush;
                }
                else
                {
                    Background = IsAlternate ?
                        parent.AlternatingProgram2Brush :
                        parent.AlternatingProgram1Brush;
                }
            }
        }

        /// <summary>
        /// Represents a row of programs for a channel
        /// </summary>
        private class ChannelRow
        {
            public int ChannelIndex { get; set; }
            public ImageSource ChannelLogo { get; set; }
            public string ChannelName { get; set; }
            public List<ProgramInfo> Programs { get; set; } = new List<ProgramInfo>();
        }

        /// <summary>
        /// Event arguments for program selection
        /// </summary>
        public class ProgramSelectionEventArgs : EventArgs
        {
            public ProgramInfo Program { get; set; }
        }

        /// <summary>
        /// Event arguments for view changed events
        /// </summary>
        public class ViewChangedEventArgs : EventArgs
        {
            public double HorizontalOffset { get; set; }
            public double VerticalOffset { get; set; }
            public double ViewportWidth { get; set; }
            public double ViewportHeight { get; set; }
        }

        /// <summary>
        /// Event arguments for program actions
        /// </summary>
        public class ProgramActionEventArgs : EventArgs
        {
            public ProgramInfo Program { get; set; }
            public ProgramActionType Action { get; set; }
        }

        /// <summary>
        /// Types of program actions
        /// </summary>
        public enum ProgramActionType
        {
            Execute,
            Info,
            Record
        }

        #endregion
    }

    /// <summary>
    /// Represents a TV program in the EPG
    /// </summary>
    public class ProgramInfo
    {
        /// <summary>
        /// Unique identifier for the program
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Channel index (position in the grid)
        /// </summary>
        public int ChannelIndex { get; set; }

        /// <summary>
        /// Program title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Program description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Start time of the program
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Duration of the program
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Category of the program
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Flag indicating if this program is currently selected
        /// </summary>
        public bool IsSelected { get; set; }
    }
}