using System.Threading;
using InfoTrackTask.Server.Models;
using InfoTrackTask.Server.Models.dto;

namespace InfoTrackTask.Server.Services;

public interface ISolicitorScraper
{
    IAsyncEnumerable<SolicitorRecord> ScrapeSolicitorsAsync(List<string> locations, CancellationToken cancellationToken = default);
}