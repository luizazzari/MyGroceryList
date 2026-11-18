# MyGroceryList

Aplicativo nativo de lista de compras em C#/.NET 10, .NET MAUI, XAML e MVVM, com interface em português do Brasil. Esta entrega implementa a **etapa 1: compra local**, sem conta e sem conexão. A cultura inicial é `pt-BR` (BRL); preços e orçamento entram na etapa de planejamento.

## O que funciona

- Criar várias listas, abrir novamente, renomear, arquivar e restaurar.
- Adicionar produtos pelo botão **Adicionar item**, no rodapé. Ele abre um campo que permanece acessível para inclusões em sequência, limpa e recebe foco depois da gravação.
- Interpretar `2 leites`, `500 g de queijo` e `1,5 kg de arroz`, preservando sufixos de embalagem como `Leite integral 1 L`. Quantidades usam `decimal`, nunca ponto flutuante.
- Editar nome, quantidade e unidade opcionais, setor e observação; excluir produtos com confirmação.
- Agrupar pendentes nos nove setores iniciais e lembrar o setor escolhido para inclusões futuras do mesmo produto, ignorando caixa e acentos na identidade.
- Oferecer revisão de duplicata: somar somente quantidades explícitas de nomes equivalentes, mesma unidade normalizada e observação compatível, ou manter separado. Variações de produto permanecem distintas; não há conversão automática entre g e kg.
- Marcar e desmarcar com uma área ampla, mostrar progresso, recolher/expandir comprados e desfazer a última marcação. O desfazer também é persistido.
- Salvar cada alteração imediatamente no SQLite. Não é necessário sair da tela para salvar. Reabrir a lista recupera os itens e suas marcações.
- Escolher os temas **Claro**, **Escuro** ou **Automático** em **⋮ → Aparência**. A escolha fica salva no aparelho; Automático acompanha o sistema.
- Usar uma interface simples com cabeçalhos verdes por setor, ícones de produtos, quantidade à direita, marcação ampla e descrições para leitores de tela.
- Exportar listas em CSV ou JSON e importar arquivos com revisão dos itens antes de salvar.

Arquivar guarda a lista e seus estados; a ação **não representa fechamento de compra**. Histórico, fechamento e **Usar última compra** fazem parte da próxima etapa e ainda não aparecem como ações na interface.

## Interface

A interface foi refeita a partir da referência visual fornecida: fundo claro quente, verde escuro nos destaques, setores em verde suave e cartões de produto discretos. O modo escuro usa superfícies escuras e verdes claros com a mesma organização. As cores ficam centralizadas em `Resources/Styles/Colors.xaml` e são aplicadas com `AppThemeBinding`.

- **Listas:** toque em uma lista para abrir, ou em **Nova lista** para criar. As arquivadas ficam em **⋮ → Listas arquivadas**.
- **Compra:** toque na linha ou na caixa para marcar. A quantidade fica à direita; **⋯** abre a edição do item. Os comprados podem ser mostrados ou recolhidos no fim da lista. **Desfazer** aparece após uma marcação.
- **Adicionar:** o botão principal abre o campo; adicione vários produtos em sequência e toque em **Fechar** quando terminar.
- **Editar:** nome, quantidade, unidade, setor e observação; **Salvar** fica no rodapé. **⋮ → Excluir item** pede confirmação antes de remover.
- **Aparência:** disponível no menu das três telas. A seleção é persistida em `Preferences`, sem alterar as listas nem o banco SQLite.

Os ícones de produtos são emojis decorativos do sistema, com alternativa por setor para produtos não reconhecidos. Nome e quantidade continuam em texto. Não há imagens ou serviços externos para exibir a lista.

## Exportar e importar listas

Abra uma lista e use **⋮ → Exportar lista** para escolher **CSV (.csv)** ou **MyGroceryList (.json)**. O aplicativo gera o arquivo e abre o compartilhamento do sistema para enviá-lo ou salvá-lo pelos destinos disponíveis no aparelho.

Na tela **Minhas listas**, use **⋮ → Importar lista** e selecione um arquivo CSV ou JSON. A revisão permite alterar o nome da lista, selecionar os itens e editar produto, quantidade, unidade, setor, observação e marcação de comprado. Toque em **Importar lista** para salvar e abrir a nova lista.

A importação cria uma lista ativa com IDs próprios e preserva as marcações selecionadas na revisão. As linhas repetidas são mantidas separadas. O lote inteiro é gravado em uma transação SQLite. Se a navegação falhar depois da gravação, o botão **Abrir lista** permite tentar novamente usando a lista já criada.

