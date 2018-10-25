using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WagahighChoices.Ashe;
using WagahighChoices.Kaoruko.Models;

namespace WagahighChoices.Kaoruko.Pages.Workers
{
    public class DetailsModel : PageModel
    {
        private readonly SearchStatusRepository _repository;

        public DetailsModel(SearchStatusRepository repository)
        {
            this._repository = repository;
        }

        public Worker Worker { get; set; }
        public int CompletedJobCount { get; set; }
        public IReadOnlyList<ChoiceAction> CurrentJob { get; set; }
        public IReadOnlyList<WorkerLog> Logs { get; set; }

        public IActionResult OnGet(int id)
        {
            this.Worker = this._repository.GetWorkerById(id);

            if (this.Worker == null)
                return this.NotFound();

            this.CompletedJobCount = this._repository.CountCompletedJobsByWorker(id);

            var job = this._repository.GetJobByWorker(id);
            if (job != null)
                this.CurrentJob = ModelUtils.ParseChoices(job.Choices);

            this.Logs = this._repository.GetLogsByWorker(id);

            return this.Page();
        }
    }
}
