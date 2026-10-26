using Zenit.Interop;

namespace Zenit.Services;

/// <summary>
/// Nivel de pico del dispositivo de salida (Core Audio). Solo lee un número 0..1:
/// no graba ni captura el audio.
/// </summary>
public sealed class AudioMeter : IDisposable
{
    private IAudioMeterInformation? _meter;
    private DateTime _acquiredAt = DateTime.MinValue;

    /// <summary>Devuelve el pico actual o -1 si no hay medidor disponible.</summary>
    public float GetPeak()
    {
        // Se reobtiene cada pocos segundos para seguir los cambios de dispositivo predeterminado.
        if ((DateTime.UtcNow - _acquiredAt).TotalSeconds > 5)
        {
            _acquiredAt = DateTime.UtcNow;
            CoreAudio.Release(_meter);
            _meter = CoreAudio.ActivateOnDefaultOutput<IAudioMeterInformation>();
        }
        if (_meter is null) return -1;

        try
        {
            return _meter.GetPeakValue(out var peak) == 0 ? peak : -1;
        }
        catch
        {
            CoreAudio.Release(_meter);
            _meter = null;
            return -1;
        }
    }

    public void Dispose() => CoreAudio.Release(_meter);
}
