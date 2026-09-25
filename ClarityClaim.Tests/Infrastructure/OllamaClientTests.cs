using ClarityClaim.Infrastructure.Llm;
using FluentAssertions;
using Xunit;

namespace ClarityClaim.Tests.Infrastructure;

// INF-04 -- No phi-2 reachable via Models constants (BP-06)
public class OllamaClientTests
{
    [Fact]
    public void Models_DoesNotExposeExcludedPhi2Id()
    {
        var fields = typeof(OllamaClient.Models)
            .GetFields()
            .Select(f => (string)f.GetValue(null)!);

        fields.Should().NotContain(id => id.Contains("phi-2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Models_ContainsAllFourRoleIds()
    {
        OllamaClient.Models.CLINICAL_REASONING.Should().Be("gpt-oss:20b");
        OllamaClient.Models.VALIDATION_PRIMARY.Should().Be("granite3.3:latest");
        OllamaClient.Models.VALIDATION_SECONDARY.Should().Be("llama3:latest");
        OllamaClient.Models.SUMMARY.Should().Be("mistral:7b-instruct");
    }
}
