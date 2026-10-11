using System.Numerics;
namespace AudioSignalInspector.Core;
public record Metrics(double RmsDbfs, double PeakDbfs);
public record Bin(double Hz, double Dbfs);
public static class Analysis
{
    public static double Db(double amplitude) => 20 * Math.Log10(Math.Max(1e-10, amplitude));
    public static Metrics Measure(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0) throw new ArgumentException("Sem amostras.");
        double power = 0, peak = 0;
        foreach (var x in samples) { if (!float.IsFinite(x)) throw new ArgumentException("Amostra inválida."); power += (double)x*x; peak = Math.Max(peak, Math.Abs(x)); }
        return new(Db(Math.Sqrt(power/samples.Length)), Db(peak));
    }
    public static float[] Decode(byte[] bytes, int count, int bits, bool floating, int channels, int channel)
    {
        if (channels < 1 || channel < 0 || channel >= channels || count < 0 || count > bytes.Length || (floating ? bits != 32 : bits is not (8 or 16 or 24 or 32))) throw new ArgumentException("Formato inválido ou não suportado.");
        int width = bits/8, frame = width*channels;
        if(count % frame != 0) throw new ArgumentException("Frame incompleto.");
        var result = new float[count/frame];
        for(int i=0;i<result.Length;i++)
        {
            int p=i*frame+channel*width;
            if(floating) result[i]=BitConverter.ToSingle(bytes,p);
            else if(bits==8) result[i]=(bytes[p]-128)/128f;
            else if(bits==16) result[i]=BitConverter.ToInt16(bytes,p)/32768f;
            else if(bits==32) result[i]=BitConverter.ToInt32(bytes,p)/2147483648f;
            else { int raw=bytes[p]|bytes[p+1]<<8|bytes[p+2]<<16; if((raw&0x800000)!=0) raw|=unchecked((int)0xff000000); result[i]=raw/8388608f; }
            if(!float.IsFinite(result[i])) throw new ArgumentException("Amostra não finita.");
        }
        return result;
    }
    public static double QuietEstimate(IEnumerable<double> windows)
    {
        var sorted=windows.Order().ToArray(); if(sorted.Length==0 || sorted.Any(x=>!double.IsFinite(x))) throw new ArgumentException("Sem janelas.");
        return sorted[(int)((sorted.Length-1)*0.1)];
    }
    public static Bin[] Spectrum(float[] samples, int sampleRate)
    {
        int n=samples.Length;
        if(n<4 || (n&(n-1))!=0 || sampleRate<=0) throw new ArgumentException("FFT inválida.");
        var data=new Complex[n]; double windowSum=0;
        for(int i=0;i<n;i++) { if(!float.IsFinite(samples[i])) throw new ArgumentException("Amostra inválida."); double w=0.5-0.5*Math.Cos(2*Math.PI*i/(n-1)); windowSum+=w; data[i]=new Complex(samples[i]*w,0); }
        for(int i=1,j=0;i<n;i++) { int bit=n>>1; for(;(j&bit)!=0;bit>>=1) j^=bit; j^=bit; if(i<j) (data[i],data[j])=(data[j],data[i]); }
        for(int len=2;len<=n;len<<=1) { Complex step=Complex.FromPolarCoordinates(1,-2*Math.PI/len); for(int i=0;i<n;i+=len) { Complex w=Complex.One; for(int j=0;j<len/2;j++) { var u=data[i+j];var v=data[i+j+len/2]*w;data[i+j]=u+v;data[i+j+len/2]=u-v;w*=step; } } }
        return Enumerable.Range(0,n/2+1).Select(i=>new Bin(i*(double)sampleRate/n,Db(data[i].Magnitude*(i==0||i==n/2?1:2)/windowSum))).ToArray();
    }
}

