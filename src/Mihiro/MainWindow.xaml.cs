using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WagahighChoices.Toa;
using WagahighChoices.Toa.Grpc;

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
        private readonly Subject<Unit> _updateCursorImageSubject = new Subject<Unit>();
        private Argb32Image _screenImage;
        private Argb32Image _cursorImage;
        private IDisposable _logSubscription;

        public MainWindowBindingModel BindingModel => (MainWindowBindingModel)this.DataContext;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this._updateCursorImageSubject
                .Buffer(new TimeSpan(100 * TimeSpan.TicksPerMillisecond))
                .Where(x => x.Count > 0)
                .ObserveOn(this.Dispatcher)
                .SelectMany(_ => this.UpdateCursorImage().ToObservable())
                .Subscribe();
        }

        private void ValueTextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || e.ClickCount != 2) return;

            var text = ((TextBlock)sender).Text;
            Clipboard.SetText(text);

            e.Handled = true;
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (this._logSubscription != null)
            {
                this._logSubscription.Dispose();
                this._logSubscription = null;
            }

            if (this._connection != null)
            {
                this._connection.Dispose();
                this._connection = null;
            }

            var host = this.txtRemoteAddr.Text;
            int port;

            try
            {
                var colonIndex = host.LastIndexOf(':');
                if (colonIndex >= 0)
                {
                    port = int.Parse(host.Substring(colonIndex + 1));
                    host = host.Remove(colonIndex);
                }
                else
                {
                    port = GrpcToaServer.DefaultPort;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "接続先エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.btnConnect.IsEnabled = false;

            try
            {
                var conn = new GrpcRemoteWagahighOperator(host, port);
                this._connection = conn;
                await conn.ConnectAsync();

                this._logSubscription = conn.LogStream
                    .ObserveOn(this.Dispatcher)
                    .Subscribe(
                        this.OnNextLog,
                        ex =>
                        {
                            if (Debugger.IsAttached) Debugger.Break();
                            MessageBox.Show(this, ex.ToString(), "ログエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        },
                        () => MessageBox.Show(this, "ログストリームが終了しました。", "ログエラー", MessageBoxButton.OK, MessageBoxImage.Error)
                    );
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached) Debugger.Break();

                MessageBox.Show(this, ex.ToString(), "接続失敗", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                this.btnConnect.IsEnabled = true;
            }

            if (this._timer == null)
            {
                // 0.1 秒ごとに画面を更新
                this._timer = new DispatcherTimer(
                    new TimeSpan(100 * TimeSpan.TicksPerMillisecond),
                    DispatcherPriority.Normal,
                    this.TimerTick,
                    this.Dispatcher
                );
            }
        }

        private async void TimerTick(object sender, EventArgs e)
        {
            if (this._connection == null) return;

            try
            {
                await this.UpdateScreen();
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached) Debugger.Break();

                this._connection = null;
                MessageBox.Show(this, ex.ToString(), "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateScreen()
        {
            var source = this.imgScreen.Source as WriteableBitmap;

            var img = await this._connection.CaptureContentAsync();
            this._screenImage?.Dispose();
            this._screenImage = img;

            var createNewBitmap = source == null || source.PixelWidth != img.Width || source.PixelHeight != img.Height;
            if (createNewBitmap)
                source = new WriteableBitmap(img.Width, img.Height, 96, 96, PixelFormats.Bgr32, null);

            source.Lock();
            try
            {
                CopyPixels(img, source.BackBuffer, source.BackBufferStride * source.PixelHeight);
                source.AddDirtyRect(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight));
            }
            finally
            {
                source.Unlock();
            }

            this.BindingModel.GameWidth = img.Width;
            this.BindingModel.GameHeight = img.Height;

            if (createNewBitmap)
                this.imgScreen.Source = source;
        }

        private static unsafe void CopyPixels(Argb32Image image, IntPtr dest, int destLength)
        {
            ((Span<byte>)image.Data).CopyTo(new Span<byte>((void*)dest, destLength));
        }

        private static IEnumerable<DependencyObject> TraverseVisualTreeBreadthFirst(DependencyObject x)
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(x);

            while (queue.Count > 0)
            {
                x = queue.Dequeue();
                yield return x;

                var childrenCount = VisualTreeHelper.GetChildrenCount(x);
                for (var i = 0; i < childrenCount; i++)
                    queue.Enqueue(VisualTreeHelper.GetChild(x, i));
            }
        }

        private void OnNextLog(string log)
        {
            var scrollViewer = TraverseVisualTreeBreadthFirst(this.lstLog)
                .OfType<ScrollViewer>().First();
            var isBottommost = scrollViewer.VerticalOffset - (scrollViewer.ExtentHeight - scrollViewer.ViewportHeight) >= -double.Epsilon;

            this.BindingModel.Logs.Add(log);

            if (isBottommost) scrollViewer.ScrollToBottom();
        }

        private void imgScreen_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.UpdateCursorPosition(e.GetPosition(this.imgScreen));
            e.Handled = true;
        }

        private void imgScreen_MouseMove(object sender, MouseEventArgs e)
        {
            var buttonState = e.LeftButton | e.RightButton | e.MiddleButton | e.XButton1 | e.XButton2;
            if (buttonState == 0) return;

            // 何か押されていたら更新
            this.UpdateCursorPosition(e.GetPosition(this.imgScreen));
            e.Handled = true;
        }

        private void imgScreen_TouchDownOrMove(object sender, TouchEventArgs e)
        {
            this.UpdateCursorPosition(e.GetTouchPoint(this.imgScreen).Position);
            e.Handled = true;
        }

        private async void UpdateCursorPosition(Point pos)
        {
            var imgSource = this.imgScreen.Source;
            if (!(imgSource is WriteableBitmap)) return;

            pos = this.ToGamePosition(pos);
            this.BindingModel.CursorX = (int)pos.X;
            this.BindingModel.CursorY = (int)pos.Y;
            this.BindingModel.CursorRatioX = pos.X / imgSource.Width;
            this.BindingModel.CursorRatioY = pos.Y / imgSource.Height;

            this._updateCursorImageSubject.OnNext(Unit.Default);

            if (this._connection != null)
            {
                try
                {
                    await this._connection.SetCursorPositionAsync(checked((short)pos.X), checked((short)pos.Y)).ConfigureAwait(false);
                }
                catch
                {
                    if (Debugger.IsAttached) Debugger.Break();
                }
            }
        }

        private Point ToGamePosition(Point point)
        {
            var uiSize = this.imgScreen.RenderSize;
            var imgSource = this.imgScreen.Source;
            var imgWidth = imgSource.Width;
            var imgHeight = imgSource.Height;

            var scale = Math.Min(
                imgWidth / uiSize.Width,
                imgHeight / uiSize.Height
            );

            var uiCenterX = uiSize.Width / 2.0;
            var diffFromCenterX = point.X - uiCenterX;
            var x = imgWidth / 2.0 + diffFromCenterX * scale;

            var uiCenterY = uiSize.Height / 2.0;
            var diffFromCenterY = point.Y - uiCenterY;
            var y = imgHeight / 2.0 + diffFromCenterY * scale;

            return new Point(x, y);
        }

        private async Task UpdateCursorImage()
        {
            if (this._connection == null) return;

            try
            {
                var source = this.imgCursor.Source as WriteableBitmap;
                bool createNewBitmap;

                var img = await this._connection.GetCursorImageAsync();
                this._cursorImage?.Dispose();
                this._cursorImage = img;

                createNewBitmap = source == null || source.PixelWidth != img.Width || source.PixelHeight != img.Height;
                if (createNewBitmap)
                    source = new WriteableBitmap(img.Width, img.Height, 96, 96, PixelFormats.Pbgra32, null);

                source.Lock();

                try
                {
                    CopyPixels(img, source.BackBuffer, source.BackBufferStride * source.PixelHeight);
                    source.AddDirtyRect(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight));
                }
                finally
                {
                    source.Unlock();
                }

                if (createNewBitmap)
                    this.imgCursor.Source = source;
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached) Debugger.Break();

                this._connection = null;
                MessageBox.Show(this, ex.ToString(), "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void imgScreen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.SendClickEvent();
        }

        private void imgScreen_TouchUp(object sender, TouchEventArgs e)
        {
            this.SendClickEvent();
        }

        private async void SendClickEvent()
        {
            if (this._connection == null) return;

            try
            {
                await this._connection.MouseClickAsync();
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached) Debugger.Break();

                this._connection = null;
                MessageBox.Show(this, ex.ToString(), "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnHash_Click(object sender, RoutedEventArgs e)
        {
            const int hashLength = 32; // bits * bits / 8
            const string table = "0123456789abcdef";

            if (this._screenImage == null) return;

            var hash = new byte[hashLength];
            Blockhash.ComputeHash(new Rgb2432InputImage(this._screenImage), hash);

            var s = new string('\0', hashLength * 2);
            unsafe
            {
                fixed (char* c = s)
                {
                    for (var i = 0; i < hashLength; i++)
                    {
                        c[i * 2] = table[hash[i] >> 4];
                        c[i * 2 + 1] = table[hash[i] & 0xf];
                    }
                }
            }

            Clipboard.SetText(s);
            MessageBox.Show(this, s, "画像ハッシュ");
        }

        private void chkExpansion_Checked(object sender, RoutedEventArgs e)
        {
            this.imgScreen.StretchDirection = StretchDirection.Both;
        }

        private void chkExpansion_Unchecked(object sender, RoutedEventArgs e)
        {
            this.imgScreen.StretchDirection = StretchDirection.DownOnly;
        }
    }
}
