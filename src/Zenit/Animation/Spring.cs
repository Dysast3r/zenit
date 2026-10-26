namespace Zenit.Animation;

/// <summary>
/// Resorte amortiguado (masa 1). Es el mismo modelo que usa iOS: las animaciones se pueden
/// interrumpir en cualquier momento y conservan la velocidad, por eso se sienten físicas.
/// </summary>
public sealed class Spring(double value)
{
    private const double StepSize = 1.0 / 240.0;

    public double Value { get; set; } = value;
    public double Target { get; set; } = value;
    public double Velocity { get; set; }

    public double Stiffness { get; set; } = 300;
    public double Damping { get; set; } = 26;

    public bool IsSettled => Math.Abs(Value - Target) < 0.01 && Math.Abs(Velocity) < 0.01;

    public void Configure(double stiffness, double damping)
    {
        Stiffness = stiffness;
        Damping = damping;
    }

    /// <summary>Lleva el resorte a una posición sin animar (p. ej. mientras se arrastra).</summary>
    public void Snap(double value)
    {
        Value = Target = value;
        Velocity = 0;
    }

    public void Step(double dt)
    {
        if (IsSettled)
        {
            Value = Target;
            Velocity = 0;
            return;
        }

        // Euler semi-implícito con subpasos fijos: estable aunque el frame tarde.
        while (dt > 0)
        {
            var h = Math.Min(StepSize, dt);
            var force = -Stiffness * (Value - Target) - Damping * Velocity;
            Velocity += force * h;
            Value += Velocity * h;
            dt -= h;
        }
    }
}
