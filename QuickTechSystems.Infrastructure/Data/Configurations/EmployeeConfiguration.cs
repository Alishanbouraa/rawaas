using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.HasKey(e => e.EmployeeId);

            builder.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.Role)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.Property(e => e.UpdatedAt);

            builder.Property(e => e.IsActive)
                .HasDefaultValue(true);

            builder.HasIndex(e => e.Username)
                .IsUnique();

            builder.Property(e => e.MonthlySalary)
                .HasPrecision(18, 2)
                .HasDefaultValue(0);

            builder.Property(e => e.CurrentBalance)
                .HasPrecision(18, 2)
                .HasDefaultValue(0);

            builder.HasMany(e => e.SalaryTransactions)
                .WithOne(st => st.Employee)
                .HasForeignKey(st => st.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.AttendanceRecords)
                .WithOne(ar => ar.Employee)
                .HasForeignKey(ar => ar.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}