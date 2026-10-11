namespace AudioSignalInspector.Core;
public static class Diagnosis
{
    public static string[] Suggest(Bin[] bins, double peak)
    {
        var notes = new List<string>();
        if (peak >= -0.1) notes.Add("Pico próximo de 0 dBFS: verificar clipping e ganho na cadeia.");
        foreach (int hz in new[] {50,60})
        {
            var target = bins.Where(b=>Math.Abs(b.Hz-hz)<=2).ToArray();
            var neighbors = bins.Where(b=>Math.Abs(b.Hz-hz)>=5 && Math.Abs(b.Hz-hz)<=15).ToArray();
            if(target.Length>0 && neighbors.Length>0 && target.Max(b=>b.Dbfs)>-120 && target.Max(b=>b.Dbfs)-neighbors.Average(b=>b.Dbfs)>15)
                notes.Add($"Componente tonal próxima de {hz} Hz: compatível com interferência de rede; investigar alimentação, cabo e aterramento. Não confirma origem física.");
        }
        notes.Add("Hiss amplo exige comparação controlada: silêncio, mesmo ganho/canal, efeitos desativados e exportações A/B. Espectro sozinho não distingue pré-amplificador de ruído ambiente.");
        return notes.ToArray();
    }
}
