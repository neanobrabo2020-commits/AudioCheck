# Validação 0.3 — 10/10/2026, horário de São Paulo

## Executado neste Windows 10 x64
- ZIP 0.2 original: 11 testes passaram; dotnet publish self-contained concluiu.
- Versão 0.3: 18 testes numéricos passaram. Casos incluem PCM, float, silêncio, RMS/pico, FFT, separação de tons sintéticos 50/60 Hz, heurística tonal, clipping, rejeição de NaN e janelas fixas independentes dos buffers.
- 3 testes de integração passaram: WAV float estéreo de 10 s (canais opostos, RMS -15,0515 dBFS, pico -12,0412 dBFS, frequência 1 kHz), canal inválido, JSON sem medição e criação/renderização WinForms.
- Publicação .NET 8.0.425 win-x64 self-contained single-file concluída.
- Executável publicado executado com --smoke-test: interface renderizada, relatório JSON serializado, saída 0, marcador PASS. Não iniciou captura.
- Renderização inspecionada visualmente. Dois endpoints WASAPI foram enumerados na máquina.
- A primeira tentativa de smoke test baseada em MainWindowHandle falhou porque a execução era oculta; foi substituída por evidência interna de renderização e saída explícita.

Logs em evidence/ no pacote de entrega; SHA256 acompanha o aplicativo.

## Pendente / não demonstrado
Captura real WASAPI, loopback, sessões/processos reais, desconexão/reconexão, PCM extensível em driver real, responsividade prolongada, Windows 11 e hardware XLR. Não foram iniciadas gravações no hardware do usuário. ASIO direto não implementado. Não se alega identificação conclusiva da origem do ruído.

## GitHub
Workflow executado com sucesso no GitHub Actions: https://github.com/neanobrabo2020-commits/AudioCheck/actions/runs/38105450552 . Commit de código: a05fa0a213021f8b1054063257b999fb00f00144. Testes numéricos, integração WAV/WinForms, publicação win-x64, smoke test do exe e upload dos artefatos concluíram. Executável e evidências foram baixados. SHA256 conferido: A62FDD776CB685E73D9862DF8E8FFD530DBB1904521E333FDD742BEB788D02C9. Captura real não foi iniciada; validação de hardware continua pendente.

## Teste manual recomendado
Medir 10 s de silêncio; iniciar/parar repetidamente; confirmar canal; comparar com medidor da DAW; testar loopback compartilhado; remover/reconectar interface durante captura; exportar JSON com e sem sinal; comparar WAVs equivalentes sem normalização. Registrar taxa, ganho, canal e efeitos em cada ensaio.

