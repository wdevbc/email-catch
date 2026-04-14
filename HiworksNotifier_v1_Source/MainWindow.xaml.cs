using System;
using System.Windows;
using System.Windows.Input;

namespace HiworksNotifier
{
    public partial class MainWindow : Window
    {
        private bool _isDragging = false;
        private Point _clickOffset;

        public MainWindow(MailNotification notification)
        {
            InitializeComponent();
            DataContext = notification;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _clickOffset = e.GetPosition(this); // Store click offset within the window
                
                // Capture mouse to track even outside the window while dragging
                if (sender is UIElement el)
                {
                    el.CaptureMouse();
                }
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && sender is UIElement el) // Ensure sender is valid (Border)
            {
                // Get current mouse position on screen (physical pixels)
                var currentMousePos = System.Windows.Forms.Cursor.Position;

                // We need to convert physical pixels to logical pixels (WPF units)
                // This requires DPI scaling factor. Simplest way from code behind:
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var matrix = source.CompositionTarget.TransformFromDevice;
                    var logicalMousePos = matrix.Transform(new Point(currentMousePos.X, currentMousePos.Y));

                    // Calculate new Window position
                    double newLeft = logicalMousePos.X - _clickOffset.X;
                    double newTop = logicalMousePos.Y - _clickOffset.Y;

                    // Clamp to Virtual Screen (All Monitors)
                    double minLeft = SystemParameters.VirtualScreenLeft;
                    double maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - this.ActualWidth;
                    double minTop = SystemParameters.VirtualScreenTop;
                    double maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - this.ActualHeight;

                    // Apply clamping
                    this.Left = Math.Max(minLeft, Math.Min(newLeft, maxLeft));
                    this.Top = Math.Max(minTop, Math.Min(newTop, maxTop));
                }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                if (sender is UIElement el)
                {
                    el.ReleaseMouseCapture();
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
