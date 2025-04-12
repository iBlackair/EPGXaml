using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows;
using System.Diagnostics;

namespace EPGVirtualization;
public partial class VideoControl : UserControl
{
    #region Events
    // Events remain unchanged
    public event EventHandler PlayPauseClicked;
    public event EventHandler FullScreenClicked;
    public event EventHandler MuteToggleClicked;
    public event EventHandler<double> VolumeChanged;
    public event EventHandler<double> SeekPositionChanged;
    public event EventHandler PreviousClicked;
    public event EventHandler NextClicked;
    public event EventHandler<string> QualityChanged;
    public event EventHandler TheaterModeClicked;
    public event EventHandler<double> PlaybackSpeedChanged;
    public event EventHandler LoopClicked;
    public event EventHandler MiniPlayerClicked;
    public event EventHandler CastClicked;
    public event EventHandler<string> SubtitleChanged;
    #endregion

    #region Properties
    // Properties remain unchanged 
    private bool _isPlaying = false;
    public bool IsPlaying
    {
        get { return _isPlaying; }
        set
        {
            _isPlaying = value;
            UpdatePlayPauseIcon();
        }
    }

    private bool _isMuted = false;
    public bool IsMuted
    {
        get { return _isMuted; }
        set
        {
            _isMuted = value;
            UpdateVolumeIcon();
        }
    }

    private double _volumeLevel = 75.0;
    public double VolumeLevel
    {
        get { return _volumeLevel; }
        set
        {
            _volumeLevel = value;
            if (VolumeSlider != null)
                VolumeSlider.Value = value;
        }
    }

    private double _progressPercentage = 0.0;
    public double ProgressPercentage
    {
        get { return _progressPercentage; }
        set
        {
            _progressPercentage = value;
            UpdateProgressUI();
        }
    }

    private double _bufferPercentage = 0.0;
    public double BufferPercentage
    {
        get { return _bufferPercentage; }
        set
        {
            _bufferPercentage = value;
            UpdateBufferUI();
        }
    }

    private bool _isDraggingProgress = false;
    private bool _isSettingsOpen = false;
    private DispatcherTimer _progressUpdateTimer;
    #endregion

    #region Constructor
    public VideoControl()
    {
        InitializeComponent();

        // Initialize UI states
        UpdatePlayPauseIcon();
        UpdateVolumeIcon();

        // Setup auto-hide timer
        SetupAutoHideTimer();

        // Setup progress update timer
        SetupProgressUpdateTimer();

        // Add handlers for control resizing
        this.SizeChanged += VideoControl_SizeChanged;
        this.Loaded += VideoControl_Loaded;
    }

