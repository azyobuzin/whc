using System;

namespace WagahighChoices.Kaoruko.Models
{
    public class Worker
    {
        public int Id { get; set; }

        public DateTimeOffset ConnectedAt { get; set; }

        public DateTimeOffset? DisconnectedAt { get; set; }

        public bool IsAlive => !this.DisconnectedAt.HasValue;
    }
}