O JSON do MyGroceryList usa um formato versionado e transporta nome, itens, quantidades, unidades, setores, observações e marcações. Cada arquivo representa uma lista.

O CSV exportado usa UTF-8 com BOM, ponto e vírgula como separador e vírgula decimal. Todos os campos são colocados entre aspas, com escape de aspas internas e suporte a observações com várias linhas. Um arquivo vazio de itens também preserva o nome da lista.

```csv
lista;nome;quantidade;unidade;setor;observacao;comprado
Mercado;Arroz;1,5;kg;Mercearia;Integral;não
Mercado;Leite;2;un;Laticínios;;sim
```

Para arquivos externos, o importador aceita separador `;` ou `,`, cabeçalhos sem distinção de caixa e acentos, e quantidades decimais com vírgula ou ponto. Com separador vírgula, coloque números como `"1,5"` entre aspas. Salve o texto em UTF-8 ou em Unicode com BOM.

A coluna `nome` ou `produto` é obrigatória. As colunas opcionais são `lista`, `quantidade`, `unidade`, `setor`, `observacao` e `comprado`. O nome da lista vem da coluna `lista` ou do nome do arquivo. Setor em branco usa **Outros** e marcação em branco usa **Pendente**. `comprado` aceita `sim`/`não`, `true`/`false`, `1`/`0` ou `comprado`/`pendente`. Setores preenchidos devem corresponder aos setores do aplicativo. Erros de conteúdo são apresentados antes de qualquer gravação.

## Requisitos

- SDK .NET **10.0.402**, fixado em `global.json`, com atualizações de patch estáveis permitidas.
- Workloads MAUI correspondentes ao alvo. A implementação foi compilada com MAUI **10.0.110**.
- Android: Android SDK/JDK compatíveis com o workload .NET 10. O ambiente inspecionado possui SDK Android 36 e JDK em `C:\Program Files\Android\openjdk`.
- iOS: Mac com Xcode compatível com o workload iOS instalado e ferramentas de linha de comando configuradas. Para aparelho físico, configure a identidade e o provisionamento de assinatura. Windows pode usar o pareamento com um Mac pelo Visual Studio.
- Acesso ao NuGet na primeira restauração dos pacotes que não estão no cache.

```powershell
dotnet --info
dotnet workload list
# Em uma máquina de desenvolvimento que ainda não tenha os workloads:
dotnet workload install maui
```

Os projetos de domínio, aplicação, persistência e testes funcionam em .NET 10 sem MAUI, em Windows, macOS ou Linux.

## Compilar e executar

Execute os comandos na raiz da solução. A pasta `MyGroceryList/` original foi mantida; o projeto mobile agora se chama `MyGroceryList.Mobile.csproj`.

```powershell
# Testes de domínio, casos de uso, SQLite real e ViewModels; não requer emulador.
dotnet test MyGroceryList.Tests/MyGroceryList.Tests.csproj

# Android: compila C#, XAML e pacote de depuração.
dotnet build MyGroceryList/MyGroceryList.Mobile.csproj -f net10.0-android -p:MobileTargetFrameworks=net10.0-android

# Executar com um emulador já iniciado ou aparelho conectado e depuração USB autorizada.
dotnet build MyGroceryList/MyGroceryList.Mobile.csproj -t:Run -f net10.0-android -p:MobileTargetFrameworks=net10.0-android

# APK de depuração autocontido, para instalar via adb sem fast deployment.
dotnet build MyGroceryList/MyGroceryList.Mobile.csproj -t:SignAndroidPackage -f net10.0-android -p:MobileTargetFrameworks=net10.0-android -p:EmbedAssembliesIntoApk=true

# Compilar a solução inteira somente com o alvo mobile Android.
dotnet build MyGroceryList.slnx -p:MobileTargetFrameworks=net10.0-android

# Windows: alvo auxiliar preservado do template, útil no desenvolvimento.
dotnet build MyGroceryList/MyGroceryList.Mobile.csproj -f net10.0-windows10.0.19041.0 -p:MobileTargetFrameworks=net10.0-windows10.0.19041.0
```

Os APKs ficam em `MyGroceryList/bin/Debug/net10.0-android/`. Uma cópia do APK autocontido gerado nesta entrega está em `artifacts/MyGroceryList-debug.apk`. A assinatura de depuração destina-se ao desenvolvimento. Publicação em lojas requer identidade de aplicativo e certificados próprios.

