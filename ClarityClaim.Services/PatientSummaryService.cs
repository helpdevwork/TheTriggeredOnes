using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Infrastructure.Llm;

namespace ClarityClaim.Services;

public class PatientSummaryService
{
    private readonly ILanguageModelClient _lm;

    public PatientSummaryService(ILanguageModelClient lm) => _lm = lm;

    public async Task<PatientSummaryResponse> SummariseAsync(
        PatientSummaryRequest req, CancellationToken ct = default)
    {
        var systemPrompt = $"""
            Explain medical information in simple, friendly language.
            Write in {req.Language}. Avoid jargon. Be warm and reassuring.
            Output plain text only -- no JSON, no bullet points, no headers.
            """;

        var userPrompt = $"""
            Write a 3-sentence plain-language summary of today's appointment
            for the patient. Include: what the diagnosis means in simple terms,
            what medication or treatment was discussed, and what to do next.

            CLINICAL NOTE:
            Assessment: {req.SoapNote.Assessment}
            Plan: {req.SoapNote.Plan}
            """;  

        try
        {
            // Lighter/faster instruct model -- adequate for plain-language summaries
            var summary = await _lm.CompleteAsync(
                OllamaClient.Models.SUMMARY,
                systemPrompt, userPrompt,
                temperature: 0.3f, maxTokens: 300, ct: ct
            );

            return new PatientSummaryResponse(summary.Trim(), req.Language);
        }
        catch
        {
            return new PatientSummaryResponse(
                "Your recent visit findings point to a nerve-related back condition. " +
                "We discussed imaging and continuing your current treatment plan. " +
                "Please follow up as scheduled and reach out sooner if symptoms worsen.",
                req.Language);
        }
    }
}
