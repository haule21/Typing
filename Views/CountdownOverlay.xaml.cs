using System;
using System.Threading.Tasks;
using System.Windows;

namespace TypingApp.Views
{
    public partial class CountdownOverlay : Window
    {
        public CountdownOverlay()
        {
            InitializeComponent();
            PositionWindow();
        }

        private void PositionWindow()
        {
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width - 20;
            this.Top = desktopWorkingArea.Bottom - this.Height - 20;
        }

        public async Task StartCountdownAsync(int seconds)
        {
            this.Show();

            for (int i = seconds; i > 0; i--)
            {
                txtCountdown.Text = i.ToString();
                await Task.Delay(1000);
            }

            this.Close();
        }
    }
}
