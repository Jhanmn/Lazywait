using System.Linq.Expressions;
using Lazywait.Tests.Stubs;

namespace Lazywait.Tests;

[TestFixture]
internal class WhenIsTests : AwaitableContractTests
{
    protected override Task Wait<TValue>(
        TestClass owner,
        Expression<Func<TestClass, TValue>> selector,
        TValue expected,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => owner.WhenIs(PropertyNameOf(selector), expected, timeout, cancellationToken);

    [Test]
    public void Throws_ArgumentNullException_when_owner_is_null()
    {
        TestClass owner = null!;
        Assert.Throws<ArgumentNullException>(() =>
            owner.WhenIs(nameof(TestClass.CurrentPosition), 1));
    }

    [Test]
    public void Throws_ArgumentException_for_empty_name()
    {
        var device = new TestClass();
        Assert.Throws<ArgumentException>(() =>
            device.WhenIs("  ", 1));
    }

    [Test]
    public void Throws_ArgumentException_for_unknown_property()
    {
        var device = new TestClass();
        Assert.Throws<ArgumentException>(() =>
            device.WhenIs("DoesNotExist", 1));
    }
}
