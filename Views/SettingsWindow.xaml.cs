using System.Windows;
using System.Windows.Input;
using TypingApp.Models;

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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtDelay.Text, out int delay))
            {
                _config.TypingDelay = delay;
            }

            _config.PasteHotkey.Key = _newKey;
            _config.PasteHotkey.Modifiers = _newModifiers;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
