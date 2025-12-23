using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using Windows.Foundation;

namespace Waterflow.WinUI
{
    /// <summary>
    /// Manages dot animations and visual effects
    /// </summary>
    public class DotAnimationManager
    {
        private const double ELASTIC_FACTOR = 0.3; // elastic effect factor
        private const double MAX_OFFSET_RATIO = 0.5; // max offset as ratio of radius
        private const double HIGHLIGHT_MARGIN_RATIO = 0.25; // margin for highlight point as ratio of radius
        private const double HIGHLIGHT_SIZE_RATIO = 0.5; // highlight point size as ratio of dot size

        private readonly FrameworkElement _glassDot;
        private readonly RadialGradientBrush? _highlightBorder;
        private readonly Microsoft.UI.Xaml.Shapes.Ellipse? _highlightDot;

        // Computed values (based on actual dot size)
        private double _dotRadius;
        private double _dotCenterX;
        private double _dotCenterY;
        private double _highlightRadius;
        private double _maxOffset;

        // static position (when triggered)
        private Point _fixedCenter = new Point(0, 0);
        private bool _isDotFixed = false;

        public DotAnimationManager(
            FrameworkElement glassDot,
            RadialGradientBrush? highlightBorder,
            Microsoft.UI.Xaml.Shapes.Ellipse? highlightDot)
        {
            _glassDot = glassDot;
            _highlightBorder = highlightBorder;
            _highlightDot = highlightDot;

            // Compute values based on actual dot size
            UpdateDotDimensions();

            // Initialize highlight point position to center
            InitializeHighlightPosition();
        }

        private void InitializeHighlightPosition()
        {
            if (_highlightDot != null)
            {
                var transform = _highlightDot.RenderTransform as TranslateTransform;
                if (transform == null)
                {
                    transform = new TranslateTransform();
                    _highlightDot.RenderTransform = transform;
                }

                // Center the highlight point initially
                transform.X = _dotCenterX - _highlightRadius;
                transform.Y = _dotCenterY - _highlightRadius;
            }
        }

        private void UpdateDotDimensions()
        {
            // Get actual dot size
            double dotWidth = _glassDot.Width;
            double dotHeight = _glassDot.Height;

            // Calculate center and radius
            _dotRadius = Math.Min(dotWidth, dotHeight) / 2.0;
            _dotCenterX = dotWidth / 2.0;
            _dotCenterY = dotHeight / 2.0;

            // Calculate highlight point size and position (based on dot size)
            _highlightRadius = _dotRadius * HIGHLIGHT_SIZE_RATIO;
            _maxOffset = _dotRadius * MAX_OFFSET_RATIO;

            // Update highlight point size if it exists
            if (_highlightDot != null)
            {
                double highlightSize = _dotRadius * HIGHLIGHT_SIZE_RATIO * 2.0;
                _highlightDot.Width = highlightSize;
                _highlightDot.Height = highlightSize;
            }
        }

        public void ShowAtPosition(Point position)
        {
            // Update dimensions in case size changed
            UpdateDotDimensions();

            // static position (center of dot)
            _fixedCenter = position;
            _isDotFixed = true;

            // Set position immediately (no flying) - center the dot at position
            Canvas.SetLeft(_glassDot, _fixedCenter.X - _dotCenterX);
            Canvas.SetTop(_glassDot, _fixedCenter.Y - _dotCenterY);

            // Show dot with Fluent 2 fade-in animation
            ShowDotWithAnimation();
        }

        public void UpdatePosition(Point mousePosition)
        {
            if (!_isDotFixed) return;

            // calculate offset from fixed center
            double offsetX = mousePosition.X - _fixedCenter.X;
            double offsetY = mousePosition.Y - _fixedCenter.Y;

            // apply elastic offset (rubber effect) - limit to max offset
            double distance = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
            if (distance > _maxOffset)
            {
                double scale = _maxOffset / distance;
                offsetX *= scale;
                offsetY *= scale;
            }

            // apply elastic factor, make offset smoother
            double elasticOffsetX = offsetX * ELASTIC_FACTOR;
            double elasticOffsetY = offsetY * ELASTIC_FACTOR;

            // update point position (fixed center + elastic offset)
            Canvas.SetLeft(_glassDot, _fixedCenter.X - _dotCenterX + elasticOffsetX);
            Canvas.SetTop(_glassDot, _fixedCenter.Y - _dotCenterY + elasticOffsetY);

            // highlight point follows mouse (in circle range)
            UpdateHighlightPosition(mousePosition.X, mousePosition.Y);
        }

        public void Hide()
        {
            // Hide dot with Fluent 2 fade-out animation
            HideDotWithAnimation();
        }

        private void ShowDotWithAnimation()
        {
            // Fluent 2 fade-in animation
            _glassDot.Visibility = Visibility.Visible;
            _glassDot.Opacity = 0;

            // create fade-in animation
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200), // Fluent 2 standard fade-in duration
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(fadeInAnimation, _glassDot);
            Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(fadeInAnimation);
            storyboard.Begin();
        }

        private void HideDotWithAnimation()
        {
            // Fluent 2 fade-out animation
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150), // Fluent 2 standard fade-out duration
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            Storyboard.SetTarget(fadeOutAnimation, _glassDot);
            Storyboard.SetTargetProperty(fadeOutAnimation, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(fadeOutAnimation);
            storyboard.Completed += (s, e) =>
            {
                _glassDot.Visibility = Visibility.Collapsed;
                _isDotFixed = false;
            };
            storyboard.Begin();
        }

        private void UpdateHighlightPosition(double mouseX, double mouseY)
        {
            // calculate offset from fixed center
            double relativeX = mouseX - _fixedCenter.X;
            double relativeY = mouseY - _fixedCenter.Y;

            // Limit highlight point within circle range (leave margin)
            double maxDistance = _dotRadius * (1.0 - HIGHLIGHT_MARGIN_RATIO);
            double distance = Math.Sqrt(relativeX * relativeX + relativeY * relativeY);

            if (distance > maxDistance)
            {
                double scale = maxDistance / distance;
                relativeX *= scale;
                relativeY *= scale;
            }

            // Update highlight border gradient position (normalized to 0-1 range)
            if (_highlightBorder != null)
            {
                double normalizedX = relativeX / _dotRadius;
                double normalizedY = relativeY / _dotRadius;

                _highlightBorder.GradientOrigin = new Point(0.5 + normalizedX * 0.3, 0.5 + normalizedY * 0.3);
                _highlightBorder.Center = new Point(0.5 + normalizedX * 0.3, 0.5 + normalizedY * 0.3);
            }

            // Update highlight point position (using RenderTransform, because it's inside Grid)
            if (_highlightDot != null)
            {
                // Use RenderTransform to position
                var transform = _highlightDot.RenderTransform as TranslateTransform;
                if (transform == null)
                {
                    transform = new TranslateTransform();
                    _highlightDot.RenderTransform = transform;
                }

                // Position highlight point center at (dotCenter + relative offset)
                // Since it uses HorizontalAlignment="Left" VerticalAlignment="Top",
                // we need to offset by -highlightRadius to center it
                transform.X = _dotCenterX - _highlightRadius + relativeX;
                transform.Y = _dotCenterY - _highlightRadius + relativeY;
            }
        }
    }
}

