using System;
using SQLite;

namespace WagahighChoices.Kaoruko.Models
{
    public class WorkerScreenshot
    {
        // ワーカーの最新のスクリーンショットだけ保持
        [PrimaryKey]
        public int WorkerId { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public byte[] Data { get; set; }

        public DateTimeOffset TimestampOnWorker { get; set; }

        public DateTimeOffset TimestampOnServer { get; set; }
    }
}
