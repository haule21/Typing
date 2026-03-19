using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace TypingApp.Views
{
    public partial class OcrOverlay : Window
    {
        public event EventHandler? CancelRequested;

        public OcrOverlay()
        {
            InitializeComponent();
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CancelRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            };
        }

        public void UpdateStatus(string message, bool isSuccess)
        {
            // 애니메이션 상태에 따라 투명도 조정
            txtStatus.Text = message;
            txtStatus.Foreground = isSuccess ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed;
        }

        public async Task ShowDoneAsync(string message, bool isSuccess)
        {
            // 애니메이션 중지 및 상태 업데이트
            txtStatus.BeginAnimation(OpacityProperty, null);
            txtStatus.Opacity = 1.0;
            txtStatus.Text = message;
            txtStatus.Foreground = isSuccess ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed;
            txtSubStatus.Text = isSuccess ? "Completed" : "";

            // 잠시 보여준 후 닫기
            await Task.Delay(1000);
            this.Close();
        }
    }
}
