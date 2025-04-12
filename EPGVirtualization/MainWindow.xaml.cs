using EPGVirtualization.Classes;
using EPGVirtualization.Classes.EPGVirtualization;
using EPGVirtualization.Models;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
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
        private AspectRatioEnforcer _videoAspectRatioEnforcer;
        private bool _isResizing = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        private ProgramInfo _selectedProgram = new ProgramInfo();
        private ProgramInfo SelectedProgram 
        { 
            get => _selectedProgram;
            set
            {
                _selectedProgram = value;
                //controlPanel.UpdateTime(_selectedProgram.StartTime, _selectedProgram.Duration);
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
            _mediaPlayer.Volume = 0; // Set default volume to 100%
            // Connect the VideoView with the MediaPlayer
            VideoView.MediaPlayer = _mediaPlayer;

            InitializeAsync();
            // Set up the control panel events
            SetupControlPanel();
            this.StateChanged += MainWindow_StateChanged;
            this.Loaded += MainWindow_Loaded;


        // Create a timer to update the UI from LibVLC state
        _updateTimer = new System.Windows.Threading.DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(1);
            _updateTimer.Tick += UpdateTimer_Tick;

            // Start the timer
            _updateTimer.Start();
            // Handle window closing to properly dispose resources
            Closing += MainWindow_Closing;

            // Optional: Load a media file when the application starts
             LoadMedia("http://11.troya.info:34000/ch2452/mono.m3u8?token=mrgold.Kj3afDUEcHu1PHn46lO-gmvsX6SeBaNDIaqlbosizMegtK_457uo3YE-MwcQ-LHT");
            // Initialize asynchronously - can't use await directly in constructor
            
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Create an aspect ratio enforcer for the video view container
            // You might need to adjust which element needs the aspect ratio enforced
            // depending on your exact layout

            // Option 1: If using the ViewBox approach in XAML:
            // The ViewBox handles the aspect ratio automatically

            // Option 2: If not using ViewBox, create an AspectRatioEnforcer:
            // Find the container Grid that holds the VideoView (adjust as needed)
            var videoContainer = VideoView.Parent as FrameworkElement;
            if (videoContainer != null)
            {
                // Create enforcer with 16:9 aspect ratio (width/height = 1.7778)
                _videoAspectRatioEnforcer = new AspectRatioEnforcer(videoContainer, 16.0 / 9.0);
            }
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
                            var videoContainer = VideoView.Parent as FrameworkElement;
                            if (videoContainer != null)
                            {
                                // Manually trigger a size change to update aspect ratio
                                videoContainer.InvalidateMeasure();
                            }
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
                if (WindowStyle == WindowStyle.None)
                {
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Maximized;
                }
            };

            // Playback speed
            controlPanel.PlaybackSpeedChanged += (s, speed) => {
                _mediaPlayer.SetRate((float)speed);
            };

            // Initialize control panel state
            controlPanel.IsPlaying = false;
            controlPanel.IsMuted = false;
            controlPanel.VolumeLevel = 75; // Default volume

            //// Set buffer position (this would come from your streaming info)
            //// For example, if you're 15 minutes into a 2-hour show and have 30 minutes buffered:
            //double showProgressPercent = 12.5; // (15 / 120) * 100
            //double bufferProgressPercent = 25.0; // ((15+30) / 120) * 100
            //controlPanel.BufferPercentage = bufferProgressPercent;
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // Only update if we have media
            if (_mediaPlayer.Media != null)
            {
                // Update position
                //controlPanel.Position = _mediaPlayer.Position * 100;
                if(_selectedProgram != null)
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
                _mediaPlayer.NetworkCaching = 30 * 1000; // Set network caching to 1 second
                // Auto-play if desired
                _mediaPlayer.Play();
            }
        }
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Play();
            }
            else
            {
                _mediaPlayer.Pause();
            }
        }
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Pause();
        }
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Stop();
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
                //EPGRow.Height = new GridLength(0,GridUnitType.Pixel);
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