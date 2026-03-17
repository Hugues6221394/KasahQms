using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.News;

public class NewsArticle : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NewsType Type { get; set; }
    public NewsPriority Priority { get; set; }
    public DateTime PublishedAt { get; set; }
    public Guid PublishedById { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User? PublishedBy { get; set; }
    public ICollection<NewsRead> ReadBy { get; set; } = new List<NewsRead>();
}
