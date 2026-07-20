namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Configuration for the PublishEvent submission handler.
/// </summary>
public class PublishEventOptions
{
    /// <summary>
    /// When false, the PublishEvent handler does nothing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Events to map and publish per submission. Handler runs once per submission but iterates over this array.
    /// </summary>
    public List<EventEntryOptions> Events { get; set; } = [];
}
