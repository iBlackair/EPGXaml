using EPGVirtualization.Classes;
using EPGVirtualization.Models;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using System.IO;
using System.CodeDom;



namespace EPGVirtualization
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

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
            Unosquare.FFME.Library.FFmpegDirectory = Path.Combine(Directory.GetCurrentDirectory(),"Codecs");
            InitializeAsync();
            //InitializeWebView();

            this.StateChanged += MainWindow_StateChanged;
            this.Loaded += MainWindow_Loaded;

            // Handle keyboard events for fullscreen
            this.KeyDown += MainWindow_KeyDown;

            // Handle window closing to properly dispose resources
            Closing += MainWindow_Closing;
        }

        private async void InitializeWebView()
        {
            try
            {
                // Create WebView2 environment with UHD-optimized settings
                var environment = await CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = string.Join(" ", new[]
                    {
                        "--enable-features=VaapiVideoDecoder,VaapiVideoEncoder",
                        "--enable-gpu-rasterization",
                        "--enable-zero-copy",
                        "--enable-hardware-overlays",
                        "--disable-features=UseChromeOSDirectVideoDecoder",
                        "--disable-background-timer-throttling",
                        "--disable-backgrounding-occluded-windows",
                        "--disable-renderer-backgrounding",
                        "--max_old_space_size=8192", // 8GB memory limit
                        "--memory-pressure-off", // Disable memory pressure
                        "--disable-dev-shm-usage",
                        "--enable-features=MSECodecs",
                        "--disable-features=VizDisplayCompositor"// Use /tmp instead of /dev/shm
                    })
                });

                //await webView.EnsureCoreWebView2Async(environment);

                // Configure for high performance video
                //webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                //webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

                // Load the HTML player
                await LoadHtmlPlayer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing WebView2: {ex.Message}");
            }
        }

        private async Task LoadHtmlPlayer()
        {
            string htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>HLS Player</title>
    <style>
        body {
            margin: 0;
            padding: 0;
            background: black;
            font-family: Arial, sans-serif;
        }
        #videoContainer {
            position: relative;
            width: 100%;
            height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        #video {
            width: 100%;
            height: 100%;
            max-width: 100%;
            max-height: 100%;
            object-fit: contain;
        }
        .error {
            color: red;
        }
        .loading {
            color: yellow;
        }
        .ready {
            color: green;
        }
    </style>
