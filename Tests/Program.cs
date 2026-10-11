using AudioSignalInspector.Core;
int passed=0;
void Check(string name,Action test) { test(); Console.WriteLine("PASS "+name);passed++; }
void Near(double actual,double expected,double tolerance=0.001) { if(!double.IsFinite(actual) || Math.Abs(actual-expected)>tolerance) throw new Exception($"{actual} != {expected}"); }
void Throws(Action action) { try { action(); } catch(ArgumentException) { return; } throw new Exception("Exceção esperada"); }
Check("silêncio",()=>Near(Analysis.Measure(new float[256]).RmsDbfs,-200));
Check("DC e pico",()=>{var m=Analysis.Measure(Enumerable.Repeat(0.5f,256).ToArray());Near(m.RmsDbfs,-6.0206);Near(m.PeakDbfs,-6.0206);});
Check("seno RMS",()=>Near(Analysis.Measure(Enumerable.Range(0,4096).Select(i=>(float)(0.5*Math.Sin(2*Math.PI*128*i/4096))).ToArray()).RmsDbfs,-9.0309));
Check("FFT amplitude e frequência",()=>{var s=Analysis.Spectrum(Enumerable.Range(0,4096).Select(i=>(float)(0.5*Math.Sin(2*Math.PI*128*i/4096))).ToArray(),48000);var b=s.MaxBy(b=>b.Dbfs)!;Near(b.Hz,1500);Near(b.Dbfs,-6.0206,0.01);});
Check("canais em oposição preservados",()=>{var bytes=new byte[]{0,64,0,192};Near(Analysis.Decode(bytes,4,16,false,2,0)[0],0.5);Near(Analysis.Decode(bytes,4,16,false,2,1)[0],-0.5);});
Check("PCM24 sinal",()=>Near(Analysis.Decode(new byte[]{0,0,128},3,24,false,1,0)[0],-1));
Check("PCM8",()=>Near(Analysis.Decode(new byte[]{128},1,8,false,1,0)[0],0));
Check("PCM32",()=>Near(Analysis.Decode(BitConverter.GetBytes(int.MinValue),4,32,false,1,0)[0],-1));
Check("float32",()=>Near(Analysis.Decode(BitConverter.GetBytes(0.25f),4,32,true,1,0)[0],0.25));
Check("dados inválidos",()=>{Throws(()=>Analysis.Measure(new[]{float.NaN}));Throws(()=>Analysis.Decode(new byte[3],3,16,false,1,0));Throws(()=>Analysis.Spectrum(new float[7],48000));});
Check("percentil",()=>Near(Analysis.QuietEstimate(new[]{-90.0,-60,-30,-20,-10}),-90));
Check("FFT rejeita NaN",()=>Throws(()=>Analysis.Spectrum(new[]{float.NaN,0,0,0},48000)));
Check("percentil rejeita NaN",()=>Throws(()=>Analysis.QuietEstimate(new[]{double.NaN})));
Check("janelas independem de buffers",()=>{
    var samples=Enumerable.Range(0,480000).Select(i=>(float)(0.1*Math.Sin(2*Math.PI*1000*i/48000))).ToArray();
    var a=new MeasurementWindow(48000); var b=new MeasurementWindow(48000);
    a.Add(samples);
    for(int offset=0;offset<samples.Length;offset+=137) b.Add(samples.AsSpan(offset,Math.Min(137,samples.Length-offset)));
    if(a.Count!=100 || b.Count!=100) throw new Exception("Contagem incorreta");
    Near(a.QuietDbfs!.Value,b.QuietDbfs!.Value); Near(a.QuietDbfs.Value,-23.0103);
});
Check("janela parcial não produz medição",()=>{var m=new MeasurementWindow(48000);m.Add(new float[4799]);if(m.Count!=0 || m.Latest!=null)throw new Exception();});
Check("FFT separa 50 e 60 Hz",()=>{
    foreach(int hz in new[]{50,60}) {
        var bins=Analysis.Spectrum(Enumerable.Range(0,32768).Select(i=>(float)(0.01*Math.Sin(2*Math.PI*hz*i/48000))).ToArray(),48000);
        Near(bins.MaxBy(b=>b.Dbfs)!.Hz,hz,1.47);
        if(!Diagnosis.Suggest(bins,-40).Any(s=>s.Contains($"{hz} Hz")))throw new Exception("Tonal não detectado");
    }
});
Check("silêncio não sugere interferência",()=>{if(Diagnosis.Suggest(Analysis.Spectrum(new float[32768],48000),-200).Any(s=>s.Contains("Componente tonal")))throw new Exception();});
Check("clipping gera orientação",()=>{if(!Diagnosis.Suggest(Array.Empty<Bin>(),0).Any(s=>s.Contains("clipping")))throw new Exception();});
Console.WriteLine($"{passed} testes aprovados");

