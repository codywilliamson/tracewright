using Tracewright.Core;

namespace Tracewright.Tests;

public class UlidTests
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    [Fact]
    public void NewUlid_is_26_characters_long()
    {
        Assert.Equal(26, Ulid.NewUlid().Length);
    }

    [Fact]
    public void NewUlid_uses_only_crockford_base32_alphabet_characters()
    {
        var id = Ulid.NewUlid();

        Assert.All(id, c => Assert.Contains(c, CrockfordAlphabet));
    }

    [Fact]
    public void NewUlid_is_uppercase()
    {
        var id = Ulid.NewUlid();

        Assert.Equal(id.ToUpperInvariant(), id);
    }

    [Fact]
    public void NewUlid_produces_unique_values()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => Ulid.NewUlid()).ToHashSet();

        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public async Task NewUlid_sorts_lexically_by_creation_time()
    {
        var first = Ulid.NewUlid();
        await Task.Delay(5);
        var second = Ulid.NewUlid();

        Assert.True(string.CompareOrdinal(first, second) < 0);
    }
}
