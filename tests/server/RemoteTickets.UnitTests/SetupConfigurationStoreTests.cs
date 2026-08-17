namespace RemoteTickets.UnitTests;

/// <summary>Verifies setup configuration file persistence and malformed-state handling.</summary>
public sealed class SetupConfigurationStoreTests
{
    [Fact]
    public async Task Setup_configuration_store_should_read_write_and_recover_from_invalid_state()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "setup.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Delete(path);
        var store = new SetupConfigurationStore();

        store.GetMasterConnectionString().Should().BeNull();
        await store.SetMasterConnectionStringAsync("Server=localhost;Database=master", TestContext.Current.CancellationToken);
        store.GetMasterConnectionString().Should().Be("Server=localhost;Database=master");

        await File.WriteAllTextAsync(path, "{invalid", TestContext.Current.CancellationToken);
        store.GetMasterConnectionString().Should().BeNull();
        await FluentActions.Awaiting(() => store.SetMasterConnectionStringAsync(string.Empty, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();

        File.Delete(path);
    }
}