Em um **Mac**, para simulador iOS:

```sh
dotnet build MyGroceryList/MyGroceryList.Mobile.csproj -f net10.0-ios -p:MobileTargetFrameworks=net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
dotnet build MyGroceryList/MyGroceryList.Mobile.csproj -t:Run -f net10.0-ios -p:MobileTargetFrameworks=net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
```

Use `iossimulator-x64` em um Mac Intel. Para aparelho físico, use `ios-arm64` e as propriedades de assinatura apropriadas. Não use `-p:TargetFrameworks=...` globalmente: essa propriedade também modifica a restauração dos projetos compartilhados. `MobileTargetFrameworks` limita somente o projeto MAUI. Execute builds de diferentes alvos sequencialmente, pois compartilham o diretório intermediário do projeto.

## Arquitetura

| Projeto | Papel nesta entrega |
| --- | --- |
| `MyGroceryList.Domain` | Listas e itens imutáveis, validação, setores, equivalência, marcação e desfazer. Sem dependências de infraestrutura. |
| `MyGroceryList.Application` | Casos de uso em `ShoppingService`, parser de entrada rápida e contrato específico de persistência. Usa `TimeProvider` para a criação de listas. |
| `MyGroceryList.Mobile` | Páginas XAML com bindings tipados, Shell, ViewModels do CommunityToolkit.Mvvm, estilos e composição por DI em `MauiProgram`. |
| `MyGroceryList.Mobile.Data` | SQLite real via Microsoft.Data.Sqlite 10.0.13; migrações, mapeamento explícito e transações. |
| `MyGroceryList.Tests` | xUnit v3: regras, casos de uso, persistência, migração e comandos das telas. |

As pastas internas são organizadas por fluxo (`Shopping`, `Views`, `ViewModels`). O template e seus alvos de Windows/MacCatalyst foram preservados. O domínio não conhece a interface, SQLite, HTTP ou MAUI.

Os testes compilam os mesmos arquivos de ViewModel do app por links de projeto, permitindo exercitar os comandos sem um alvo MAUI. Serviços de navegação, diálogos e despacho de UI têm implementações de teste; a persistência usada nos testes de integração é SQLite em disco.

### Persistência e responsividade

- Banco `mygrocerylist.db` em `FileSystem.AppDataDirectory`, mantido após encerrar o processo e enquanto os dados do aplicativo forem preservados.
- Esquema v1: listas e itens, com índices para listas ativas/arquivadas e itens por lista/setor/posição. V2: preferência de setor e estado de desfazer.
- Migrações em transação, com `PRAGMA user_version`; banco mais novo que o aplicativo é rejeitado sem modificações. Há teste de migração de v1 com itens já existentes.
- IDs `Guid` gerados no cliente. Quantidades armazenadas como texto decimal invariável para preservar precisão; apresentação e entrada usam vírgula.
- Atualização de lista, itens afetados, preferência de setor e desfazer em uma transação. Falha ou cancelamento antes do commit não deixa gravação parcial.
- Conexões curtas, chaves estrangeiras habilitadas, leituras de lista em snapshot e escritas serializadas. O trabalho SQLite síncrono roda fora da thread da interface, pois o provedor não oferece I/O assíncrono real.
- Os ViewModels bloqueiam operações concorrentes na tela e preservam o texto digitado se a gravação falhar. As coleções são reconciliadas por identidade na thread da UI: itens não envolvidos mantêm suas instâncias, sem `Clear()`/`Reset` durante uma marcação.
- Os logs registram o tipo de falha, sem nomes de produtos, observações ou credenciais.

