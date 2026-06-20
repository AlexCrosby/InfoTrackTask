using InfoTrackTask.Server.Models;
using InfoTrackTask.Server.Models.dto;
using InfoTrackTask.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfoTrackTask.Server.Tests;

public class ScraperTests
{
    [Fact]
    public void ParseHtml_ShouldExtractAllFieldsCorrectly_WhenHtmlIsValid()
    {
        // Arrange
        var service = new SolicitorScraper(new HttpClient(), NullLogger<SolicitorScraper>.Instance, null!);
        
        string sampleHtml = """
        <div class="result-section">
            <div class="result-item">
                <div class="top-holder">
                    <span class="h2">Birchall Blackburn<div class="greentick"></div></span>
                    <div class="phone-block">
                        <a href="tel:01612360662">0161 236 0662</a>
                    </div>
                </div>
                <a href="#" class="link-map"><address>14th Floor, Manchester, M1 4DZ</address></a>
                <ul class="list-item">
                    <li><a href="/enquiry-form.asp?SiD=53974">Email</a></li>
                    <li><a target="_blank" href="http://www.birchallblackburn.co.uk">Website</a></li>
                </ul>
            </div>
        </div>
        """;

        // Act
        IEnumerable<SolicitorRecord> results = service.ParseHtml(sampleHtml);

        // Assert
        var solicitorRecords = results.ToList();
        Assert.Single(solicitorRecords); // Should find exactly 1 record
        
        var record = solicitorRecords.First();
        Assert.Equal("Birchall Blackburn", record.Name);
        Assert.Equal("01612360662", record.PhoneNumber);
        Assert.Equal("14th Floor, Manchester, M1 4DZ", record.Address);
        Assert.Equal("http://www.birchallblackburn.co.uk", record.Website);
        Assert.Contains("enquiry-form.asp", record.Email);
    }

    [Fact]
    public void ParseHtml_ShouldHandleMessyAndUnclosedTags_WithoutCrashing()
    {
        // Arrange
        var service = new SolicitorScraper(new HttpClient(), NullLogger<SolicitorScraper>.Instance, null!);
        
        // This simulates the messy live web layouts (unclosed img tags, raw break lines)
        string brokenHtml = """
        <div class="result-item">
            <span class="h2">Messy Firm PLC</span>
            <img src="logo.png" alt="logo"> <br>
            <a href="tel:02071234567">0207 123 4567</a>
            <address>London Road</address>
        </div>
        """;

        // Act
        IEnumerable<SolicitorRecord> results = service.ParseHtml(brokenHtml);

        // Assert
        // Landmark tokenization shouldn't care about the unclosed <img> or <br> tags
        var solicitorRecords = results.ToList();
        Assert.Single(solicitorRecords);
        Assert.Equal("Messy Firm PLC", solicitorRecords.First().Name);
        Assert.Equal("02071234567", solicitorRecords.First().PhoneNumber);
        Assert.Equal("London Road", solicitorRecords.First().Address);
    }
}