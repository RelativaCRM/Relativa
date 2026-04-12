using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Migration.Models;
namespace Relativa.Migration.Configurations;
public class PersonalDataPropertyValueConfiguration : IEntityTypeConfiguration<PersonalDataPropertyValue> {
    public void Configure(EntityTypeBuilder<PersonalDataPropertyValue> builder) {
        builder.ToTable("personal_data_property_values");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(e => e.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(e => e.PhoneNumber).HasColumnName("phone_number");
        builder.Property(e => e.Email).HasColumnName("email");
        builder.Property(e => e.PassportNumber).HasColumnName("passport_number");
        builder.Property(e => e.BirthDate).HasColumnName("birth_date");
    }
}
