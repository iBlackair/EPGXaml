using EPGVirtualization.Controls;
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
using System.Windows.Media.Animation;
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
        public static readonly DependencyProperty CurrentAiringProgramBrushProperty =
            DependencyProperty.Register(
                "CurrentAiringProgramBrush",
                typeof(Brush),
                typeof(EPGCanvas),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(71, 123, 184))));

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

        public Brush CurrentAiringProgramBrush
        {
            get => (Brush)GetValue(CurrentAiringProgramBrushProperty);
            set => SetValue(CurrentAiringProgramBrushProperty, value);
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
        private DispatcherTimer _programUpdateTimer;

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
            Unloaded += OnUnloaded;     
            InitializeProgramUpdateTimer();
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
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Stop the timer to prevent memory leaks
            if (_programUpdateTimer != null)
            {
                _programUpdateTimer.Stop();
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

            // Wire up the date display click event
            // Wire up the date display click event
            if (GetTemplateChild("PART_DateDisplay") is TextBlock dateDisplay)
            {
                // Convert the TextBlock to a clickable element by adding a transparent Button
                var parent = dateDisplay.Parent as Grid;
                if (parent != null)
                {
                    var clickArea = new Button
                    {
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        Content = dateDisplay
                    };

                    // Remove the TextBlock from its parent
                    parent.Children.Remove(dateDisplay);

                    // Add the button with the TextBlock inside
                    parent.Children.Add(clickArea);

                    // Wire up the click event
                    clickArea.Click += (s, e) =>
                    {
                        if (GetTemplateChild("PART_DatePickerPopup") is Popup popup)
                        {
                            popup.IsOpen = !popup.IsOpen;
                            e.Handled = true;
                        }
                    };
                }
            }
            if (GetTemplateChild("PART_DateButton") is Button dateButton)
            {
                dateButton.Click += (s, e) =>
                {
                    if (GetTemplateChild("PART_DatePickerPopup") is Popup popup)
                    {
                        popup.IsOpen = !popup.IsOpen;
                        e.Handled = true;
                    }
                };
            }
            // Wire up the date picker selection event
            if (GetTemplateChild("PART_DatePicker") is DatePicker datePicker)
            {
                datePicker.SelectedDateChanged += (s, e) =>
                {
                    // When date changes, close the popup
                    if (GetTemplateChild("PART_DatePickerPopup") is Popup popup)
                    {
                        popup.IsOpen = false;
                    }

                    // Update the layout with the new date
                    UpdateLayout();
                };
            }
            // Wire up the calendar selection event
            if (GetTemplateChild("PART_Calendar") is System.Windows.Controls.Calendar calendar)
            {
                calendar.SelectedDatesChanged += (s, e) =>
                {
                    // When date changes, close the popup
                    if (GetTemplateChild("PART_DatePickerPopup") is Popup popup)
                    {
                        popup.IsOpen = false;
                    }

                    // Update the layout with the new date
                    UpdateLayout();
                };
            }

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
                    

                    _programGrid.MouseWheel += OnProgramGridMouseWheel;
                    //_programGrid.MouseLeftButtonDown += OnProgramGridMouseLeftButtonDown;
                    //_programGrid.PreviewMouseDown += _programGrid_PreviewMouseDown; ;
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
        

        private void OnProgramGridMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _programGridScrollViewer != null)
            {
                var currentPos = e.GetPosition(_programGrid);
                var delta = _lastDragPosition.X - currentPos.X;

                //Point currentPosition = e.GetPosition(this);

                //// Only allow horizontal scrolling - remove vertical scrolling
                double deltaX = currentPos.X - _lastDragPosition.X;

                _horizontalOffset = Math.Max(0, _horizontalOffset - deltaX);
                //// Vertical offset remains fixed during dragging

                //// Update only X position, keep Y the same
               // _lastMousePosition = new Point(currentPosition.X, _lastMousePosition.Y);

                _programGridScrollViewer.ScrollToHorizontalOffset(_horizontalOffset);


                //_programGridScrollViewer.ScrollToVerticalOffset(_programGridScrollViewer.VerticalOffset + delta.Y);

                _lastDragPosition = currentPos;
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
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                var zoom = 60 * PixelsPerMinute * Zoom;
                double scrollAmount = e.Delta > 0 ? zoom * -1 : zoom; // Adjust the value as needed for scroll sensitivity
                //double scrollAmount = e.Delta > 0 ? time : 500; // Adjust the value as needed for scroll sensitivity
                double newOffset = _programGridScrollViewer.HorizontalOffset + scrollAmount;
                newOffset = Math.Max(0, newOffset); // Prevent scrolling past the beginning
                _programGridScrollViewer.ScrollToHorizontalOffset(newOffset);
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

        public void SetChannels(List<ChannelRow> channels)
        {
            // This method is not used in the current implementation
            // but can be used to set channel information if needed
        }
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
                    ChannelLogo = null, // Placeholder for channel logo
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
            var evenRowBrush = new SolidColorBrush(Color.FromRgb(25, 25, 28)); // Light blue-gray
            var oddRowBrush = new SolidColorBrush(Color.FromRgb(19, 20, 22));  // Slightly darker blue-gray

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
                    Fill = new SolidColorBrush(Color.FromRgb(7, 7, 9)),
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
                };
                Canvas.SetLeft(text, x + rect.Width / 2 -17);
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
            var evenRowBrush = new SolidColorBrush(Color.FromRgb(22, 22, 22)); // Light blue-gray
            var oddRowBrush = new SolidColorBrush(Color.FromRgb(22, 22, 22));  // Slightly darker blue-gray
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
                    Background = new SolidColorBrush(Color.FromRgb(7,7,9))                    
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

                // Create an Image control (you can set the Source property later)
                var image = new Image
                {
                    Stretch = Stretch.Uniform
                };
                image.Source = channel.ChannelLogo;
                imageBox.Child = image;

                // Create the text block for the channel name
                var text = new TextBlock
                {
                    Text = channel.ChannelName,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(188,188,186)),
                };

                // Add both to the stack panel
                stackPanel.Children.Add(imageBox);
                stackPanel.Children.Add(text);

                // Set the stack panel as the content of the border
                label.Child = stackPanel;

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

        private void InitializeProgramUpdateTimer()
        {
            _programUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30) // Update every 30 seconds
            };

            _programUpdateTimer.Tick += (s, e) =>
            {
                // Update all program controls to refresh "currently airing" status
                foreach (var control in _programControls)
                {
                    if (control.DataContext is ProgramInfo p)
                    {
                        control.UpdateStyle(p == _selectedProgram);
                    }
                }
            };

            _programUpdateTimer.Start();
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

        public class ChannelRow
        {
            public int ChannelIndex { get; set; }
            public ImageSource ChannelLogo { get; set; }
            public string ChannelName { get; set; }
            public List<ProgramInfo> Programs { get; set; } = new List<ProgramInfo>();
        }

        #endregion
    }

    
    
}