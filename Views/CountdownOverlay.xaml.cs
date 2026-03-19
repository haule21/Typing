using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TypingApp.Views
{
    public partial class CountdownOverlay : Window
    {
        private bool _isCancelled = false;

        public CountdownOverlay()
        {
            InitializeComponent();
            PositionWindow();

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    _isCancelled = true;
                    e.Handled = true;
                    this.Close();
                }
            };
        }

        private void PositionWindow()
        {
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width - 20;
            this.Top = desktopWorkingArea.Bottom - this.Height - 20;
        }

        public async Task<bool> StartCountdownAsync(int seconds)
        {
            this.Show();

            for (int i = seconds; i > 0; i--)
            {
                if (_isCancelled) return false;
                txtCountdown.Text = i.ToString();
                await Task.Delay(1000);
            }

            if (_isCancelled) return false;
            
            this.Close();
            return true;
        }
    }
}
