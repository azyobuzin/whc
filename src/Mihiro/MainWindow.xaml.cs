using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
        private readonly Subject<Unit> _updateCursorImageSubject = new Subject<Unit>();
        private Image<Rgb2432> _screenImage;
        private Image<Argb32> _cursorImage;

        public MainWindowBindingModel BindingModel => (MainWindowBindingModel)this.DataContext;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this._updateCursorImageSubject
                .Buffer(new TimeSpan(100 * TimeSpan.TicksPerMillisecond))
                .Where(x => x.Count > 0)
                .ObserveOnDispatcher()
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
            if (this._connection != null)
            {
                this._connection.Dispose();
                this._connection = null;
            }

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
                this._connection = await WagahighOperator.ConnectAsync(host, display, screen);
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

        private static (string, int, int) ParseDisplayString(string s)
        {
            if (string.IsNullOrEmpty(s)) return ("localhost", 0, 0);

            var colonIndex = s.IndexOf(':');
            if (colonIndex < 0) return (s, 0, 0);

            var host = colonIndex == 0 ? "localhost" : s.Remove(colonIndex);

            var dotIndex = s.IndexOf('.', colonIndex + 1);

            var display = int.Parse(s.Substring(colonIndex + 1, (dotIndex < 0 ? s.Length : dotIndex) - colonIndex - 1));
            var screen = dotIndex < 0 ? 0 : int.Parse(s.Substring(dotIndex + 1));

            return (host, display, screen);
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
                CopyPixels(img.Frames.RootFrame, source.BackBuffer, source.BackBufferStride * source.PixelHeight);
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

        private static unsafe void CopyPixels<TPixel>(ImageFrame<TPixel> frame, IntPtr dest, int destLength)
            where TPixel : struct, IPixel<TPixel>
        {
            var buf = new Span<TPixel>((void*)dest, destLength / Unsafe.SizeOf<TPixel>());

            for (var y = 0; y < frame.Height; y++)
            {
                var baseIndex = frame.Width * y;

                for (var x = 0; x < frame.Width; x++)
                {
                    buf[baseIndex + x] = frame[x, y];
                }
            }
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
                    await this._connection.SetCursorPositionAsync((int)pos.X, (int)pos.Y).ConfigureAwait(false);
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

            var scale = Math.Max(
                1.0,
                Math.Min(
                    imgWidth / uiSize.Width,
                    imgHeight / uiSize.Height
                )
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
                    CopyPixels(img.Frames.RootFrame, source.BackBuffer, source.BackBufferStride * source.PixelHeight);
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

            var hash = new byte[hashLength];
            Blockhash.ComputeHash(this._screenImage.Frames.RootFrame, hash);

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
    }
}
