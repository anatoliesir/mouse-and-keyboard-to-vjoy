using MouseToVJoy.Data;
using MouseToVJoy.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MouseToVJoy
{
    public partial class ResponseCurveEditorWindow : Window
    {
        public ResponseCurveEditorWindow(string curvePoints)
        {
            InitializeComponent();
            CurveTextBox.Text = string.IsNullOrWhiteSpace(curvePoints)
                ? PresetSettings.DefaultResponseCurvePoints
                : curvePoints;
            DrawCurve();
        }

        public string CurvePoints { get; private set; } = PresetSettings.DefaultResponseCurvePoints;

        private void CurveTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            DrawCurve();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            CurveTextBox.Text = PresetSettings.DefaultResponseCurvePoints;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            CurvePoints = NormalizeCurveText(CurveTextBox.Text);
            DialogResult = true;
        }

        private void DrawCurve()
        {
            if (CurveCanvas == null) return;

            CurveCanvas.Children.Clear();

            double width = Math.Max(1.0, CurveCanvas.ActualWidth);
            if (width <= 1.0) width = 560.0;
            double height = Math.Max(1.0, CurveCanvas.ActualHeight);
            if (height <= 1.0) height = 220.0;

            DrawGrid(width, height);

            var points = MainViewModel.ParseResponseCurvePoints(CurveTextBox.Text).ToArray();
            Polyline line = new()
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                StrokeThickness = 3
            };

            foreach (ResponseCurvePoint point in points)
            {
                line.Points.Add(new Point(point.X * width, height - (point.Y * height)));
            }

            CurveCanvas.Children.Add(line);

            foreach (ResponseCurvePoint point in points)
            {
                Ellipse handle = new()
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(46, 125, 50))
                };

                Canvas.SetLeft(handle, (point.X * width) - 4);
                Canvas.SetTop(handle, height - (point.Y * height) - 4);
                CurveCanvas.Children.Add(handle);
            }
        }

        private void DrawGrid(double width, double height)
        {
            for (int i = 0; i <= 4; i++)
            {
                double x = width * i / 4.0;
                CurveCanvas.Children.Add(new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 0,
                    Y2 = height,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                });

                double y = height * i / 4.0;
                CurveCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = width,
                    Y1 = y,
                    Y2 = y,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                });
            }
        }

        private static string NormalizeCurveText(string text)
        {
            var points = MainViewModel.ParseResponseCurvePoints(text);
            return string.Join(";", points.Select(point => $"{point.X:0.###},{point.Y:0.###}"));
        }
    }
}
