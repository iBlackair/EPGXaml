using EPGVirtualization.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Xml;

namespace EPGVirtualization.Classes
{
    public class EPGParserCore : IDisposable
    {
        private readonly string _dataPath;
        private readonly string _iconPath;
        private readonly string _logPath;
        private readonly HttpClient _httpClient;
        Logger _logger;
        public class Logger : IDisposable
        {
            private readonly string _logPath;
            private readonly string _logFile;
            private static readonly object _lockObj = new object();
            private FileStream _fileStream;
            private StreamWriter _streamWriter;
            private bool _disposed = false;
            private readonly int _maxRetries = 3;
            private readonly int _retryDelayMs = 100;

            public Logger(string logPath)
            {
                _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
                Directory.CreateDirectory(_logPath); // Ensure directory exists

                _logFile = Path.Combine(_logPath, "app.log");

                try
                {
                    // Open the file with FileShare.ReadWrite to allow other processes to read it
                    _fileStream = new FileStream(_logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    _streamWriter = new StreamWriter(_fileStream) { AutoFlush = true };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not open log file: {ex.Message}");
                    // Continue without logging to file - will use console only
                }
            }

            public void LogInfo(string message)
            {
                WriteToLog("INFO", message);
            }

            public void LogWarning(string message)
            {
                WriteToLog("WARNING", message);
            }

            public void LogError(string message, Exception ex = null)
            {
                WriteToLog("ERROR", $"{message}{(ex != null ? $": {ex.Message}" : "")}");
                if (ex != null)
                {
                    WriteToLog("EXCEPTION", ex.ToString());
                }
            }

            private void WriteToLog(string level, string message)
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}";

                // Always write to console
                Console.WriteLine(logMessage);

                // Try to write to file if available
                if (_streamWriter != null && !_disposed)
                {
                    for (int attempt = 0; attempt < _maxRetries; attempt++)
                    {
                        try
                        {
                            lock (_lockObj)
                            {
                                _streamWriter.WriteLine(logMessage);
                            }
                            return; // Success
                        }
                        catch (IOException)
                        {
                            if (attempt == _maxRetries - 1)
                            {
                                Console.WriteLine("Warning: Could not write to log file after multiple attempts");
                            }
                            else
                            {
                                // Wait before retrying
                                Thread.Sleep(_retryDelayMs);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // Stream was closed unexpectedly
                            ReopenLogFile();

                            if (_streamWriter != null)
                            {
                                try
                                {
                                    lock (_lockObj)
                                    {
                                        _streamWriter.WriteLine(logMessage);
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine("Warning: Could not write to log file after reopening");
                                }
                            }
                            return;
                        }
                    }
                }
            }

            private void ReopenLogFile()
            {
                try
                {
                    _streamWriter?.Dispose();
                    _fileStream?.Dispose();

                    _fileStream = new FileStream(_logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    _streamWriter = new StreamWriter(_fileStream) { AutoFlush = true };
                }
                catch
                {
                    // If reopening fails, set to null so we don't try to use them
                    _streamWriter = null;
                    _fileStream = null;
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        try
                        {
                            _streamWriter?.Dispose();
                            _fileStream?.Dispose();
                        }
                        catch
                        {
                            // Ignore disposal errors
                        }
                    }

                    _disposed = true;
                }
            }

            ~Logger()
            {
                Dispose(false);
            }
        }
        private readonly Dictionary<string, List<ProgramInfo>> _programCache = new Dictionary<string, List<ProgramInfo>>();
        private readonly ConcurrentDictionary<string, ImageSource> _logoCache = new ConcurrentDictionary<string, ImageSource>();
        private bool _epgLoaded = false;

        /// <summary>
        /// Creates a new instance of the EPG parser
        /// </summary>
        /// <param name="dataPath">Optional custom data path. If null, uses default location</param>
        /// <param name="iconPath">Optional custom Icon path. If null, uses default location</param>
        /// <param name="logPath">Optional custom log path. If null, uses default location</param>
        public EPGParserCore(string dataPath = null, string iconPath = null, string logPath = null)
        {
            _dataPath = dataPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Data");
            _iconPath = iconPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Data", "Icons");
            _logPath = logPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Logs");


            // Create required directories
            try
            {
                Directory.CreateDirectory(_dataPath);
                Directory.CreateDirectory(_iconPath);
                Directory.CreateDirectory(_logPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Warning: Access denied when creating directories: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error creating directories: {ex.Message}");
            }

            // Initialize logger (must be done after creating directories)
            _logger = new Logger(_logPath);

            // Configure HttpClient with connection pooling
            _httpClient = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                MaxConnectionsPerServer = 20
            })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Test directory permissions
            TestAndReportDirectoryAccess();
        }

