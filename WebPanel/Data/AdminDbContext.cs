using Microsoft.EntityFrameworkCore;

namespace WebPanel.Data;

public sealed class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<RequestEntity> Requests => Set<RequestEntity>();
    public DbSet<UserAccessEntity> Users => Set<UserAccessEntity>();
    public DbSet<RecommendationEntity> Recommendations => Set<RecommendationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequestEntity>().ToTable("requests");
        modelBuilder.Entity<RequestEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<RequestEntity>().Property(x => x.Category).HasMaxLength(32);
        modelBuilder.Entity<RequestEntity>().Property(x => x.Status).HasMaxLength(32);

        modelBuilder.Entity<UserAccessEntity>().ToTable("users");
        modelBuilder.Entity<UserAccessEntity>().HasKey(x => x.UserId);
        modelBuilder.Entity<UserAccessEntity>().Property(x => x.Username).HasMaxLength(255);

        modelBuilder.Entity<RecommendationEntity>().ToTable("recommendations");
        modelBuilder.Entity<RecommendationEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<RecommendationEntity>().Property(x => x.Status).HasMaxLength(32);
    }
}

public sealed class RequestEntity
{
    public long Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string Reporter { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class UserAccessEntity
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool CanUseBot { get; set; }
    public bool CanComplete { get; set; }
    public bool CanManageAccess { get; set; }
    public bool NotifyRequests { get; set; }
    public bool NotifyRecommendations { get; set; }
}

public sealed class RecommendationEntity
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Recommender { get; set; } = string.Empty;
    public string RecommendedUsername { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime? ReviewedAtUtc { get; set; }
    public string ReviewedBy { get; set; } = string.Empty;
}
