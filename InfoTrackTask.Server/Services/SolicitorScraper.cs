using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using InfoTrackTask.Server.Data;
using InfoTrackTask.Server.Entities;
using InfoTrackTask.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace InfoTrackTask.Server.Services
{
    public class SolicitorScraper : ISolicitorScraper
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SolicitorScraper> _logger;
        private readonly DataContext _context; // Inject database context dependency

        internal SolicitorScraper(HttpClient httpClient, ILogger<SolicitorScraper> logger, DataContext context)
        {
            _httpClient = httpClient;
            _logger = logger;
            _context = context;

            _httpClient.DefaultRequestHeaders.Clear();

            // 1. Tell the server you are a real Windows user running modern Google Chrome
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // 2. Add human browser intent headers (Firewalls require these to accept connections)
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        }

        public async IAsyncEnumerable<SolicitorRecord> ScrapeSolicitorsAsync(List<string> locations, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Tracks unique records globally across cache loading and live scraper passes
            var seenRecords = new HashSet<string>();

            foreach (var loc in locations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string location = loc.Trim().ToLower();

                _logger.LogInformation("==================================================");
                _logger.LogInformation("[CACHE-ENGINE] Checking In-Memory SQLite database for: {Location}", location);

                // 1. FETCH CACHED DATA FIRST
                var cachedRecords = await _context.Solicitors
                    .Where(s => s.SearchLocation == location)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("[CACHE-ENGINE] Found {Count} matching rows in memory cache. Streaming instantly...", cachedRecords.Count);

                foreach (var entity in cachedRecords)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string signature = $"{entity.Name}|{entity.PhoneNumber}|{entity.Address}".ToLower().Replace(" ", "");
                    seenRecords.Add(signature);

                    // Map entity back to DTO record format for React frontend consumption
                    yield return new SolicitorRecord
                    {
                        Name = entity.Name,
                        PhoneNumber = entity.PhoneNumber,
                        Address = entity.Address,
                        Website = entity.Website,
                        Email = entity.Email
                    };
                }

                // 2. RUN CONTINUOUS LIVE SCRAPER ENGINE FOR ADDITIONAL DATA
                string targetUrl = $"conveyancing+{location}.html";
                int pass = 1;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger.LogInformation("[STREAM] [{Location}] Running Live Pass #{Pass}...", location, pass);

                    string htmlContent;
                    try
                    {
                        var response = await _httpClient.GetAsync(targetUrl, cancellationToken);
                        if (!response.IsSuccessStatusCode) break;
                        htmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("[STREAM] Network failure on Pass #{Pass}: {Message}", pass, ex.Message);
                        break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var parsedBatch = ParseHtml(htmlContent).ToList();

                    // ==== ADD THESE DIAGNOSTIC LOGS HERE ====
                    _logger.LogInformation("[DIAGNOSTIC] Raw HTML characters downloaded: {Length}", htmlContent.Length);
                    _logger.LogInformation("[DIAGNOSTIC] ParseHtml extracted {Count} raw items from the page.", parsedBatch.Count());
                    // ========================================
                    bool discoveredNewItemsInThisPass = false;
                    int newItemsCounter = 0;

                    foreach (var record in parsedBatch)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string signature = $"{record.Name}|{record.PhoneNumber}|{record.Address}".ToLower().Replace(" ", "");

                        // If it's NOT in our seenRecords pool, it's a completely new layout entry
                        if (seenRecords.Add(signature))
                        {
                            discoveredNewItemsInThisPass = true;
                            newItemsCounter++;

                            // Map the newly discovered row to database entity and add to context tracking
                            var newEntity = new SolicitorRecordEntity
                            {
                                SearchLocation = location,
                                Name = record.Name,
                                PhoneNumber = record.PhoneNumber,
                                Address = record.Address,
                                Website = record.Website,
                                Email = record.Email
                            };
                            _context.Solicitors.Add(newEntity);

                            // Stream fresh row to React client instantly
                            yield return record;
                        }
                    }

                    // Commit fresh records into the SQLite RAM engine context state
                    if (newItemsCounter > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await _context.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("[CACHE-ENGINE] Saved {Count} newly discovered records to In-Memory database.", newItemsCounter);
                    }

                    // If less than 75 results, there won't be any additional results on refresh.
                    if (parsedBatch.Count() < 75)
                    {
                        _logger.LogInformation("[STREAM] Page returned {Count} items (pool exhausted — under 75 max). Scraper finishing.", parsedBatch.Count);
                        break;
                    }

                    // Infinite loop termination rule: Exit instantly when data saturation occurs
                    if (!discoveredNewItemsInThisPass)
                    {
                        _logger.LogWarning("[STREAM] Zero new rows detected. Scraper fully exhausted for {Location}.", location);
                        break;
                    }

                    pass++;
                    await Task.Delay(350, cancellationToken); // Respectful crawl delay
                }
            }
        }

        public IEnumerable<SolicitorRecord> ParseHtml(string htmlContent)
        {
            //Split the string into raw chunks by the "result-item" class.
            string[] rawBlocks = htmlContent.Split(["class=\"result-item"], StringSplitOptions.None);

            // Use LINQ to process and filter the blocks cleanly
            var results = rawBlocks
                .Skip(1) // Skip the first block as it contains the HTML headers
                .Select(ExtractFields)
                .Where(record => record != null)
                .Cast<SolicitorRecord>()
                .ToList();
            return results;
        }

        private static SolicitorRecord? ExtractFields(string block)
        {
            try
            {
                // Extract Name: Look for text inside <span class="h2">
                // We strip out any inner tags by matching up to the first '<'
                var nameMatch = Regex.Match(block, @"<span class=""h2"">([^<]+)", RegexOptions.IgnoreCase);
                string name = nameMatch.Success ? nameMatch.Groups[1].Value : string.Empty;

                //Extract Phone: Look for the digits inside href="tel:..."
                var phoneMatch = Regex.Match(block, @"href=""tel:([^""]+)""", RegexOptions.IgnoreCase);
                string phone = phoneMatch.Success ? phoneMatch.Groups[1].Value : string.Empty;

                // Extract Address: Look for everything inside <address>
                var addressMatch = Regex.Match(block, @"<address>([^<]+)</address>", RegexOptions.IgnoreCase);
                string address = addressMatch.Success ? addressMatch.Groups[1].Value : string.Empty;

                // Extract Website: Look for a href that belongs to a link containing the globe icon
                // We capture the href directly inside the <a> tag that features the globe class
                var websiteMatch = Regex.Match(block, @"<a[^>]*href=""([^""]+)""[^>]*><i[^>]*fa-globe",
                    RegexOptions.IgnoreCase);
                string website = websiteMatch.Success ? websiteMatch.Groups[1].Value : string.Empty;

                // Fallback for website if the structural layout varies slightly
                if (string.IsNullOrEmpty(website))
                {
                    var altWebMatch = Regex.Match(block, @"href=""([^""]+)""[^>]*>([^<]*Website)", RegexOptions.IgnoreCase);
                    website = altWebMatch.Success ? altWebMatch.Groups[1].Value : string.Empty;
                }

                // Extract Email Link: Look for the href of the enquiry form link
                var emailMatch = Regex.Match(block, @"href=""([^""]*enquiry-form\.asp[^""]+)""", RegexOptions.IgnoreCase);
                string emailLink = emailMatch.Success ? emailMatch.Groups[1].Value : string.Empty;

                if (!string.IsNullOrEmpty(emailLink) && emailLink.StartsWith("/"))
                {
                    emailLink = "https://www.solicitors.com" + emailLink;
                }

                // If we can't find a name, skip this item entirely
                if (string.IsNullOrWhiteSpace(name)) return null;

                return new SolicitorRecord
                {
                    Name = name.Trim(),
                    PhoneNumber = phone.Replace("tel:", ""),
                    Address = address.Replace("&nbsp", " ").Trim(),
                    Website = website.Trim(),
                    Email = emailLink.Trim()
                };
            }
            catch
            {
                return null; // Gracefully drop single bad entries
            }
        }
    }
}