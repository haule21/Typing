using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            string iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "TypingClipboard v0.1.png");
            if (System.IO.File.Exists(iconPath))
            {
                _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
            }
            else
            {
                _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/TypingClipboard v0.1.png"));
            }

            _notifyIcon.ToolTipText = "Typing v1.0.5.0";

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
            // [추가] 전체 로직을 try-catch로 감싸 예외 발생 시 프로그램이 종료되는 것을 방지합니다.
            try
            {
                string? text = _clipboardManager.GetText();
                bool isFromOcr = false;
                using var cts = new CancellationTokenSource();
                OcrOverlay? overlay = null;

                if (string.IsNullOrEmpty(text) && _configStore.Current.EnableImageOcr)
                {
                    var image = _clipboardManager.GetImage();
                    if (image != null)
                    {
                        var ocrService = new OcrService();
                        if (ocrService.IsSupported())
                        {
                            overlay = new OcrOverlay();
                            overlay.CancelRequested += (s, e) => {
                                cts.Cancel();
                                overlay.Close();
                            };

                            overlay.Show();

                            try
                            {
                                text = await ocrService.RecognizeTextAsync(image, cts.Token);
                                isFromOcr = !string.IsNullOrEmpty(text);
                                
                                if (cts.IsCancellationRequested) return;

                                if (!isFromOcr)
                                {
                                    await overlay.ShowDoneAsync("No Text Detected.", false);
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"OCR Process Error: {ex.Message}");
                                if (overlay != null && overlay.IsVisible) overlay.Close();
                                return;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(text))
                {
                    if (cts.IsCancellationRequested)
                    {
                        if (overlay != null && overlay.IsVisible) overlay.Close();
                        return;
                    }

                    if (_configStore.Current.ExecutionDelaySeconds > 0)
                    {
                        var countdownOverlay = new CountdownOverlay();
                        bool success = await countdownOverlay.StartCountdownAsync(_configStore.Current.ExecutionDelaySeconds);
                        if (!success)
                        {
                            if (overlay != null && overlay.IsVisible) overlay.Close();
                            return;
                        }
                    }

                    await _inputSimulator.EnsureModifiersUpAsync();
                    
                    try
                    {
                        // [추가] 옵저버 패턴 연결: 타이핑 종료 시 UI 업데이트를 위해 이벤트 구독
                        if (overlay != null && isFromOcr)
                        {
                            _inputSimulator.ProcessingChanged += (isProcessing) =>
                            {
                                // 사용자의 요청대로 '타이핑이 완료된 시점'(isProcessing == false)에 성공 메시지 표시
                                if (!isProcessing && overlay.IsVisible)
                                {
                                    // UI 스레드에서 실행 보장
                                    overlay.Dispatcher.Invoke(async () => {
                                        await overlay.ShowDoneAsync("Successfully Pasted!", true);
                                    });
                                }
                            };
                        }

                        if (isFromOcr)
                        {
                            // OCR 결과 주입 (내부에서 ProcessingChanged 이벤트 발생)
                            await _inputSimulator.PasteTextAsBulkAsync(text, cts.Token);
                        }
                        else
                        {
                            // 일반 클립보드 텍스트 주입
                            await _inputSimulator.TypeTextAsync(text, _configStore.Current.TypingDelay, cts.Token);
                        }

                        // 일반 타이핑의 경우 (overlay가 없는 경우) 별도 처리 불필요
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Paste Simulation Error: {ex.Message}");
                        if (overlay != null && overlay.IsVisible) overlay.Close();
                    }
                }
                else if (overlay != null && overlay.IsVisible)
                {
                    overlay.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Critical Error in HandlePaste: {ex.Message}");
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
