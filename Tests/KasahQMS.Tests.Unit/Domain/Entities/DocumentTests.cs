using FluentAssertions;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Enums;
using Xunit;

namespace KasahQMS.Tests.Unit.Domain.Entities;

public class DocumentTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private Document CreateDraftDocument()
    {
        return Document.Create(_tenantId, "Test Document", "DOC-001", _userId, "Description");
    }

    [Fact]
    public void Create_ShouldSetAllDefaults()
    {
        var doc = Document.Create(_tenantId, "Title", "DOC-001", _userId, "Desc");

        doc.Id.Should().NotBeEmpty();
        doc.TenantId.Should().Be(_tenantId);
        doc.Title.Should().Be("Title");
        doc.DocumentNumber.Should().Be("DOC-001");
        doc.Description.Should().Be("Desc");
        doc.CreatedById.Should().Be(_userId);
        doc.Status.Should().Be(DocumentStatus.Draft);
        doc.CurrentVersion.Should().Be(1);
        doc.Versions.Should().NotBeNull().And.BeEmpty();
        doc.Approvals.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Create_WithOptionalParameters_ShouldSetDocumentTypeAndCategory()
    {
        var typeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var doc = Document.Create(_tenantId, "Title", "DOC-001", _userId,
            documentTypeId: typeId, categoryId: categoryId);

        doc.DocumentTypeId.Should().Be(typeId);
        doc.CategoryId.Should().Be(categoryId);
    }

    [Fact]
    public void UpdateTitle_OnDraft_ShouldUpdateTitle()
    {
        var doc = CreateDraftDocument();
        doc.UpdateTitle("New Title");
        doc.Title.Should().Be("New Title");
    }

    [Fact]
    public void UpdateTitle_OnApproved_ShouldThrow()
    {
        var doc = CreateDraftDocument();
        doc.Status = DocumentStatus.Approved;

        var act = () => doc.UpdateTitle("New Title");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*approved*");
    }

    [Fact]
    public void UpdateDescription_OnDraft_ShouldUpdateDescription()
    {
        var doc = CreateDraftDocument();
        doc.UpdateDescription("New Desc");
        doc.Description.Should().Be("New Desc");
    }

    [Fact]
    public void UpdateDescription_OnApproved_ShouldThrow()
    {
        var doc = CreateDraftDocument();
        doc.Status = DocumentStatus.Approved;

        var act = () => doc.UpdateDescription("New Desc");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateContent_OnDraft_ShouldUpdateContent()
    {
        var doc = CreateDraftDocument();
        doc.UpdateContent("New Content");
        doc.Content.Should().Be("New Content");
    }

    [Fact]
    public void UpdateContent_OnApproved_ShouldThrow()
    {
        var doc = CreateDraftDocument();
        doc.Status = DocumentStatus.Approved;

        var act = () => doc.UpdateContent("New Content");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Submit_OnDraft_ShouldSetStatusAndCreateVersion()
    {
        var doc = CreateDraftDocument();
        var submitterId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        doc.Submit(submitterId, approverId);

        doc.Status.Should().Be(DocumentStatus.Submitted);
        doc.SubmittedAt.Should().NotBeNull();
        doc.CurrentApproverId.Should().Be(approverId);
        doc.LastModifiedById.Should().Be(submitterId);
        doc.Versions.Should().HaveCount(1);
        doc.Versions!.First().VersionNumber.Should().Be(1);
    }

    [Fact]
    public void Submit_OnSubmitted_ShouldThrow()
    {
        var doc = CreateDraftDocument();
        doc.Submit(_userId);

        var act = () => doc.Submit(_userId);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*draft or rejected*");
    }

    [Fact]
    public void Submit_OnRejected_ShouldBeAllowed()
    {
        var doc = CreateDraftDocument();
        doc.Status = DocumentStatus.Rejected;

        var act = () => doc.Submit(_userId);
        act.Should().NotThrow();
        doc.Status.Should().Be(DocumentStatus.Submitted);
    }

    [Fact]
    public void Approve_ShouldSetStatusAndAddApprovalRecord()
    {
        var doc = CreateDraftDocument();
        var approverId = Guid.NewGuid();

        doc.Approve(approverId, "Looks good");

        doc.Status.Should().Be(DocumentStatus.Approved);
        doc.ApprovedAt.Should().NotBeNull();
        doc.ApprovedById.Should().Be(approverId);
        doc.CurrentApproverId.Should().BeNull();
        doc.Approvals.Should().HaveCount(1);
        doc.Approvals!.First().IsApproved.Should().BeTrue();
        doc.Approvals!.First().Comments.Should().Be("Looks good");
    }

    [Fact]
    public void Reject_WithReason_ShouldSetStatusAndAddRecord()
    {
        var doc = CreateDraftDocument();
        var rejectedById = Guid.NewGuid();

        doc.Reject(rejectedById, "Needs revision");

        doc.Status.Should().Be(DocumentStatus.Draft);
        doc.CurrentApproverId.Should().BeNull();
        doc.Approvals.Should().HaveCount(1);
        doc.Approvals!.First().IsApproved.Should().BeFalse();
        doc.Approvals!.First().Comments.Should().Be("Needs revision");
    }

    [Fact]
    public void Reject_WithEmptyReason_ShouldThrowArgumentException()
    {
        var doc = CreateDraftDocument();

        var act = () => doc.Reject(_userId, "");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Rejection reason*");
    }

    [Fact]
    public void Reject_WithWhitespaceReason_ShouldThrowArgumentException()
    {
        var doc = CreateDraftDocument();

        var act = () => doc.Reject(_userId, "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Archive_OnApproved_ShouldSetArchivedStatus()
    {
        var doc = CreateDraftDocument();
        doc.Status = DocumentStatus.Approved;
        var archivedById = Guid.NewGuid();

        doc.Archive(archivedById, "End of life");

        doc.Status.Should().Be(DocumentStatus.Archived);
        doc.ArchivedAt.Should().NotBeNull();
        doc.ArchivedById.Should().Be(archivedById);
        doc.ArchiveReason.Should().Be("End of life");
    }

    [Fact]
    public void Archive_OnDraft_ShouldThrow()
    {
        var doc = CreateDraftDocument();

        var act = () => doc.Archive(_userId);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*approved*");
    }

    [Fact]
    public void IncrementVersion_ShouldIncreaseVersionAndCreateSnapshot()
    {
        var doc = CreateDraftDocument();
        doc.Content = "Version 1 content";
        var initialVersion = doc.CurrentVersion;

        doc.IncrementVersion(_userId, "Updated formatting");

        doc.CurrentVersion.Should().Be(initialVersion + 1);
        doc.Versions.Should().HaveCount(1);
        doc.Versions!.Last().ChangeNotes.Should().Be("Updated formatting");
    }

    [Fact]
    public void IncrementVersion_MultipleTimes_ShouldTrackAllVersions()
    {
        var doc = CreateDraftDocument();

        doc.IncrementVersion(_userId, "V2");
        doc.IncrementVersion(_userId, "V3");

        doc.CurrentVersion.Should().Be(3);
        doc.Versions.Should().HaveCount(2);
    }
}
