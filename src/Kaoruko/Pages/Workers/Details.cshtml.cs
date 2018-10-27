using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
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
        public bool HasMore { get; set; }
        public string ScreenshotUri { get; set; }
        public DateTimeOffset ScreenshotTimestamp { get; set; }

        public IActionResult OnGet(int id, bool all = false)
        {
            this.Worker = this._repository.GetWorkerById(id);

            if (this.Worker == null)
                return this.NotFound();

            this.CompletedJobCount = this._repository.CountCompletedJobsByWorker(id);

            var job = this._repository.GetJobByWorker(id);
            if (job != null)
                this.CurrentJob = ModelUtils.ParseChoices(job.Choices);

            if (all)
            {
                this.Logs = this._repository.GetLogsByWorker(id);
            }
            else
            {
                this.Logs = this._repository.GetLogsByWorker(id, 100, out var hasMore);
                this.HasMore = hasMore;
            }

            var screenshot = this._repository.GetScreenshotByWorker(id);
            if (screenshot != null)
            {
                this.ScreenshotUri = CreateScreenshotUri(screenshot);
                this.ScreenshotTimestamp = screenshot.TimestampOnWorker;
            }

            return this.Page();
        }

        private static string CreateScreenshotUri(WorkerScreenshot screenshot)
        {
            ArraySegment<byte> buffer;

            using (var ms = new MemoryStream())
            {
                using (var image = Image.LoadPixelData<Bgra32>(screenshot.Data, screenshot.Width, screenshot.Height))
                    image.Save(ms, new PngEncoder() { ColorType = PngColorType.Rgb });

                if (!ms.TryGetBuffer(out buffer))
                    buffer = ms.ToArray();
            }

            var base64 = Convert.ToBase64String(buffer);
            return "data:image/png;base64," + base64;
        }
    }
}
