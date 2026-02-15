#!/bin/bash
# Script de diagnóstico para Cyzor Provisioning

echo "════════════════════════════════════════"
echo "🔍 Diagnóstico Cyzor Provisioning"
echo "════════════════════════════════════════"
echo ""

APP_DIR="/var/www/cyzor_dotnet"
APP_BINARY="${APP_DIR}/Cyzor.Provisioning"
DB_FILE="${APP_DIR}/tenants.db"
LOG_FILE="/var/log/cyzor.log"

# 1. Verificar se aplicação existe
echo "1️⃣  Verificando aplicação..."
if [ -f "$APP_BINARY" ]; then
    echo "✅ Aplicação encontrada: $APP_BINARY"
    SIZE=$(du -h "$APP_BINARY" | cut -f1)
    echo "   Tamanho: $SIZE"
else
    echo "❌ Aplicação NÃO ENCONTRADA em: $APP_BINARY"
fi
echo ""

# 2. Verificar se processo está rodando
echo "2️⃣  Verificando processo..."
if pgrep -a "Cyzor.Provisioning" > /dev/null; then
    echo "✅ Processo está RODANDO"
    pgrep -a "Cyzor.Provisioning" | sed 's/^/   /'
else
    echo "❌ Processo NÃO ESTÁ RODANDO"
fi
echo ""

# 3. Verificar porta 5000
echo "3️⃣  Verificando porta 5000..."
if netstat -tlnp 2>/dev/null | grep -q ":5000 "; then
    echo "✅ Porta 5000 está ABERTA"
    netstat -tlnp 2>/dev/null | grep ":5000 " | sed 's/^/   /'
else
    echo "❌ Porta 5000 NÃO ESTÁ ABERTA"
    echo "   Checando se alguma porta está em uso:"
    netstat -tlnp 2>/dev/null | grep "LISTEN" | grep -i dotnet | sed 's/^/   /' || echo "   Nenhum dotnet listening"
fi
echo ""

# 4. Verificar banco de dados
echo "4️⃣  Verificando banco de dados..."
if [ -f "$DB_FILE" ]; then
    echo "✅ Banco de dados encontrado: $DB_FILE"
    SIZE=$(du -h "$DB_FILE" | cut -f1)
    echo "   Tamanho: $SIZE"
    echo "   Última modificação:"
    ls -lh "$DB_FILE" | awk '{print $6, $7, $8}' | sed 's/^/      /'
else
    echo "⚠️  Banco de dados NÃO ENCONTRADO"
    echo "   Será criado automaticamente na próxima inicialização"
fi
echo ""

# 5. Verificar logs
echo "5️⃣  Verificando logs..."
if [ -f "$LOG_FILE" ]; then
    echo "✅ Log encontrado: $LOG_FILE"
    echo "   Últimas 5 linhas:"
    tail -5 "$LOG_FILE" | sed 's/^/      /'
else
    echo "⚠️  Nenhum arquivo de log encontrado"
fi
echo ""

# 6. Verificar systemd service
echo "6️⃣  Verificando systemd service..."
if systemctl is-active --quiet cyzor 2>/dev/null; then
    echo "✅ Service 'cyzor' está ATIVO"
    echo "   Status:"
    systemctl status cyzor --no-pager | head -10 | sed 's/^/      /'
else
    echo "❌ Service 'cyzor' NÃO ESTÁ ATIVO ou NÃO EXISTE"
    echo "   Verifique com: systemctl status cyzor"
fi
echo ""

# 7. Testar conectividade HTTP
echo "7️⃣  Testando HTTP..."
RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null)
if [ "$RESPONSE" = "200" ]; then
    echo "✅ HTTP /health respondendo (200)"
else
    echo "❌ HTTP /health não responde ou erro: $RESPONSE"
fi
echo ""

# 8. Verificar permissões
echo "8️⃣  Verificando permissões..."
if [ -d "$APP_DIR" ]; then
    echo "✅ Diretório existe: $APP_DIR"
    PERMS=$(ls -ld "$APP_DIR" | awk '{print $1}')
    echo "   Permissões: $PERMS"
    OWNER=$(ls -ld "$APP_DIR" | awk '{print $3":"$4}')
    echo "   Proprietário: $OWNER"
else
    echo "❌ Diretório NÃO EXISTE: $APP_DIR"
fi
echo ""

# 9. Resumo final
echo "════════════════════════════════════════"
echo "📋 Próximos passos:"
echo "════════════════════════════════════════"
echo ""

if pgrep -a "Cyzor.Provisioning" > /dev/null; then
    echo "✅ Serviço está rodando!"
    echo "   Teste: curl http://localhost:5000/swagger"
else
    echo "❌ Serviço está PARADO!"
    echo "   Para iniciar manualmente:"
    echo "   $ cd $APP_DIR"
    echo "   $ ./Cyzor.Provisioning"
    echo ""
    echo "   Para iniciar via systemd:"
    echo "   $ systemctl start cyzor"
fi

echo ""
echo "✨ Diagnóstico completo!"
