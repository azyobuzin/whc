using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WagahighChoices.Ashe;
using WagahighChoices.Kaoruko.Models;

namespace WagahighChoices.Kaoruko.Pages
{
    public class ResultsModel : PageModel
    {
        private readonly SearchStatusRepository _repository;

        public ResultsModel(SearchStatusRepository repository)
        {
            this._repository = repository;
        }

        public IReadOnlyDictionary<Heroine, int> RouteCount { get; set; }
        public IReadOnlyDictionary<(int, ChoiceAction), IReadOnlyDictionary<Heroine, double>> RouteProbabilities { get; set; }
        public IReadOnlyList<int> AvailableSelections { get; set; }

        public void OnGet()
        {
            this.RouteCount = this._repository.GetRouteCountByHeroine();

            var results = this._repository.GetSearchResults();
            this.RouteProbabilities = results
                .SelectMany(result => ModelUtils.ParseSelections(result.Selections)
                    .Zip(
                        ModelUtils.ParseChoices(result.Choices),
                        (selection, choice) => new { Selection = selection, Choice = choice, result.Heroine }
                    ))
                .GroupBy(
                    x => (x.Selection, x.Choice),
                    (key, values) =>
                    {
                        var valuesArray = values.ToArray();
                        var dic = valuesArray.GroupBy(x => x.Heroine)
                            .ToDictionary(x => x.Key, x => (double)x.Count() / valuesArray.Length * 100.0);
                        return (key.Selection, key.Choice, dic);
                    })
                .ToDictionary(x => (x.Selection, x.Choice), x => (IReadOnlyDictionary<Heroine, double>)x.dic);

            this.AvailableSelections = this.RouteProbabilities
                .Select(x => x.Key.Item1)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }

        public string GetProbability(int selectionId, ChoiceAction choice, Heroine heroine)
        {
            var p = this.RouteProbabilities
                .GetValueOrDefault((selectionId, choice))
                ?.GetValueOrDefault(heroine);
            return $"{p:F1} %";
        }
    }
}