</head>
<body>
    <div id='videoContainer'>
        <video id='video' controls autoplay muted>
            Your browser does not support the video tag.
        </video>
    </div>

    <script src='https://cdn.jsdelivr.net/npm/hls.js@latest'></script>
    <script>
        const video = document.getElementById('video');
        const status = document.getElementById('status');
        let hls = null;

        function loadStream(url) {
            if (!url) {
                return;
            }

            // Check if this is a UHD stream
            const isUHD = url.toLowerCase().includes('uhd') || url.toLowerCase().includes('4k');
            if (isUHD) {
                console.warn('UHD stream detected:', url);
            }

            // Check if HLS is supported
            if (Hls.isSupported()) {
                // Destroy existing HLS instance
                if (hls) {
                    hls.destroy();
                }

                hls = new Hls({
                    debug: true,  // Enable debug for troubleshooting
                    enableWorker: true,
                    lowLatencyMode: true,
                    backBufferLength: 90
                });

                hls.loadSource(url);
                hls.attachMedia(video);
                
                hls.on(Hls.Events.MEDIA_ATTACHED, () => {
                  video.muted = false;
                  video.volume = 1; // Set volume right after attaching
                });
                hls.on(Hls.Events.MANIFEST_PARSED, function() {
                    console.log('=== MANIFEST PARSED ===');
                    console.log('Total levels:', hls.levels.length);
                    console.log('Audio tracks:', hls.audioTracks.length);
                    console.log('Subtitle tracks:', hls.subtitleTracks ? hls.subtitleTracks.length : 0);
                    
                    hls.levels.forEach((level, index) => {
                        console.log(`Level ${index}:`, {
                            width: level.width,
                            height: level.height,
                            bitrate: level.bitrate,
                            videoCodec: level.videoCodec,
                            audioCodec: level.audioCodec,
                            fps: level.attrs ? level.attrs['FRAME-RATE'] : 'unknown',
                            url: level.url
                        });
                        
                        // Check for UHD resolution
                        if (level.height >= 2160) {
                            console.warn(`UHD/4K level detected: ${level.width}x${level.height}`);
                        }
                        
                        // Check for unsupported codecs
                        if (level.videoCodec && (level.videoCodec.includes('hev1') || level.videoCodec.includes('hvc1'))) {
                            console.warn('H.265/HEVC codec detected - not supported in most browsers');
                        }
                    });
                    
                    // Check if we have any video levels
                    const hasVideo = hls.levels.some(level => level.videoCodec);
                    const hasAudio = hls.audioTracks.length > 0 || hls.levels.some(level => level.audioCodec);
                    
                    console.log('Stream analysis:', {
                        hasVideo: hasVideo,
                        hasAudio: hasAudio,
                        currentLevel: hls.currentLevel,
                        startLevel: hls.startLevel
                    });
                   
                    
                    video.play().catch(e => {
                        console.error('Play failed:', e);
                    });
                });

                // Add more detailed event logging
                hls.on(Hls.Events.LEVEL_SWITCHED, function(event, data) {
                    console.log('Level switched to:', data.level, hls.levels[data.level]);

                });

                hls.on(Hls.Events.FRAG_LOADED, function(event, data) {
                    console.log('Fragment loaded:', data.frag.type, data.frag.level);
                });

                hls.on(Hls.Events.BUFFER_APPENDED, function(event, data) {
                    console.log('Buffer appended:', data.type, 'TimeRanges:', data.timeRanges);
                });

                hls.on(Hls.Events.ERROR, function(event, data) {
                    console.error('HLS Error Details:', {
                        type: data.type,
                        details: data.details,
                        fatal: data.fatal,
                        reason: data.reason,
                        level: data.level,
                        frag: data.frag,
                        response: data.response
                    });
                    
                    let errorMsg = 'Error: ' + data.type + ' - ' + data.details;
                    
                    if (data.fatal) {
                        errorMsg += ' (FATAL)';
                        
                        // UHD-specific recovery strategies
                        switch(data.type) {
                            case Hls.ErrorTypes.NETWORK_ERROR:
                                console.log('Network error - attempting recovery...');
                                setTimeout(() => hls.startLoad(), 3000); // Wait longer for UHD
                                break;
                            case Hls.ErrorTypes.MEDIA_ERROR:
                                console.log('Media error - attempting recovery...');
                                hls.recoverMediaError();
                                break;
                            default:
                                // For UHD, try one more time with different settings
                                if (isUHD && !window.uhdRetryAttempted) {
                                    console.log('UHD stream failed, trying with reduced settings...');
                                    window.uhdRetryAttempted = true;
                                    
                                    // Destroy and recreate with even more conservative settings
                                    hls.destroy();
                                    setTimeout(() => {
                                        loadStreamWithReducedSettings(url);
                                    }, 2000);
                                } else {
                                    console.log('Cannot recover from error');
                                }
                                break;
                        }
                    }
                    
                });

            } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
                // Native HLS support (Safari, etc.)
                video.src = url;
            } else {
            }
        }

        // Expose functions to C#
        window.chrome.webview.hostObjects.sync.player = {
            loadStream: loadStream
        };

        // Video event listeners with detailed logging
        video.addEventListener('loadstart', () => {
            console.log('Video loadstart');
            updateStatus('Loading...', 'loading');
        });
        video.addEventListener('loadedmetadata', () => {
            console.log('=== VIDEO METADATA ===');
            console.log('Duration:', video.duration);
            console.log('Video dimensions:', video.videoWidth + 'x' + video.videoHeight);
            console.log('Ready state:', video.readyState);
            console.log('Has video tracks:', video.videoWidth > 0);
            console.log('Has audio tracks:', video.duration > 0);
            
        });
        video.addEventListener('canplay', () => {
            console.log('=== CAN PLAY EVENT ===');
            console.log('Ready state:', video.readyState);
            console.log('Video dimensions:', video.videoWidth + 'x' + video.videoHeight);
            console.log('Current time:', video.currentTime);
            console.log('Buffered ranges:', video.buffered.length);
            
            // Force a frame update check
            setTimeout(() => {
                console.log('POST-CANPLAY CHECK:');
                console.log('Video rendering:', video.videoWidth > 0 ? 'YES' : 'NO');
                console.log('Current time:', video.currentTime);
                console.log('Paused:', video.paused);
            }, 1000);
            
        });
        video.addEventListener('playing', () => {
            console.log('Video playing');

        });
        video.addEventListener('pause', () => {
            console.log('Video paused');
        });
        video.addEventListener('ended', () => {
            console.log('Video ended');
        });
        video.addEventListener('error', (e) => {
            console.error('Video error:', e.target.error);
            if (e.target.error) {
                console.error('Error code:', e.target.error.code, 'Message:', e.target.error.message);
            }
        });
        video.addEventListener('waiting', () => {
            console.log('Video waiting/buffering');
        });
        video.addEventListener('stalled', () => {
            console.log('Video stalled');
        });

        // Check video rendering with more details
        setInterval(() => {
            if (video.currentTime > 0 && !video.paused) {
                const hasVideo = video.videoWidth > 0 && video.videoHeight > 0;
                const hasAudio = video.duration > 0;
                const bufferedEnd = video.buffered.length > 0 ? video.buffered.end(0) : 0;
                
                console.log('=== PLAYBACK STATUS ===');
                console.log('Time:', video.currentTime.toFixed(2) + 's');
                console.log('Video dimensions:', video.videoWidth + 'x' + video.videoHeight);
                console.log('Ready state:', video.readyState);
                console.log('Network state:', video.networkState);
                console.log('Buffered:', bufferedEnd.toFixed(2) + 's');
                console.log('Paused:', video.paused);
                console.log('Ended:', video.ended);
                console.log('Current src:', video.currentSrc.substring(0, 100) + '...');
            }
        }, 5000);

    </script>
