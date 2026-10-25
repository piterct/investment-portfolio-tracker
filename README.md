# Controle de Carteira — .NET + SQLite

Aplicação web local para cadastrar ativos da B3, acompanhar cotações, registrar notas/anotações e calcular o tamanho da posição necessário para aproximar a carteira do percentual ideal definido pelo usuário.

## Requisitos

- SDK do .NET 10 instalado.
- Conexão com a internet para consultar cotações externas.
- Um token da brapi é recomendado para consultar tickers além dos quatro símbolos de teste disponíveis sem token.

## Como executar

1. Extraia a pasta `ControleCarteira` para um diretório local.
2. Abra um terminal nessa pasta.
3. Execute:

   ```bash
   dotnet restore
   dotnet run --urls http://localhost:5080
   ```

4. Abra `http://localhost:5080` no navegador.

A base local `portfolio.db` será criada automaticamente na pasta do projeto. Ela guarda os ativos, quantidades, percentuais ideais, notas, anotações, últimos preços consultados, patrimônio de referência e token de API.

## Cotações de mercado

A integração usa a API da brapi (`https://brapi.dev`). Para obter cotações de tickers que exigem autenticação:

1. Crie uma conta em `https://brapi.dev/login` e obtenha uma chave no painel da brapi.
2. Na aplicação, cole a chave no campo **Token brapi** e clique em **Salvar**.
3. Clique em **Atualizar cotações**. Quando um token estiver salvo, a aplicação também tenta atualizar os preços ao abrir a página.

O token é armazenado no banco SQLite local e nunca é devolvido pela API da própria aplicação. Deixe o campo de token vazio para manter o token existente; marque **Remover token salvo** se quiser apagá-lo.

**Importante:** a fonte de dados e o plano escolhido determinam a frequência e a cobertura das cotações. A brapi informa atualizações periódicas, não ticks garantidos em tempo real. Os preços podem estar atrasados ou indisponíveis, por exemplo fora do horário de negociação, e não devem ser usados para execução automática de ordens.

## Como os cálculos funcionam

- **Patrimônio de referência:** valor informado no painel. Se permanecer em zero, a aplicação usa a soma do valor de mercado das posições cadastradas como base.
- **Tenho (R$):** preço salvo mais recente × quantidade atual.
- **Carteira ideal (%):** percentual desejado definido no cadastro de cada ativo.
- **Quero (R$):** patrimônio de referência × percentual ideal.
- **Diferença (R$):** alvo em reais − valor atual da posição. Positivo indica valor que falta para alcançar o alvo; negativo indica que a posição está acima do alvo.
- **Qtd. alvo:** parte inteira truncada de diferença ÷ preço. Um número positivo indica a quantidade a comprar para ficar mais próximo do alvo sem ultrapassá-lo por uma fração de ação/cota. Números negativos indicam quantas unidades a posição excede o alvo matemático; a aplicação não recomenda venda automática e mantém a indicação como **Aguardar**.
- **Nota e anotações:** campos pessoais, sem influência no cálculo.

A soma dos percentuais ideais aparece no resumo. Você precisa ajustá-los manualmente para totalizar 100% conforme sua estratégia.

## Funcionalidades incluídas

- Cadastro, edição e remoção de ativos.
- Campos para ticker, descrição, quantidade, peso ideal, nota pessoal e anotações.
- Banco de dados SQLite local.
- Consulta manual de cotações e atualização na abertura quando há token salvo.
- Cálculo dos valores atuais, valores-alvo, diferenças em reais e quantidades para atingir cada alvo.
- Busca por ticker/nome/anotação e exportação CSV.
- Interface adaptável para desktop e celular.

## Segurança e cópia de segurança

Esta versão é destinada ao uso local e não inclui autenticação de usuário nem proteção para expor o servidor à internet. Não publique a aplicação diretamente em uma rede pública. Para fazer backup, feche a aplicação e copie o arquivo `portfolio.db` para um local seguro.
