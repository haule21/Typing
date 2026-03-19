using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using TypingApp.Models;

namespace TypingApp.Services
{
    public class OcrService
    {
        private readonly TesseractManager _tessManager;
        private readonly ConfigStore _configStore;

        public OcrService()
        {
            _configStore = new ConfigStore();
            string localLang = GetLocalTessLang();
            string initialLang = string.IsNullOrEmpty(_configStore.Current.OcrLanguage) ? localLang : _configStore.Current.OcrLanguage;
            _tessManager = new TesseractManager(initialLang);
        }

        private string GetLocalTessLang()
        {
            try
            {
                // Try system preferred language first
                var userLangs = Windows.System.UserProfile.GlobalizationPreferences.Languages;
                if (userLangs != null && userLangs.Count > 0)
                {
                    string primary = userLangs[0].Split('-')[0].ToLower();
                    string mapped = MapTagToTess(primary);
                    if (mapped != "eng") return mapped;
                }

                var tag = Windows.Globalization.Language.CurrentInputMethodLanguageTag.Split('-')[0].ToLower();
                return MapTagToTess(tag);
            }
            catch { return "eng"; }
        }

        private string MapTagToTess(string tag)
        {
            return tag switch
            {
                "ko" => "kor",
                "ja" => "jpn",
                "zh" => "chi_sim",
                "fr" => "fra",
                "de" => "deu",
                "es" => "spa",
                "it" => "ita",
                "ru" => "rus",
                "pt" => "por",
                _ => "eng"
            };
        }

        public bool IsSupported() => true; 

        public bool HasLanguagePacks()
        {
            try
            {
                string lang = _configStore.Current.OcrLanguage ?? GetLocalTessLang();
                return _tessManager.HasLanguagePack(lang);
            }
            catch { return false; }
        }

