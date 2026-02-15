#!/bin/bash
# Relatório Completo: Diagnóstico de Nginx, Subdomínios e Tenants

echo "════════════════════════════════════════════════════════════════════════"
echo "📊 RELATÓRIO COMPLETO - CYZOR PROVISIONING"
echo "════════════════════════════════════════════════════════════════════════"
echo ""
echo "Data: $(date)"
echo "Servidor: $(hostname)"
echo ""

# ============================================================
# SEÇÃO 1: STATUS DO SERVIÇO CYZOR
# ============================================================
echo ""
echo "┌─ 1️⃣  CYZOR API SERVICE ─────────────────────────────────────────┐"
echo ""

echo "Processo:"
if pgrep -f "Cyzor.Provisioning" > /dev/null; then
    echo "   ✅ RODANDO"
    PID=$(pgrep -f "Cyzor.Provisioning" | head -1)
    echo "   PID: $PID"
    UPTIME=$(ps -p $PID -o etime= | tr -d ' ')
    echo "   Uptime: $UPTIME"
else
    echo "   ❌ PARADO"
    echo "   Iniciar com: systemctl start cyzor"
fi
echo ""

echo "Porta 5000 (API):"
if ss -tlnp 2>/dev/null | grep -q ":5000"; then
    echo "   ✅ ABERTA"
else
    echo "   ❌ FECHADA"
fi
echo ""

echo "HTTP Health Check:"
HEALTH=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null)
if [ "$HEALTH" = "200" ]; then
    echo "   ✅ RESPONDENDO (HTTP $HEALTH)"
else
    echo "   ❌ NÃO RESPONDE (HTTP $HEALTH)"
fi
echo ""

echo "└────────────────────────────────────────────────────────────────────┘"

# ============================================================
# SEÇÃO 2: NGINX E SUBDOMÍNIOS
# ============================================================
echo ""
echo "┌─ 2️⃣  NGINX - CONFIGURAÇÃO ─────────────────────────────────────┐"
echo ""

echo "Status Nginx:"
if systemctl is-active --quiet nginx; then
    echo "   ✅ ATIVO"
    systemctl status nginx --no-pager | grep -E "Active|Loaded" | sed 's/^/      /'
else
    echo "   ❌ INATIVO"
fi
echo ""

echo "Validação de Configuração:"
if nginx -t 2>&1 | grep -q "successful"; then
    echo "   ✅ VÁLIDA"
else
    echo "   ❌ INVÁLIDA"
    nginx -t 2>&1 | sed 's/^/      /'
fi
echo ""

