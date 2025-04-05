using System;
using System.Collections.Generic;
using System.Windows;

namespace EPGVirtualization
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            // Make sure to add the style resources in App.xaml or MainWindow.xaml
            InitializeComponent();

            // Generate sample data
            var programs = GenerateSamplePrograms();

            // Set the data for our EPG control
            EPGControl.SetPrograms(programs);

            // Log information to debug
            Console.WriteLine($"Created {programs.Count} programs");
        }

        private void EPGControl_ProgramSelected(object sender, ProgramInfo program)
        {
            // Handle program selection
            MessageBox.Show($"Selected: {program.Title} at {program.StartTime:HH:mm}");
        }


        private List<ProgramInfo> GenerateSamplePrograms()
        {
            var programs = new List<ProgramInfo>();
            var random = new Random(42); // Fixed seed for reproducible results

            // Generate programs across 20 channels for 24 hours
            for (int channelIndex = 0; channelIndex < 20; channelIndex++)
            {
                DateTime currentTime = DateTime.Today;

                // Add programs until we fill the 24-hour period
                while (currentTime < DateTime.Today.AddDays(1))
                {
                    // Random duration between 15 and 120 minutes, in 15-minute increments
                    int durationMinutes = random.Next(1, 8) * 15;
                    var duration = TimeSpan.FromMinutes(durationMinutes);

                    // Create program
                    var program = new ProgramInfo
                    {
                        Title = $"Program {currentTime.Hour:00}:{currentTime.Minute:00} Ch{channelIndex + 1}",
                        StartTime = currentTime,
                        Duration = duration,
                        ChannelIndex = channelIndex
                    };

                    programs.Add(program);

                    // Move to next program
                    currentTime = currentTime.AddMinutes(durationMinutes);
                }
            }

            return programs;
        }
    }
}