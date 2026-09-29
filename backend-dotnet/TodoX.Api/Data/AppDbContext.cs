using Microsoft.EntityFrameworkCore;
using TodoX.Api.Entities;

namespace TodoX.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskEntity>(task =>
        {
            task.ToTable("Tasks", t =>
            {
                // With NOT NULL, rejects missing, empty and whitespace-only titles: the DF-01 path to 500.
                t.HasCheckConstraint("CK_Tasks_Title_NotBlank", "trim(\"Title\") <> ''");
                t.HasCheckConstraint("CK_Tasks_Status_Enum", "\"Status\" IN ('active', 'complete')");
            });

            task.HasKey(t => t.Id);
            task.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
            task.Property(t => t.Title).IsRequired();
            task.Property(t => t.Status).IsRequired().HasDefaultValue("active");
            task.Property(t => t.CompletedAt).HasColumnType("timestamptz");
            task.Property(t => t.CreatedAt).IsRequired();
            task.Property(t => t.UpdatedAt).IsRequired();
        });
    }
}
