using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using Tesseract;

namespace TypingApp.Services
{
    public class OcrWordRegion
    {
        public string Text { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class TesseractManager
    {
        private readonly string _tessDataPath;
        private string _currentLang;

        public TesseractManager(string currentLang)
        {
            _currentLang = currentLang;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _tessDataPath = Path.Combine(appData, "TypingApp", "tessdata");
        }

        public async Task InitializeAsync()
        {
            if (!Directory.Exists(_tessDataPath))
            {
                Directory.CreateDirectory(_tessDataPath);
            }
        }

        public async Task<bool> EnsureLanguagePackAsync(string lang)
        {
            var langs = lang.Split('+');
            bool allSuccess = true;
            foreach (var l in langs)
            {
                string filePath = Path.Combine(_tessDataPath, $"{l}.traineddata");
                if (File.Exists(filePath)) continue;
                try
                {
                    using var client = new HttpClient();
                    var url = $"https://github.com/tesseract-ocr/tessdata_fast/raw/main/{l}.traineddata";
                    var data = await client.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(filePath, data);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to download language pack [{l}]: {ex.Message}");
                    allSuccess = false;
                }
            }
            return allSuccess;
        }

        public bool HasLanguagePack(string lang)
        {
            var langs = lang.Split('+');
            foreach (var l in langs)
            {
                if (!File.Exists(Path.Combine(_tessDataPath, $"{l}.traineddata")))
                    return false;
            }
            return true;
        }

        public List<OcrWordRegion> ExecuteTesseract(Mat processedMat, List<OpenCvSharp.Rect> zones, string lang, double currentScale, string whitelist = "", bool disableDict = false)
        {
            var results = new List<OcrWordRegion>();
            try
            {
                using var engine = new TesseractEngine(_tessDataPath, lang, EngineMode.Default);
                
                if (!string.IsNullOrEmpty(whitelist))
                {
                    engine.SetVariable("tessedit_char_whitelist", whitelist);
                }

                // 공백 및 기호 보존 설정
                engine.SetVariable("preserve_interword_spaces", "1");
                
                if (disableDict)
                {
                    engine.SetVariable("load_system_dawg", "0");
                    engine.SetVariable("load_freq_dawg", "0");
                }

                int calculatedDpi = (int)(96 * currentScale);
                if (calculatedDpi < 72) calculatedDpi = 72;

                if (zones == null || zones.Count == 0)
                {
                    Cv2.ImEncode(".bmp", processedMat, out byte[] fullBuf);
                    using var pix = Pix.LoadFromMemory(fullBuf);
                    pix.XRes = calculatedDpi; pix.YRes = calculatedDpi;
                    using var page = engine.Process(pix, PageSegMode.Auto);
                    string text = page.GetText()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(text))
                        results.Add(new OcrWordRegion { Text = text });
                }
                else
                {
                    foreach (OpenCvSharp.Rect zone in zones)
                    {
                        using var crop = new Mat(processedMat, zone);
                        Cv2.ImEncode(".bmp", crop, out byte[] buf);
                        using var pix = Pix.LoadFromMemory(buf);
                        pix.XRes = calculatedDpi; pix.YRes = calculatedDpi;
                        
                        // [변경] PSM 7 (SingleLine) 사용
                        using var page = engine.Process(pix, PageSegMode.SingleLine);
                        
                        var sbLine = new StringBuilder();
                        using (var iter = page.GetIterator())
                        {
                            iter.Begin();
                            Tesseract.Rect lastRect = new Tesseract.Rect();
                            bool firstWord = true;

                            do
                            {
                                if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
                                {
                                    string word = iter.GetText(PageIteratorLevel.Word)?.Trim() ?? "";
                                    if (string.IsNullOrEmpty(word)) continue;

                                    double wordHeight = (rect.Height + lastRect.Height) / 2.0;
                                    if (firstWord) wordHeight = rect.Height;

                                    if (firstWord)
                                    {
                                        // [추가] 들여쓰기(Indentation) 감지
                                        // 줄 시작점과 첫 단어 사이의 간격 계산
                                        double leadingGap = rect.X1;
                                        if (leadingGap > wordHeight * 1.5)
                                        {
                                            int tabCount = (int)(leadingGap / (wordHeight * 2.0));
                                            for(int i=0; i<Math.Max(1, tabCount); i++) sbLine.Append("\t");
                                        }
                                        else if (leadingGap > wordHeight * 0.3)
                                        {
                                            sbLine.Append(" ");
                                        }
                                    }
                                    else
                                    {
                                        // 단어 사이의 간격 분석
                                        double gap = rect.X1 - lastRect.X2;
                                        // 탭 판정 임계값 상향 (1.2 -> 1.8)
                                        if (gap > wordHeight * 1.8)
                                            sbLine.Append("\t");
                                        else if (gap > wordHeight * 0.1)
                                            sbLine.Append(" ");
                                    }
                                    
                                    sbLine.Append(word);
                                    lastRect = rect;
                                    firstWord = false;
                                }
                            } while (iter.Next(PageIteratorLevel.Word));
                        }

                        if (sbLine.Length > 0)
                        {
                            results.Add(new OcrWordRegion { Text = sbLine.ToString(), X = zone.X, Y = zone.Y });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tesseract processing error: {ex.Message}");
            }
            return results;
        }
    }
}
