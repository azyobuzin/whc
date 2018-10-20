using System;
using System.ComponentModel.DataAnnotations;

namespace WagahighChoices.Kaoruko.Models
{
    public class WorkerLog
    {
        public int Id { get; set; }

        public int WorkerId { get; set; }

        public Worker Worker { get; set; }

        [Required]
        public string Message { get; set; }

        public bool IsError { get; set; }

        public DateTimeOffset TimestampOnWorker { get; set; }

        public DateTimeOffset TimestampOnServer { get; set; }
    }
}
