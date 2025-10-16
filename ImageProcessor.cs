using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImageStatsWpf
{
    public class RgbStats
    {
        public double MeanR, MeanG, MeanB;
        public double StdDevR, StdDevG, StdDevB;
        public double VarianceR, VarianceG, VarianceB;
        public double MedianR, MedianG, MedianB;
    }

    public static class ImageProcessor
    {
        // Zakładamy PixelFormats.Bgra32 dla kopiowanych pikseli
        public static RgbStats GetRgbStatistics(BitmapSource src, int x, int y, int width, int height)
        {
            // Convert or ensure PixelFormat is Bgra32
            var formatted = new FormatConvertedBitmap();
            formatted.BeginInit();
            formatted.Source = src;
            formatted.DestinationFormat = System.Windows.Media.PixelFormats.Bgra32;
            formatted.EndInit();

            int stride = width * 4;
            byte[] pixels = new byte[height * stride];

            var rect = new Int32Rect(x, y, width, height);
            try
            {
                formatted.CopyPixels(rect, pixels, stride, 0);
            }
            catch (Exception ex)
            {
                throw new Exception("Błąd kopiowania danych pikseli: " + ex.Message);
            }

            var listR = new List<int>(width * height);
            var listG = new List<int>(width * height);
            var listB = new List<int>(width * height);

            for (int row = 0; row < height; row++)
            {
                int rowOffset = row * stride;
                for (int col = 0; col < width; col++)
                {
                    int idx = rowOffset + col * 4;
                    byte b = pixels[idx + 0];
                    byte g = pixels[idx + 1];
                    byte r = pixels[idx + 2];
                    // byte a = pixels[idx + 3]; // alpha ignored in stats

                    listR.Add(r);
                    listG.Add(g);
                    listB.Add(b);
                }
            }

            return ComputeStatsFromLists(listR, listG, listB);
        }

        private static RgbStats ComputeStatsFromLists(List<int> r, List<int> g, List<int> b)
        {
            int n = r.Count;
            if (n == 0) return new RgbStats();

            double meanR = r.Average();
            double meanG = g.Average();
            double meanB = b.Average();

            var rSorted = r.OrderBy(v => v).ToArray();
            var gSorted = g.OrderBy(v => v).ToArray();
            var bSorted = b.OrderBy(v => v).ToArray();

            double medianR = ComputeMedianFromSorted(rSorted);
            double medianG = ComputeMedianFromSorted(gSorted);
            double medianB = ComputeMedianFromSorted(bSorted);

            // Population variance (divide by n)
            double varR = r.Select(v => (v - meanR) * (v - meanR)).Sum() / n;
            double varG = g.Select(v => (v - meanG) * (v - meanG)).Sum() / n;
            double varB = b.Select(v => (v - meanB) * (v - meanB)).Sum() / n;

            double stdR = Math.Sqrt(varR);
            double stdG = Math.Sqrt(varG);
            double stdB = Math.Sqrt(varB);

            return new RgbStats
            {
                MeanR = meanR,
                MeanG = meanG,
                MeanB = meanB,
                MedianR = medianR,
                MedianG = medianG,
                MedianB = medianB,
                VarianceR = varR,
                VarianceG = varG,
                VarianceB = varB,
                StdDevR = stdR,
                StdDevG = stdG,
                StdDevB = stdB
            };
        }

        private static double ComputeMedianFromSorted(int[] arr)
        {
            int n = arr.Length;
            if (n % 2 == 1) return arr[n / 2];
            return (arr[n / 2 - 1] + arr[n / 2]) / 2.0;
        }
    }
}