Referências oficiais: [SQLite com MAUI](https://learn.microsoft.com/dotnet/maui/data-cloud/database-sqlite?view=net-maui-10.0), [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/), [limitações assíncronas do Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/async).

## Validação desta entrega

Exportação e importação adicionadas em **17/11/2026**. Os 63 testes passaram, incluindo ida e volta em CSV e JSON, revisão dos itens, falha de gravação com rollback e repetição de navegação após importar. Os builds de Windows e Android, com APK de depuração assinado, passaram sem avisos ou erros. Os diálogos nativos de seleção e compartilhamento de arquivos ainda precisam ser conferidos em aparelho.

Revisão da interface em **13/11/2026**: 31 testes de regressão aprovados; Android (incluindo APK autocontido) e Windows compilados sem avisos ou erros. A restauração do build de Windows precisou usar o cache local de pacotes porque o acesso ao NuGet está restrito neste ambiente. A aparência nos dois temas, a preferência após reiniciar e a interação por toque ainda precisam ser conferidas em aparelho, conforme o roteiro abaixo.

Ambiente inspecionado em **12/11/2026**: Windows, SDK 10.0.402; workloads Android 36.1.118, iOS/MacCatalyst 26.5.10315 e MAUI Windows 10.0.110.

| Verificação | Resultado |
| --- | --- |
| `dotnet test MyGroceryList.Tests/MyGroceryList.Tests.csproj` | 63 testes aprovados. |
| Compilação Android Debug | Aprovada, sem avisos/erros, incluindo geração XAML e APK autocontido assinado para depuração. |
| Compilação Windows Debug | Aprovada, sem avisos/erros. |
| Reabrir banco offline | Validado por testes: nova instância recupera várias listas, IDs, quantidades, observações, marcações e desfazer. |
| Migração v1 → v2 | Validada com dados existentes. |
| Falha durante escrita | Validado rollback de marcação e desfazer por falha SQLite induzida. |
| Concorrência/toques repetidos | Validados 25 inclusões concorrentes e toque repetido durante confirmação de duplicata. |
| Exportação/importação CSV e JSON | Validadas precisão decimal, acentos, campos com aspas e várias linhas, listas vazias, novos IDs, marcações, edição da revisão e rollback do lote. |
| Seleção e compartilhamento de arquivos | Integração compilada para Android e Windows; conferência em aparelho pendente. |
| Execução Android em emulador/aparelho | Não validada: nenhum dispositivo conectado; emulador configurado não iniciou por driver de aceleração indisponível. |
| Compilação/execução iOS | Não validada: restauração depende de pacotes iOS fora do cache e acesso ao NuGet; ambiente atual é Windows sem Mac pareado. |
| Uso com uma mão, leitores de tela, escala de fontes e aparência | Implementação preparada; validação em aparelho ainda necessária. |

### Roteiro para validação em dispositivo

1. Crie “Mercado da semana” e “Churrasco”. Saia e abra novamente cada lista.
2. Toque em **Adicionar item** e adicione `2 leites`, `500 g de queijo`, `1,5 kg de arroz` e `Leite integral 1 L`; confirme interpretação, foco contínuo e preservação do nome da embalagem.
3. Edite quantidade, unidade, observação e setor. Adicione novamente o mesmo nome com outra caixa/acentuação e confira o setor lembrado.
4. Teste duplicata com quantidades e mesma unidade, depois g versus kg, quantidade em branco e produtos com variações.
5. Marque, desmarque, expanda/recolha comprados e desfaça. Confirme que os outros itens não perdem a posição de rolagem.
6. Ative modo avião, force o encerramento e reabra. Abra a lista: campos, marcações e botão Desfazer devem estar preservados.
7. Renomeie, arquive, visite **⋮ → Listas arquivadas** e restaure. Exclua um produto e confira após reiniciar.
8. Em **⋮ → Aparência**, teste **Claro**, **Escuro** e **Automático** nas três telas; feche e abra o app para confirmar a preferência. Com fonte ampliada e TalkBack/VoiceOver, confira legibilidade, nomes, quantidades e área de toque. Teste também em tela pequena e com teclado aberto.

## Próximas etapas e backend

Esta etapa não exige conta, variável de ambiente, segredo nem backend. **Ainda não existe API para iniciar localmente.** Os projetos `MyGroceryList.Api` e `MyGroceryList.Contracts` serão criados na etapa de compartilhamento, quando houver endpoints reais e contratos versionados.

Ordem restante do pedido:

1. Fechamento com tratamento de pendentes, histórico imutável, favoritos, sugestões, importação em lote com revisão e **Usar última compra**.
2. Orçamento e preços em centavos, modelos com mesclagem transacional, recorrência semanal/mensal e ordem de setores por supermercado.
3. API ASP.NET Core/EF Core/PostgreSQL em monólito modular, OIDC com PKCE, convites e membros, fila transacional de operações, conflitos e sincronização offline. Configuração de PostgreSQL, provedor OIDC e comandos para iniciar o backend devem acompanhar essa implementação.
4. Validação completa em Android e iOS, inclusive acessibilidade, fontes ampliadas e retomada em aparelho real.

As listas desta versão são pessoais e locais. Ainda não existe fila de sincronização nem compartilhamento; autenticação futura deverá preservá-las e permitir escolha explícita das listas a compartilhar.
