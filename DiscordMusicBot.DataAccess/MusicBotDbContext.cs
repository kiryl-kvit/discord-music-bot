using DiscordMusicBot.Domain.PlayQueue;
using Microsoft.EntityFrameworkCore;

namespace DiscordMusicBot.DataAccess;

public sealed class MusicBotDbContext(DbContextOptions<MusicBotDbContext> options) : DbContext(options)
{
    public DbSet<PlayQueueItem> PlayQueueItems => Set<PlayQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlayQueueItem>(entity =>
        {
            entity.ToTable("play_queue_items");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            entity.Property(x => x.GuildId)
                .IsRequired();

            entity.Property(x => x.Type)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(x => x.Source)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(x => x.Position)
                .IsRequired();

            entity.Property(x => x.EnqueuedAtUtc)
                .IsRequired();

            entity.HasIndex(x => new { x.GuildId, x.Position })
                .IsUnique();
        });
    }
}
