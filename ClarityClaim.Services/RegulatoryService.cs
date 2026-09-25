using ClarityClaim.Domain.DTOs;

namespace ClarityClaim.Services;

// PS 35 -- Regulatory Intelligence (background/internal capability). Static mock for
// demo purposes; references the corrected LCD IDs (L39266, L36839), not the stale
// L34067/L35101 values from the original HLD draft.
public class RegulatoryService
{
    public IReadOnlyList<RegulatoryUpdate> GetUpdates() =>
    [
        new RegulatoryUpdate("L39266", "Cognitive Assessment",
            "Coverage criteria expanded — new dementia staging tools accepted",
            DateTime.UtcNow.AddHours(-6).ToString("O")),
        new RegulatoryUpdate("L36839", "Sleep Studies",
            "Documentation requirements tightened — accreditation proof required",
            DateTime.UtcNow.AddHours(-14).ToString("O"))
    ];
}
