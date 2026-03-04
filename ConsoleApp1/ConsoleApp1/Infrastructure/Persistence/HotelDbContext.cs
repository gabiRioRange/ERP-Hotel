using ConsoleApp1.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConsoleApp1.Infrastructure.Persistence;

public class HotelDbContext(DbContextOptions<HotelDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
    public DbSet<AuthLockout> AuthLockouts => Set<AuthLockout>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<SecurityExportAuditLog> SecurityExportAuditLogs => Set<SecurityExportAuditLog>();
    public DbSet<SecurityAuditExportJob> SecurityAuditExportJobs => Set<SecurityAuditExportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(room => room.Id);
            entity.Property(room => room.Number).HasMaxLength(10).IsRequired();
            entity.Property(room => room.Type).HasMaxLength(40).IsRequired();
            entity.HasIndex(room => room.Number).IsUnique();
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(reservation => reservation.Id);
            entity.Property(reservation => reservation.GuestFullName).HasMaxLength(150).IsRequired();
            entity.Property(reservation => reservation.SourceChannel).HasMaxLength(50).IsRequired();
            entity.HasIndex(reservation => new { reservation.RoomId, reservation.CheckInUtc, reservation.CheckOutUtc });

            entity.HasOne(reservation => reservation.Room)
                .WithMany(room => room.Reservations)
                .HasForeignKey(reservation => reservation.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.Id);
            entity.Property(token => token.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(token => token.Scope).HasMaxLength(300).IsRequired();
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(128);
            entity.Property(token => token.RevocationReason).HasMaxLength(120);
            entity.Property(token => token.FamilyId).HasMaxLength(64).IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.ClientId, token.ExpiresUtc });
            entity.HasIndex(token => new { token.FamilyId, token.ExpiresUtc });
        });

        modelBuilder.Entity<SecurityAuditLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.EventType).HasMaxLength(40).IsRequired();
            entity.Property(log => log.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(log => log.Reason).HasMaxLength(200);
            entity.Property(log => log.TraceId).HasMaxLength(80);
            entity.Property(log => log.IpAddress).HasMaxLength(64);
            entity.HasIndex(log => log.OccurredUtc);
            entity.HasIndex(log => new { log.ClientId, log.OccurredUtc });
            entity.HasIndex(log => new { log.EventType, log.OccurredUtc });
            entity.HasIndex(log => log.TraceId);
        });

        modelBuilder.Entity<AuthLockout>(entity =>
        {
            entity.HasKey(lockout => lockout.Id);
            entity.Property(lockout => lockout.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(lockout => lockout.IpAddress).HasMaxLength(64).IsRequired();
            entity.HasIndex(lockout => new { lockout.ClientId, lockout.IpAddress }).IsUnique();
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Scope).HasMaxLength(120).IsRequired();
            entity.Property(record => record.Key).HasMaxLength(120).IsRequired();
            entity.Property(record => record.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(record => record.ResponseJson).HasColumnType("text").IsRequired();
            entity.HasIndex(record => new { record.Scope, record.Key }).IsUnique();
            entity.HasIndex(record => record.CreatedUtc);
        });

        modelBuilder.Entity<SecurityExportAuditLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.RequestedBy).HasMaxLength(100).IsRequired();
            entity.Property(log => log.ClientIdFilter).HasMaxLength(100);
            entity.Property(log => log.EventTypeFilter).HasMaxLength(40);
            entity.Property(log => log.Sha256).HasMaxLength(128).IsRequired();
            entity.Property(log => log.TraceId).HasMaxLength(80).IsRequired();
            entity.HasIndex(log => log.OccurredUtc);
        });

        modelBuilder.Entity<SecurityAuditExportJob>(entity =>
        {
            entity.HasKey(job => job.Id);
            entity.Property(job => job.RequestedBy).HasMaxLength(100).IsRequired();
            entity.Property(job => job.ClientIdFilter).HasMaxLength(100);
            entity.Property(job => job.EventTypeFilter).HasMaxLength(40);
            entity.Property(job => job.Status).HasMaxLength(30).IsRequired();
            entity.Property(job => job.FileName).HasMaxLength(200);
            entity.Property(job => job.ContentType).HasMaxLength(100);
            entity.Property(job => job.Sha256).HasMaxLength(128);
            entity.HasIndex(job => new { job.Status, job.CreatedUtc });
        });
    }
}