using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WagahighChoices.Ashe;

namespace WagahighChoices.Kaoruko.Pages
{
    public class IndexModel : PageModel
    {
        private readonly SearchStatusRepository _repository;

        public IndexModel(SearchStatusRepository repository)
        {
            this._repository = repository;
        }

        public IReadOnlyDictionary<Heroine, int> RouteCount { get; set; }
        public WorkerJobStatistics WorkerJobStatistics { get; set; }
        public IReadOnlyList<WorkerSummary> WorkerSummaries { get; set; }

        public void OnGet()
        {
            this.RouteCount = this._repository.GetRouteCountByHeroine();
            this.WorkerJobStatistics = this._repository.GetWorkerJobStatistics();
            this.WorkerSummaries = this._repository.GetWorkerSummaries(true);
        }
    }
}
