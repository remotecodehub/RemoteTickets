namespace RemoteTickets.Infrastructure.Persistence.Configurations.Tenancy;

internal sealed class TenancyConfigurations :
IEntityTypeConfiguration<Tenant>,
IEntityTypeConfiguration<SystemSetupState>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(128);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DatabaseName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ConnectionString).IsRequired();
        builder.HasIndex(x => x.DatabaseName).IsUnique();
    }

    public void Configure(EntityTypeBuilder<SystemSetupState> builder)
    {
        builder.ToTable("SystemSetupState");
        builder.HasKey(x => x.Id);
    }
}
