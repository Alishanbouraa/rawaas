using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class EmployeeAttendanceConfiguration : IEntityTypeConfiguration<EmployeeAttendance>
    {
        public void Configure(EntityTypeBuilder<EmployeeAttendance> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Date)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(e => e.StartTime)
                .IsRequired();

            builder.Property(e => e.EndTime)
                .IsRequired();

            builder.Property(e => e.HourlyRate)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(e => e.StandardDailyHours)
                .HasPrecision(18, 2)
                .HasDefaultValue(8.0m)
                .IsRequired();

            builder.Property(e => e.OvertimeMultiplier)
                .HasPrecision(18, 2)
                .HasDefaultValue(1.5m)
                .IsRequired();

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.Property(e => e.UpdatedAt);

            builder.HasOne(e => e.Employee)
                .WithMany(emp => emp.AttendanceRecords)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.EmployeeId, e.Date })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeAttendance_EmployeeId_Date");

            builder.HasIndex(e => e.Date)
                .HasDatabaseName("IX_EmployeeAttendance_Date");

            builder.HasIndex(e => e.EmployeeId)
                .HasDatabaseName("IX_EmployeeAttendance_EmployeeId");
        }
    }
}