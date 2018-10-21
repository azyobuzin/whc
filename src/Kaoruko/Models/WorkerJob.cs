using System;
using SQLite;

namespace WagahighChoices.Kaoruko.Models
{
    public class WorkerJob
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        /// <summary>
        /// 選ぶべき選択肢（<see cref="ChoiceAction"/>）の数値をカンマ区切りにしたもの
        /// </summary>
        [Unique]
        public string Choices { get; set; }

        public int? WorkerId { get; set; }

        public int? SearchResultId { get; set; }

        public DateTimeOffset EnqueuedAt { get; set; }
    }
}