</body>
</html>";

            //webView.NavigateToString(htmlContent);
        }
        private bool _isPlayerReady = false;
        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _isPlayerReady = true;

                // Add host object for JavaScript communication
               // webView.CoreWebView2.AddHostObjectToScript("player", new PlayerController());
            }
        }

        private void ShowCursor()
        {
            if (!_isCursorVisible)
            {
                _isCursorVisible = true;
                Mouse.OverrideCursor = null;

                // Show video controls if they should be visible
                //controlPanel.ShowWithAnimation();
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
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

        }
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
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

                ShowCursor();
            }

            // Update layout to reflect changes
            MainGrid.UpdateLayout();
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading EPG data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Fallback to sample data if parsing fails
                EPGControl.SetChannels(GenerateSampleChannels());
            }
        }
        private async void EPGControl_ProgramSelected(object sender, ProgramInfo program)
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

                    try
                    {
                        // URL to your .ts stream;
                        await media.Open(channel.TvgStreamLink);
                        await media.Play();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error: {ex.Message}");
                    }
                   
                    
                   //await webView.CoreWebView2.ExecuteScriptAsync($"loadStream('{channel.TvgStreamLink}')");
                }
            }
        }
        private void Media_MediaFailed(object sender, Unosquare.FFME.Common.MediaFailedEventArgs e)
        {
            MessageBox.Show($"Media Failed:\n{e.ErrorException.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Media_MessageLogged(object sender, Unosquare.FFME.Common.MediaLogMessageEventArgs e)
        {
            Console.WriteLine($"[{e.MessageType}] {e.Message}");
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
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

            //EPGRow.Height = new GridLength(0, GridUnitType.Star);
        }
        private void webView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _isPlayerReady = true;

                // Add host object for JavaScript communication
                //webView.CoreWebView2.AddHostObjectToScript("player", new PlayerController());
            }
        }
    }
    [System.Runtime.InteropServices.ComVisible(true)]
    public class PlayerController
    {
        public void OnVideoEvent(string eventType, string data)
        {
            // Handle video events from JavaScript
            System.Diagnostics.Debug.WriteLine($"Video event: {eventType} - {data}");
        }
    }
}