        private void TestAndReportDirectoryAccess()
        {
            bool dataWritable = IsDirectoryWritable(_dataPath);
            bool iconWritable = IsDirectoryWritable(_iconPath);
            bool logWritable = IsDirectoryWritable(_logPath);

            if (!dataWritable)
                _logger.LogWarning($"Data directory is not writable: {_dataPath}");

            if (!iconWritable)
                _logger.LogWarning($"Icon directory is not writable: {_iconPath}");

            if (!logWritable)
                _logger.LogWarning($"Log directory is not writable: {_logPath}");
        }

        private bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                // Try to create a temporary file as a test
                string testFile = Path.Combine(dirPath, $"write_test_{Guid.NewGuid()}.tmp");
                using (FileStream fs = File.Create(testFile, 1, FileOptions.DeleteOnClose))
                {
                    // Just creating the file is enough to test write access
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parse an M3U8 playlist file and return channel information
        /// </summary>
        /// <param name="m3u8FilePath">Path to the M3U8 file</param>
        /// <returns>Collection of channel information</returns>
        public async Task<ICollection<ChannelInfo>> Parse(string m3u8FilePath = null)
        {
            if(m3u8FilePath == null) {
                m3u8FilePath = Path.Combine(_dataPath, "playlist.m3u8");
            }

            if (!File.Exists(m3u8FilePath)) {
                throw new FileNotFoundException($"M3U8 file not found at {m3u8FilePath}");
            }

            _logger.LogInfo($"Reading M3U8 file: {m3u8FilePath}");
            return await GetM3u8ChannelsAsync(m3u8FilePath);
        }

        /// <summary>
        /// Parse an M3U8 playlist string and return channel information
        /// </summary>
        /// <param name="m3u8Content">M3U8 content as string</param>
        /// <returns>Collection of channel information</returns>
        public async Task<ICollection<ChannelInfo>> GetChannelsFromM3u8ContentAsync(string m3u8Content)
        {
            if (string.IsNullOrWhiteSpace(m3u8Content))
            {
                throw new ArgumentException("M3U8 content cannot be empty", nameof(m3u8Content));
            }

            _logger.LogInfo("Parsing M3U8 content from string");
            return await ParseM3u8ContentAsync(m3u8Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private async Task<HashSet<ChannelInfo>> GetM3u8ChannelsAsync(string m3u8Path)
        {
            // Read all lines at once rather than line by line
            var lines = await File.ReadAllLinesAsync(m3u8Path);
            return await ParseM3u8ContentAsync(lines);
        }

        private async Task<HashSet<ChannelInfo>> ParseM3u8ContentAsync(string[] lines)
        {
            // Preload EPG data if not already loaded
            if (!_epgLoaded)
            {
                await PreloadEpgDataAsync();
            }

            var channels = new HashSet<ChannelInfo>(lines.Length / 3); // Estimate capacity
            var logoDownloadTasks = new List<Task>();

            for (int i = 0; i < lines.Length - 1; i++)
            {
                if (!lines[i].StartsWith("#EXTINF"))
                {
                    continue;
                }

                var extInf = lines[i];
                var channelUrl = i + 1 < lines.Length ? lines[i + 1] : null;

                if (string.IsNullOrWhiteSpace(channelUrl) || channelUrl.StartsWith("#"))
                {
                    channelUrl = i + 2 < lines.Length ? lines[i + 2] : null;
                    if (string.IsNullOrWhiteSpace(channelUrl) || channelUrl.StartsWith("#"))
                    {
                        _logger.LogWarning($"Missing URL for channel at line {i + 2}");
                        continue;
                    }
                }

                try
                {
                    var channelName = ExtractChannelName(extInf);
                    if (string.IsNullOrWhiteSpace(channelName))
                    {
                        _logger.LogWarning($"Could not extract channel name from line: {extInf}");
                        continue;
                    }

                    var logoUri = GetTvgLogoUri(extInf);

                    // Create channel without waiting for logo download
                    var channel = new ChannelInfo
                    {
                        TvgName = channelName,
                        TvgLogo = null, // Will be set later
                        TvgRec = GetTvgRecordedDays(extInf),
                        TvgStreamLink = new Uri(channelUrl, UriKind.Absolute),
                        Programs = GetCachedPrograms(channelName)
                    };

                    channels.Add(channel);

                    // Process logo separately to avoid blocking
                    if (logoUri != null)
                    {
                        var logoTask = DownloadAndCacheLogoAsync(channel, logoUri);
                        logoDownloadTasks.Add(logoTask);
                    }

                    _logger.LogInfo($"Processed channel: {channelName}");
                }
                catch (UriFormatException uriEx)
                {
                    _logger.LogError($"Invalid URI for channel at line {i + 2}", uriEx);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing channel at line {i + 2}", ex);
                }
            }

            // Wait for all logos to complete downloading with a timeout
            if (logoDownloadTasks.Count > 0)
            {
                await Task.WhenAll(logoDownloadTasks.ToArray());
            }

            return channels;
        }

        private async Task PreloadEpgDataAsync()
        {
            var epgPath = Path.Combine(_dataPath, "filtered_epg.xml");
            if (!File.Exists(epgPath))
            {
                _logger.LogWarning($"EPG file not found at {epgPath}");
                _epgLoaded = true;
                return;
            }

            await Task.Run(() =>
            {
                using var reader = XmlReader.Create(epgPath, new XmlReaderSettings
                {
                    IgnoreWhitespace = true,
                    Async = true,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true
                });

                string currentChannel = null;
                List<ProgramInfo> currentPrograms = null;

                while (reader.Read())
                {
                    // Look for programme elements
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "programme")
                    {
                        string channel = reader.GetAttribute("channel");

                        // Get or create program list for this channel
                        if (currentChannel != channel)
                        {
                            currentChannel = channel;
                            if (!_programCache.TryGetValue(channel, out currentPrograms))
                            {
                                currentPrograms = new List<ProgramInfo>();
                                _programCache[channel] = currentPrograms;
                            }
                        }

                        // Parse datetime values from attributes
                        DateTime start = ParseEpgDateTime(reader.GetAttribute("start"));
                        DateTime stop = ParseEpgDateTime(reader.GetAttribute("stop"));

                        var programInfo = new ProgramInfo
                        {
                            Channel = channel,
                            StartTime = start,
                            StopTime = stop
                        };

                        // Use a reader subtree to process this programme's child elements
                        using (var subtree = reader.ReadSubtree())
                        {
                            // Move to content to start reading child elements
                            subtree.Read();

                            // Process all child elements of this programme
                            while (subtree.Read())
                            {
                                if (subtree.NodeType == XmlNodeType.Element)
                                {
                                    if (subtree.Name == "title")
                                    {
                                        programInfo.Title = subtree.ReadElementContentAsString();

                                    }
                                    if (subtree.Name == "desc")
                                    {
                                        if (!subtree.IsEmptyElement)
                                            programInfo.Description = subtree.ReadElementContentAsString();
                                        else
                                            programInfo.Description = "No Description";


                                    }
                                }
                            }
                        }

                        // Add the completed programme info to our list
                        currentPrograms.Add(programInfo);
                    }
                }
            });

            _epgLoaded = true;
        }

        private List<ProgramInfo> GetCachedPrograms(string channelName)
        {
            return _programCache.TryGetValue(channelName, out var programs) ? programs : new List<ProgramInfo>();
        }

        // Helper function to parse EPG datetime format (YYYYMMDDHHMMSS +HHMM)
        private DateTime ParseEpgDateTime(string dateTimeStr)
        {
            if (string.IsNullOrEmpty(dateTimeStr) || dateTimeStr.Length < 14)
                return DateTime.MinValue;

            try
            {
                // Extract the date/time portion and the timezone offset
                string datePart = dateTimeStr.Substring(0, 14);
                string offsetPart = dateTimeStr.Length > 14 ? dateTimeStr.Substring(14).Trim() : "+0000";

                // Parse the main date/time part
                int year = int.Parse(datePart.Substring(0, 4));
                int month = int.Parse(datePart.Substring(4, 2));
                int day = int.Parse(datePart.Substring(6, 2));
                int hour = int.Parse(datePart.Substring(8, 2));
                int minute = int.Parse(datePart.Substring(10, 2));
                int second = int.Parse(datePart.Substring(12, 2));

                // Create the datetime in UTC
                var dt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

                // Apply the timezone offset
                if (offsetPart.Length >= 5)
                {
                    int offsetSign = offsetPart[0] == '-' ? -1 : 1;
                    int offsetHours = int.Parse(offsetPart.Substring(1, 2));
                    int offsetMinutes = int.Parse(offsetPart.Substring(3, 2));

                    dt = dt.AddHours(-offsetSign * offsetHours);
                    dt = dt.AddMinutes(-offsetSign * offsetMinutes);
                }

                return dt;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private async Task DownloadAndCacheLogoAsync(ChannelInfo channel, Uri logoUri)
        {
            string channelName = channel.TvgName;
            if (string.IsNullOrEmpty(channelName) || logoUri == null)
            {
                return;
            }

            try
            {
                string safeChannelName = GetSafeFileName(channelName);

                // Check memory cache first
                if (_logoCache.TryGetValue(safeChannelName, out var cachedLogo))
                {
                    if (cachedLogo != null)
                    {
                        channel.TvgLogo = cachedLogo;
                        return;
                    }
                }

                string logoPath = Path.Combine(_iconPath, $"{safeChannelName}.png");

                // Then check disk cache
                if (File.Exists(logoPath))
                {
                    try
                    {
                        var logo = await Task.Run(() => LoadImageFromFile(logoPath));
                        if (logo != null)
                        {
                            _logoCache.TryAdd(safeChannelName, logo);
                            channel.TvgLogo = logo;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error loading cached logo for {channelName}: {ex.Message}");
                        // Continue to download a new copy
                    }
                }

                try
                {
                    _logger.LogInfo($"Downloading logo for {channelName} from {logoUri}");
                    var imageBytes = await _httpClient.GetByteArrayAsync(logoUri);

                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        // Write to disk asynchronously without blocking
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await File.WriteAllBytesAsync(logoPath, imageBytes);
                                _logger.LogInfo($"Saved logo for {channelName} ({imageBytes.Length} bytes)");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Failed to save logo for {channelName}", ex);
                            }
                        });

                        // Create image from bytes
                        var image = await Task.Run(() => CreateBitmapFromBytes(imageBytes));
                        if (image != null)
                        {
                            _logoCache.TryAdd(safeChannelName, image);
                            channel.TvgLogo = image;
                        }
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogWarning($"HTTP error downloading logo for {channelName}: {httpEx.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to download or process logo for {channelName}", ex);
            }
        }

        private static ImageSource LoadImageFromFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.DecodePixelWidth = 100; // Optimize size for typical UI display
                bitmap.EndInit();
                if (bitmap.CanFreeze)
                    bitmap.Freeze(); // Important for cross-thread usage
                return bitmap;
            }
            catch
            {
                // Return null if image loading fails for any reason
                return null;
            }
        }

        private static ImageSource CreateBitmapFromBytes(byte[] imageBytes)
        {
            try
            {
                if (imageBytes == null || imageBytes.Length == 0)
                    return null;

                var bitmap = new BitmapImage();
                using (var ms = new MemoryStream(imageBytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.DecodePixelWidth = 100; // Optimize size for typical UI display
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();
                    if (bitmap.CanFreeze)
                        bitmap.Freeze(); // Important for cross-thread usage
                }
                return bitmap;
            }
            catch
            {
                // Return null if image processing fails
                return null;
            }
        }

        private static string GetSafeFileName(string input)
        {
            // Avoid using colons as they're invalid in Windows filenames
            return string.Join("", input.Split(Path.GetInvalidFileNameChars()));
        }

        private static Uri GetTvgLogoUri(string extInf)
        {
            return ExtractAttributeUri(extInf, "tvg-logo");
        }

        private static int GetTvgRecordedDays(string extInf)
        {
            string value = ExtractAttributeValue(extInf, "tvg-rec");
            return int.TryParse(value, out int result) ? result : 0;
        }

        private static string ExtractChannelName(string extInf)
        {
            int commaIndex = extInf.IndexOf(',');
            if (commaIndex >= 0 && commaIndex < extInf.Length - 1)
            {
                return extInf.Substring(commaIndex + 1).Trim();
            }
            return null;
        }

        private static Uri ExtractAttributeUri(string input, string attributeName)
        {
            try
            {
                string value = ExtractAttributeValue(input, attributeName);
                if (string.IsNullOrEmpty(value))
                    return null;

                // Only create URI if it's a valid format
                if (Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
                    return uri;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractAttributeValue(string input, string attributeName)
        {
            string pattern = $"{attributeName}=\"";
            int startIndex = input.IndexOf(pattern);
            if (startIndex < 0)
            {
                return null;
            }

            startIndex += pattern.Length;
            int endIndex = input.IndexOf('"', startIndex);
            if (endIndex <= startIndex)
            {
                return null;
            }

            return input.Substring(startIndex, endIndex - startIndex);
        }

        // Clean up resources
        public void Dispose()
        {
            _httpClient?.Dispose();
            _programCache?.Clear();
            _logoCache?.Clear();
            _logger?.Dispose();
        }
    }
}
