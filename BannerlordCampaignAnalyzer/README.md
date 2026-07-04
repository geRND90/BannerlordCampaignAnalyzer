# Campaign Performance Analyzer

Analyzer independente para Mount & Blade II: Bannerlord.

Ele nao altera mecanicas da campanha. A v0.3 mede tempo de execucao de metodos de campanha, grava logs rotativos em CSV, adiciona contexto nos spikes e inclui um Doctor para resumir suspeitos dentro do jogo.

## O que ele mede

- Metodos de classes derivadas de `CampaignBehaviorBase` com nomes contendo `Tick`, `Hourly`, `Daily` ou `Weekly`.
- Metodos manuais definidos em `_Module/ModuleData/BCA_Settings.xml`.
- Spikes, chamadas lentas, media, maximo, contagem de chamadas e excecoes.
- Contexto em spikes/excecoes quando houver objetos conhecidos nos argumentos, como `MobileParty`, `Hero`, `Clan`, `Kingdom`, `Settlement`, `Town`, `Village` ou `Army`.
- Suspeitos agregados pelo Doctor: party, settlement, clan, hero, kingdom ou sistema/metodo quando nao houver objeto especifico no contexto.

## Arquivos gerados no jogo

Depois de ativar o mod no launcher:

- `Modules/BannerlordCampaignAnalyzer/ModuleData/BCA_Logs/BCA_profile.csv`
- `Modules/BannerlordCampaignAnalyzer/ModuleData/BCA_Logs/BCA_patched_methods.txt`

O CSV tem rotacao automatica. Por padrao, cada arquivo cresce ate 5 MB e o mod guarda 5 arquivos antigos.

## Comandos uteis

No console do jogo:

- `bca.status`
- `bca.top 10`
- `bca.flush`
- `bca.doctor`
- `bca.doctor 10`
- `bca.parties`
- `bca.doctor_clear`

`bca.doctor` mostra os maiores suspeitos agregados desde que o save foi carregado. Ele nao procura apenas parties: se o gargalo vier de daily tick de clan, settlement/economia, reino, issues, decisoes ou outro metodo sem contexto claro, ele cai como `Clan`, `Settlement`, `Kingdom`, `Hero` ou `System`.

`bca.parties` e um atalho para ver apenas parties suspeitas.

## Configuracao

Edite:

`Modules/BannerlordCampaignAnalyzer/ModuleData/BCA_Settings.xml`

Opcoes principais:

- `Enabled`: liga/desliga o analyzer.
- `MinimumLogMilliseconds`: minimo para registrar chamada lenta.
- `SpikeLogMilliseconds`: limite para registrar spike.
- `SummaryEverySeconds`: intervalo do resumo.
- `AutoPatchCampaignBehaviorTicks`: mede handlers de campanha automaticamente.
- `IncludeTaleWorldsBehaviors`: inclui comportamentos nativos do jogo.
- `IncludeContextOnSpikes`: adiciona contexto no campo `note` quando uma chamada vira spike.
- `IncludeContextOnExceptions`: adiciona contexto no campo `note` quando uma chamada gera excecao.
- `IncludeContextOnSlow`: tambem adiciona contexto em chamadas lentas comuns. Fica desligado por padrao para reduzir custo.
- `SlowContextMilliseconds`: minimo para contexto em chamadas lentas, se `IncludeContextOnSlow` estiver ativo.
- `DoctorEnabled`: liga/desliga o Doctor.
- `DoctorAutoAlerts`: mostra alerta simples dentro do jogo quando um suspeito acumula spikes.
- `DoctorAlertSpikeCount`: quantidade de spikes antes do alerta automatico.
- `DoctorAlertMinimumMilliseconds`: minimo de tempo para um alerta automatico.
- `DoctorTopSuspects`: quantidade padrao usada por alguns relatorios.
- `ManualMethods`: lista de metodos extras no formato `Namespace.Tipo:Metodo`.

## Build local

Se usar Visual Studio, defina a variavel de ambiente:

`BANNERLORD_GAME_DIR=E:\Steam\steamapps\common\Mount & Blade II Bannerlord`

Ao compilar, o projeto copia a DLL e os arquivos do modulo para:

`Modules/BannerlordCampaignAnalyzer`

## Observacao

Esta v0.3 e deliberadamente conservadora. Ela serve para descobrir gargalos em campanha longa antes de criar qualquer otimizacao real.

Quando um spike envolve uma party, o campo `note` pode incluir dados como nome da party, lider, cla, faccao, tropas, feridos, prisioneiros, settlement atual, alvo, army e posicao. O objetivo e descobrir se um pico vem de uma party/settlement especifico antes de mexer em IA, recrutamento ou guerra.

O Doctor usa os mesmos spikes do CSV para mostrar uma resposta mais simples no jogo. Exemplo de leitura:

`Party: Prince Harry's Party | system=Party AI / Recruitment | spikes=5 | max=2285ms | target=Sanakia`

Isso quer dizer que a party apareceu repetidamente em spikes daquele sistema. Nao significa que o mod consertou a causa; significa que ele encontrou um suspeito forte para o jogador testar com uma acao simples, como chamar, redirecionar, dissolver/recriar ou observar se o alvo se repete.
