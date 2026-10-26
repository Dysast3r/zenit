using System.Diagnostics;

namespace Zenit.Animation;

/// <summary>Velocidad del puntero en px/s a partir de las muestras de los últimos 100 ms.</summary>
public sealed class VelocityTracker
{
    private readonly Queue<(long ticks, double x)> _samples = new();

    public void Reset() => _samples.Clear();

    public void Add(double x)
    {
        var now = Stopwatch.GetTimestamp();
        _samples.Enqueue((now, x));
        while (_samples.Count > 2 && Stopwatch.GetElapsedTime(_samples.Peek().ticks, now).TotalMilliseconds > 100)
            _samples.Dequeue();
    }

    public double Velocity
    {
        get
        {
            // Si el puntero se quedó quieto antes de soltar, las muestras viejas darían una velocidad que ya no existe.
            var now = Stopwatch.GetTimestamp();
            var recent = _samples.Where(s => Stopwatch.GetElapsedTime(s.ticks, now).TotalMilliseconds <= 100).ToList();
            if (recent.Count < 2) return 0;
            var first = recent[0];
            var last = recent[^1];
            var seconds = Stopwatch.GetElapsedTime(first.ticks, last.ticks).TotalSeconds;
            return seconds <= 0 ? 0 : (last.x - first.x) / seconds;
        }
    }
}
