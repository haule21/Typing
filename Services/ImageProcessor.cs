using System;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace TypingApp.Services
{
    public static class ImageProcessor
    {
        public static Mat Preprocess(BitmapSource bitmapSource, double scale)
        {
            Mat src = bitmapSource.ToMat();
            if (src.Channels() >= 3)
                Cv2.CvtColor(src, src, ColorConversionCodes.BGRA2BGR);

            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // 1. 5배 초고해상도 확대
            double finalScale = Math.Max(scale, 5.0); 
            Mat resized = new Mat();
            Cv2.Resize(gray, resized, new Size(0, 0), finalScale, finalScale, InterpolationFlags.Lanczos4);

            // 2. 괄호() 및 미세 기호 보호를 위한 민감한 이진화
            // AdaptiveThreshold를 사용하여 배경과 글자의 경계를 명확히 분리
            Mat binary = new Mat();
            Cv2.AdaptiveThreshold(resized, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 41, 10);

            // 3. Tesseract 최적화 (흰색 배경에 검은 글씨)
            if (Cv2.Mean(binary).Val0 < 127)
                Cv2.BitwiseNot(binary, binary);

            return binary;
        }
    }
}
