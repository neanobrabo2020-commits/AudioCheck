namespace AudioSignalInspector.Core;

// Fixed 100 ms windows, independent of driver buffer sizes.
public sealed class MeasurementWindow
{
    private readonly float[] pending;
    private int used;
    private readonly Queue<Metrics> history = new();
    public MeasurementWindow(int sampleRate)
    {
        if (sampleRate < 10) throw new ArgumentException("Taxa inválida.");
        pending = new float[sampleRate / 10];
    }
    public int Count => history.Count;
    public Metrics? Latest { get; private set; }
    public double? QuietDbfs => history.Count == 0 ? null : Analysis.QuietEstimate(history.Select(m => m.RmsDbfs));
    public void Add(ReadOnlySpan<float> samples)
    {
        foreach (float sample in samples)
        {
            if (!float.IsFinite(sample)) throw new ArgumentException("Amostra inválida.");
            pending[used++] = sample;
            if (used != pending.Length) continue;
            Latest = Analysis.Measure(pending);
            history.Enqueue(Latest);
            if (history.Count > 100) history.Dequeue();
            used = 0;
        }
    }
}
