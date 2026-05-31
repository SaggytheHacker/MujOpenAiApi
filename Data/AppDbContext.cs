using Microsoft.EntityFrameworkCore;
using MujOpenAiApi.Models;

namespace MujOpenAiApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Chat> Chats => Set<Chat>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    public DbSet<AgentAction> AgentActions => Set<AgentAction>();

    public DbSet<GeneratedArtifact> GeneratedArtifacts => Set<GeneratedArtifact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chat>(entity =>
        {
            entity.ToTable("Chats");

            entity.HasKey(chat => chat.Id);

            entity.Property(chat => chat.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(chat => chat.SourceApp)
                .HasMaxLength(100);

            entity.HasIndex(chat => chat.CreatedAtUtc);
            entity.HasIndex(chat => chat.UpdatedAtUtc);
            entity.HasIndex(chat => chat.Title);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");

            entity.HasKey(message => message.Id);

            entity.Property(message => message.Role)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(message => message.Content)
                .IsRequired();

            entity.Property(message => message.Model)
                .HasMaxLength(100);

            entity.HasIndex(message => message.CreatedAtUtc);
            entity.HasIndex(message => new { message.ChatId, message.CreatedAtUtc });

            entity.HasOne(message => message.Chat)
                .WithMany(chat => chat.Messages)
                .HasForeignKey(message => message.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentRun>(entity =>
        {
            entity.ToTable("AgentRuns");

            entity.HasKey(run => run.Id);

            entity.Property(run => run.LessonId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(run => run.LessonTitle)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(run => run.UserPrompt)
                .IsRequired();

            entity.Property(run => run.Status)
                .HasMaxLength(40)
                .IsRequired();

            entity.HasIndex(run => run.ChatId);
            entity.HasIndex(run => run.LessonId);
            entity.HasIndex(run => run.CreatedAtUtc);

            entity.HasOne(run => run.Chat)
                .WithMany()
                .HasForeignKey(run => run.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentAction>(entity =>
        {
            entity.ToTable("AgentActions");

            entity.HasKey(action => action.Id);

            entity.Property(action => action.ToolName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(action => action.ArgumentsJson)
                .IsRequired();

            entity.Property(action => action.ResultJson)
                .IsRequired();

            entity.HasIndex(action => action.AgentRunId);
            entity.HasIndex(action => action.ToolName);

            entity.HasOne(action => action.AgentRun)
                .WithMany(run => run.Actions)
                .HasForeignKey(action => action.AgentRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GeneratedArtifact>(entity =>
        {
            entity.ToTable("GeneratedArtifacts");

            entity.HasKey(artifact => artifact.Id);

            entity.Property(artifact => artifact.LessonId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(artifact => artifact.Type)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(artifact => artifact.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(artifact => artifact.ContentJson)
                .IsRequired();

            entity.HasIndex(artifact => artifact.AgentRunId);
            entity.HasIndex(artifact => artifact.LessonId);
            entity.HasIndex(artifact => artifact.Type);
            entity.HasIndex(artifact => artifact.CreatedAtUtc);

            entity.HasOne(artifact => artifact.AgentRun)
                .WithMany(run => run.Artifacts)
                .HasForeignKey(artifact => artifact.AgentRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
