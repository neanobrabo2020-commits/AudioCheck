using NAudio.Wave;
using AudioSignalInspector.Core;
namespace AudioSignalInspector;
public static class WaveFileAnalysis
{
    public const int FftSize = 32768;
    public static (MeasurementWindow meter, int rate, Bin[]? bins, double rms, double peak, double seconds) Analyze(string path, int channel)
    {
        if(channel<0) throw new ArgumentException("Canal inválido.");
                using var reader = new AudioFileReader(path);
                if(channel >= reader.WaveFormat.Channels) throw new ArgumentException("Canal ausente no arquivo.");
                var meter = new MeasurementWindow(reader.WaveFormat.SampleRate);
                var tail = new Queue<float>(FftSize);
                var buffer = new float[reader.WaveFormat.Channels*4096];
                var single=new float[1]; int read; long count=0; double power=0, max=0;
                while((read=reader.Read(buffer,0,buffer.Length))>0)
                {
                    for(int i=channel;i<read;i+=reader.WaveFormat.Channels)
                    {
                        float value=buffer[i];
                        single[0]=value; meter.Add(single); count++; power+=(double)value*value; max=Math.Max(max,Math.Abs(value));
                        tail.Enqueue(value); if(tail.Count>FftSize) tail.Dequeue();
                    }
                }
                if(count==0) throw new ArgumentException("Arquivo sem amostras.");
                return (meter, rate:reader.WaveFormat.SampleRate, bins:tail.Count==FftSize?Analysis.Spectrum(tail.ToArray(),reader.WaveFormat.SampleRate):null,
                    rms:Analysis.Db(Math.Sqrt(power/count)), peak:Analysis.Db(max), seconds:(double)count/reader.WaveFormat.SampleRate);

    }
}
