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

            // AppDomain.CurrentDomain.BaseDirectory works consistently for Content files copied to output directory
            string iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "TypingClipboard v0.1.png");
            if (System.IO.File.Exists(iconPath))
            {
                _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
            }
            else
            {
                // Fallback to pack URI if it was embedded as a Resource
                _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/TypingClipboard v0.1.png"));
            }

            _notifyIcon.ToolTipText = "Typing v1.0.4.0";

            var contextMenu = new ContextMenu();

            var settingsItem = new MenuItem { Header = "Settings" };
            settingsItem.Click += (s, e) => OpenSettings();
            contextMenu.Items.Add(settingsItem);

            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) => System.Windows.Application.Current.Shutdown();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;

            _hotkeyListener = new HotkeyListener();
            _hotkeyListener.UpdateHotkey(_configStore.Current.PasteHotkey.Key, _configStore.Current.PasteHotkey.Modifiers);

            _inputSimulator = new InputSimulator(_configStore);
            _hotkeyListener.OnPasteHotkeyDetected += HandlePaste;
        }

        private async void HandlePaste()
        {
            string? text = _clipboardManager.GetText();
            bool isFromOcr = false;

            // If no text, try to get image for OCR if enabled
            if (string.IsNullOrEmpty(text) && _configStore.Current.EnableImageOcr)
            {
                var image = _clipboardManager.GetImage();
                if (image != null)
                {
                    var ocrService = new OcrService();
                    if (ocrService.IsSupported())
                    {
                        text = await ocrService.RecognizeTextAsync(image);
                        isFromOcr = !string.IsNullOrEmpty(text);
                    }
                }
            }

            if (!string.IsNullOrEmpty(text))
            {
                if (_configStore.Current.ExecutionDelaySeconds > 0)
                {
                    var overlay = new CountdownOverlay();
                    await overlay.StartCountdownAsync(_configStore.Current.ExecutionDelaySeconds);
                }

                await _inputSimulator.EnsureModifiersUpAsync();
                
                if (isFromOcr)
                {
                    // For OCR text, type it instantly (0ms delay) without touching the clipboard
                    await _inputSimulator.TypeTextAsync(text, 0);
                }
                else
                {
                    await _inputSimulator.TypeTextAsync(text, _configStore.Current.TypingDelay);
                }
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
