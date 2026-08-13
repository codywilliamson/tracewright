namespace Tracewright.Core;

/// <summary>
/// short display form of an event id: first 4 chars + ellipsis + last 2 (spec §7 example "01J9…4F").
/// </summary>
public static class ShortEventId
{
    public static string Of(string eventId) =>
        eventId.Length <= 6 ? eventId : $"{eventId[..4]}…{eventId[^2..]}";
}
