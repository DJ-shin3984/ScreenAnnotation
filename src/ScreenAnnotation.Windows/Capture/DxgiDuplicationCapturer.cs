using Microsoft.Extensions.Logging;
using ScreenAnnotation.Core.Capture;
using ScreenAnnotation.Core.Monitors;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using MapFlags = Vortice.Direct3D11.MapFlags;
using ResultCode = Vortice.DXGI.ResultCode;

namespace ScreenAnnotation.Windows.Capture;

/// <summary>
/// DXGI Desktop Duplication. 출력별 duplication 객체를 생성 시점에 미리 만들어 둔다.
/// AcquireNextFrame이 타임아웃이면(마지막 획득 후 화면 변화 없음) 직전 프레임 복사본을 돌려준다.
/// </summary>
public sealed class DxgiDuplicationCapturer : IDisposable
{
    private const uint AcquireTimeoutMs = 20; // 화면 변화가 없으면 캐시를 쓰므로 길게 기다릴 이유가 없다
    private readonly ILogger _log;
    private readonly Dictionary<string, Output> _outputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ID3D11Device> _devices = new();

    public DxgiDuplicationCapturer(ILogger log)
    {
        _log = log;
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
        {
            using (adapter)
            {
                if (D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport, null, out var device).Failure || device is null)
                    continue;
                _devices.Add(device);
                for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
                {
                    using (output)
                    {
                        var name = output.Description.DeviceName;
                        try
                        {
                            _outputs[name] = new Output(device, output.QueryInterface<IDXGIOutput1>());
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning(ex, "DXGI duplication 생성 실패: {Device}", name);
                        }
                    }
                }
            }
        }
        _log.LogInformation("DXGI duplication 준비: {Outputs}", string.Join(", ", _outputs.Keys));
    }

    public bool Supports(MonitorInfo monitor) => _outputs.ContainsKey(monitor.DeviceName);

    /// <returns>프레임, 또는 아직 캐시가 없고 화면 변화도 없어 캡처할 수 없으면 null.</returns>
    public CapturedFrame? Capture(MonitorInfo monitor)
    {
        if (!_outputs.TryGetValue(monitor.DeviceName, out var output)) return null;
        var bgra = output.Acquire(_log);
        if (bgra is null) return null;
        var (w, h) = output.Size;
        if (w != monitor.Bounds.Width || h != monitor.Bounds.Height)
        {
            _log.LogWarning("DXGI 출력 크기 {W}x{H}가 모니터 경계 {MW}x{MH}와 다름 (회전/스케일?) — GDI로", w, h, monitor.Bounds.Width, monitor.Bounds.Height);
            return null; // ponytail: 회전된 출력은 미지원, 필요해지면 Rotation에 따라 픽셀 회전
        }
        return new CapturedFrame(monitor, w, h, w * 4, bgra, "DXGI");
    }

    public void Dispose()
    {
        foreach (var o in _outputs.Values) o.Dispose();
        foreach (var d in _devices) d.Dispose();
        _outputs.Clear();
        _devices.Clear();
    }

    private sealed class Output : IDisposable
    {
        private readonly ID3D11Device _device;
        private readonly IDXGIOutput1 _output;
        private IDXGIOutputDuplication? _dup;
        private ID3D11Texture2D? _staging;
        private byte[]? _last;

        public Output(ID3D11Device device, IDXGIOutput1 output)
        {
            _device = device;
            _output = output;
            Recreate();
        }

        public (int Width, int Height) Size { get; private set; }

        private void Recreate()
        {
            _dup?.Dispose();
            _dup = _output.DuplicateOutput(_device);
            var mode = _dup.Description.ModeDescription;
            Size = ((int)mode.Width, (int)mode.Height);
            _staging?.Dispose();
            _staging = _device.CreateTexture2D(new Texture2DDescription
            {
                Width = mode.Width,
                Height = mode.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                CPUAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
            });
            _last = null;
        }

        public byte[]? Acquire(ILogger log)
        {
            lock (_device) // 같은 어댑터의 ImmediateContext는 스레드 안전하지 않다
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var r = _dup!.AcquireNextFrame(AcquireTimeoutMs, out _, out var resource);
                    if (r == ResultCode.WaitTimeout) return _last; // 변화 없음 → 직전 프레임 (없으면 null)
                    if (r == ResultCode.AccessLost)
                    {
                        log.LogWarning("DXGI ACCESS_LOST — duplication 재생성");
                        Recreate();
                        continue;
                    }
                    if (r.Failure) { log.LogWarning("AcquireNextFrame 실패 {Result}", r); return _last; }

                    try
                    {
                        using var tex = resource.QueryInterface<ID3D11Texture2D>();
                        var ctx = _device.ImmediateContext;
                        ctx.CopyResource(_staging!, tex);
                        var map = ctx.Map(_staging!, 0, MapMode.Read, MapFlags.None);
                        try
                        {
                            var (w, h) = Size;
                            var buf = new byte[w * 4 * h];
                            unsafe
                            {
                                var src = (byte*)map.DataPointer;
                                for (var y = 0; y < h; y++)
                                    new ReadOnlySpan<byte>(src + y * map.RowPitch, w * 4).CopyTo(buf.AsSpan(y * w * 4));
                            }
                            _last = buf;
                            return buf;
                        }
                        finally
                        {
                            ctx.Unmap(_staging!, 0);
                        }
                    }
                    finally
                    {
                        resource.Dispose();
                        _dup.ReleaseFrame();
                    }
                }
                return _last;
            }
        }

        public void Dispose()
        {
            _staging?.Dispose();
            _dup?.Dispose();
            _output.Dispose();
        }
    }
}
