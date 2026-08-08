using Consular.Api.Services;
using Xunit;

namespace Consular.Api.Tests;

public class DemandeWorkflowRulesTests
{
    [Theory]
    [InlineData("SUBMITTED", "begin-review", true, "UNDER_REVIEW")]
    [InlineData("SUBMITTED", "reject", true, "REJECTED")]
    [InlineData("UNDER_REVIEW", "approve", true, "APPROVED")]
    [InlineData("UNDER_REVIEW", "mark-missing-documents", true, "MISSING_DOCUMENTS")]
    [InlineData("MISSING_DOCUMENTS", "reject", true, "REJECTED")]
    [InlineData("MISSING_DOCUMENTS", "submit-documents", true, "DOCUMENTS_RECEIVED")]
    [InlineData("DOCUMENTS_RECEIVED", "approve", true, "COLLECTED")]
    [InlineData("DOCUMENTS_RECEIVED", "reject", true, "REJECTED")]
    [InlineData("REJECTED", "appeal", true, "APPEAL_REVIEW")]
    [InlineData("APPEAL_REVIEW", "approve", true, "COLLECTED")]
    [InlineData("APPEAL_REVIEW", "reject", true, "REJECTED")]
    [InlineData("APPROVED", "collect", true, "COLLECTED")]
    [InlineData("SUBMITTED", "approve", false, null)]
    public void TryGetTransition_ResolvesExpectedNextState(
        string currentStatutCode, string action, bool expectedSuccess, string? expectedNextStatut)
    {
        var success = DemandeWorkflowRules.TryGetTransition(currentStatutCode, action, out var nextStatut);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedNextStatut, nextStatut);
    }

    [Theory]
    [InlineData("COLLECTED", "collect")]
    [InlineData("COLLECTED", "appeal")]
    [InlineData("REJECTED", "reject")]
    [InlineData("REJECTED", "begin-review")]
    [InlineData("UNDER_REVIEW", "reject")]
    public void TryGetTransition_RejectsIllegalActions(string currentStatutCode, string action)
    {
        var success = DemandeWorkflowRules.TryGetTransition(currentStatutCode, action, out _);

        Assert.False(success);
    }
}
