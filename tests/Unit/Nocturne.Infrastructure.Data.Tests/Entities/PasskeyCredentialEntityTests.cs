namespace Nocturne.Infrastructure.Data.Tests.Entities;

public class PasskeyCredentialEntityTests
{
    [Fact]
    public void Should_Implement_ITenantScoped()
    {
        var entity = new PasskeyCredentialEntity();

        entity.Should().BeAssignableTo<ITenantScoped>();
    }

    [Fact]
    public void CreatedAt_Should_Default_To_UtcNow()
    {
        var before = DateTime.UtcNow;
        var entity = new PasskeyCredentialEntity();
        var after = DateTime.UtcNow;

        entity.CreatedAt.Should().BeOnOrAfter(before);
        entity.CreatedAt.Should().BeOnOrBefore(after);
        entity.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
