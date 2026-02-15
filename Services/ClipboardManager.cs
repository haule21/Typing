using System.Windows;

namespace TypingApp.Services
{
    public interface IClipboardManager
    {
        string? GetText();
    }

    public class ClipboardManager : IClipboardManager
    {
        public string? GetText()
        {
            string? text = null;
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Clipboard.ContainsText())
                    {
                        text = Clipboard.GetText();
                    }
                });
            }
            return text;
        }
    }
}
