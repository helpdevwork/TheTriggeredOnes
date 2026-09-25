using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClarityClaim.API.Controllers;

// Backs the doctor's "Send for Review Team" hand-off and the reviewer's tab
// that opens from it -- validation runs on the review side, not the ordering
// physician's, matching how medical-necessity sign-off actually works.
[ApiController]
[Route("api")]
public class ReviewTeamController : ControllerBase
{
    private readonly IReviewTeamRepository _reviewTeam;
    private readonly IPipelinePersistenceRepository _persistence;

    public ReviewTeamController(IReviewTeamRepository reviewTeam, IPipelinePersistenceRepository persistence)
    {
        _reviewTeam = reviewTeam;
        _persistence = persistence;
    }

    // ── GET /api/review-team ─────────────────────────────────
    [HttpGet("review-team")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var reviewers = await _reviewTeam.ListAsync(ct);
        return Ok(reviewers);
    }

    // ── PUT /api/soap-notes/{id} -- persist the doctor's final edits before hand-off ──
    [HttpPut("soap-notes/{soapNoteId:int}")]
    public async Task<IActionResult> UpdateSoapNote(int soapNoteId, [FromBody] UpdateSoapNoteRequest req, CancellationToken ct)
    {
        await _persistence.UpdateSoapNoteAsync(soapNoteId, req.SoapNote, ct);
        return NoContent();
    }

    // ── POST /api/visits/{id}/assign-reviewer ─────────────────
    [HttpPost("visits/{visitId:int}/assign-reviewer")]
    public async Task<IActionResult> AssignReviewer(int visitId, [FromBody] AssignReviewerRequest req, CancellationToken ct)
    {
        await _reviewTeam.AssignReviewerAsync(visitId, req.ReviewTeamMemberId, ct);
        return NoContent();
    }

    // ── GET /api/visits/{id}/review -- what the reviewer's tab loads ────
    [HttpGet("visits/{visitId:int}/review")]
    public async Task<IActionResult> GetReviewContext(int visitId, CancellationToken ct)
    {
        var context = await _reviewTeam.GetReviewContextAsync(visitId, ct);
        return context is null ? NotFound() : Ok(context);
    }
}
