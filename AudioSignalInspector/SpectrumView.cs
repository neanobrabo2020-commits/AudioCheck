using System.Drawing;
using System.Windows.Forms;
using AudioSignalInspector.Core;
namespace AudioSignalInspector;
public sealed class SpectrumView : Control
{
    public Bin[]? Bins { get; set; }
    public SpectrumView() { DoubleBuffered = true; BackColor = Color.FromArgb(15,23,42); ForeColor = Color.White; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var area = new Rectangle(55, 18, Math.Max(1,Width-75), Math.Max(1,Height-55));
        using var grid = new Pen(Color.FromArgb(51,65,85));
        using var signal = new Pen(Color.FromArgb(56,189,248), 1.5f);
        float X(double hz) => area.Left + (float)(Math.Log10(Math.Clamp(hz,20,20000)/20)/3)*area.Width;
        float Y(double db) => area.Bottom - (float)((Math.Clamp(db,-140,0)+140)/140)*area.Height;
        foreach(int db in new[]{0,-20,-40,-60,-80,-100,-120,-140}) {
            e.Graphics.DrawLine(grid,area.Left,Y(db),area.Right,Y(db));
            e.Graphics.DrawString(db.ToString(),Font,Brushes.White,3,Y(db)-7);
        }
        foreach(int hz in new[]{20,50,100,1000,10000,20000}) {
            e.Graphics.DrawLine(grid,X(hz),area.Top,X(hz),area.Bottom);
            e.Graphics.DrawString(hz.ToString(),Font,Brushes.White,X(hz)-(hz==20000?45:12),area.Bottom+4);
        }
        e.Graphics.DrawString("dBFS • amplitude por bin / Hz • eixo log",Font,Brushes.White,area.Left,0);
        if(Bins == null) return;
        var points = Bins.Where(b=>b.Hz>=20 && b.Hz<=20000).Select(b=>new PointF(X(b.Hz),Y(b.Dbfs))).ToArray();
        if(points.Length>1) e.Graphics.DrawLines(signal,points);
    }
}

