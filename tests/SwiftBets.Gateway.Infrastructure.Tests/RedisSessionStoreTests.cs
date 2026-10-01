using StackExchange.Redis;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Gateway.Domain;
using SwiftBets.Gateway.Infrastructure.Sessions;

namespace SwiftBets.Gateway.Infrastructure.Tests;

public sealed class RedisSessionStoreTests(RedisFixture redis)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    [Fact]
    public async Task Rotated_session_moves_to_its_new_hash_and_the_old_one_points_there_until_the_grace_ends()
    {
        var store = await StoreAsync();
        var user = NewUser();
        await store.SaveAsync("old", Session(user, "Laptop"), Lifetime);

        await store.RotateAsync("old", "new", "new-cookie", Session(user, "Laptop"), Lifetime, TimeSpan.FromSeconds(1));

        (await store.GetAsync("old"))!.RotatedTo.ShouldBe("new-cookie");
        (await store.GetAsync("new"))!.Session!.Device.ShouldBe("Laptop");
        (await store.ListAsync(user)).Select(s => s.Hash).ShouldBe(["new"]);
        await Task.Delay(TimeSpan.FromMilliseconds(1500), TestContext.Current.CancellationToken);
        (await store.GetAsync("old")).ShouldBeNull();
    }

    [Fact]
    public async Task Deleting_every_session_of_a_user_leaves_other_users_alone()
    {
        var store = await StoreAsync();
        var (user, other) = (NewUser(), NewUser());
        await store.SaveAsync($"{user}-a", Session(user, "Phone"), Lifetime);
        await store.SaveAsync($"{user}-b", Session(user, "Laptop"), Lifetime);
        await store.SaveAsync($"{other}-a", Session(other, "Phone"), Lifetime);

        await store.DeleteAllAsync(user);

        (await store.ListAsync(user)).ShouldBeEmpty();
        (await store.GetAsync($"{user}-a")).ShouldBeNull();
        (await store.ListAsync(other)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Listing_drops_index_entries_whose_session_has_expired()
    {
        var store = await StoreAsync();
        var user = NewUser();
        await store.SaveAsync("short", Session(user, "Old tablet"), TimeSpan.FromMilliseconds(300));
        await store.SaveAsync("long", Session(user, "Laptop"), Lifetime);
        await Task.Delay(TimeSpan.FromMilliseconds(800), TestContext.Current.CancellationToken);

        (await store.ListAsync(user)).Select(s => s.Session.Device).ShouldBe(["Laptop"]);
    }

    [Fact]
    public async Task Only_one_holder_can_claim_a_session_refresh_until_it_is_released()
    {
        var store = await StoreAsync();
        var hash = Guid.NewGuid().ToString("N");

        var claims = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.TryLockRefreshAsync(hash, TimeSpan.FromSeconds(10))));
        await store.ReleaseRefreshAsync(hash);

        claims.Count(c => c).ShouldBe(1);
        (await store.TryLockRefreshAsync(hash, TimeSpan.FromSeconds(10))).ShouldBeTrue();
    }

    private async Task<RedisSessionStore> StoreAsync() => new(await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString));

    private static string NewUser() => Guid.NewGuid().ToString("N");

    private static BrowserSession Session(string user, string device) =>
        new(Guid.NewGuid(), user, ["Punter"], "access", "refresh", DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, device);
}
