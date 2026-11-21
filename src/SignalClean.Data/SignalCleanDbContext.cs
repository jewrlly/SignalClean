using Microsoft.EntityFrameworkCore;

namespace SignalClean.Data;

public class SignalCleanDbContext(DbContextOptions<SignalCleanDbContext> options) : DbContext(options)
{
    public DbSet<SensorType> SensorTypes => Set<SensorType>();

    public DbSet<Sensor> Sensors => Set<Sensor>();

    public DbSet<Reading> Readings => Set<Reading>();

    public DbSet<Anomaly> Anomalies => Set<Anomaly>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<SensorType>(e =>
        {
            e.ToTable("SensorTypes", t => t.HasCheckConstraint("CK_SensorTypes_Range", "\"MinValid\" < \"MaxValid\""));
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Sensor>(e =>
        {
            e.ToTable("Sensors");
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Location).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();

            // Тип не можна видалити, поки є датчики цього типу.
            e.HasOne(x => x.SensorType)
                .WithMany(t => t.Sensors)
                .HasForeignKey(x => x.SensorTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reading>(e =>
        {
            e.ToTable("Readings");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

            // Найчастіший запит: «останні N вимірювань датчика».
            e.HasIndex(x => new { x.SensorId, x.TimestampUtc });

            e.HasOne(x => x.Sensor)
                .WithMany(s => s.Readings)
                .HasForeignKey(x => x.SensorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Anomaly>(e =>
        {
            e.ToTable("Anomalies");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Details).HasMaxLength(300).IsRequired();
            e.HasIndex(x => new { x.SensorId, x.TimestampUtc });

            e.HasOne(x => x.Sensor)
                .WithMany(s => s.Anomalies)
                .HasForeignKey(x => x.SensorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}