using System.Threading;
using InfoTrackTask.Server.Models;
using InfoTrackTask.Server.Models.dto;
using InfoTrackTask.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace InfoTrack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SolicitorsController : ControllerBase
{
    private readonly ISolicitorScraper _scraperService;

    public SolicitorsController(ISolicitorScraper scraperService)
    {
        _scraperService = scraperService;
    }

    [HttpGet("stream")]
    public IAsyncEnumerable<SolicitorRecord> GetSolicitorsStream([FromQuery] string locations, CancellationToken cancellationToken)
    {
        var locationList = locations.Split(',').Select(l => l.Trim()).ToList();

        // .NET automatically optimizes IAsyncEnumerable endpoints to stream chunked JSON payloads to the browser
        return _scraperService.ScrapeSolicitorsAsync(locationList, cancellationToken);
    }
}