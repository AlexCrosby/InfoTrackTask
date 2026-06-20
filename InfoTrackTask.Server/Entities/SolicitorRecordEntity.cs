namespace InfoTrackTask.Server.Entities;

public class SolicitorRecordEntity
{
    public int Id { get; set; }
    public string SearchLocation { get; set; } = string.Empty; // Used for cache lookups
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}