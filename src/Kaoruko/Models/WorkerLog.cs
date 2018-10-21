using System;
using SQLite;

namespace WagahighChoices.Kaoruko.Models
{
    public class WorkerLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int WorkerId { get; set; }

        [NotNull]
        public string Message { get; set; }

        public bool IsError { get; set; }

        public DateTimeOffset TimestampOnWorker { get; set; }

        public DateTimeOffset TimestampOnServer { get; set; }
    }
}