    private void VideoControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Initial update of progress bars based on actual control size
        UpdateProgressUI();
        UpdateBufferUI();
    }


    private void SetupProgressUpdateTimer()
    {
        _progressUpdateTimer = new DispatcherTimer();
        _progressUpdateTimer.Interval = TimeSpan.FromSeconds(1);
        //_progressUpdateTimer.Tick += (s, e) => UpdateProgress();
        //_progressUpdateTimer.Start();
    }

    private void UpdateProgressUI()
    {
        if (ShowProgress != null && ProgressBarInteractive != null)
        {
            // Get available width from the parent container
            double availableWidth = ProgressBarInteractive.ActualWidth;

            // Calculate width based on percentage, but only if we have a valid width
            if (availableWidth > 0)
            {
                // Calculate the width as a percentage of available space
                ShowProgress.Width = (_progressPercentage / 100.0) * availableWidth;
            }
        }
    }

    private void UpdateBufferUI()
    {
        if (BufferProgress != null && ProgressBarInteractive != null)
        {
            // Get available width from the parent container
            double availableWidth = ProgressBarInteractive.ActualWidth;

            // Calculate width based on percentage, but only if we have a valid width
            if (availableWidth > 0)
            {
                // Calculate the width as a percentage of available space
                BufferProgress.Width = (_bufferPercentage / 100.0) * availableWidth;
            }
        }
    }
    #endregion

    #region UI Event Handlers

    // Play/Pause button click
    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        PlayPauseClicked?.Invoke(this, EventArgs.Empty);
    }

    // Previous button click
    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        PreviousClicked?.Invoke(this, EventArgs.Empty);
    }

    // Next button click
    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        NextClicked?.Invoke(this, EventArgs.Empty);
    }

    // Volume button click - toggle mute or show volume popup
    private void VolumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (VolumePopup.IsOpen)
        {
            VolumePopup.IsOpen = false;
        }
        else
        {
            // Toggle mute on direct button click
            IsMuted = !IsMuted;
            MuteToggleClicked?.Invoke(this, EventArgs.Empty);
            VolumeChanged?.Invoke(this, IsMuted ? 0 : VolumeLevel);
        }
    }

    // Volume slider value changed
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _volumeLevel = e.NewValue;
        if (_volumeLevel > 0 && IsMuted)
            IsMuted = false;
        else if (_volumeLevel == 0)
            IsMuted = true;

        VolumeChanged?.Invoke(this, _volumeLevel);
    }

    // Progress bar interactive events
    private void ProgressBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingProgress = true;
        StopAutoHideTimer(); // Keep controls visible while seeking

        // Calculate position based on mouse position
        UpdateProgressFromMousePosition(e);
    }

    private void ProgressBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingProgress)
        {
            _isDraggingProgress = false;
            UpdateProgressFromMousePosition(e);

            // Fire the seek event with the calculated position
            SeekPositionChanged?.Invoke(this, ProgressPercentage);

            StartAutoHideTimer(); // Resume auto-hide
            TimePreview.Visibility = Visibility.Collapsed;
        }
    }

    private void ProgressBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingProgress || ProgressBarInteractive.IsMouseOver)
        {
            // Show time preview
            TimePreview.Visibility = Visibility.Visible;

            // Get progress percentage from mouse position
            Point mousePosition = e.GetPosition(ProgressBarInteractive);
            double progressPercent = Math.Min(100, Math.Max(0,
                (mousePosition.X / ProgressBarInteractive.ActualWidth) * 100));

            // Update the preview position and text
            double previewWidth = TimePreview.ActualWidth > 0 ? TimePreview.ActualWidth : 50;
            double previewPosition = (mousePosition.X) - (previewWidth / 2);

            // Ensure preview stays within bounds
            previewPosition = Math.Max(0, Math.Min(previewPosition,
                ProgressBarInteractive.ActualWidth - previewWidth));

            TimePreview.Margin = new Thickness(previewPosition, 0, 0, 10);

            // Format preview time text (would be implemented based on your specific time calculation)
            // PreviewTimeText.Text = CalculateTimeFromProgress(progressPercent).ToString("HH:mm:ss");

            // If dragging, update the actual progress
            if (_isDraggingProgress)
            {
                ProgressPercentage = progressPercent;
            }
        }
    }

    private void ProgressBar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDraggingProgress)
        {
            TimePreview.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateProgressFromMousePosition(MouseEventArgs e)
    {
        Point mousePosition = e.GetPosition(ProgressBarInteractive);
        double progressPercent = Math.Min(100, Math.Max(0,
            (mousePosition.X / ProgressBarInteractive.ActualWidth) * 100));

        ProgressPercentage = progressPercent;
    }

    // Settings button click
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _isSettingsOpen = !_isSettingsOpen;
        SettingsPopup.IsOpen = _isSettingsOpen;

        if (_isSettingsOpen)
            StopAutoHideTimer();
        else
            StartAutoHideTimer();
    }

    // Settings dropdown events
    private void PlaybackSpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaybackSpeedComboBox.SelectedItem != null)
        {
            string speedText = (PlaybackSpeedComboBox.SelectedItem as ComboBoxItem).Content.ToString();
            double speed = Convert.ToDouble(speedText.Replace("x", ""));
            PlaybackSpeedChanged?.Invoke(this, speed);
        }
    }

    private void QualityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QualityComboBox.SelectedItem != null)
        {
            string quality = (QualityComboBox.SelectedItem as ComboBoxItem).Content.ToString();
            QualityChanged?.Invoke(this, quality);
        }
    }

    private void SubtitlesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SubtitlesComboBox.SelectedItem != null)
        {
            string subtitle = (SubtitlesComboBox.SelectedItem as ComboBoxItem).Content.ToString();
            SubtitleChanged?.Invoke(this, subtitle);
        }
    }

    // Theater mode button click
    private void TheaterButton_Click(object sender, RoutedEventArgs e)
    {
        TheaterModeClicked?.Invoke(this, EventArgs.Empty);
    }

    // Full screen button click
    private void FullScreenButton_Click(object sender, RoutedEventArgs e)
    {
        FullScreenClicked?.Invoke(this, EventArgs.Empty);
    }

    // Loop button click
    private void LoopButton_Click(object sender, RoutedEventArgs e)
    {
        LoopClicked?.Invoke(this, EventArgs.Empty);
    }

    // Mini player button click
    private void MiniPlayerButton_Click(object sender, RoutedEventArgs e)
    {
        MiniPlayerClicked?.Invoke(this, EventArgs.Empty);
    }

    // Cast button click
    private void CastButton_Click(object sender, RoutedEventArgs e)
    {
        CastClicked?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    #region UI Update Methods
    private void UpdatePlayPauseIcon()
    {
        if (PlayPauseIcon == null)
            return;

        if (IsPlaying)
        {
            // Pause icon (two vertical bars)
            PlayPauseIcon.Data = Geometry.Parse("M 16.8320 47.0898 L 22.1523 47.0898 C 24.2148 47.0898 25.2695 46.0117 25.2695 43.9492 L 25.2695 12.0273 C 25.2695 9.8945 24.2148 8.9102 22.1523 8.9102 L 16.8320 8.9102 C 14.7695 8.9102 13.6914 9.9883 13.6914 12.0273 L 13.6914 43.9492 C 13.6914 46.0117 14.7695 47.0898 16.8320 47.0898 Z M 33.8477 47.0898 L 39.1679 47.0898 C 41.2305 47.0898 42.3086 46.0117 42.3086 43.9492 L 42.3086 12.0273 C 42.3086 9.8945 41.2305 8.9102 39.1679 8.9102 L 33.8477 8.9102 C 31.7852 8.9102 30.7305 9.9883 30.7305 12.0273 L 30.7305 43.9492 C 30.7305 46.0117 31.7852 47.0898 33.8477 47.0898 Z");
        }
        else
        {
            // Play icon (triangle)
            PlayPauseIcon.Data = Geometry.Parse("M20.4086 9.35258C22.5305 10.5065 22.5305 13.4935 20.4086 14.6474L7.59662 21.6145C5.53435 22.736 3 21.2763 3 18.9671L3 5.0329C3 2.72368 5.53435 1.26402 7.59661 2.38548L20.4086 9.35258Z");
        }
    }

    private void UpdateVolumeIcon()
    {
        if (VolumeIcon == null)
            return;

        if (IsMuted)
        {
            // Muted icon (speaker with X)
            VolumeIcon.Data = Geometry.Parse("M8 1H6L2 5H0V11H2L6 15H8V1Z M9.29289 6.20711L11.0858 8L9.29289 9.79289L10.7071 11.2071L12.5 9.41421L14.2929 11.2071L15.7071 9.79289L13.9142 8L15.7071 6.20711L14.2929 4.79289L12.5 6.58579L10.7071 4.79289L9.29289 6.20711Z");
        }
        else
        {
            // Normal volume icon (speaker with waves)
            VolumeIcon.Data = Geometry.Parse("M277,571.015 L277,573.068 C282.872,574.199 287,578.988 287,585 C287,590.978 283,595.609 277,596.932 L277,598.986 C283.776,597.994 289,592.143 289,585 C289,577.857 283.776,572.006 277,571.015 L277,571.015 Z M272,573 L265,577.667 L265,592.333 L272,597 C273.104,597 274,596.104 274,595 L274,575 C274,573.896 273.104,573 272,573 L272,573 Z M283,585 C283,581.477 280.388,578.59 277,578.101 L277,580.101 C279.282,580.564 281,582.581 281,585 C281,587.419 279.282,589.436 277,589.899 L277,591.899 C280.388,591.41 283,588.523 283,585 L283,585 Z M258,581 L258,589 C258,590.104 258.896,591 260,591 L263,591 L263,579 L260,579 C258.896,579 258,579.896 258,581 L258,581 Z");
        }
    }

    /// <summary>
    /// Updates the time display in the control panel
    /// </summary>
    /// <param name="startTime">Show start time</param>
    /// <param name="stopTime">Show stop time</param>
    public void UpdateTime(DateTime startTime, DateTime stopTime)
    {
        DateTime now = DateTime.Now;
        TimeSpan totalDuration = stopTime - startTime;
        TimeSpan timePassed = now - startTime;

        if (CurrentTimeText == null || TotalDurationText == null)
            return;

        // Calculate percentage for progress bar
        double percentagePassed = 0;
        if (totalDuration.TotalSeconds > 0)
        {
            percentagePassed = Math.Min(100, Math.Max(0,
                (timePassed.TotalSeconds / totalDuration.TotalSeconds) * 100));

            // Update progress percentage
            ProgressPercentage = percentagePassed;
        }

        string timePassedFormatted = timePassed.ToString(@"hh\:mm\:ss");

        // Display start time if before show start, current time if during show
        if (now < startTime) // Before Program Start
            CurrentTimeText.Text = "00:00";
        else if (now > stopTime) // After Show
            CurrentTimeText.Text = FormatTimeSpan(totalDuration);
        else
            CurrentTimeText.Text = FormatTimeSpan(timePassed);

        // Always display total duration
        TotalDurationText.Text = FormatTimeSpan(totalDuration);
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
        {
            return timeSpan.ToString(@"h\:mm\:ss");
        }
        else
        {
            return timeSpan.ToString(@"m\:ss");
        }
    }
    #endregion

    #region Visibility Management
    private double originalOpacity = 1.0;
    private DispatcherTimer hideTimer;

    private void SetupAutoHideTimer()
    {
        hideTimer = new DispatcherTimer();
        hideTimer.Interval = TimeSpan.FromSeconds(3);
        hideTimer.Tick += (s, e) => Hide();
    }

    /// <summary>
    /// Shows the control panel
    /// </summary>
    public void Show()
    {
        this.Visibility = Visibility.Visible;
        this.Opacity = originalOpacity;
    }

    /// <summary>
    /// Hides the control panel
    /// </summary>
    public void Hide()
    {
        // In a full implementation, you'd use animation
        this.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Starts the auto-hide timer
    /// </summary>
    public void StartAutoHideTimer()
    {
        hideTimer.Stop();
        hideTimer.Start();
    }

    /// <summary>
    /// Resets the auto-hide timer (call when mouse moves)
    /// </summary>
    public void ResetAutoHideTimer()
    {
        hideTimer.Stop();
        hideTimer.Start();
    }

    /// <summary>
    /// Stops the auto-hide timer
    /// </summary>
    public void StopAutoHideTimer()
    {
        hideTimer.Stop();
    }
    #endregion

    // Add this to your VideoControl.xaml.cs file

    // Add a new property to track if we should animate resizing
    public bool AnimateResizing { get; set; } = true;

    // Add this method to the class
    public void HandleWindowStateChanged(WindowState newState)
    {
        if (newState == WindowState.Maximized || newState == WindowState.Normal)
        {
            // Hide during the transition
            var currentVisibility = this.Visibility;
            this.Visibility = Visibility.Hidden;

            // Create a delay to match the Window animation time (typically around 150-250ms)
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (s, e) =>
            {
                this.Visibility = currentVisibility;
                timer.Stop();
            };
            timer.Start();
        }
    }

    // Modify your existing SizeChanged handler or add this if you don't have one
    private void VideoControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Use dispatcher to delay the update slightly, allowing parent containers to finish resizing
        if (AnimateResizing && Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 5)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateProgressUI();
                UpdateBufferUI();
            }), DispatcherPriority.Render);
        }
        else
        {
            UpdateProgressUI();
            UpdateBufferUI();
        }
    }
}