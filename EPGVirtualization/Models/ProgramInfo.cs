using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EPGVirtualization
{

    public class ProgramInfo : INotifyPropertyChanged
    {
        private string _channel = string.Empty;
        private DateTime _startTime;
        private DateTime _stopTime;
        private string? _title;
        private string? _description;
        private bool _isSelected;

        /// <summary>
        /// Unique identifier for the program
        /// </summary>
        public string Channel
        {
            get => _channel;
            set
            {
                if (_channel != value)
                {
                    _channel = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Start time of the program
        /// </summary>
        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Duration));
                }
            }
        }

        /// <summary>
        /// Stop time of the program
        /// </summary>
        public DateTime StopTime
        {
            get => _stopTime;
            set
            {
                if (_stopTime != value)
                {
                    _stopTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Duration));
                }
            }
        }

        /// <summary>
        /// Program title
        /// </summary>
        public string? Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Program description
        /// </summary>
        public string? Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Flag indicating if this program is currently selected
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Calculated duration of the program
        /// </summary>
        public TimeSpan Duration => StopTime - StartTime;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
