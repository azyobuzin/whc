using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WagahighChoices.Kaoruko.Pages.Workers
{
    public class IndexModel : PageModel
    {
        private readonly SearchStatusRepository _repository;

        public IndexModel(SearchStatusRepository repository)
        {
            this._repository = repository;
        }

        public IReadOnlyList<WorkerSummary> WorkerSummaries { get; set; }

        public void OnGet()
        {
            this.WorkerSummaries = this._repository.GetWorkerSummaries(false);
        }
    }
}
