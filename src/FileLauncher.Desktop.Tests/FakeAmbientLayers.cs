using FileLauncher.Platform;

namespace FileLauncher.Desktop.Tests;

/// <summary>常時の演出を OS の層で描くサービスの偽物（issue/CA_AMBIENT.md §6）。呼ばれた値を記録する。</summary>
internal sealed class FakeAmbientLayers : IAmbientLayerService
{
    public bool IsSupported { get; set; } = true;

    /// <summary>false にすると Attach が null（取り付け失敗）。</summary>
    public bool AttachSucceeds { get; set; } = true;

    public FakeAmbientLayerHost? Host { get; private set; }

    public IAmbientLayerHost? Attach(nint windowHandle) => AttachSucceeds ? Host = new FakeAmbientLayerHost() : null;
}

internal sealed class FakeAmbientLayerHost : IAmbientLayerHost
{
    public AmbientGeometry? Geometry { get; private set; }
    public AmbientOrbSpec? Orbs { get; private set; }
    public AmbientPulseSpec? Pulse { get; private set; }
    public AmbientBeamSpec? Beam { get; private set; }
    public double Opacity { get; private set; } = 1;
    public bool Running { get; private set; }
    public bool Disposed { get; private set; }

    public void SetGeometry(AmbientGeometry geometry) => Geometry = geometry;
    public void SetOrbs(AmbientOrbSpec? orbs) => Orbs = orbs;
    public void SetPulse(AmbientPulseSpec? pulse) => Pulse = pulse;
    public void SetBeam(AmbientBeamSpec? beam) => Beam = beam;
    public void SetOpacity(double opacity) => Opacity = opacity;
    public void SetRunning(bool running) => Running = running;
    public void Dispose() => Disposed = true;
}
