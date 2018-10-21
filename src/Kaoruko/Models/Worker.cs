using System;
using SQLite;

namespace WagahighChoices.Kaoruko.Models
{
    public class Worker
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Unique, NotNull]
        public string ConnectionId { get; set; }

        public string HostName { get; set; }

        public DateTimeOffset ConnectedAt { get; set; }

        public DateTimeOffset? DisconnectedAt { get; set; }

        [Ignore]
        public bool IsAlive => !this.DisconnectedAt.HasValue;
    }
}
