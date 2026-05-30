using Lazywait.Tests.Stubs;

namespace Lazywait.Tests;

[TestFixture]
public class WhenIsTests
{
    [Test]
    [CancelAfter(1000)]
    public async Task Completes_when_property_reaches_value()
    {
        var device = new TestClass();
        var waiter =  device.WhenIs(observableName: nameof(TestClass.CurrentPosition), value: 2);

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            device.CurrentPosition = 1;
            await Task.Delay(50);
            device.CurrentPosition = 2;
        });

        await waiter;
    }
    
}