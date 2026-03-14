using System.Windows;

namespace TypingApp.Services
{
    public interface IClipboardManager
    {
        string? GetText();
        void SetText(string text);
        System.Windows.Media.Imaging.BitmapSource? GetImage();
        System.Windows.IDataObject? GetDataObject();
        void SetDataObject(System.Windows.IDataObject data);
    }

    public class ClipboardManager : IClipboardManager
    {
        public string? GetText()
        {
            string? text = null;
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        text = System.Windows.Clipboard.GetText();
                    }
                });
            }
            return text;
        }

        public void SetText(string text)
        {
            if (System.Windows.Application.Current != null && !string.IsNullOrEmpty(text))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(text);
                    }
                    catch (System.Runtime.InteropServices.COMException) { }
                });
            }
        }

        public System.Windows.IDataObject? GetDataObject()
        {
            System.Windows.IDataObject? data = null;
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { data = System.Windows.Clipboard.GetDataObject(); }
                    catch (System.Runtime.InteropServices.COMException) { }
                });
            }
            return data;
        }

        public void SetDataObject(System.Windows.IDataObject data)
        {
            if (System.Windows.Application.Current != null && data != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { System.Windows.Clipboard.SetDataObject(data, true); }
                    catch (System.Runtime.InteropServices.COMException) { }
                });
            }
        }

        public System.Windows.Media.Imaging.BitmapSource? GetImage()
        {
            System.Windows.Media.Imaging.BitmapSource? image = null;
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Clipboard.ContainsImage())
                    {
                        image = System.Windows.Clipboard.GetImage();
                    }
                    else if (System.Windows.Clipboard.ContainsFileDropList())
                    {
                        var fileDropList = System.Windows.Clipboard.GetFileDropList();
                        if (fileDropList != null && fileDropList.Count > 0)
                        {
                            string? file = fileDropList[0];
                            if (!string.IsNullOrEmpty(file))
                            {
                                string ext = System.IO.Path.GetExtension(file).ToLower();
                                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                                {
                                    try
                                    {
                                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                                        bitmap.BeginInit();
                                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; // Keep file unlocked
                                        bitmap.UriSource = new System.Uri(file);
                                        bitmap.EndInit();
                                        image = bitmap;
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                });
            }
            return image;
        }
    }
}
