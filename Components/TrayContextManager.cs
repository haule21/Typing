using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using TypingApp.Models;
using TypingApp.Services;
using TypingApp.Views;

namespace TypingApp.Components
{
    public class TrayContextManager : IDisposable
    {
        private TaskbarIcon _notifyIcon;
        private ConfigStore _configStore;
        private HotkeyListener? _hotkeyListener;
        private InputSimulator? _inputSimulator;
        private IClipboardManager _clipboardManager;

        public TrayContextManager()
        {
            _configStore = new ConfigStore();
            _clipboardManager = new ClipboardManager();

            _notifyIcon = new TaskbarIcon();
            _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/TypingClipboard v0.1.png"));
            _notifyIcon.ToolTipText = "Typing v0.1";

            var contextMenu = new ContextMenu();

            var settingsItem = new MenuItem { Header = "설정" };
            settingsItem.Click += (s, e) => OpenSettings();
            contextMenu.Items.Add(settingsItem);

            var exitItem = new MenuItem { Header = "종료" };
            exitItem.Click += (s, e) => Application.Current.Shutdown();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;

            _hotkeyListener = new HotkeyListener();
            _hotkeyListener.UpdateHotkey(_configStore.Current.PasteHotkey.Key, _configStore.Current.PasteHotkey.Modifiers);

            _inputSimulator = new InputSimulator();
            _hotkeyListener.OnPasteHotkeyDetected += HandlePaste;
        }

        private async void HandlePaste()
        {
            string? text = _clipboardManager.GetText();

            if (!string.IsNullOrEmpty(text))
            {
                await _inputSimulator.EnsureModifiersUpAsync();
                await _inputSimulator.TypeTextAsync(text, _configStore.Current.TypingDelay);
            }
        }


        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(_configStore.Current);
            if (settingsWindow.ShowDialog() == true)
            {
                _configStore.Save();
                if (_hotkeyListener != null)
                {
                    _hotkeyListener.UpdateHotkey(_configStore.Current.PasteHotkey.Key, _configStore.Current.PasteHotkey.Modifiers);
                }
            }
        }

        public void Dispose()
        {
            _hotkeyListener?.Dispose();
            _notifyIcon?.Dispose();
        }
    }
}
