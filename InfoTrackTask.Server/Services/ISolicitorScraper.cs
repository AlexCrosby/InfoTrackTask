using InfoTrackTask.Server.Models;

namespace InfoTrackTask.Server.Services;

public interface ISolicitorScraper
{
    IAsyncEnumerable<SolicitorRecord> ScrapeSolicitorsAsync(List<string> locations, CancellationToken cancellationToken = default);
}