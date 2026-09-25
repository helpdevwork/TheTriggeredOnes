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

public class ValidationServiceTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();
    private static IPipelinePersistenceRepository NoopPersistence() => Mock.Of<IPipelinePersistenceRepository>();

    private static string ScoreJson(int score) => $$"""
        {"approvalScore":{{score}},"gaps":[],"fixes":[],"passingCriteria":[]}
        """;

    private (Mock<ILanguageModelClient> lm, Mock<IPolicySearchService> vs) BuildMocks(int qwenScore, int gemmaScore)
    {
        var mockLm = new Mock<ILanguageModelClient>();
        mockLm.Setup(m => m.CompleteAsync(
                OllamaClient.Models.VALIDATION_PRIMARY, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScoreJson(qwenScore));
        mockLm.Setup(m => m.CompleteAsync(
                OllamaClient.Models.VALIDATION_SECONDARY, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScoreJson(gemmaScore));

        var mockVs = new Mock<IPolicySearchService>();
        mockVs.Setup(v => v.RetrieveAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync([new PolicyChunk("text", "LCD L34220 — Lumbar MRI", "L34220", 3)]);

        return (mockLm, mockVs);
    }

    // SVC-06 -- merged score = round(qwen*0.6 + gemma*0.4)
    [Fact]
    public async Task ValidateAsync_MergesScoresWithWeightedAverage()
    {
        var (lm, vs) = BuildMocks(qwenScore: 90, gemmaScore: 70);
        var svc = new ValidationService(lm.Object, vs.Object, EmptyConfig(), NullLogger<ValidationService>.Instance, NoopPersistence());

        var result = await svc.ValidateAsync(new ValidateDocumentRequest(new SoapNote(), ["M51.16"]));

        result.Result.ApprovalProbability.Should().Be((int)Math.Round(90 * 0.6 + 70 * 0.4));
        result.FromFallback.Should().BeFalse();
    }

    // SVC-07 -- policy citation sourced from L34220
    [Fact]
    public async Task ValidateAsync_CitesCorrectedLcdId()
    {
        var (lm, vs) = BuildMocks(80, 80);
        var svc = new ValidationService(lm.Object, vs.Object, EmptyConfig(), NullLogger<ValidationService>.Instance, NoopPersistence());

        var result = await svc.ValidateAsync(new ValidateDocumentRequest(new SoapNote(), ["M51.16"]));

        result.Result.PolicyReferences.Should().Contain(r => r.Contains("L34220"));
        result.Result.PolicyReferences.Should().NotContain(r => r.Contains("L35062"));
    }

    // SVC-09 -- disagreement flag triggers at >20 point delta
    [Fact]
    public async Task ValidateAsync_FlagsDisagreement_WhenDeltaExceeds20()
    {
        var (lm, vs) = BuildMocks(qwenScore: 90, gemmaScore: 60);
        var svc = new ValidationService(lm.Object, vs.Object, EmptyConfig(), NullLogger<ValidationService>.Instance, NoopPersistence());

        var result = await svc.ValidateAsync(new ValidateDocumentRequest(new SoapNote(), ["M51.16"]));

        result.Result.HumanReviewFlags.Should().NotBeEmpty();
    }

    // SVC-10 -- no disagreement flag within 20 points
    [Fact]
    public async Task ValidateAsync_NoDisagreementFlag_WhenDeltaWithin20()
    {
        var (lm, vs) = BuildMocks(qwenScore: 80, gemmaScore: 70);
        var svc = new ValidationService(lm.Object, vs.Object, EmptyConfig(), NullLogger<ValidationService>.Instance, NoopPersistence());

        var result = await svc.ValidateAsync(new ValidateDocumentRequest(new SoapNote(), ["M51.16"]));

        result.Result.HumanReviewFlags.Should().BeEmpty();
    }

    // SVC-11 / SVC-12 -- one or both models fail -> whole call falls back
    [Fact]
    public async Task ValidateAsync_OneModelFails_ServesFallback()
    {
        var mockLm = new Mock<ILanguageModelClient>();
        mockLm.Setup(m => m.CompleteAsync(
                OllamaClient.Models.VALIDATION_PRIMARY, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScoreJson(90));
        mockLm.Setup(m => m.CompleteAsync(
                OllamaClient.Models.VALIDATION_SECONDARY, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("gemma unreachable"));

        var mockVs = new Mock<IPolicySearchService>();
        mockVs.Setup(v => v.RetrieveAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync([]);

        var svc = new ValidationService(mockLm.Object, mockVs.Object, EmptyConfig(), NullLogger<ValidationService>.Instance, NoopPersistence());
        var result = await svc.ValidateAsync(new ValidateDocumentRequest(new SoapNote(), ["M51.16"]));

        result.FromFallback.Should().BeTrue();
    }

    // SVC-08 -- fallback gap requirement text says "4 weeks", not "6 weeks"
    [Fact]
    public async Task ValidateAsync_FallbackGapText_UsesFourWeeks()
    {
        var mockLm = new Mock<ILanguageModelClient>();
        mockLm.Setup(m => m.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        var mockVs = new Mock<IPolicySearchService>();
        mockVs.Setup(v => v.RetrieveAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync([]);

        var svc = new ValidationService(mockLm.Object, mockVs.Object, EmptyConfig(), NullLogger<ValidationService>.Instance, NoopPersistence());
        var result = await svc.ValidateAsync(new ValidateDocumentRequest(new SoapNote(), ["M51.16"]));

        result.Result.Gaps.Should().Contain(g => g.Requirement.Contains("4 weeks"));
        result.Result.Gaps.Should().NotContain(g => g.Requirement.Contains("6 weeks"));
    }
}
