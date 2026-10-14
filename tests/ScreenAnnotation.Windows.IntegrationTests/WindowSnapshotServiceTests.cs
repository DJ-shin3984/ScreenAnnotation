using Microsoft.Extensions.Logging.Abstractions;
using ScreenAnnotation.Windows.Windows;

namespace ScreenAnnotation.Windows.IntegrationTests;

public class WindowSnapshotServiceTests
{
    [Fact]
    public void Take_ExcludesOwnProcessAndEmptyWindows()
    {
        var snapshot = new WindowSnapshotService(NullLogger<WindowSnapshotService>.Instance).Take();

        snapshot.Windows.Should().NotContain(w => w.ProcessId == (uint)Environment.ProcessId);
        snapshot.Windows.Should().OnlyContain(w => w.Bounds.Width > 0 && w.Bounds.Height > 0);
    }
}
