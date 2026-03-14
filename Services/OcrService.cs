using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TypingApp.Services
{
    public class OcrService
    {
        public bool IsSupported()
        {
            try
            {
                var lang = new Windows.Globalization.Language(Windows.Globalization.Language.CurrentInputMethodLanguageTag);
                return Windows.Media.Ocr.OcrEngine.IsLanguageSupported(lang)
                    || Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public bool HasLanguagePacks()
        {
            try
            {
                return Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> RecognizeTextAsync(BitmapSource bitmapSource)
        {
            if (bitmapSource == null) return string.Empty;

            try
            {
                // Convert WPF BitmapSource to SoftwareBitmap
                using (var stream = new System.IO.MemoryStream())
                {
                    var encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    encoder.Save(stream);
                    stream.Position = 0;

                    // Workaround to get IRandomAccessStream from MemoryStream in WPF (.NET 9)
                    var randomAccessStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    using (var writer = new Windows.Storage.Streams.DataWriter(randomAccessStream.GetOutputStreamAt(0)))
                    {
                        writer.WriteBytes(stream.ToArray());
                        await writer.StoreAsync();
                    }

                    randomAccessStream.Seek(0);

                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
                    var softwareBitmap = await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);

                    var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
                    if (engine != null)
                    {
                        var ocrResult = await engine.RecognizeAsync(softwareBitmap);
                        if (ocrResult.Lines != null && ocrResult.Lines.Count > 0)
                        {
                            var stringBuilder = new System.Text.StringBuilder();
                            foreach (var line in ocrResult.Lines)
                            {
                                stringBuilder.AppendLine(line.Text);
                            }
                            return stringBuilder.ToString().TrimEnd();
                        }
                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR Error: {ex.Message}");
            }

            return string.Empty;
        }
    }
}
