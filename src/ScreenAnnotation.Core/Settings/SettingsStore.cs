namespace ScreenAnnotation.Core.Settings;

/// <summary>실행 중 바뀌는 설정의 현재값. 소비자는 쓸 때마다 Current를 읽는다.</summary>
public sealed class SettingsStore(Settings initial)
{
    public Settings Current { get; private set; } = initial;

    public event Action? Changed;

    public void Update(Func<Settings, Settings> change)
    {
        Current = change(Current);
        Changed?.Invoke();
    }
}