echo "Sites Ativados:"
SITES_COUNT=$(ls /etc/nginx/sites-enabled/*.conf 2>/dev/null | wc -l)
echo "   Total: $SITES_COUNT sites"
echo ""
ls -1 /etc/nginx/sites-enabled/*.conf 2>/dev/null | while read site; do
    DOMAIN=$(grep -h "server_name" "$site" | head -1 | awk '{print $2}' | tr -d ';')
    PORT=$(grep -h "proxy_pass" "$site" | head -1 | grep -oE '[0-9]+' | tail -1)
    echo "   📄 $(basename "$site")"
    echo "      Domain: $DOMAIN"
    echo "      Port: $PORT"
done
echo ""

echo "└────────────────────────────────────────────────────────────────────┘"

# ============================================================
# SEÇÃO 3: TENANTS E APLICAÇÕES
# ============================================================
echo ""
echo "┌─ 3️⃣  TENANTS EM BANCO DE DADOS ──────────────────────────────────┐"
echo ""

DB_FILE="/var/www/cyzor_dotnet/tenants.db"
if [ -f "$DB_FILE" ]; then
    echo "✅ Banco de dados encontrado"
    echo "   Arquivo: $DB_FILE"
    echo "   Tamanho: $(du -h "$DB_FILE" | cut -f1)"
    echo ""
    
    # Tentar usar o cliente de banco se disponível  
    if command -v sqlite3 &> /dev/null; then
        echo "   Tenants registrados:"
        sqlite3 "$DB_FILE" "SELECT COUNT(*) FROM tenants;" 2>/dev/null | sed 's/^/      Total: /'
        echo ""
        
        # Mostrar cada tenant
        sqlite3 "$DB_FILE" <<EOF 2>/dev/null | while IFS='|' read id domain apptype status; do
.mode list
.separator |
SELECT substr(id, 1, 8), domain, app_type, status FROM tenants ORDER BY created_at DESC;
EOF
            echo "      Tenant: $id (${id:0:8}...)"
            echo "         Domain: $domain"
            echo "         Type: $apptype"
            echo "         Status: $status"
        done
    fi
else
    echo "⚠️  Banco de dados não encontrado"
fi
echo ""

echo "└────────────────────────────────────────────────────────────────────┘"

# ============================================================
# SEÇÃO 4: PROCESSOS PM2
# ============================================================
echo ""
echo "┌─ 4️⃣  APLICAÇÕES (PM2) ──────────────────────────────────────────┐"
echo ""

if command -v pm2 &> /dev/null; then
    echo "✅ PM2 disponível"
    echo ""
    
    # Contar apps online
    ONLINE=$(pm2 list 2>/dev/null | grep -c "online")
    echo "   Aplicações: $ONLINE online"
    echo ""
    
    # Listar detalhes
    pm2 list 2>/dev/null | tail -n +3 | head -20
else
    echo "❌ PM2 não disponível"
fi
echo ""

echo "└────────────────────────────────────────────────────────────────────┘"

# ============================================================
# SEÇÃO 5: PORTAS EM USO
# ============================================================
echo ""
echo "┌─ 5️⃣  PORTAS EM USO ─────────────────────────────────────────────┐"
echo ""

echo "Serviços listening:"
ss -tlnp 2>/dev/null | grep -E ':(80|443|5000|6[0-9]{3})' | sed 's/^/   /'
echo ""

echo "└────────────────────────────────────────────────────────────────────┘"

# ============================================================
# SEÇÃO 6: RESOLUÇÃO DE NOMES
# ============================================================
echo ""
echo "┌─ 6️⃣  RESOLUÇÃO DE NOMES (DNS/HOSTS) ────────────────────────────┐"
echo ""

echo "/etc/hosts (Cyzor entries):"
grep "cyzor" /etc/hosts || echo "   (nenhuma entrada)"
echo ""

# Testar um domínio se houver
TEST_DOMAIN=$(ls /etc/nginx/sites-enabled | head -1 | grep -oE '[^_]+_[^_]+_[^.]+' | head -1)
if [ -n "$TEST_DOMAIN" ]; then
    echo "Teste de resolução ($TEST_DOMAIN):"
    if dig $TEST_DOMAIN +short 2>/dev/null | grep -q .; then
        echo "   ✅ Resolvido em: $(dig $TEST_DOMAIN +short)"
    else
        echo "   ⚠️  Não resolvido via DNS"
    fi
fi
echo ""

echo "└────────────────────────────────────────────────────────────────────┘"

# ============================================================
# SEÇÃO 7: PROBLEMAS IDENTIFI CADOS
# ============================================================
echo ""
echo "┌─ ⚠️  PROBLEMAS IDENTIFICADOS ─────────────────────────────────────┐"
echo ""

ISSUES=0

# Verificar Cyzor
if ! pgrep -f "Cyzor.Provisioning" > /dev/null; then
    echo "  [P$((ISSUES+=1))] Cyzor não está rodando"
fi

# Verificar Nginx
if ! systemctl is-active --quiet nginx; then
    echo "  [P$((ISSUES+=1))] Nginx não está ativo"
fi

# Verificar portas
if ! ss -tlnp 2>/dev/null | grep -q ":5000"; then
    echo "  [P$((ISSUES+=1))] Porta 5000 (Cyzor API) não está aberta"
fi

# Verificar banco
if [ ! -f "$DB_FILE" ]; then
    echo "  [P$((ISSUES+=1))] Banco de dados não encontrado"
fi

# Verificar PM2
if ! command -v pm2 &> /dev/null; then
    echo "  [P$((ISSUES+=1))] PM2 não está disponível"
fi

if [ $ISSUES -eq 0 ]; then
    echo "  ✅ Nenhum problema identificado!"
else
    echo "  Total de problemas: $ISSUES"
fi
echo ""

echo "└────────────────────────────────────────────────────────────────────┘"

# ============================================================
# SEÇÃO 8: RECOMENDAÇÕES
# ============================================================
echo ""
echo "┌─ 💡 RECOMENDAÇÕES ──────────────────────────────────────────────┐"
echo ""

echo "Para testar um novo tenant:"
echo ""
echo "  1. Provisionar via API:"
echo "     curl -X POST http://localhost:5000/api/provision \\"
echo "       -H 'Content-Type: application/json' \\"
echo "       -H 'X-API-Key: test-key-12345' \\"
echo "       -d '{\"domain\": \"test.cyzor.local\", \"appType\": \"node\"}'"
echo ""

echo "  2. Verificar status:"
echo "     curl http://localhost:5000/api/status/{instanceId} \\"
echo "       -H 'X-API-Key: test-key-12345'"
echo ""

echo "  3. Testar domínio:"
echo "     curl -H 'Host: test.cyzor.local' http://localhost/"
echo ""

echo "  4. Verificar logs:"
echo "     tail -f /var/log/nginx/access.log"
echo "     tail -f /var/log/nginx/error.log"
echo ""

echo "└────────────────────────────────────────────────────────────────────┘"

echo ""
echo "════════════════════════════════════════════════════════════════════════"
echo "✨ Relatório concluído em $(date)"
echo "════════════════════════════════════════════════════════════════════════"
