using System;
using SQLite;
using WagahighChoices.Ashe;

namespace WagahighChoices.Kaoruko.Models
{
    public class SearchResult
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// 出会った選択肢の <see cref="SelectionInfo.Id"/> をカンマ区切りにしたもの
        /// </summary>
        [NotNull]
        public string Selections { get; set; }

        /// <summary>
        /// 選んだ選択肢（<see cref="ChoiceAction"/>）の数値をカンマ区切りにしたもの
        /// </summary>
        [NotNull, Unique]
        public string Choices { get; set; }

        public Heroine Heroine { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }
}
