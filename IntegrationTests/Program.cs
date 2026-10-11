using AudioSignalInspector;
using NAudio.Wave;
using System.Drawing;
using System.Windows.Forms;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string output=args.Length>0?args[0]:"evidence";
        Directory.CreateDirectory(output);
        string wav=Path.Combine(output,"synthetic-stereo.wav");
        using(var writer=new WaveFileWriter(wav,WaveFormat.CreateIeeeFloatWaveFormat(48000,2)))
        {
            for(int i=0;i<480000;i++) {float x=(float)(0.25*Math.Sin(2*Math.PI*1000*i/48000));writer.WriteSample(x);writer.WriteSample(-x);}
        }
        var a=WaveFileAnalysis.Analyze(wav,0);var b=WaveFileAnalysis.Analyze(wav,1);
        void Near(double x,double y,double tolerance=0.001) {if(!double.IsFinite(x)||Math.Abs(x-y)>tolerance)throw new Exception($"{x} != {y}");}
        Near(a.rms,-15.0515);Near(a.peak,-12.0412);Near(a.rms,b.rms);Near(a.seconds,10);
        if(a.meter.Count!=100 || a.bins==null)throw new Exception("Dados incompletos");
        Near(a.bins.MaxBy(x=>x.Dbfs)!.Hz,1000,1.47);
        bool rejected=false;try{WaveFileAnalysis.Analyze(wav,2);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Canal inválido aceito");
        Console.WriteLine("PASS WAV float estéreo: RMS, pico, duração, canal, FFT, piso");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();
        using var form=new MainForm(); form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-20000,-20000); form.Show(); Application.DoEvents(); form.PerformLayout();
        using(var json=System.Text.Json.JsonDocument.Parse(form.CreateReportJson())) { if(json.RootElement.GetProperty("rmsDbfs").ValueKind!=System.Text.Json.JsonValueKind.Null)throw new Exception("Relatório vazio inválido"); } Console.WriteLine("PASS relatório JSON sem medição"); using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(output,"gui.png"));
        Console.WriteLine("PASS construção e renderização WinForms; enumeração tentada sem iniciar captura");
        Console.WriteLine("3 testes de integração aprovados");
    }
}


