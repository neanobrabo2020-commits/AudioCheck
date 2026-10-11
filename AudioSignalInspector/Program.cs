using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using AudioSignalInspector.Core;
using System.Text.Json;
using System.Diagnostics;

namespace AudioSignalInspector;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var form=new MainForm();
        if(args.Length==2 && args[0]=="--smoke-test")
        {
            form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-20000,-20000);
            form.Shown += (_,_) => form.BeginInvoke(new Action(() =>
            {
                try {
                    System.IO.Directory.CreateDirectory(args[1]);
                    using var bitmap=new Bitmap(form.Width,form.Height);
                    form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));
                    bitmap.Save(System.IO.Path.Combine(args[1],"exe-gui.png"));
                    System.IO.File.WriteAllText(System.IO.Path.Combine(args[1],"startup-report.json"),form.CreateReportJson());
                    System.IO.File.WriteAllText(System.IO.Path.Combine(args[1],"PASS.txt"),"WinForms window rendered; report serialized; no audio capture started.");
                } catch(Exception ex) { Environment.ExitCode=1; System.IO.File.WriteAllText(System.IO.Path.Combine(args[1],"FAIL.txt"),ex.ToString()); }
                finally { form.Close(); }
            }));
        }
        Application.Run(form);
    }
}
public sealed class MainForm : Form
{
    private readonly ComboBox devices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 530 };
    private readonly Button refresh = new() { Text = "Atualizar dispositivos", AutoSize = true };
    private readonly Button start = new() { Text = "Iniciar medição", AutoSize = true };
    private readonly Button stop = new() { Text = "Parar", AutoSize = true, Enabled = false };
    private readonly Label info = new() { AutoSize = true, Text = "Selecione uma entrada de áudio." };
    private readonly Label rms = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), Text = "RMS: — dBFS" };
    private readonly Label peak = new() { AutoSize = true, Text = "Pico: — dBFS" };
    private readonly Label floor = new() { AutoSize = true, Text = "Ruído estimado: — dBFS" };
    private readonly Label spectrum = new() { AutoSize = true, Text = "Espectro: aguardando medição" };
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 250 };
    private readonly object sync = new();
    private readonly List<float> recent = new();
    private MeasurementWindow? windows; private const int FftSize = 32768; private readonly SpectrumView chart = new() { Dock = DockStyle.Top, Height = 230 };
    private MMDeviceEnumerator? enumerator;
    private List<MMDevice> endpoints = new();
    private WasapiCapture? capture;
    private Bin[]? latestSpectrum;
    private readonly NumericUpDown selectedChannel = new() { Minimum = 1, Maximum = 64, Value = 1, Width = 55 };
    private readonly Button inventory = new() { Text = "Sessões / ASIO", AutoSize = true };
    private readonly Button baseline = new() { Text = "Guardar referência A", AutoSize = true };
    private readonly Button import = new() { Text = "Analisar WAV da DAW", AutoSize = true }; private string[] findings = Array.Empty<string>(); private readonly Button export = new() { Text = "Exportar relatório", AutoSize = true };
    private double? referenceRms;
    private string? referenceSource;
    private string? measurementSource;
    private DateTime startedAt;
    private int sampleRate;
    private int captureChannel;
    private readonly List<string> observations = new();
    private double currentRms = double.NaN, currentPeak = double.NaN;

    public MainForm()
    {
        Text = "Audio Signal Inspector — Diagnóstico de hiss";
        Size = new Size(1100, 900);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(241, 245, 249);
        log.BackColor = Color.FromArgb(15, 23, 42); log.ForeColor = Color.FromArgb(226, 232, 240);
        MinimumSize = new Size(700, 510);
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 130, Padding = new Padding(12), AutoScroll = true };
        top.Controls.AddRange(new Control[] { devices, refresh, new Label { Text = "Canal", AutoSize = true }, selectedChannel, start, stop, inventory, baseline, import, export });
        var status = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 145, Padding = new Padding(14), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        status.Controls.AddRange(new Control[] { info, rms, peak, floor, spectrum });
        var heading = new Label { Dock = DockStyle.Top, Height = 38, Padding = new Padding(12, 10, 0, 0), Text = "Eventos e orientações de diagnóstico" };
        Controls.Add(log); Controls.Add(heading); Controls.Add(chart); Controls.Add(status); Controls.Add(top);
        inventory.Click += (_, _) => InspectSessions();
        baseline.Click += (_, _) => { lock(sync) { if(!double.IsFinite(currentRms)) { AddLog("Faça uma medição antes de guardar a referência."); return; } if(windows?.Count < 100) { AddLog("Aguarde 10 segundos completos antes de guardar A."); return; } referenceRms=windows?.QuietDbfs; referenceSource=measurementSource; AddLog($"Referência A: {referenceSource}, {referenceRms:F2} dBFS. Compare sob as mesmas condições."); } };
        import.Click += async (_, _) => await ImportWave(); export.Click += (_, _) => ExportReport();
        refresh.Click += (_, _) => LoadDevices();
        start.Click += (_, _) => StartCapture();
        stop.Click += (_, _) => StopCapture();
        uiTimer.Tick += (_, _) => UpdateDisplay();
        FormClosing += (_, _) => { StopCapture(); uiTimer.Dispose(); foreach (var d in endpoints) d.Dispose(); enumerator?.Dispose(); };
        LoadDevices();
        AddLog("Medições são locais. O programa não salva nem transmite áudio.");
        AddLog("Este MVP captura entradas WASAPI; não revela roteamento interno de DAWs/ASIO.");
    }

    private void AddLog(string text) { if(observations.Count >= 500) observations.RemoveAt(0); observations.Add(text); if(log.TextLength > 100000) log.Clear(); log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}"); }
    private void LoadDevices()
    {
        if (capture != null) { AddLog("Pare a medição antes de atualizar dispositivos."); return; }
        try
        {
            foreach (var d in endpoints) d.Dispose(); endpoints.Clear();
            enumerator?.Dispose(); enumerator = new MMDeviceEnumerator();
            endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active).ToList();
            devices.Items.Clear();
            foreach (var d in endpoints) devices.Items.Add($"{d.DataFlow}: {d.FriendlyName}");
            if (devices.Items.Count > 0) { int input=endpoints.FindIndex(d=>d.DataFlow==DataFlow.Capture); devices.SelectedIndex = input>=0?input:0; }
            AddLog($"{endpoints.Count} endpoints WASAPI ativos (captura e saída) encontradas.");
        }
        catch (Exception ex) { AddLog("Erro ao listar entradas: " + ex.Message); }
    }

    private void StartCapture()
    {
        if (devices.SelectedIndex < 0 || capture != null) return;
        try
        {
            var device = endpoints[devices.SelectedIndex];
            capture = device.DataFlow == DataFlow.Render ? new WasapiLoopbackCapture(device) : new WasapiCapture(device);
            if(selectedChannel.Value > capture.WaveFormat.Channels) throw new InvalidOperationException("Canal selecionado não existe neste endpoint.");
            var activeCapture = capture;
            measurementSource = $"{device.FriendlyName} | {device.DataFlow} | canal {selectedChannel.Value}";
            captureChannel = (int)selectedChannel.Value - 1;
            startedAt = DateTime.UtcNow;
            sampleRate = capture.WaveFormat.SampleRate;
            capture.DataAvailable += CaptureData;
            capture.RecordingStopped += (_, e) =>
            {
                if(IsDisposed || Disposing || !IsHandleCreated) return;
                try { BeginInvoke(new Action(() =>
            {
                if(capture != activeCapture) return;
                if (e.Exception != null) AddLog("Captura interrompida: " + e.Exception.Message);
                StopCapture();
            })); } catch (InvalidOperationException) { }
            };
            lock (sync) { recent.Clear(); windows = new MeasurementWindow(sampleRate); currentRms = double.NaN; currentPeak = double.NaN; latestSpectrum = null; findings = Array.Empty<string>(); }
            capture.StartRecording();
            start.Enabled = false; stop.Enabled = true; refresh.Enabled = false; devices.Enabled = false; selectedChannel.Enabled = false;
            info.Text = $"Capturando: {device.FriendlyName}";
            AddLog($"Início: {device.FriendlyName} | {capture.WaveFormat.SampleRate} Hz | {capture.WaveFormat.Channels} canais | {capture.WaveFormat.Encoding}");
            AddLog("Fique em silêncio por 10 segundos para estimar o piso de ruído.");
            uiTimer.Start();
        }
        catch (Exception ex) { AddLog("Não foi possível iniciar captura: " + ex.Message); StopCapture(); }
    }

    private void CaptureData(object? sender, WaveInEventArgs e)
    {
        if(sender is not WasapiCapture active || active != capture) return; var fmt = active.WaveFormat;
        if (fmt == null) return;
        float[] values;
        try
        {
            bool floating = fmt.Encoding == WaveFormatEncoding.IeeeFloat;
            if(fmt is WaveFormatExtensible ext)
            {
                floating = ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
                if(!floating && ext.SubFormat != new Guid("00000001-0000-0010-8000-00aa00389b71")) throw new NotSupportedException("Subformato WASAPI não suportado.");
            }
            else if(!floating && fmt.Encoding != WaveFormatEncoding.Pcm) throw new NotSupportedException("Formato WASAPI não suportado.");
            values = Analysis.Decode(e.Buffer, e.BytesRecorded, fmt.BitsPerSample, floating, fmt.Channels, captureChannel);
            if(values.Length == 0) return;
        }
        catch(Exception ex)
        {
            var source = sender;
            if(IsHandleCreated && !IsDisposed) try { BeginInvoke(new Action(() => { if(capture == source) { AddLog("Erro na leitura: " + ex.Message); StopCapture(); } })); } catch(InvalidOperationException) { }
            return;
        }

        lock (sync)
        {
            if(active != capture) return; windows!.Add(values); currentRms = windows.Latest?.RmsDbfs ?? double.NaN; currentPeak = windows.Latest?.PeakDbfs ?? double.NaN;
            recent.AddRange(values);
            if (recent.Count > FftSize) recent.RemoveRange(0, recent.Count - FftSize);

        }
    }

    private void UpdateDisplay()
    {
        lock (sync)
        {
            if(capture != null && recent.Count >= FftSize) latestSpectrum = Analysis.Spectrum(recent.ToArray(),sampleRate); rms.Text = $"RMS: {currentRms:F1} dBFS";
            peak.Text = $"Pico: {currentPeak:F1} dBFS";
            if (windows?.QuietDbfs is double quiet)
                floor.Text = $"Ruído estimado: {quiet:F1} dBFS | {windows.Count}/100 janelas de 100 ms";
            if(latestSpectrum != null) findings = Diagnosis.Suggest(latestSpectrum,currentPeak); chart.Bins = latestSpectrum;
            chart.Invalidate();
            if (latestSpectrum != null)
                {
                    var strongest = latestSpectrum.Skip(1).MaxBy(b => b.Dbfs)!;
                    spectrum.Text = $"Maior componente: {strongest.Hz:F1} Hz / {strongest.Dbfs:F1} dBFS (resolução {sampleRate/(double)FftSize:F1} Hz)";
                    if(referenceRms.HasValue) spectrum.Text += $" | B − A: {(windows?.QuietDbfs ?? currentRms)-referenceRms.Value:+0.0;-0.0;0} dB";
                }
        }
    }

    private void InspectSessions()
    {
        foreach(var device in endpoints)
        {
            AddLog($"Endpoint: {device.FriendlyName} | {device.ID} | {device.DataFlow}. Físico/virtual: não determinado com segurança pelo nome.");
            try
            {
                var sessions = device.AudioSessionManager.Sessions;
                for(int i=0;i<sessions.Count;i++)
                {
                    using var session = sessions[i];
                    uint pid = session.GetProcessID;
                    string name = "sistema / indisponível";
                    try { using var process = Process.GetProcessById((int)pid); name=process.ProcessName; } catch { }
                    AddLog($"  Sessão: PID {pid} | {name} | {session.State}");
                }
            }
            catch(Exception ex) { AddLog("  Sessões indisponíveis: " + ex.Message); }
        }
        try { foreach(var name in AsioOut.GetDriverNames()) AddLog("Driver ASIO registrado: " + name); }
        catch(Exception ex) { AddLog("ASIO indisponível: " + ex.Message); }
        AddLog("ASIO: inventário apenas. Não abre drivers nem identifica rotas internas da DAW. Sessões WASAPI não representam todos os clientes de áudio.");
    }
    private async System.Threading.Tasks.Task ImportWave()
    {
        if(capture != null) { AddLog("Pare a captura antes de importar."); return; }
        using var dialog = new OpenFileDialog { Filter = "Áudio WAV|*.wav" };
        if(dialog.ShowDialog()!=DialogResult.OK) return;
        int channel = (int)selectedChannel.Value-1;
        start.Enabled = refresh.Enabled = import.Enabled = baseline.Enabled = export.Enabled = devices.Enabled = selectedChannel.Enabled = false;
        try
        {
            var result = await System.Threading.Tasks.Task.Run(() => WaveFileAnalysis.Analyze(dialog.FileName,channel));
            if(IsDisposed || Disposing) return;
            lock(sync) { windows=result.meter; sampleRate=result.rate; captureChannel=channel; latestSpectrum=result.bins; currentRms=result.rms; currentPeak=result.peak; measurementSource=$"WAV: {System.IO.Path.GetFileName(dialog.FileName)} | canal {channel+1}"; startedAt=DateTime.UtcNow; }
            UpdateDisplay(); info.Text=measurementSource;
            AddLog($"Arquivo analisado: {result.seconds:F1}s. RMS/pico do arquivo inteiro; piso estimado dos últimos 10s; FFT do trecho final.");
            foreach(var finding in findings) AddLog(finding);
        }
        catch(Exception ex) { if(!IsDisposed) AddLog("Falha ao analisar WAV: " + ex.Message); }
        finally { if(!IsDisposed) start.Enabled = refresh.Enabled = import.Enabled = baseline.Enabled = export.Enabled = devices.Enabled = selectedChannel.Enabled = true; }
    }
    private void ExportReport()
    {
        using var dialog = new SaveFileDialog { Filter = "Relatório JSON|*.json", FileName = "AudioSignalInspector-report.json" };
        if(dialog.ShowDialog()!=DialogResult.OK) return;
        try
        {
            System.IO.File.WriteAllText(dialog.FileName, CreateReportJson());
            AddLog("Relatório salvo. Inclui nomes de dispositivos e processos; revise antes de compartilhar.");
        }
        catch(Exception ex) { AddLog("Falha ao exportar: " + ex.Message); }
    }

    public string CreateReportJson()
    {
            object report;
            lock(sync) report = new {
                schemaVersion=2, generatedUtc=DateTime.UtcNow, source=measurementSource,
                captureStartedUtc=startedAt, sampleRate, channel=captureChannel+1,
                rmsDbfs=double.IsFinite(currentRms)?(double?)currentRms:null,
                peakDbfs=double.IsFinite(currentPeak)?(double?)currentPeak:null,
                quietWindowEstimateDbfs=windows?.QuietDbfs,
                quietWindowCount=windows?.Count ?? 0, windowDurationMs=100, fftSize=FftSize, referenceRmsDbfs=referenceRms, referenceSource,
                spectrum=latestSpectrum, diagnosticHypotheses=findings, observations=observations.ToArray(),
                limitations=new[]{"Estimativa depende de silêncio; não é ruído calibrado do hardware.","Loopback mede a mistura da saída, não necessariamente apenas a DAW.","Comparação A/B sequencial; não sincronizada e não revela causalidade.","FFT Hann: amplitude por bin; resolução depende da taxa de amostragem.","Sem captura ASIO direta ou inspeção de roteamento interno.","Nenhuma conclusão automática de origem física do hiss."}
            };
            return JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true });
    }
    private void StopCapture()
    {
        UpdateDisplay(); uiTimer.Stop();
        var old = capture;
        capture = null;
        if (old != null)
        {
            try { old.StopRecording(); } catch { }
            try { old.Dispose(); } catch(Exception ex) { AddLog("Falha ao liberar captura: " + ex.Message); }
            AddLog("Medição encerrada.");
        }
        start.Enabled = true; stop.Enabled = false; refresh.Enabled = true; devices.Enabled = true; selectedChannel.Enabled = true;
        info.Text = "Pronto para nova medição.";
    }
}







