using Microsoft.Extensions.Logging.Abstractions;
using ScreenAnnotation.Recognition;
using ScreenAnnotation.Windows.Windows;

namespace ScreenAnnotation.Windows.IntegrationTests;

public class UiaElementLocatorTests
{
    private readonly UiaElementLocator _locator = new(NullLogger<UiaElementLocator>.Instance);

    [Fact]
    public async Task FindAsync_AtWindowCenter_ReturnsWithinTimeout()
    {
        var snapshot = new WindowSnapshotService(NullLogger<WindowSnapshotService>.Instance).Take();
        var window = snapshot.Windows.FirstOrDefault();
        if (window is null) return; // 빈 데스크톱이면 확인할 것이 없다

        var (x, y) = (window.Bounds.X + window.Bounds.Width / 2, window.Bounds.Y + window.Bounds.Height / 2);
        var task = _locator.FindAsync(x, y, snapshot, CancellationToken.None);

        (await Task.WhenAny(task, Task.Delay(1000))).Should().BeSameAs(task);
        var element = await task;
        if (element is not null)
        {
            element.Bounds.Contains(x, y).Should().BeTrue();
            element.Method.Should().Be("Snapshot+Descend");
        }
    }

    [Fact]
    public async Task FindFromPointAsync_DoesNotThrow()
    {
        var act = () => _locator.FindFromPointAsync(10, 10, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
