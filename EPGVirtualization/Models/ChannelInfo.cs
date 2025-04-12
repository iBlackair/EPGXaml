using System.Windows.Media;

namespace EPGVirtualization.Models
{
    public class ChannelInfo
    {
        /// <summary>
        /// Name of the channel
        /// </summary>
        public required string TvgName { get; set; }

        /// <summary>
        /// Channel logo as an image
        /// </summary>
        public ImageSource? TvgLogo { get; set; }

        /// <summary>
        /// Number of days for which recordings are available
        /// </summary>
        public int TvgRec { get; set; }

        /// <summary>
        /// URL to the channel's stream
        /// </summary>
        public required Uri TvgStreamLink { get; set; }

        /// <summary>
        /// List of programs for this channel
        /// </summary>
        public List<ProgramInfo> Programs { get; set; } = new List<ProgramInfo>();
    }
}