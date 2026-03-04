<div align="center">
  <h1>🏨 ERP-Hotel</h1>
  <p>
    <em>Backend API-first para operações hoteleiras e gestão de reservas.</em>
  </p>

  [![CI](https://github.com/gabiRioRange/ERP-Hotel/actions/workflows/ci.yml/badge.svg)](https://github.com/gabiRioRange/ERP-Hotel/actions/workflows/ci.yml)
  [![Coverage](https://codecov.io/gh/gabiRioRange/ERP-Hotel/branch/main/graph/badge.svg)](https://codecov.io/gh/gabiRioRange/ERP-Hotel)
</div>

---

O **ERP-Hotel** é um sistema completo focado em operações hoteleiras, oferecendo gestão de reservas, controle de disponibilidade, roteirização de housekeeping, auditoria e integração MCP. Inclui também uma interface web local para uso operacional ágil.

## ✨ Funcionalidades Principais

- **⚙️ API REST:** Desenvolvida em .NET 10 com autenticação JWT segura.
- **📅 Gestão de Reservas:** Controle de disponibilidade de quartos em tempo real.
- **🧹 Housekeeping:** Roteirização e organização de tarefas de limpeza.
- **🛡️ Auditoria de Segurança:** Rastreamento completo com opções de exportação.
- **🖥️ UI Web Local:** Interface para operação e validação rápida do sistema.
- **🚀 CI/CD:** Pipeline automatizada com build, testes e cobertura de código.

## 🛠️ Requisitos

Antes de começar, certifique-se de ter os seguintes requisitos instalados:
- **.NET 10 SDK**
- Ambiente Linux, macOS ou Windows
- **PostgreSQL** *(Opcional: necessário apenas para o perfil com banco relacional)*

---

## 🚀 Começando Rápido

1. Clone o repositório:
```bash
git clone [https://github.com/gabiRioRange/ERP-Hotel.git](https://github.com/gabiRioRange/ERP-Hotel.git)
cd ERP-Hotel
```
    Execute com o perfil local em memória (ideal para o primeiro contato):

Bash

    ./run-local.sh --profile local-inmemory --port 5087

    Acesse os serviços nos seguintes endpoints:

    UI Operacional: http://localhost:5087/

    Health Check: http://localhost:5087/health/ready

    Swagger API: http://localhost:5087/swagger

⚙️ Modos de Execução Local

Escolha o perfil adequado para sua necessidade através do script de execução:
Perfil	Comando	Descrição
InMemory ⭐	./run-local.sh --profile local-inmemory --port 5087	Roda sem dependência de banco de dados. Excelente para desenvolvimento rápido.
PostgreSQL	./run-local.sh --profile local-postgres --port 5087	Valida migrações e o comportamento do banco de dados relacional.

    💡 Dica de Troubleshooting: Se ocorrer um erro de porta em uso (address already in use), basta alterar a porta no comando, ex: --port 5090. Se houver problemas de conexão com o PostgreSQL (Ident failed), mude para o perfil local-inmemory para não bloquear seu desenvolvimento.

🧪 Testes e Qualidade

O projeto conta com uma pipeline de CI robusta (configurada em .github/workflows/ci.yml). Para rodar os testes localmente:

Execução padrão:
Bash
```
dotnet test ConsoleApp1.Tests/ConsoleApp1.Tests.csproj
```

Execução com geração de relatório .trx:
Bash
```
dotnet test ConsoleApp1.Tests/ConsoleApp1.Tests.csproj \
  --results-directory TestResults \
  --logger "trx;LogFileName=test-results.trx"
```

🗺️ Roadmap
Curto Prazo

    [ ] Melhorar UX da UI web operacional (feedbacks, tabelas e navegação)

    [ ] Expandir cobertura de testes de integração

    [ ] Refinar observabilidade (métricas e diagnósticos de produção)

Médio Prazo

    [ ] Módulo financeiro inicial (faturamento/recebíveis)

    [ ] Melhorias de performance em consultas de disponibilidade e auditoria

    [ ] Evoluir exportações e jobs assíncronos administrativos

Longo Prazo

    [ ] Suporte multi-unidade (cadeias hoteleiras)

    [ ] Estratégias avançadas de precificação e ocupação

    [ ] Extensão dos fluxos MCP para automações operacionais

🤝 Como Contribuir

Toda contribuição é muito bem-vinda! Siga o passo a passo:

    Faça um fork do repositório.

    Crie uma branch para a sua feature: git checkout -b feature/minha-feature.

    Faça as alterações mantendo o escopo focado e objetivo.

    Rode o build e os testes localmente.

    Abra um Pull Request com uma descrição clara do que foi feito.

<details>
<summary><b>✅ Checklist antes de abrir o PR</b></summary>

    Certifique-se de que o código compila: dotnet build ConsoleApp1.slnx

    Verifique se os testes passam: dotnet test ConsoleApp1.Tests/ConsoleApp1.Tests.csproj

    Evite quebrar contratos públicos da API sem justificativa.

    Atualize a documentação do README se necessário.

</details>

## Licença

Este projeto está licenciado sob a licença MIT.

Veja [LICENSE](LICENSE).

