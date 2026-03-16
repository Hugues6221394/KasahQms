using FluentAssertions;
using KasahQMS.Domain.Entities.Capa;
using KasahQMS.Domain.Enums;
using Xunit;

namespace KasahQMS.Tests.Unit.Domain.Entities;

public class CapaTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _createdById = Guid.NewGuid();

    private Capa CreateCapa()
    {
        return Capa.Create(_tenantId, "Test CAPA", "CAPA-001",
            CapaType.Corrective, CapaPriority.High, _createdById);
    }

    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var capa = CreateCapa();

        capa.Id.Should().NotBeEmpty();
        capa.TenantId.Should().Be(_tenantId);
        capa.Title.Should().Be("Test CAPA");
        capa.CapaNumber.Should().Be("CAPA-001");
        capa.CapaType.Should().Be(CapaType.Corrective);
        capa.Priority.Should().Be(CapaPriority.High);
        capa.Status.Should().Be(CapaStatus.Draft);
        capa.CreatedById.Should().Be(_createdById);
        capa.Actions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void FullLifecycle_ShouldTransitionThroughAllStates()
    {
        var capa = CreateCapa();
        var verifierId = Guid.NewGuid();

        capa.StartInvestigation().Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.UnderInvestigation);

        capa.DefineActions().Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.ActionsDefined);

        capa.ImplementActions("Implemented all actions").Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.ActionsImplemented);

        capa.VerifyEffectiveness(verifierId, "Verified effective", true).Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.EffectivenessVerified);

        capa.Close().Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.Closed);
        capa.ActualCompletionDate.Should().NotBeNull();
    }

    [Fact]
    public void StartInvestigation_FromDraft_ShouldSucceed()
    {
        var capa = CreateCapa();

        capa.StartInvestigation().Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.UnderInvestigation);
    }

    [Fact]
    public void StartInvestigation_FromNonDraft_ShouldReturnFalse()
    {
        var capa = CreateCapa();
        capa.StartInvestigation();

        capa.StartInvestigation().Should().BeFalse();
        capa.Status.Should().Be(CapaStatus.UnderInvestigation);
    }

    [Fact]
    public void DefineActions_FromUnderInvestigation_ShouldSucceed()
    {
        var capa = CreateCapa();
        capa.StartInvestigation();

        capa.DefineActions().Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.ActionsDefined);
    }

    [Fact]
    public void DefineActions_FromDraft_ShouldReturnFalse()
    {
        var capa = CreateCapa();

        capa.DefineActions().Should().BeFalse();
        capa.Status.Should().Be(CapaStatus.Draft);
    }

    [Fact]
    public void ImplementActions_FromActionsDefined_ShouldSucceedAndSetNotes()
    {
        var capa = CreateCapa();
        capa.StartInvestigation();
        capa.DefineActions();

        capa.ImplementActions("All actions done").Should().BeTrue();

        capa.Status.Should().Be(CapaStatus.ActionsImplemented);
        capa.ImplementationNotes.Should().Be("All actions done");
    }

    [Fact]
    public void ImplementActions_FromDraft_ShouldReturnFalse()
    {
        var capa = CreateCapa();

        capa.ImplementActions().Should().BeFalse();
        capa.Status.Should().Be(CapaStatus.Draft);
    }

    [Fact]
    public void VerifyEffectiveness_WhenVerifierIsCreator_ShouldReturnFalse()
    {
        var capa = CreateCapa();
        capa.StartInvestigation();
        capa.DefineActions();
        capa.ImplementActions();

        capa.VerifyEffectiveness(_createdById, "Self-verify", true).Should().BeFalse();
        capa.Status.Should().Be(CapaStatus.ActionsImplemented);
    }

    [Fact]
    public void VerifyEffectiveness_WithDifferentVerifier_ShouldSucceed()
    {
        var capa = CreateCapa();
        capa.StartInvestigation();
        capa.DefineActions();
        capa.ImplementActions();
        var verifierId = Guid.NewGuid();

        capa.VerifyEffectiveness(verifierId, "Effective", true).Should().BeTrue();

        capa.Status.Should().Be(CapaStatus.EffectivenessVerified);
        capa.VerifiedById.Should().Be(verifierId);
        capa.VerificationNotes.Should().Be("Effective");
        capa.IsEffective.Should().BeTrue();
        capa.VerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void VerifyEffectiveness_FromWrongStatus_ShouldReturnFalse()
    {
        var capa = CreateCapa();
        var verifierId = Guid.NewGuid();

        capa.VerifyEffectiveness(verifierId, "Notes", true).Should().BeFalse();
    }

    [Fact]
    public void Close_FromEffectivenessVerified_ShouldSucceed()
    {
        var capa = CreateCapa();
        capa.StartInvestigation();
        capa.DefineActions();
        capa.ImplementActions();
        capa.VerifyEffectiveness(Guid.NewGuid(), "OK", true);

        capa.Close().Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.Closed);
    }

    [Fact]
    public void Close_FromNonVerifiedStatus_ShouldReturnFalse()
    {
        var capa = CreateCapa();

        capa.Close().Should().BeFalse();
        capa.Status.Should().Be(CapaStatus.Draft);
    }

    [Theory]
    [InlineData(CapaStatus.Draft, CapaStatus.UnderInvestigation, true)]
    [InlineData(CapaStatus.UnderInvestigation, CapaStatus.ActionsDefined, true)]
    [InlineData(CapaStatus.ActionsDefined, CapaStatus.ActionsImplemented, true)]
    [InlineData(CapaStatus.ActionsImplemented, CapaStatus.EffectivenessVerified, true)]
    [InlineData(CapaStatus.EffectivenessVerified, CapaStatus.Closed, true)]
    [InlineData(CapaStatus.Draft, CapaStatus.ActionsDefined, false)]
    [InlineData(CapaStatus.Draft, CapaStatus.Closed, false)]
    [InlineData(CapaStatus.Closed, CapaStatus.Draft, false)]
    public void CanTransitionTo_ShouldValidateTransitions(
        CapaStatus currentStatus, CapaStatus targetStatus, bool expected)
    {
        var capa = CreateCapa();
        capa.Status = currentStatus;

        capa.CanTransitionTo(targetStatus).Should().Be(expected);
    }

    [Fact]
    public void CanTransitionTo_BackwardFromUnderInvestigation_ShouldAllowDraft()
    {
        var capa = CreateCapa();
        capa.Status = CapaStatus.UnderInvestigation;

        capa.CanTransitionTo(CapaStatus.Draft).Should().BeTrue();
    }

    [Fact]
    public void GetNextStatus_ShouldReturnCorrectValues()
    {
        var capa = CreateCapa();

        capa.Status = CapaStatus.Draft;
        capa.GetNextStatus().Should().Be(CapaStatus.UnderInvestigation);

        capa.Status = CapaStatus.UnderInvestigation;
        capa.GetNextStatus().Should().Be(CapaStatus.ActionsDefined);

        capa.Status = CapaStatus.ActionsDefined;
        capa.GetNextStatus().Should().Be(CapaStatus.ActionsImplemented);

        capa.Status = CapaStatus.ActionsImplemented;
        capa.GetNextStatus().Should().Be(CapaStatus.EffectivenessVerified);

        capa.Status = CapaStatus.EffectivenessVerified;
        capa.GetNextStatus().Should().Be(CapaStatus.Closed);

        capa.Status = CapaStatus.Closed;
        capa.GetNextStatus().Should().BeNull();
    }

    [Fact]
    public void GetPreviousStatus_ShouldReturnCorrectValues()
    {
        var capa = CreateCapa();

        capa.Status = CapaStatus.Draft;
        capa.GetPreviousStatus().Should().BeNull();

        capa.Status = CapaStatus.UnderInvestigation;
        capa.GetPreviousStatus().Should().Be(CapaStatus.Draft);

        capa.Status = CapaStatus.ActionsDefined;
        capa.GetPreviousStatus().Should().Be(CapaStatus.UnderInvestigation);

        capa.Status = CapaStatus.ActionsImplemented;
        capa.GetPreviousStatus().Should().Be(CapaStatus.ActionsDefined);

        capa.Status = CapaStatus.EffectivenessVerified;
        capa.GetPreviousStatus().Should().Be(CapaStatus.ActionsImplemented);

        capa.Status = CapaStatus.Closed;
        capa.GetPreviousStatus().Should().BeNull();
    }

    [Theory]
    [InlineData(CapaStatus.Draft, true)]
    [InlineData(CapaStatus.UnderInvestigation, true)]
    [InlineData(CapaStatus.ActionsDefined, true)]
    [InlineData(CapaStatus.ActionsImplemented, true)]
    [InlineData(CapaStatus.EffectivenessVerified, false)]
    [InlineData(CapaStatus.Closed, false)]
    public void CanBeDeleted_ShouldReflectStatus(CapaStatus status, bool expected)
    {
        var capa = CreateCapa();
        capa.Status = status;

        capa.CanBeDeleted.Should().Be(expected);
    }

    [Fact]
    public void AddAction_ShouldCreateActionWithCorrectDefaults()
    {
        var capa = CreateCapa();
        var assigneeId = Guid.NewGuid();
        var dueDate = DateTime.UtcNow.AddDays(7);

        var action = capa.AddAction("Fix defect", "Corrective", dueDate, assigneeId);

        action.Id.Should().NotBeEmpty();
        action.CapaId.Should().Be(capa.Id);
        action.Description.Should().Be("Fix defect");
        action.ActionType.Should().Be("Corrective");
        action.DueDate.Should().Be(dueDate);
        action.AssigneeId.Should().Be(assigneeId);
        action.IsCompleted.Should().BeFalse();
        action.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capa.Actions.Should().HaveCount(1);
    }

    [Fact]
    public void AddAction_WithoutAssignee_ShouldLeaveAssigneeNull()
    {
        var capa = CreateCapa();

        var action = capa.AddAction("Task", "Preventive", DateTime.UtcNow.AddDays(5));

        action.AssigneeId.Should().BeNull();
    }

    [Fact]
    public void AdvanceStatus_FromDraft_ShouldMoveToUnderInvestigation()
    {
        var capa = CreateCapa();

        capa.AdvanceStatus().Should().BeTrue();
        capa.Status.Should().Be(CapaStatus.UnderInvestigation);
    }

    [Fact]
    public void AdvanceStatus_FromClosed_ShouldReturnFalse()
    {
        var capa = CreateCapa();
        capa.Status = CapaStatus.Closed;

        capa.AdvanceStatus().Should().BeFalse();
    }
}
