namespace OddSnap.Helpers;

/// <summary>
/// Fluent-style 20x20 vector paths for the three recording target actions.
/// These stay separate from the generic capture icons so area, monitor, and
/// window recording remain visually distinct at every DPI scale.
/// </summary>
internal static class RecordingTargetIconData
{
    internal static readonly IReadOnlyDictionary<string, (string Regular, string Filled)> Icons =
        new Dictionary<string, (string Regular, string Filled)>(StringComparer.Ordinal)
        {
            ["_record"] = (
                "M3 3h5v1H4v4H3V3Zm9 0h5v5h-1V4h-4V3ZM3 12h1v4h4v1H3v-5Zm13 0h1v5h-5v-1h4v-4Zm-6-4.25a2.25 2.25 0 1 1 0 4.5 2.25 2.25 0 0 1 0-4.5Zm0 1a1.25 1.25 0 1 0 0 2.5 1.25 1.25 0 0 0 0-2.5Z",
                "M2.75 2.5h5.5v1.5H4v4.25H2.5v-5.5c0-.14.11-.25.25-.25Zm9 0h5.5c.14 0 .25.11.25.25v5.5H16V4h-4.25V2.5ZM2.5 11.75H4V16h4.25v1.5h-5.5a.25.25 0 0 1-.25-.25v-5.5Zm13.5 0h1.5v5.5a.25.25 0 0 1-.25.25h-5.5V16H16v-4.25ZM10 7.25a2.75 2.75 0 1 1 0 5.5 2.75 2.75 0 0 1 0-5.5Z"),
            ["_recordMonitor"] = (
                "M4.5 3h11A2.5 2.5 0 0 1 18 5.5v7a2.5 2.5 0 0 1-2.5 2.5H11v1.5h2.5a.5.5 0 0 1 0 1h-7a.5.5 0 0 1 0-1H9V15H4.5A2.5 2.5 0 0 1 2 12.5v-7A2.5 2.5 0 0 1 4.5 3Zm0 1A1.5 1.5 0 0 0 3 5.5v7A1.5 1.5 0 0 0 4.5 14h11a1.5 1.5 0 0 0 1.5-1.5v-7A1.5 1.5 0 0 0 15.5 4h-11Zm8.75 2.5a2.25 2.25 0 1 1 0 4.5 2.25 2.25 0 0 1 0-4.5Zm0 1a1.25 1.25 0 1 0 0 2.5 1.25 1.25 0 0 0 0-2.5Z",
                "M4.5 2.5h11A2.5 2.5 0 0 1 18 5v7.5a2.5 2.5 0 0 1-2.5 2.5H11v1.25h2.75a.75.75 0 0 1 0 1.5h-7.5a.75.75 0 0 1 0-1.5H9V15H4.5A2.5 2.5 0 0 1 2 12.5V5a2.5 2.5 0 0 1 2.5-2.5Zm8.75 3.25a2.75 2.75 0 1 0 0 5.5 2.75 2.75 0 0 0 0-5.5Z"),
            ["_recordWindow"] = (
                "M4.5 3h11A2.5 2.5 0 0 1 18 5.5v9a2.5 2.5 0 0 1-2.5 2.5h-11A2.5 2.5 0 0 1 2 14.5v-9A2.5 2.5 0 0 1 4.5 3Zm0 1A1.5 1.5 0 0 0 3 5.5V7h14V5.5A1.5 1.5 0 0 0 15.5 4h-11ZM3 8v6.5A1.5 1.5 0 0 0 4.5 16h11a1.5 1.5 0 0 0 1.5-1.5V8H3Zm10.25 1.5a2.25 2.25 0 1 1 0 4.5 2.25 2.25 0 0 1 0-4.5Zm0 1a1.25 1.25 0 1 0 0 2.5 1.25 1.25 0 0 0 0-2.5ZM4.5 5a.5.5 0 1 1 0 1 .5.5 0 0 1 0-1Zm2 0a.5.5 0 1 1 0 1 .5.5 0 0 1 0-1Z",
                "M4.5 2.5h11A2.5 2.5 0 0 1 18 5v9.5a2.5 2.5 0 0 1-2.5 2.5h-11A2.5 2.5 0 0 1 2 14.5V5a2.5 2.5 0 0 1 2.5-2.5ZM3.5 7h13V5A1 1 0 0 0 15.5 4h-11a1 1 0 0 0-1 1v2Zm9.75 2a2.75 2.75 0 1 0 0 5.5 2.75 2.75 0 0 0 0-5.5Z")
        };

    internal static bool TryGetIcon(string id, out (string Regular, string Filled) icon) =>
        Icons.TryGetValue(id, out icon);
}
