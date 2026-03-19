using System.Windows;
using System.Windows.Input;
using TypingApp.Models;
using TypingApp.Services;
using System.Threading.Tasks;

namespace TypingApp.Views
{
    public partial class SettingsWindow : Window
    {
        private AppConfig _config;
        private Key _newKey;
        private ModifierKeys _newModifiers;

        private bool _isListening = false;

        public SettingsWindow(AppConfig config)
        {
            InitializeComponent();
            _config = config;

            // Set Icon dynamically to avoid XamlParseException with MSIX packaged environments
            string iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "TypingClipboard v0.1.png");
            if (System.IO.File.Exists(iconPath))
            {
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(iconPath));
            }
            else
            {
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(new System.Uri("pack://application:,,,/Resources/TypingClipboard v0.1.png"));
            }

            txtDelay.Text = _config.TypingDelay.ToString();
            UpdateHotkeyDisplay(_config.PasteHotkey.Key, _config.PasteHotkey.Modifiers);

            chkIgnoreTabs.IsChecked = _config.IgnoreTabs;
            chkIgnoreNewlines.IsChecked = _config.IgnoreNewlines;
            txtExecutionDelay.Text = _config.ExecutionDelaySeconds.ToString();
            chkEnableImageOcr.IsChecked = _config.EnableImageOcr;
            lblOcrLang.Text = string.IsNullOrEmpty(_config.OcrLanguage) ? "[Auto]" : $"[{_config.OcrLanguage}]";

            _newKey = _config.PasteHotkey.Key;
            _newModifiers = _config.PasteHotkey.Modifiers;

