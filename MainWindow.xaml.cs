using Microsoft.Win32;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace ImageStatsWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BitmapSource loadedBitmap;
        private Point selectionStart;
        private Rectangle rubberBand;
        private bool isSelecting = false;

        public MainWindow()
        {
            InitializeComponent();
            rubberBand = new Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 0, 0)),
                Visibility = Visibility.Collapsed
            };
            OverlayCanvas.Children.Add(rubberBand);
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog()
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.svg|All files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                TbFileName.Text = dlg.FileName;
                try
                {
                    var ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext == ".svg")
                        loadedBitmap = LoadSvgAsBitmap(dlg.FileName);
                    else
                        loadedBitmap = new BitmapImage(new Uri(dlg.FileName));

                    ImageViewer.Source = loadedBitmap;

                    // Adjust overlay canvas size to image pixel size (since Stretch=None)
                    OverlayCanvas.Width = loadedBitmap.PixelWidth;
                    OverlayCanvas.Height = loadedBitmap.PixelHeight;

                    rubberBand.Visibility = Visibility.Collapsed;
                    ClearStats();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading image: " + ex.Message);
                }
            }
        }

        private BitmapSource LoadSvgAsBitmap(string path)
        {
            // render SVG
            var svg = new SKSvg();
            using (var stream = File.OpenRead(path))
            {
                svg.Load(stream);
            }

            // decide bitmap size - use dimensions from SVG viewBox or width 800 if missing
            var svgRect = svg.Picture?.CullRect ?? SKRect.Create(800, 600);
            int width = (int)Math.Max(1, svgRect.Width);
            int height = (int)Math.Max(1, svgRect.Height);

            // Render at native size
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var bitmap = new SKBitmap(info))
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawPicture(svg.Picture);
                canvas.Flush();

                // Convert SKBitmap to BitmapSource
                using (var image = SKImage.FromBitmap(bitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var ms = new MemoryStream())
                {
                    data.SaveTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
        }

        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (loadedBitmap == null) return;

            isSelecting = true;
            selectionStart = e.GetPosition(OverlayCanvas);
            Canvas.SetLeft(rubberBand, selectionStart.X);
            Canvas.SetTop(rubberBand, selectionStart.Y);
            rubberBand.Width = 0;
            rubberBand.Height = 0;
            rubberBand.Visibility = Visibility.Visible;
            OverlayCanvas.CaptureMouse();
        }

        private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting) return;
            var pos = e.GetPosition(OverlayCanvas);

            double x = Math.Min(pos.X, selectionStart.X);
            double y = Math.Min(pos.Y, selectionStart.Y);
            double w = Math.Abs(pos.X - selectionStart.X);
            double h = Math.Abs(pos.Y - selectionStart.Y);

            Canvas.SetLeft(rubberBand, x);
            Canvas.SetTop(rubberBand, y);
            rubberBand.Width = w;
            rubberBand.Height = h;
        }

        private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isSelecting) return;
            isSelecting = false;
            OverlayCanvas.ReleaseMouseCapture();

            var x = Canvas.GetLeft(rubberBand);
            var y = Canvas.GetTop(rubberBand);
            var w = rubberBand.Width;
            var h = rubberBand.Height;

            // convert to integers and clamp
            int ix = (int)Math.Max(0, Math.Floor(x));
            int iy = (int)Math.Max(0, Math.Floor(y));
            int iw = (int)Math.Max(1, Math.Floor(w));
            int ih = (int)Math.Max(1, Math.Floor(h));

            if (loadedBitmap != null)
            {
                // clamp to image bounds
                ix = Math.Min(ix, loadedBitmap.PixelWidth - 1);
                iy = Math.Min(iy, loadedBitmap.PixelHeight - 1);
                if (ix + iw > loadedBitmap.PixelWidth) iw = loadedBitmap.PixelWidth - ix;
                if (iy + ih > loadedBitmap.PixelHeight) ih = loadedBitmap.PixelHeight - iy;

                ComputeAndShowStats(ix, iy, iw, ih);
            }
        }

        private void ComputeAndShowStats(int x, int y, int w, int h)
        {
            TbSelection.Text = $"Selection: X={x}, Y={y}, W={w}, H={h}";

            var stats = ImageProcessor.GetRgbStatistics(loadedBitmap, x, y, w, h);

            TbMeanR.Text = stats.MeanR.ToString("0.00");
            TbMeanG.Text = stats.MeanG.ToString("0.00");
            TbMeanB.Text = stats.MeanB.ToString("0.00");

            TbMedR.Text = stats.MedianR.ToString("0");
            TbMedG.Text = stats.MedianG.ToString("0");
            TbMedB.Text = stats.MedianB.ToString("0");

            TbStdR.Text = stats.StdDevR.ToString("0.00");
            TbStdG.Text = stats.StdDevG.ToString("0.00");
            TbStdB.Text = stats.StdDevB.ToString("0.00");
        }

        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            rubberBand.Visibility = Visibility.Collapsed;
            ClearStats();
        }

        private void ClearStats()
        {
            TbSelection.Text = "No selection";
            TbMeanR.Text = TbMeanG.Text = TbMeanB.Text = "-";
            TbMedR.Text = TbMedG.Text = TbMedB.Text = "-";
            TbStdR.Text = TbStdG.Text = TbStdB.Text = "-";
        }
    }
}
