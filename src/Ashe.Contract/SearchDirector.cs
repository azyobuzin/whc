using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WagahighChoices.Ashe
{
    public abstract class SearchDirector : IDisposable
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

        /// <summary>
        /// 新しい指示をもらいます。
        /// </summary>
        public abstract Task<SeekDirectionResult> SeekDirectionAsync();

        /// <summary>
        /// 探索結果をレポートします。
        /// </summary>
        /// <param name="jobId"><see cref="SeekDirectionResult.JobId"/></param>
        /// <param name="heroine">誰のルートに至ったか</param>
        /// <param name="selectionIds">通過した選択画面のリスト</param>
        public abstract Task ReportResultAsync(Guid jobId, Heroine heroine, IReadOnlyList<int> selectionIds);

        /// <summary>
        /// ログを送信します。
        /// </summary>
        public abstract Task LogAsync(string message, bool isError, DateTimeOffset timestamp);
    }
}
