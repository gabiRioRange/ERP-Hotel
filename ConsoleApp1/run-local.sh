#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="/home/gabriel/RiderProjects/ConsoleApp1/ConsoleApp1/ConsoleApp1.csproj"
DEFAULT_URL="http://localhost:5087"
DEFAULT_CONN=""
POSTGRES_CONN="Host=/var/run/postgresql;Database=hotel_erp;Username=gabriel"
DEFAULT_PROFILE="local-inmemory"

URL="${ASPNETCORE_URLS:-$DEFAULT_URL}"
CONN="${ConnectionStrings__HotelDb:-$DEFAULT_CONN}"
USE_POSTGRES="false"
KILL_ONLY="false"
PROFILE="$DEFAULT_PROFILE"
DOTNET_ENV="LocalInMemory"

print_help() {
  cat <<EOF
Uso:
  ./run-local.sh [opções]

Opções:
  --postgres         Usa PostgreSQL local por socket (peer auth)
  --profile <nome>   local-inmemory | local-postgres
  --port <porta>     Define porta HTTP (padrão: 5087)
  --kill-only        Apenas encerra processos antigos e sai
  -h, --help         Mostra esta ajuda

Variáveis de ambiente suportadas:
  ASPNETCORE_URLS
  ConnectionStrings__HotelDb
  DOTNET_ENVIRONMENT
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --postgres)
      USE_POSTGRES="true"
      PROFILE="local-postgres"
      DOTNET_ENV="LocalPostgres"
      shift
      ;;
    --profile)
      [[ $# -lt 2 ]] && { echo "Erro: --profile requer um valor"; exit 1; }
      PROFILE="$2"
      case "$PROFILE" in
        local-inmemory)
          DOTNET_ENV="LocalInMemory"
          ;;
        local-postgres)
          DOTNET_ENV="LocalPostgres"
          USE_POSTGRES="true"
          ;;
        *)
          echo "Erro: perfil inválido '$PROFILE'. Use local-inmemory ou local-postgres."
          exit 1
          ;;
      esac
      shift
      ;;
    --port)
      [[ $# -lt 2 ]] && { echo "Erro: --port requer um valor"; exit 1; }
      URL="http://localhost:$2"
      shift 2
      ;;
    --kill-only)
      KILL_ONLY="true"
      shift
      ;;
    -h|--help)
      print_help
      exit 0
      ;;
    *)
      echo "Opção inválida: $1"
      print_help
      exit 1
      ;;
  esac
done

if [[ "$USE_POSTGRES" == "true" && -z "$CONN" ]]; then
  CONN="$POSTGRES_CONN"
fi

if [[ "$USE_POSTGRES" != "true" && -n "$CONN" ]]; then
  echo "[run-local] Aviso: ConnectionStrings__HotelDb definida, mas perfil atual não exige PostgreSQL."
fi

echo "[run-local] Limpando instâncias antigas da API..."
pkill -f "ConsoleApp1/bin/Debug/net10.0/ConsoleApp1" >/dev/null 2>&1 || true
pkill -f "dotnet run --project $PROJECT_PATH" >/dev/null 2>&1 || true

PORT_ONLY="${URL##*:}"
if command -v fuser >/dev/null 2>&1; then
  fuser -k "${PORT_ONLY}/tcp" >/dev/null 2>&1 || true
fi

if [[ "$KILL_ONLY" == "true" ]]; then
  echo "[run-local] Limpeza concluída."
  exit 0
fi

echo "[run-local] Subindo API em $URL"
echo "[run-local] Perfil: $PROFILE ($DOTNET_ENV)"
if [[ -z "$CONN" ]]; then
  echo "[run-local] Banco: InMemory"
else
  echo "[run-local] Banco: PostgreSQL"
fi

echo "[run-local] Pressione Ctrl+C para encerrar"
ASPNETCORE_URLS="$URL" DOTNET_ENVIRONMENT="$DOTNET_ENV" ASPNETCORE_ENVIRONMENT="$DOTNET_ENV" ConnectionStrings__HotelDb="$CONN" dotnet run --project "$PROJECT_PATH"
