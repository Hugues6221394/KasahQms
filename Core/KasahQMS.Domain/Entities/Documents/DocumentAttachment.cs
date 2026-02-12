namespace KasahQMS.Domain.Entities.Documents;

/// <summary>
/// File attached to a document. Supports multiple attachments per document.
/// </summary>
public class DocumentAttachment
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    /// <summary>When attachment was created from an existing document (attach from library).</summary>
    public Guid? SourceDocumentId { get; set; }

    public virtual Document? Document { get; set; }
}
