using Microsoft.EntityFrameworkCore;
using MujOpenAiApi.Models;

namespace MujOpenAiApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Chat> Chats => Set<Chat>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

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
    }
}
