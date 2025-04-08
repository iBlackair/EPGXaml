using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using EPGVirtualization;
using EPGVirtualization.Controls;

namespace EPGVirtualization
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private DateTime _currentDate = DateTime.Today;
        private ObservableCollection<ProgramInfo> _programList = new ObservableCollection<ProgramInfo>();
        private ProgramInfo _selectedProgram;

        public event PropertyChangedEventHandler PropertyChanged;

        public DateTime CurrentDate
        {
            get => _currentDate;
            set
            {
                if (_currentDate != value)
                {
                    _currentDate = value;
                    OnPropertyChanged();
                    LoadProgramsForDate(_currentDate);
                }
            }
        }

        public ObservableCollection<ProgramInfo> ProgramList
        {
            get => _programList;
            set
            {
                _programList = value;
                OnPropertyChanged();
            }
        }

        public ProgramInfo SelectedProgram
        {
            get => _selectedProgram;
            set
            {
                if (_selectedProgram != value)
                {
                    _selectedProgram = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedProgramEndTime));
                }
            }
        }

        public DateTime SelectedProgramEndTime
        {
            get => SelectedProgram != null ? SelectedProgram.StartTime.Add(SelectedProgram.Duration) : DateTime.MinValue;
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize EPG with sample data
            LoadSampleData();

            // Navigate to current time
            EPG.Loaded += (s, e) => EPG.NavigateToNow();
        }

        private void LoadSampleData()
        {
            // Generate channel data
            var channels = new List<string>
            {
                "BBC One", "BBC Two", "ITV", "Channel 4", "Channel 5",
                "Sky One", "Sky Atlantic", "History", "Discovery", "National Geographic"
            };

            // Generate random programs for today
            var random = new Random(42); // Fixed seed for consistent results
            var programs = new List<ProgramInfo>();

            // Generate programs for all channels
            for (int channelIndex = 0; channelIndex < channels.Count; channelIndex++)
            {
                var startTime = CurrentDate.AddHours(6); // Start at 6 AM

                while (startTime < CurrentDate.AddHours(30)) // Go to 6 AM next day
                {
                    // Random duration between 30 minutes and 2 hours
                    var durationMinutes = random.Next(1, 5) * 30;
                    var duration = TimeSpan.FromMinutes(durationMinutes);

                    // Create program
                    programs.Add(new ProgramInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        ChannelIndex = channelIndex,
                        Title = GetRandomProgramTitle(random),
                        Description = "This is a sample program description that would typically contain information about the show, its cast, and other relevant details.",
                        StartTime = startTime,
                        Duration = duration,
                        Category = GetRandomCategory(random)
                    });

                    // Move to next time slot
                    startTime = startTime.AddMinutes(durationMinutes);
                }
            }

            ProgramList = new ObservableCollection<ProgramInfo>(programs);
        }

        private string GetRandomProgramTitle(Random random)
        {
            var titles = new[]
            {
                "News at Ten", "Movie: The Adventure", "Documentary Now", "Sports Review",
                "Late Night Show", "Morning Talk", "Nature Explorer", "History Uncovered",
                "Science Today", "The Comedy Hour", "Drama Series", "Reality Challenge",
                "Cooking with Stars", "Travel Destinations", "Music Special", "Tech Update"
            };

            return titles[random.Next(titles.Length)];
        }

        private string GetRandomCategory(Random random)
        {
            var categories = new[]
            {
                "News", "Movies", "Documentary", "Sports",
                "Entertainment", "Talk Show", "Nature", "History",
                "Science", "Comedy", "Drama", "Reality",
                "Cooking", "Travel", "Music", "Technology"
            };

            return categories[random.Next(categories.Length)];
        }

        private void LoadProgramsForDate(DateTime date)
        {
            // In a real app, you would load actual program data for the specified date
            // For this example, we'll just modify the sample data to use the new date

            var newPrograms = ProgramList.ToList();
            var dayDifference = (date - CurrentDate).Days;

            foreach (var program in newPrograms)
            {
                program.StartTime = program.StartTime.AddDays(dayDifference);
            }

            ProgramList = new ObservableCollection<ProgramInfo>(newPrograms);
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentDate = DateTime.Today;
        }

        private void PreviousDayButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentDate = CurrentDate.AddDays(-1);
        }

        private void NextDayButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentDate = CurrentDate.AddDays(1);
        }

        private void NowButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentDate = DateTime.Today;
            EPG.NavigateToNow();
        }

        private void WatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProgram != null)
            {
                MessageBox.Show($"Starting playback: {SelectedProgram.Title}", "Watch", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProgram != null)
            {
                MessageBox.Show($"Recording scheduled: {SelectedProgram.Title}", "Record", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}