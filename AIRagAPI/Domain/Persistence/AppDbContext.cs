using AIRagAPI.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AIRagAPI.Domain.Persistence;

public class AppDbContext: DbContext, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatMember> ChatMembers => Set<ChatMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Handle updated at datetime for updated entity
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // User
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>()
            .Property(u => u.Email).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<User>()
            .Property(u => u.Name).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<User>()
            .Property(u => u.Role).HasConversion<string>();
        modelBuilder.Entity<User>()
            .Property(u => u.PictureUrl).HasMaxLength(500);
        modelBuilder.Entity<User>()
            .HasMany(u => u.Conversations)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Conversation
        modelBuilder.Entity<Conversation>()
            .Property(c => c.Title).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<Conversation>()
            .Property(c => c.Summary).HasMaxLength(500);
        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Messages
        modelBuilder.Entity<Message>()
            .Property(m => m.Content).IsRequired();
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ConversationId, m.Order});
        modelBuilder.Entity<Message>()
            .Property(m => m.Role).HasConversion<string>();
        
        // Chat
        modelBuilder.Entity<Chat>()
            .Property(c => c.Name).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Chat>()
            .Property(c => c.Description).HasMaxLength(500);
        modelBuilder.Entity<Chat>()
            .Property(c => c.Type).HasConversion<string>();
        modelBuilder.Entity<Chat>()
            .HasMany(c => c.Members)
            .WithOne(cm => cm.Chat)
            .HasForeignKey(cm => cm.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Chat>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Chat)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // ChatMember
        modelBuilder.Entity<ChatMember>()
            .Property(cm => cm.DisplayName).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<ChatMember>()
            .Property(cm => cm.LastReadOrder).HasDefaultValue(-1);
        modelBuilder.Entity<ChatMember>()
            .HasOne(cm => cm.User)
            .WithMany()
            .HasForeignKey(cm => cm.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ChatMember>()
            .HasIndex(cm => new { cm.ChatId, cm.UserId }).IsUnique();
        
        // ChatMessage
        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Content).IsRequired();
        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Role).HasConversion<string>();
        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.RetrievalContext).HasMaxLength(5000);
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => new { m.ChatId, m.Order });
            
    }
}