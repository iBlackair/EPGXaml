using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EPGVirtualization.Classes
{
    using System;
    using System.Windows;
    using System.Windows.Controls;

    namespace EPGVirtualization
    {
        /// <summary>
        /// Enforces a specific aspect ratio on a FrameworkElement
        /// </summary>
        public class AspectRatioEnforcer
        {
            private readonly FrameworkElement _target;
            private readonly double _aspectRatio;
            private bool _updatingSize = false;

            /// <summary>
            /// Creates a new aspect ratio enforcer
            /// </summary>
            /// <param name="target">The element to enforce the ratio on</param>
            /// <param name="aspectRatio">Width divided by height</param>
            public AspectRatioEnforcer(FrameworkElement target, double aspectRatio)
            {
                _target = target ?? throw new ArgumentNullException(nameof(target));
                _aspectRatio = aspectRatio;

                // Subscribe to size changes
                _target.SizeChanged += OnTargetSizeChanged;
            }

            /// <summary>
            /// Handles target element size changes to enforce the aspect ratio
            /// </summary>
            private void OnTargetSizeChanged(object sender, SizeChangedEventArgs e)
            {
                if (_updatingSize || _target.Parent == null)
                    return;

                _updatingSize = true;

                try
                {
                    // Get the containing panel
                    FrameworkElement container = _target.Parent as FrameworkElement;
                    double containerWidth = container.ActualWidth;
                    double containerHeight = container.ActualHeight;

                    // Calculate size based on container and aspect ratio
                    double targetWidth, targetHeight;

                    // If the container's aspect ratio is wider than our target ratio
                    if (containerWidth / containerHeight > _aspectRatio)
                    {
                        // Height is the constraint, calculate width based on it
                        targetHeight = containerHeight;
                        targetWidth = targetHeight * _aspectRatio;
                    }
                    else
                    {
                        // Width is the constraint, calculate height based on it
                        targetWidth = containerWidth;
                        targetHeight = targetWidth / _aspectRatio;
                    }

                    // Apply calculated size
                    _target.Width = targetWidth;
                    _target.Height = targetHeight;
                }
                finally
                {
                    _updatingSize = false;
                }
            }

            /// <summary>
            /// Remove the event handler
            /// </summary>
            public void Detach()
            {
                _target.SizeChanged -= OnTargetSizeChanged;
            }
        }
    }
}
