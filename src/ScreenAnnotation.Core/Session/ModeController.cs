namespace ScreenAnnotation.Core.Session;

public enum Mode { Off, Capturing, Active }

/// <summary>핫키 한 번에 대한 응답.</summary>
public enum HotkeyAction { Ignore, Start, Stop }

/// <summary>켜기·끄기 상태 전환 규칙. Off → Capturing → Active → Off. 순수 로직, 스레드 안전하지 않음(UI 스레드 전용).</summary>
public sealed class ModeController
{
    public Mode Mode { get; private set; } = Mode.Off;

    public event Action<Mode>? ModeChanged;

    /// <summary>Off면 캡처 시작, Active면 끄기, Capturing 중이면 무시.</summary>
    public HotkeyAction OnHotkey() => Mode switch
    {
        Mode.Off => Transition(Mode.Capturing, HotkeyAction.Start),
        Mode.Active => Transition(Mode.Off, HotkeyAction.Stop),
        _ => HotkeyAction.Ignore,
    };

    /// <summary>캡처가 끝나 오버레이가 떴을 때. Capturing이 아니면 false(그 사이 취소됨).</summary>
    public bool Activate()
    {
        if (Mode != Mode.Capturing) return false;
        Transition(Mode.Active, HotkeyAction.Ignore);
        return true;
    }

    /// <summary>캡처 실패·강제 종료 등 어떤 상태에서든 Off로.</summary>
    public void Deactivate()
    {
        if (Mode != Mode.Off) Transition(Mode.Off, HotkeyAction.Ignore);
    }

    private HotkeyAction Transition(Mode next, HotkeyAction result)
    {
        Mode = next;
        ModeChanged?.Invoke(next);
        return result;
    }
}
