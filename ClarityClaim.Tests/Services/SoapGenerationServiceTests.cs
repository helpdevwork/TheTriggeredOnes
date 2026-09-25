using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using ClarityClaim.Infrastructure.Llm;
using ClarityClaim.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ClarityClaim.Tests.Services;

public class SoapGenerationServiceTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();
    private static IPipelinePersistenceRepository NoopPersistence() => Mock.Of<IPipelinePersistenceRepository>();

    // SVC-01 / SVC-02 -- happy path, correct ICD-10 specificity
    [Fact]
    public async Task GenerateAsync_HappyPath_ParsesSoapNoteAndUsesSpecificIcdCode()
    {
        var mockLm = new Mock<ILanguageModelClient>();
        mockLm.Setup(m => m.CompleteAsync(
                OllamaClient.Models.CLINICAL_REASONING, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {"subjective":"s","objective":"o","assessment":"a","plan":"p",
                 "icd10Codes":[{"code":"M51.16","description":"Disc disorder with radiculopathy","confidence":0.9}],
                 "cptCodes":[],"clinicalReasoning":"r"}
                """);

        var svc = new SoapGenerationService(mockLm.Object, EmptyConfig(), NullLogger<SoapGenerationService>.Instance, NoopPersistence());
        var result = await svc.GenerateAsync(new GenerateSoapRequest("transcript", new FhirPatientContext()));

        result.FromFallback.Should().BeFalse();
        result.SoapNote.Icd10Codes.Should().Contain(c => c.Code == "M51.16");
        result.SoapNote.Icd10Codes.Should().NotContain(c => c.Code == "M54.5");
    }

    // SVC-03 -- markdown-fenced JSON parses correctly
    [Fact]
    public async Task GenerateAsync_StripsMarkdownFences()
    {
        var mockLm = new Mock<ILanguageModelClient>();
        mockLm.Setup(m => m.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("```json\n{\"subjective\":\"s\",\"objective\":\"o\",\"assessment\":\"a\",\"plan\":\"p\",\"icd10Codes\":[],\"cptCodes\":[],\"clinicalReasoning\":\"r\"}\n```");

        var svc = new SoapGenerationService(mockLm.Object, EmptyConfig(), NullLogger<SoapGenerationService>.Instance, NoopPersistence());
        var result = await svc.GenerateAsync(new GenerateSoapRequest("transcript", new FhirPatientContext()));

        result.FromFallback.Should().BeFalse();
        result.SoapNote.Subjective.Should().Be("s");
    }

    // SVC-04 -- LLM throws -> fallback served, no exception escapes
    [Fact]
    public async Task GenerateAsync_LlmThrows_ReturnsFallback()
    {
        var mockLm = new Mock<ILanguageModelClient>();
        mockLm.Setup(m => m.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var svc = new SoapGenerationService(mockLm.Object, EmptyConfig(), NullLogger<SoapGenerationService>.Instance, NoopPersistence());
        var result = await svc.GenerateAsync(new GenerateSoapRequest("transcript", new FhirPatientContext()));

        result.FromFallback.Should().BeTrue();
        result.SoapNote.Should().NotBeNull();
        result.SoapNote.Icd10Codes.Should().NotBeEmpty();
    }
}
