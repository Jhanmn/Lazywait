using System.Linq.Expressions;
using Lazywait.Tests.Stubs;

namespace Lazywait.Tests;

[TestFixture]
internal class WaitUntilTests : AwaitableContractTests
{
    protected override Task Wait<TValue>(
        TestClass owner,
        Expression<Func<TestClass, TValue>> selector,
        TValue expected,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => owner.WaitUntil(selector, expected, timeout, cancellationToken);

    [Test]
    public void Throws_ArgumentNullException_when_owner_is_null()
    {
        TestClass owner = null!;
        Assert.Throws<ArgumentNullException>(() =>
            owner.WaitUntil(x => x.CurrentPosition, 1));
    }

    [Test]
    public void Throws_ArgumentNullException_when_selector_is_null()
    {
        var device = new TestClass();
        Assert.Throws<ArgumentNullException>(() =>
            device.WaitUntil<TestClass, int>(null!, 1));
    }

    [Test]
    public void Throws_ArgumentException_for_non_property_selector()
    {
        var device = new TestClass();
        Assert.Throws<ArgumentException>(() =>
            device.WaitUntil(x => x.CurrentPosition + 1, 1));
    }

    [Test]
    public void Throws_ArgumentException_for_method_call_selector()
    {
        var device = new TestClass();
        Assert.Throws<ArgumentException>(() =>
            device.WaitUntil(x => x.ToString(), "x"));
    }
}
