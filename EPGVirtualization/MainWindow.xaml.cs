using EPGVirtualization.Classes;
using EPGVirtualization.Classes.EPGVirtualization;
using EPGVirtualization.Models;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace EPGVirtualization
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private System.Windows.Threading.DispatcherTimer _updateTimer;
        private System.Windows.Threading.DispatcherTimer _cursorTimer;
        private AspectRatioEnforcer _videoAspectRatioEnforcer;
        private bool _isResizing = false;
        private bool _isFullScreen = false;
        private bool _isCursorVisible = true;
        private Point _lastCursorPosition;

        // Remember original layout parameters before fullscreen
        private int _originalEpgRow;
        private int _originalEpgRowSpan;
        private int _originalVideoRow;
        private int _originalVideoRowSpan;
        private int _originalVideoColumn;
        private int _originalVideoColumnSpan;

        public event PropertyChangedEventHandler? PropertyChanged;
        private ProgramInfo _selectedProgram = new ProgramInfo();
        private ProgramInfo SelectedProgram
        {
            get => _selectedProgram;
            set
            {
                _selectedProgram = value;
                OnPropertyChanged(nameof(_selectedProgram));
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            // Make sure to add the style resources in App.xaml or MainWindow.xaml
            InitializeComponent();

            // Initialize LibVLC and create a media player
            Core.Initialize(); // Important: Initialize the VLC engine

            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.Volume = 75; // Set default volume to 75%

            // Connect the VideoView with the MediaPlayer
            VideoView.MediaPlayer = _mediaPlayer;

            InitializeAsync();

            // Set up the control panel events
            SetupControlPanel();

            this.StateChanged += MainWindow_StateChanged;
            this.Loaded += MainWindow_Loaded;

            // Handle keyboard events for fullscreen
            this.KeyDown += MainWindow_KeyDown;

            // Set up cursor auto-hide timer
            SetupCursorTimer();

            // Set up mouse events for cursor handling
            VideoView.MouseMove += VideoView_MouseMove;
            VideoContainer.MouseMove += VideoView_MouseMove;
            VideoAspectGrid.MouseMove += VideoView_MouseMove;

            // Create a timer to update the UI from LibVLC state
            _updateTimer = new System.Windows.Threading.DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
            _updateTimer.Tick += UpdateTimer_Tick;

            // Start the timer
            _updateTimer.Start();

            // Handle window closing to properly dispose resources
            Closing += MainWindow_Closing;
        }

        private void SetupCursorTimer()
        {
            _cursorTimer = new DispatcherTimer();
            _cursorTimer.Interval = TimeSpan.FromSeconds(3);
            _cursorTimer.Tick += (s, e) =>
            {
                // Hide cursor when timer ticks
                if (_isCursorVisible)
                {
                    HideCursor();
                }
            };
        }

        private void VideoView_MouseMove(object sender, MouseEventArgs e)
        {
            Point currentPosition = e.GetPosition(VideoView);

            // Only process substantial mouse movements to prevent tiny movements from constantly resetting
            if (Math.Abs(currentPosition.X - _lastCursorPosition.X) > 5 ||
                Math.Abs(currentPosition.Y - _lastCursorPosition.Y) > 5)
            {
                _lastCursorPosition = currentPosition;

                // Show cursor
                ShowCursor();

                // Reset the timer
                _cursorTimer.Stop();
                _cursorTimer.Start();
            }
        }

        private void ShowCursor()
        {
            if (!_isCursorVisible)
            {
                _isCursorVisible = true;
                Mouse.OverrideCursor = null;

                // Show video controls if they should be visible
                controlPanel.ShowWithAnimation();
            }
        }

        private void HideCursor()
        {
            if (_isCursorVisible && _isFullScreen)
            {
                _isCursorVisible = false;
                Mouse.OverrideCursor = Cursors.None;

                // Hide video controls
                controlPanel.HideWithAnimation();
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _isFullScreen)
            {
                ToggleFullScreen();
            }
            else if (e.Key == Key.F11)
            {
                ToggleFullScreen();
            }
            else
            {
                // Any key press should show the cursor and controls
                ShowCursor();
                _cursorTimer.Stop();
                _cursorTimer.Start();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Save original layout parameters
            SaveOriginalLayoutParameters();

            // Set initial size for the VideoAspectGrid for correct aspect ratio
            VideoAspectGrid.Width = 1600;
            VideoAspectGrid.Height = 900;

            // Create an aspect ratio enforcer for the video view container
            // This will dynamically adjust the size while maintaining the 16:9 ratio
            _videoAspectRatioEnforcer = new AspectRatioEnforcer(VideoAspectGrid, 16.0 / 9.0);

            // Respond to size changes in the container
            VideoContainer.SizeChanged += VideoContainer_SizeChanged;
        }

        private void VideoContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only update when size changes significantly to avoid excessive layout calculations
            if (Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 5 ||
                Math.Abs(e.PreviousSize.Height - e.NewSize.Height) > 5)
            {
                // Will be handled by the AspectRatioEnforcer
                VideoAspectGrid.InvalidateMeasure();
            }
        }

        private void SaveOriginalLayoutParameters()
        {
            // Save original grid positions
            _originalEpgRow = Grid.GetRow(EPGContainer);
            _originalEpgRowSpan = Grid.GetRowSpan(EPGContainer);
            _originalVideoRow = Grid.GetRow(VideoContainer);
            _originalVideoRowSpan = Grid.GetRowSpan(VideoContainer);
            _originalVideoColumn = Grid.GetColumn(VideoContainer);
            _originalVideoColumnSpan = Grid.GetColumnSpan(VideoContainer);
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            // When maximizing or restoring, handle the transition smoothly
            if (this.WindowState == WindowState.Maximized || this.WindowState == WindowState.Normal)
            {
                _isResizing = true;

                // Temporarily hide control panel during transition
                if (controlPanel != null)
                {
                    // Store visibility state
                    var controlVisibility = controlPanel.Visibility;

                    // Hide during transition
                    controlPanel.Visibility = Visibility.Collapsed;

                    // Restore after animation completes
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    timer.Tick += (s, args) =>
                    {
                        controlPanel.Visibility = controlVisibility;
                        _isResizing = false;
                        timer.Stop();

                        // Ensure the aspect ratio is maintained
                        if (_videoAspectRatioEnforcer != null)
                        {
                            VideoAspectGrid.InvalidateMeasure();
                        }
                    };
                    timer.Start();
                }
            }
        }

        private void SetupControlPanel()
        {
            // Play/Pause
            controlPanel.PlayPauseClicked += (s, e) => {
                if (_mediaPlayer.IsPlaying)
                    _mediaPlayer.Pause();
                else
                    _mediaPlayer.Play();
            };

            // Volume
            controlPanel.VolumeChanged += (s, volume) => {
                _mediaPlayer.Volume = (int)volume;
            };

            controlPanel.MuteToggleClicked += (s, e) => {
                _mediaPlayer.Mute = !_mediaPlayer.Mute;
            };

            // Fullscreen
            controlPanel.FullScreenClicked += (s, e) => {
                ToggleFullScreen();
            };

            // Playback speed
            controlPanel.PlaybackSpeedChanged += (s, speed) => {
                _mediaPlayer.SetRate((float)speed);
            };

            // Initialize control panel state
            controlPanel.IsPlaying = false;
            controlPanel.IsMuted = false;
            controlPanel.VolumeLevel = 75; // Default volume

            // Enable auto-hide for controls
            controlPanel.AutoHide = true;
        }

        private void ToggleFullScreen()
        {
            _isFullScreen = !_isFullScreen;

            if (_isFullScreen)
            {
                // Enter full screen mode
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;

                // Hide the EPG panel
                EPGContainer.Visibility = Visibility.Collapsed;

                // Make video container take up the entire window
                Grid.SetRow(VideoContainer, 0);
                Grid.SetRowSpan(VideoContainer, 3);
                Grid.SetColumn(VideoContainer, 0);
                Grid.SetColumnSpan(VideoContainer, 2);

                // Remove border from video container in fullscreen mode
                VideoContainer.BorderThickness = new Thickness(0);

                // Start cursor hide timer
                _cursorTimer.Start();
            }
            else
            {
                // Exit full screen mode
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;

                // Restore EPG panel
                EPGContainer.Visibility = Visibility.Visible;

                // Restore video view to its original position
                Grid.SetRow(VideoContainer, _originalVideoRow);
                Grid.SetRowSpan(VideoContainer, _originalVideoRowSpan);
                Grid.SetColumn(VideoContainer, _originalVideoColumn);
                Grid.SetColumnSpan(VideoContainer, _originalVideoColumnSpan);

                // Restore the border
                VideoContainer.BorderThickness = new Thickness(.6);

                // Stop cursor hide timer and ensure cursor is visible
                _cursorTimer.Stop();
                ShowCursor();
            }

            // Update layout to reflect changes
            MainGrid.UpdateLayout();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // Only update if we have media
            if (_mediaPlayer.Media != null)
            {
                // Update position
                if (_selectedProgram != null)
                    controlPanel.UpdateTime(_selectedProgram.StartTime, _selectedProgram.StopTime);

                // Update play state
                controlPanel.IsPlaying = _mediaPlayer.IsPlaying;

                // Update volume state
                controlPanel.IsMuted = _mediaPlayer.Mute;
                controlPanel.VolumeLevel = _mediaPlayer.Volume;
            }
        }

        private void LoadMedia(string mediaPath)
        {
            // Create a new Media instance
            using (var media = new Media(_libVLC, new Uri(mediaPath)))
            {
                // Assign the media to the player
                _mediaPlayer.Media = media;
                _mediaPlayer.NetworkCaching = 30 * 1000; // Set network caching to 30 seconds
                // Auto-play if desired
                _mediaPlayer.Play();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Reset cursor to make sure it's visible when closing
            Mouse.OverrideCursor = null;

            // Stop timers
            _cursorTimer?.Stop();
            _updateTimer?.Stop();

            // Clean up resources to prevent memory leaks
            _mediaPlayer.Stop();
            _mediaPlayer.Dispose();
            _libVLC.Dispose();
        }

        private async void InitializeAsync()
        {
            try
            {
                // Generate sample data
                var parser = new EPGParserCore();

                // Get channels
                var channels = await parser.Parse();

                // Set the data for our EPG control
                EPGControl.SetChannels(channels.ToList());
                EPGControl.ScrollToCurrentTime();

                // Log information to debug
                Console.WriteLine($"Created {channels.Count} channels");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading EPG data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Fallback to sample data if parsing fails
                EPGControl.SetChannels(GenerateSampleChannels());
            }
        }

        private void EPGControl_ProgramSelected(object sender, ProgramInfo program)
        {
            var channel = EPGControl.Channels.FirstOrDefault(c => c.TvgName == program.Channel);
            if (channel != null)
            {
                // Then find the program within that channel
                var programItem = channel.Programs.FirstOrDefault(p => p.StartTime == program.StartTime);
                if (programItem != null)
                {
                    // Now set the IsSelected property
                    programItem.IsSelected = true;
                    _selectedProgram = programItem;
                    controlPanel.UpdateTime(_selectedProgram.StartTime, _selectedProgram.StopTime);
                    LoadMedia(channel.TvgStreamLink.ToString());
                }
            }
        }

        private List<ChannelInfo> GenerateSampleChannels()
        {
            var channels = new List<ChannelInfo>();
            var random = new Random(42); // Fixed seed for reproducible results

            // Generate channels
            for (int channelIndex = 0; channelIndex < 40; channelIndex++)
            {
                var channelInfo = new ChannelInfo
                {
                    TvgName = $"Channel {channelIndex + 1}",
                    TvgLogo = null, // No logo for sample data
                    TvgRec = random.Next(1, 8), // Random days of recording available
                    TvgStreamLink = new Uri($"http://example.com/stream/{channelIndex}")
                };

                // Generate programs for this channel
                DateTime currentTime = DateTime.Today;

                // Add programs until we fill a 24-hour period
                while (currentTime < DateTime.Today.AddDays(1))
                {
                    // Random duration between 15 and 120 minutes, in 15-minute increments
                    int durationMinutes = random.Next(1, 8) * 15;
                    var stopTime = currentTime.AddMinutes(durationMinutes);

                    // Create program
                    var program = new ProgramInfo
                    {
                        Channel = channelInfo.TvgName,
                        Title = $"Program {currentTime.Hour:00}:{currentTime.Minute:00}",
                        StartTime = currentTime,
                        StopTime = stopTime,
                        Description = $"This is a sample program on {channelInfo.TvgName} starting at {currentTime:HH:mm} and ending at {stopTime:HH:mm}."
                    };

                    channelInfo.Programs.Add(program);

                    // Move to next program
                    currentTime = stopTime;
                }

                channels.Add(channelInfo);
            }

            return channels;
        }
    }
}