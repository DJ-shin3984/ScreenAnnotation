using System.IO;
using ScreenAnnotation.App;

namespace ScreenAnnotation.App.Tests;

public class FirstRunWindowTests
{
    [Fact]
    public void ShowsUntilMarked()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sa-firstrun-" + Guid.NewGuid().ToString("N"));
        FirstRunWindow.MarkerPath = Path.Combine(dir, "firstrun.done");
        try
        {
            FirstRunWindow.ShouldShow().Should().BeTrue();
            FirstRunWindow.MarkShown();
            FirstRunWindow.ShouldShow().Should().BeFalse();
            File.Exists(FirstRunWindow.MarkerPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
