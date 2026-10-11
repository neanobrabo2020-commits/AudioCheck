# Audio Signal Inspector 0.3 — Windows 10/11 x64

Aplicativo WinForms/.NET 8 para medir áudio local e investigar hiss. Não altera volumes, dispositivo padrão, ganho, phantom power, efeitos ou rotas do Windows. Captura somente após Iniciar. Não salva nem envia amostras de microfone.

## Uso
1. Abra AudioSignalInspector.exe. O runtime está incluído; não precisa instalar .NET para usar.
2. Selecione Capture para uma entrada ou Render para loopback da mistura de uma saída. Escolha o canal existente no dispositivo.
3. Inicie a medição e mantenha silêncio por pelo menos 10 segundos. RMS e pico usam janelas de 100 ms. O piso estimado é o percentil inferior das últimas 100 janelas (10 segundos), não uma medida física calibrada. Silêncio digital é exibido como -200 dBFS.
4. Pare e guarde referência A. Meça B com mesmo ganho e condições. B − A compara os pisos estimados, não RMS instantâneo. A comparação é sequencial, sem compensação de ganho.
5. Para comparar antes/depois da DAW, exporte WAVs de trechos equivalentes com e sem processamento, sem normalização. Use Analisar WAV da DAW, guarde A e importe B. O arquivo inteiro fornece RMS/pico; piso usa últimos 10 s e FFT o trecho final. WAVs com menos de 10 s não podem servir de referência A.
6. Sessões / ASIO lista endpoints e sessões WASAPI visíveis com PID/processo, e nomes dos drivers ASIO registrados.
7. Exporte JSON para guardar medições, espectro, hipóteses e observações. O relatório pode conter nomes de dispositivos/processos: revise antes de compartilhar.

## Espectro e diagnóstico
Gráfico de frequência logarítmica 20 Hz–20 kHz, amplitude por bin em dBFS, FFT Hann de 32768 pontos (1,46 Hz por bin a 48 kHz). Picos tonais próximos de 50/60 Hz recebem uma hipótese de interferência elétrica se forem fortes em relação aos bins vizinhos. Picos perto de 0 dBFS sugerem verificar clipping. São heurísticas; não determinam a origem física. Hiss amplo precisa de comparação controlada de ambiente, cabo, microfone, ganho e processamento.

## Limitações explícitas
- Endpoints físicos/virtuais aparecem se expostos pelo Windows; a natureza física não é inferida pelo nome.
- ASIO: inventário apenas. Captura ASIO direta e rotas internas da DAW não estão implementadas. WASAPI não identifica todos os clientes ASIO.
- WASAPI compartilhado pode conter efeitos do Windows/driver. Loopback é a mistura da saída; ASIO/exclusivo pode não estar disponível.
- Não há comparação simultânea sincronizada nem atribuição automática de causalidade.
- PCM 8/16/24/32 e float32 são decodificados; subformatos desconhecidos são rejeitados.
- Hardware XLR, desconexão durante captura, captura prolongada e ASIO real ainda precisam de validação com o usuário.

## Build e testes
Instale SDK .NET 8 e execute na raiz:

```powershell
dotnet run --project Tests/Tests.csproj -c Release
dotnet run --project IntegrationTests/IntegrationTests.csproj -c Release -- artifacts/evidence
dotnet publish AudioSignalInspector/AudioSignalInspector.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/app
```

O executável está em artifacts/app. O workflow .github/workflows/windows.yml executa os testes e publica em runner Windows; também roda o exe com --smoke-test para renderizar a janela e serializar um relatório sem capturar áudio. Os artefatos contêm o aplicativo, SHA256 e evidências. Consulte VALIDATION.md para o estado real de validação.
