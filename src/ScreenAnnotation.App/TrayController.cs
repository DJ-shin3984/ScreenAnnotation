using System.Drawing;
using System.Windows.Forms;

namespace ScreenAnnotation.App;

public sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon = new() { Text = "Screen Annotation", Icon = SystemIcons.Application };
    private readonly ContextMenuStrip _menu = new();

    public TrayController()
    {
        _icon.ContextMenuStrip = _menu;
        AddItem("종료", () => System.Windows.Application.Current.Shutdown());
    }

    public void Show() => _icon.Visible = true;

    public void AddItem(string text, Action action) => _menu.Items.Add(text, null, (_, _) => action());

    public void ShowBalloon(string title, string text) => _icon.ShowBalloonTip(3000, title, text, ToolTipIcon.Warning);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