        public async Task<string> RecognizeTextAsync(BitmapSource bitmapSource, System.Threading.CancellationToken ct = default)
        {
            if (bitmapSource == null) return string.Empty;
            try
            {
                await _tessManager.InitializeAsync();
                var analysis = await AnalyzeContentWithDynamicScaleAsync(bitmapSource, ct);
                if (analysis.TotalCount == 0) return string.Empty;

                double finalScale = Math.Max(analysis.SuggestedScale, 5.0);
                string targetLang = _configStore.Current.OcrLanguage;

                if (string.IsNullOrEmpty(targetLang))
                {
                    double localRatio = (double)analysis.LocalCount / analysis.TotalCount;
                    double engRatio = (double)analysis.EnglishCount / analysis.TotalCount;
                    string localBase = GetLocalTessLang();
                    if (localRatio >= 0.1 && engRatio >= 0.1 && localBase != "eng") targetLang = $"{localBase}+eng";
                    else if (localRatio >= 0.1 && localBase != "eng") targetLang = localBase;
                    else targetLang = "eng";
                }

                // [추가] CJK 언어 선택 시 영어를 기본 혼용하도록 설정 (kor -> kor+eng)
                targetLang = EnsureHybridEnglish(targetLang);

                bool tessReady = await _tessManager.EnsureLanguagePackAsync(targetLang);
                if (ct.IsCancellationRequested) return string.Empty;

                using var processedMat = ImageProcessor.Preprocess(bitmapSource, finalScale);
                if (ct.IsCancellationRequested) return string.Empty;

                if (tessReady)
                {
                    var validZones = analysis.Zones.Select(z => {
                        double correction = finalScale / analysis.SuggestedScale;
                        return new OpenCvSharp.Rect(
                            (int)(z.X * correction), (int)(z.Y * correction),
                            (int)(z.Width * correction), (int)(z.Height * correction)
                        ).Intersect(new OpenCvSharp.Rect(0, 0, processedMat.Width, processedMat.Height));
                    }).Where(z => z.Width > 0 && z.Height > 0).ToList();

                    // [변경] 사용자 요청에 따른 화이트리스트 병합 및 필터링 로직
                    string finalWhitelist = "";
                    if (string.IsNullOrEmpty(analysis.Whitelist))
                    {
                        finalWhitelist = GetLanguageWhitelist(targetLang);
                    }
                    else
                    {
                        // 분석 결과 화이트리스트가 있는 경우 (숫자 전용 등), 언어 화이트리스트와 교집합 처리하거나 우선함
                        finalWhitelist = analysis.Whitelist;
                    }

                    var finalRegions = _tessManager.ExecuteTesseract(processedMat, validZones, targetLang, finalScale, finalWhitelist, analysis.DisableDict);
                    return OcrResultParser.Format(finalRegions);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OCR Error: {ex.Message}"); }
            return string.Empty;
        }

        private string EnsureHybridEnglish(string lang)
        {
            if (string.IsNullOrEmpty(lang)) return "eng";
            var parts = lang.Split('+').ToList();
            
            // 한/중/일 언어가 포함되어 있고 eng가 없다면 추가
            bool hasCJK = parts.Any(p => p.StartsWith("kor") || p.StartsWith("chi") || p.StartsWith("jpn"));
            if (hasCJK && !parts.Contains("eng"))
            {
                // Tesseract 성능을 위해 최대 3개 언어로 제한
                if (parts.Count < 3) parts.Add("eng");
            }
            return string.Join("+", parts);
        }

        private string GetLanguageWhitelist(string lang)
        {
            if (string.IsNullOrEmpty(lang)) return "";

            var langs = lang.Split('+');
            var whitelists = new List<string>();

            foreach (var l in langs)
            {
                string wl = GetBaseWhitelistForLang(l);
                if (string.IsNullOrEmpty(wl))
                {
                    // [규칙] 두 언어 중 하나라도 화이트리스트가 비어있으면(CJK 등) 전체 화이트리스트 미사용
                    return "";
                }
                whitelists.Add(wl);
            }

            // 알파벳 언어들만 있는 경우 화이트리스트 병합 (중복 제거)
            return new string(string.Concat(whitelists).Distinct().ToArray());
        }

        private string GetBaseWhitelistForLang(string lang)
        {
            // 알파벳 기반 언어별 화이트리스트 정의 (로컬 저장소 역할)
            // 숫자 및 기본 기호는 공통 포함
            string common = "0123456789!@#$%^&*()-_=+[]{}|;:',.<>/? \t\n\r";
            string alphabetLower = "abcdefghijklmnopqrstuvwxyz";
            string alphabetUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            return lang.ToLower() switch
            {
                "eng" => alphabetLower + alphabetUpper + common,
                "fra" => alphabetLower + alphabetUpper + common + "àâçéèêëîïôûùÿœæ" + "ÀÂÇÉÈÊËÎÏÔÛÙŸŒÆ",
                "deu" => alphabetLower + alphabetUpper + common + "äöüß" + "ÄÖÜ",
                "spa" => alphabetLower + alphabetUpper + common + "áéíóúñü¡¿" + "ÁÉÍÓÚÑÜ",
                "ita" => alphabetLower + alphabetUpper + common + "àèéìíîòóùú" + "ÀÈÉÌÍÎÒÓÙÚ",
                "rus" => common + "абвгдеёжзийклмнопрстуфхцчшщъыьэюя" + "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ",
                "por" => alphabetLower + alphabetUpper + common + "áâãàçéêíóôõú" + "ÁÂÃÀÇÉÊÍÓÔÕÚ",
                "vie" => alphabetLower + alphabetUpper + common + "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ",
                // CJK 및 기타 복잡 언어는 화이트리스트 미사용 (빈 문자열 반환)
                "kor" => "",
                "chi_sim" => "",
                "chi_tra" => "",
                "jpn" => "",
                "tha" => "",
                "ara" => "",
                "hin" => "",
                _ => ""
            };
        }

        private async Task<(List<OpenCvSharp.Rect> Zones, double SuggestedScale, string Whitelist, bool DisableDict, int LocalCount, int EnglishCount, int TotalCount)> AnalyzeContentWithDynamicScaleAsync(BitmapSource bitmapSource, System.Threading.CancellationToken ct)
        {
            var zones = new List<OpenCvSharp.Rect>();
            double suggestedScale = 5.0;
            string whitelist = "";
            bool disableDict = false;
            int localCount = 0, engCount = 0, totalCount = 0;

            try
            {
                using (var stream = new MemoryStream())
                {
                    var encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    encoder.Save(stream);
                    stream.Position = 0;

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
                        var ocrResult = await engine.RecognizeAsync(softwareBitmap).AsTask(ct);
                        if (ocrResult.Lines != null && ocrResult.Lines.Any())
                        {
                            var allWords = ocrResult.Lines.SelectMany(l => l.Words).ToList();
                            totalCount = allWords.Sum(w => w.Text.Length);
                            string allText = string.Concat(ocrResult.Lines.Select(l => l.Text));
                            localCount = allText.Count(c => (c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF) || (c >= 0x4E00 && c <= 0x9FFF));
                            engCount = allText.Count(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

                            double avgHeight = allWords.Average(w => w.BoundingRect.Height);
                            suggestedScale = Math.Clamp(40.0 / avgHeight, 3.5, 6.0);

                            var strategy = InferOcrStrategy(allText, (double)localCount / totalCount, (double)engCount / totalCount);
                            whitelist = strategy.Whitelist;
                            disableDict = strategy.DisableDict;

                            // 줄 병합 로직 고도화 (Center Y 기준)
                            var sortedWords = allWords.OrderBy(w => w.BoundingRect.Y).ToList();
                            var visualLines = new List<List<Windows.Media.Ocr.OcrWord>>();
                            
                            foreach (var word in sortedWords)
                            {
                                double wordCenterY = word.BoundingRect.Y + (word.BoundingRect.Height / 2.0);
                                var existingLine = visualLines.FirstOrDefault(line => {
                                    double lineAvgHeight = line.Average(w => w.BoundingRect.Height);
                                    double lineAvgCenterY = line.Average(w => w.BoundingRect.Y + (w.BoundingRect.Height / 2.0));
                                    return Math.Abs(lineAvgCenterY - wordCenterY) < (lineAvgHeight * 0.4);
                                });

                                if (existingLine != null) existingLine.Add(word);
                                else visualLines.Add(new List<Windows.Media.Ocr.OcrWord> { word });
                            }

                            foreach (var line in visualLines.OrderBy(l => l.Average(w => w.BoundingRect.Y)))
                            {
                                double minX = line.Min(w => w.BoundingRect.X);
                                double minY = line.Min(w => w.BoundingRect.Y);
                                double maxX = line.Max(w => w.BoundingRect.X + w.BoundingRect.Width);
                                double maxY = line.Max(w => w.BoundingRect.Y + w.BoundingRect.Height);

                                var rect = new OpenCvSharp.Rect(
                                    (int)(minX * suggestedScale), (int)(minY * suggestedScale),
                                    (int)((maxX - minX) * suggestedScale), (int)((maxY - minY) * suggestedScale)
                                );
                                rect.Inflate(25, 10);
                                zones.Add(rect);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Analysis Error: {ex.Message}"); }
            return (zones, suggestedScale, whitelist, disableDict, localCount, engCount, totalCount);
        }

        private (string Whitelist, bool DisableDict) InferOcrStrategy(string allText, double localRatio, double engRatio)
        {
            if (string.IsNullOrEmpty(allText)) return ("", false);
            int digitCount = allText.Count(char.IsDigit);
            int letterCount = allText.Count(char.IsLetter);
            int upperCount = allText.Count(char.IsUpper);
            int specCount = allText.Count(c => c == '_' || c == ','); // 콤마와 언더바 특별 관리
            int totalCount = allText.Length;

            if (digitCount > 0 && (double)digitCount / totalCount > 0.8)
                return ("0123456789.,-+/ \t_", true);

            if (localRatio < 0.1 && letterCount > 0 && (double)upperCount / letterCount > 0.8)
                return ("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()-_=+[]{}|;:',.<>/? \t_", true);

            if (localRatio < 0.1 && (specCount > 0 || (digitCount > 0 && letterCount > 0)))
                return ("", true);

            return ("", false);
        }
    }
}
