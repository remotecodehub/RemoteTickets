namespace RemoteTickets.Infrastructure.Persistence.Configurations.Identity;

internal sealed class IdentityConfigurations :
IEntityTypeConfiguration<User>, 
IEntityTypeConfiguration<Role>,
IEntityTypeConfiguration<IdentityUserRole<string>>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => new { u.Id, u.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_UQ_UID_TID");
    }

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);
    }

    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> builder) 
        => builder.ToTable("UserRoles");
}
