using TypingBattle.Api.Common;

namespace TypingBattle.UnitTests.Common;

[Trait("Category", "Unit")]
public class UtcDateTests
{
    private static readonly DateTime Sample = new(2026, 9, 2, 20, 0, 0);

    [Fact]
    public void A_utc_date_is_kept_as_is()
    {
        var utc = DateTime.SpecifyKind(Sample, DateTimeKind.Utc);

        Assert.True(UtcDate.TryNormalize(utc, out var result));

        Assert.Equal(utc, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void A_local_date_is_converted_to_utc()
    {
        var local = DateTime.SpecifyKind(Sample, DateTimeKind.Local);

        Assert.True(UtcDate.TryNormalize(local, out var result));

        Assert.Equal(local.ToUniversalTime(), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void A_date_without_zone_is_rejected()
    {
        Assert.False(UtcDate.TryNormalize(DateTime.SpecifyKind(Sample, DateTimeKind.Unspecified), out var result));

        Assert.Equal(default, result);
    }
}
