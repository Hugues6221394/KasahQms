using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.News;

/// <summary>
/// Tracks which users have read which news articles (for badge count).
/// </summary>
public class NewsRead : BaseEntity
{
    public Guid NewsArticleId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ReadAt { get; set; }

    // Navigation properties
    public NewsArticle? NewsArticle { get; set; }
    public User? User { get; set; }
}
