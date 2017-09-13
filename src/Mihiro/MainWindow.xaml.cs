using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WagahighChoices.Toa;

namespace WagahighChoices.Mihiro
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private WagahighOperator _connection;
        private DispatcherTimer _timer;

        public MainWindowBindingModel BindingModel => (MainWindowBindingModel)this.DataContext;

        private void ValueTextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || e.ClickCount != 2) return;

            var text = ((TextBlock)sender).Text;
            Clipboard.SetText(text);

            e.Handled = true;
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            var conn = this._connection;
            this._connection = null;
            conn?.Dispose();

            string host;
            int display, screen;

            try
            {
                (host, display, screen) = ParseDisplayString(this.txtDisplay.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ディスプレイ名エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.btnConnect.IsEnabled = false;

            try
            {
                conn = await WagahighOperator.ConnectAsync(host, display, screen);
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    MessageBox.Show(this, ex.ToString(), "接続失敗", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }
            finally
            {
                this.btnConnect.IsEnabled = true;
            }

            if (this._timer == null)
            {
                this._timer = new DispatcherTimer(
                    new TimeSpan(100 * TimeSpan.TicksPerMillisecond),
                    DispatcherPriority.Normal,
                    this.TimerTick,
                    this.Dispatcher
                );
            }
        }

        private static (string, int, int) ParseDisplayString(string s)
        {
            if (string.IsNullOrEmpty(s)) return ("localhost", 0, 0);

            var colonIndex = s.IndexOf(':');
            if (colonIndex < 0) return (s, 0, 0);

            var host = colonIndex == 0 ? "localhost" : s.Remove(colonIndex);

            var dotIndex = s.IndexOf('.', colonIndex + 1);

            var display = int.Parse(s.Substring(colonIndex + 1, (dotIndex < 0 ? s.Length : dotIndex) - colonIndex - 1));
            var screen = dotIndex < 0 ? 0 : int.Parse(s.Remove(dotIndex + 1));

            return (host, display, screen);
        }

        //private void ShowMessageBoxOnDispatcher(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        //{
        //    this.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(this, message, title, button, icon)));
        //}

        private async void TimerTick(object sender, EventArgs e)
        {
            // TODO
        }
    }
}
