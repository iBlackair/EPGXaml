using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EPGVirtualization
{
    public class ProgramInfo : INotifyPropertyChanged
    {
        private string _title = "";
        private DateTime _startTime;
        private TimeSpan _duration;
        private int _channelIndex;
        private bool _isSelected;

        public string Title
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

        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EndTime));
                }
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EndTime));
                }
            }
        }

        public DateTime EndTime => StartTime.Add(Duration);

        public int ChannelIndex
        {
            get => _channelIndex;
            set
            {
                if (_channelIndex != value)
                {
                    _channelIndex = value;
                    OnPropertyChanged();
                }
            }
        }

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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}