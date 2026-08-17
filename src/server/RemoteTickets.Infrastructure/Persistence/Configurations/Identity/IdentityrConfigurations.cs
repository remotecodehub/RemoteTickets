namespace RemoteTickets.Infrastructure.Persistence.Configurations.Identity;

internal sealed class IdentityrConfigurations : IEntityTypeConfiguration<User>, IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
    }

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
    }
}