            this.PreviewKeyDown += Window_PreviewKeyDown;
        }

        private void UpdateHotkeyDisplay(Key key, ModifierKeys modifiers)
        {
            txtHotkey.Text = $"{modifiers} + {key}";
        }

        private void BtnChangeHotkey_Click(object sender, RoutedEventArgs e)
        {
            _isListening = true;
            lblStatus.Text = "Please press the desired hotkey combination...";
            btnChangeHotkey.IsEnabled = false;
            txtHotkey.Focus();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_isListening) return;

            e.Handled = true;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Ignore modifier key presses alone
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt || key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            _newModifiers = Keyboard.Modifiers;
            _newKey = key;
            UpdateHotkeyDisplay(_newKey, _newModifiers);

            _isListening = false;
            lblStatus.Text = "";
            btnChangeHotkey.IsEnabled = true;
        }

        private async void ChkEnableImageOcr_Checked(object sender, RoutedEventArgs e)
        {
            var ocrService = new TypingApp.Services.OcrService();
            if (!ocrService.IsSupported())
            {
                System.Windows.MessageBox.Show("Feature not supported. This feature requires Windows 10 or later.", "Unsupported", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                chkEnableImageOcr.IsChecked = false;
                return;
            }

            var tessManager = new TesseractManager(_config.OcrLanguage);
            await tessManager.InitializeAsync();
            if (!tessManager.HasLanguagePack(_config.OcrLanguage))
            {
                var result = System.Windows.MessageBox.Show(
                    "언어팩 다운로드 진행 시 정밀한 OCR 사용이 가능합니다. 추가로 다운로드 받으시겠습니까?\n(Download Tesseract language pack?)",
                    "Language Pack Required",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    lblStatus.Text = "Downloading language pack...";
                    bool success = await tessManager.EnsureLanguagePackAsync(_config.OcrLanguage);
                    lblStatus.Text = success ? "Language pack ready." : "Download failed.";
                    if (!success)
                    {
                        chkEnableImageOcr.IsChecked = false;
                    }
                }
                else
                {
                    chkEnableImageOcr.IsChecked = false;
                }
            }
        }

        private async void BtnOcrLang_Click(object sender, RoutedEventArgs e)
        {
            var availableLangs = new[]
            {
                (Code: "kor", Name: "Korean (한국어)"),
                (Code: "eng", Name: "English (영어/숫자)"),
                (Code: "jpn", Name: "Japanese (일본어)"),
                (Code: "chi_sim", Name: "Chinese (중국어 간체)"),
                (Code: "chi_tra", Name: "Chinese (중국어 번체)"),
                (Code: "fra", Name: "French (프랑스어)"),
                (Code: "deu", Name: "German (독일어)"),
                (Code: "spa", Name: "Spanish (스페인어)"),
                (Code: "ita", Name: "Italian (이탈리아어)"),
                (Code: "rus", Name: "Russian (러시아어)"),
                (Code: "por", Name: "Portuguese (포르투갈어)"),
                (Code: "vie", Name: "Vietnamese (베트남어)"),
                (Code: "tha", Name: "Thai (태국어)"),
                (Code: "ara", Name: "Arabic (아랍어)"),
                (Code: "hin", Name: "Hindi (힌디어)"),
                (Code: "osd", Name: "Scripts & Orientation (OSD)")
            };

            var dialog = new Window
            {
                Title = "OCR Language Settings",
                Width = 350,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                Background = System.Windows.Media.Brushes.GhostWhite
            };

            var mainGrid = new System.Windows.Controls.Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var header = new System.Windows.Controls.TextBlock 
            { 
                Text = "Select up to 2 languages:", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10) 
            };
            System.Windows.Controls.Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            var listView = new System.Windows.Controls.ListView
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10)
            };
            System.Windows.Controls.Grid.SetRow(listView, 1);
            mainGrid.Children.Add(listView);

            var checkBoxes = new System.Collections.Generic.List<(string Code, System.Windows.Controls.CheckBox Box, System.Windows.Controls.TextBlock Status)>();
            var tm = new TesseractManager(""); 
            await tm.InitializeAsync();

            string currentLangsStr = _config.OcrLanguage ?? "";
            var currentLangs = currentLangsStr.Split('+', System.StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (var lang in availableLangs)
            {
                bool isDownloaded = tm.HasLanguagePack(lang.Code);
                
                var itemGrid = new System.Windows.Controls.Grid { Width = 300, Margin = new Thickness(5) };
                itemGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

                var cb = new System.Windows.Controls.CheckBox
                {
                    Content = isDownloaded ? lang.Name : $"{lang.Name} (Not Installed)",
                    IsChecked = currentLangs.Contains(lang.Code),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = lang.Code,
                    Foreground = isDownloaded ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.Gray
                };

                var statusIcon = new System.Windows.Controls.TextBlock
                {
                    Text = isDownloaded ? "✓" : "!",
                    Foreground = isDownloaded ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Orange,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 5, 0)
                };

                System.Windows.Controls.Grid.SetColumn(cb, 0);
                System.Windows.Controls.Grid.SetColumn(statusIcon, 1);
                itemGrid.Children.Add(cb);
                itemGrid.Children.Add(statusIcon);

                listView.Items.Add(itemGrid);
                checkBoxes.Add((lang.Code, cb, statusIcon));

                cb.Checked += async (s, ev) =>
                {
                    int checkedCount = checkBoxes.Count(x => x.Box.IsChecked == true);
                    if (checkedCount > 2)
                    {
                        cb.IsChecked = false;
                        System.Windows.MessageBox.Show("You can select up to 2 languages.", "Limit Exceeded", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!tm.HasLanguagePack(lang.Code))
                    {
                        cb.IsEnabled = false;
                        lblStatus.Text = $"Downloading {lang.Code}...";
                        bool success = await tm.EnsureLanguagePackAsync(lang.Code);
                        lblStatus.Text = success ? "Download complete." : "Download failed.";
                        
                        if (success)
                        {
                            cb.Foreground = System.Windows.Media.Brushes.Black;
                            cb.Content = lang.Name;
                            statusIcon.Text = "✓";
                            statusIcon.Foreground = System.Windows.Media.Brushes.Green;
                        }
                        else
                        {
                            cb.IsChecked = false;
                            System.Windows.MessageBox.Show($"Failed to download {lang.Code}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        cb.IsEnabled = true;
                    }
                };
            }

            var btnApply = new System.Windows.Controls.Button 
            { 
                Content = "Apply", 
                Height = 35, 
                Background = System.Windows.Media.Brushes.LightBlue,
                FontWeight = FontWeights.Bold
            };
            System.Windows.Controls.Grid.SetRow(btnApply, 2);
            mainGrid.Children.Add(btnApply);

            btnApply.Click += (s, ev) =>
            {
                var selected = checkBoxes.Where(x => x.Box.IsChecked == true).Select(x => x.Code).ToList();
                if (selected.Count == 0)
                {
                    _config.OcrLanguage = "";
                    lblOcrLang.Text = "[Auto]";
                }
                else
                {
                    _config.OcrLanguage = string.Join("+", selected);
                    lblOcrLang.Text = $"[{_config.OcrLanguage}]";
                }
                dialog.Close();
            };

            dialog.Content = mainGrid;
            dialog.ShowDialog();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtDelay.Text, out int delay))
            {
                _config.TypingDelay = delay;
            }

            _config.PasteHotkey.Modifiers = _newModifiers;

            _config.IgnoreTabs = chkIgnoreTabs.IsChecked ?? false;
            _config.IgnoreNewlines = chkIgnoreNewlines.IsChecked ?? false;
            _config.EnableImageOcr = chkEnableImageOcr.IsChecked ?? false;
            
            string langValue = lblOcrLang.Text.Trim('[', ']');
            _config.OcrLanguage = (langValue == "Auto") ? "" : langValue;

            if (int.TryParse(txtExecutionDelay.Text, out int execDelay))
            {
                // Clamp between 0 and 10
                _config.ExecutionDelaySeconds = System.Math.Clamp(execDelay, 0, 10);
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
