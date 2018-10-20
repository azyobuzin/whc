using System;

namespace WagahighChoices.Kaoruko.Models
{
    public class WorkerJob
    {
        public Guid Id { get; set; }

        /// <summary>
        /// 選ぶべき選択肢（<see cref="ChoiceAction"/>）の数値をカンマ区切りにしたもの
        /// </summary>
        public string Choices { get; set; }

        public int? WorkerId { get; set; }

        public Worker Worker { get; set; }

        public int? SearchResultId { get; set; }

        public SearchResult SearchResult { get; set; }

        public DateTimeOffset EnqueuedAt { get; set; }
    }
}
