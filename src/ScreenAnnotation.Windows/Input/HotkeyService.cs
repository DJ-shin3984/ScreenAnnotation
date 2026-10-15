using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Input;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.PInvoke;

namespace ScreenAnnotation.Windows.Input;

/// <summary>
/// 숨은 메시지 전용 창(HWND_MESSAGE)에 RegisterHotKey로 전역 단축키를 건다.
/// WM_HOTKEY는 창을 만든 스레드의 메시지 펌프로 오므로 Register는 반드시
/// 메시지 루프가 도는 스레드(WPF UI 스레드)에서 호출해야 한다. 이벤트도 그 스레드에서 발생한다.
/// </summary>
public sealed unsafe class HotkeyService : IHotkeyService, IDisposable
{
    private const int HotkeyId = 1;
    private const string ClassName = "ScreenAnnotation.HotkeyWindow";

    // GC가 델리게이트를 거두지 않도록 static으로 고정
    private static readonly WNDPROC WndProcDelegate = WndProc;
    private static HotkeyService? _instance; // 창 하나 = 서비스 하나

    private readonly ILogger<HotkeyService> _logger;
    private HWND _hwnd;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public HotkeyService(ILogger<HotkeyService> logger)
    {
        _logger = logger;
        _instance = this;
    }

    public bool Register(HotkeyGesture gesture)
    {
        Unregister();
        EnsureWindow();

        if (!TryGetVirtualKey(gesture.Key, out var vk))
        {
            _logger.LogWarning("알 수 없는 키 이름: {Key}", gesture.Key);
            return false;
        }

        var mods = (HOT_KEY_MODIFIERS)gesture.Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT;
        if (!RegisterHotKey(_hwnd, HotkeyId, mods, (uint)vk))
        {
            _logger.LogWarning("핫키 등록 실패 {Gesture}: Win32 오류 {Error}", gesture, Marshal.GetLastWin32Error());
            return false;
        }

        _registered = true;
        _logger.LogInformation("핫키 등록 {Gesture}", gesture);
        return true;
    }

    /// <summary>실행 중인 인스턴스의 핫키 창에 WM_HOTKEY를 보낸다 (`--toggle` 인자, 테스트·스크립트용). 못 찾으면 false.</summary>
    public static bool PostToggle()
    {
        var hwnd = FindWindowEx(HWND.HWND_MESSAGE, default, ClassName, null);
        if (hwnd.IsNull) return false;
        return PostMessage(hwnd, WM_HOTKEY, new WPARAM((nuint)HotkeyId), default);
    }

    public void Unregister()
    {
        if (!_registered) return;
        UnregisterHotKey(_hwnd, HotkeyId);
        _registered = false;
    }

    public void Dispose()
    {
        Unregister();
        if (!_hwnd.IsNull)
        {
            DestroyWindow(_hwnd);
            _hwnd = default;
        }
        if (_instance == this) _instance = null;
    }

    private void EnsureWindow()
    {
        if (!_hwnd.IsNull) return;

        fixed (char* cls = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = WndProcDelegate,
                lpszClassName = cls,
            };
            RegisterClassEx(in wc); // 이미 등록돼 있으면 실패해도 무방

            _hwnd = CreateWindowEx(0, ClassName, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, null, null, null);
        }

        if (_hwnd.IsNull)
            throw new InvalidOperationException($"핫키 창 생성 실패: Win32 오류 {Marshal.GetLastWin32Error()}");
    }

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM_HOTKEY && (int)wParam.Value == HotkeyId)
        {
            _instance?._logger.LogDebug("WM_HOTKEY");
            _instance?.HotkeyPressed?.Invoke(_instance, EventArgs.Empty);
            return (LRESULT)0;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static bool TryGetVirtualKey(string key, out VIRTUAL_KEY vk)
    {
        vk = default;
        if (key.Equals("Space", StringComparison.OrdinalIgnoreCase)) { vk = VIRTUAL_KEY.VK_SPACE; return true; }
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)) { vk = (VIRTUAL_KEY)c; return true; } // A-Z, 0-9는 ASCII와 같다
            return false;
        }
        if (key[0] is 'F' or 'f' && int.TryParse(key[1..], out var n) && n is >= 1 and <= 12)
        {
            vk = (VIRTUAL_KEY)((int)VIRTUAL_KEY.VK_F1 + n - 1);
            return true;
        }
        return false;
    }
}
