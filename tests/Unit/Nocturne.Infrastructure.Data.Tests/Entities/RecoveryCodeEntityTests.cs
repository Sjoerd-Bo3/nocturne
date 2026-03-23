namespace Nocturne.Infrastructure.Data.Tests.Entities;

public class RecoveryCodeEntityTests
{
    [Fact]
    public void UsedAt_Should_Default_To_Null()
    {
        var entity = new RecoveryCodeEntity();

        entity.UsedAt.Should().BeNull();
    }

    [Fact]
    public void IsUsed_Should_Return_True_When_UsedAt_Has_Value()
    {
        var entity = new RecoveryCodeEntity
        {
            UsedAt = DateTime.UtcNow
        };

        entity.IsUsed.Should().BeTrue();
    }

    [Fact]
    public void IsUsed_Should_Return_False_When_UsedAt_Is_Null()
    {
        var entity = new RecoveryCodeEntity();

        entity.IsUsed.Should().BeFalse();
    }

    [Fact]
    public void CreatedAt_Should_Default_To_UtcNow()
    {
        var before = DateTime.UtcNow;
        var entity = new RecoveryCodeEntity();
        var after = DateTime.UtcNow;

        entity.CreatedAt.Should().BeOnOrAfter(before);
        entity.CreatedAt.Should().BeOnOrBefore(after);
        entity.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
