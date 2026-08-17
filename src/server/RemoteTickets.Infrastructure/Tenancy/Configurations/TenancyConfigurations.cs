namespace RemoteTickets.Infrastructure.Tenancy.Configurations;

internal sealed class TenancyConfigurations : IEntityTypeConfiguration<TenantSetupState>
{
    public void Configure(EntityTypeBuilder<TenantSetupState> builder)
    {
        builder.ToTable("TenantSetupState");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsComplete).IsRequired();
    }

}
