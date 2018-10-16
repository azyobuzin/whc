using System;
using System.Drawing;
using System.Threading.Tasks;

namespace WagahighChoices.Toa
{
    /// <summary>ワガママハイスペックを操作する基底クラス</summary>
    public abstract class WagahighOperator : IDisposable
    {
        protected bool IsDisposed { get; private set; }

        protected virtual void Dispose(bool disposing)
        {
            this.IsDisposed = true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>ゲーム領域の画像を取得します。</summary>
        public abstract Task<Bgra32Image> CaptureContentAsync();

        /// <summary>ゲーム領域の大きさを取得します。</summary>
        public abstract Task<Size> GetContentSizeAsync();

        /// <summary>カーソルをゲーム領域の左上から見て <paramref name="x"/>, <paramref name="y"/> の位置に移動します。</summary>
        public abstract Task SetCursorPositionAsync(short x, short y);

        /// <summary>現在のカーソル位置でクリックイベントを発生させます。</summary>
        public abstract Task MouseClickAsync();

        /// <summary>現在のカーソルを取得します。</summary>
        public abstract Task<Bgra32Image> GetCursorImageAsync();

        public abstract IObservable<string> LogStream { get; }
    }
}
