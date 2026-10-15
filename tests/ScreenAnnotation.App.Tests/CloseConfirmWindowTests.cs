using System.Windows.Threading;
using ScreenAnnotation.App;

namespace ScreenAnnotation.App.Tests;

public class CloseConfirmWindowTests
{
    [Theory]
    [InlineData(CloseChoice.Save)]
    [InlineData(CloseChoice.Discard)]
    [InlineData(CloseChoice.Continue)]
    public void ChooseSetsResult(CloseChoice choice)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            CloseConfirmWindow? w = null;
            try
            {
                w = new CloseConfirmWindow();
                w.Result.Should().Be(CloseChoice.Continue, "닫히기 전 기본값");
                w.Choose(choice);
                w.Result.Should().Be(choice);
            }
            catch (Exception e) { error = e; }
            finally
            {
                w?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error.Should().BeNull();
    }
}
