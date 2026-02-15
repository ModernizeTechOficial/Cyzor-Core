#!/bin/bash
# Script de Diagnóstico Rápido para Verificar Status do Cyzor e Configurações

echo "════════════════════════════════════════════════════════════════"
echo "🔧 DIAGNÓSTICO RÁPIDO - CYZOR + NGINX + WEBUZO"
echo "════════════════════════════════════════════════════════════════"

# ============================================================
# PARTE 1: VERIFICAR CYZOR
# ============================================================
echo ""
echo "1️⃣  CYZOR PROVISIONING API"
echo "─────────────────────────────────────────────────────────"
echo ""

echo "Processo Cyzor:"
if pgrep -f "Cyzor.Provisioning" > /dev/null; then
    echo "   ✅ RODANDO"
    ps aux | grep -i "Cyzor.Provisioning" | grep -v grep | awk '{printf "   PID: %s | CPU: %s | MEM: %s\n", $2, $3, $4}'
else
    echo "   ❌ PARADO"
    echo "   Para iniciar:"
    echo "   $ systemctl start cyzor"
    echo "   Ou manualmente:"
    echo "   $ cd /var/www/cyzor_dotnet && ./Cyzor.Provisioning"
fi
echo ""

echo "Porta 5000 (API):"
if netstat -tlnp 2>/dev/null | grep -q ":5000"; then
    echo "   ✅ ABERTA"
else
    echo "   ❌ FECHADA"
fi
echo ""

echo "HTTP Health Check:"
HEALTH=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null)
if [ "$HEALTH" = "200" ]; then
    echo "   ✅ RESPONDENDO (200)"
else
    echo "   ❌ NÃO RESPONDE ou ERRO: $HEALTH"
fi
echo ""

# ============================================================
# PARTE 2: SUBDOMÍNIOS NO BANCO DE DADOS
# ============================================================
echo ""
echo "2️⃣  TENANTS/SUBDOMÍNIOS REGISTRADOS"
echo "─────────────────────────────────────────────────────────"
echo ""

DB_FILE="/var/www/cyzor_dotnet/tenants.db"
if [ -f "$DB_FILE" ]; then
    if command -v sqlite3 &> /dev/null; then
        echo "Subdomínios em banco de dados:"
        echo ""
        sqlite3 "$DB_FILE" <<EOF > /tmp/tenants.txt 2>/dev/null
.mode column
.headers on
SELECT id, domain, status, created_at FROM tenants;
EOF
        
        if [ -s /tmp/tenants.txt ]; then
            cat /tmp/tenants.txt | sed 's/^/   /'
        else
            echo "   (Nenhum tenant no banco)"
        fi
    else
        echo "   ⚠️  sqlite3 não instalado - instale com: apt install sqlite3"
    fi
else
    echo "   ❌ Banco de dados não encontrado em: $DB_FILE"
fi
echo ""

# ============================================================
# PARTE 3: CONFIGURAÇÃO NGINX
# ============================================================
echo ""
echo "3️⃣  NGINX - CONFIGURAÇÃO DE SUBDOMÍNIOS"
echo "─────────────────────────────────────────────────────────"
echo ""

echo "Status Nginx:"
if systemctl is-active --quiet nginx; then
    echo "   ✅ ATIVO"
else
    echo "   ⚠️  INATIVO - inicie com: systemctl start nginx"
fi
echo ""

echo "Arquivos de configuração de subdomínios:"
if [ -d "/etc/nginx/conf.d" ]; then
    CONF_COUNT=$(find /etc/nginx/conf.d -name "*.conf" 2>/dev/null | wc -l)
    if [ $CONF_COUNT -gt 0 ]; then
        echo "   ✅ $CONF_COUNT arquivos .conf encontrados"
        echo ""
        find /etc/nginx/conf.d -name "*.conf" -type f 2>/dev/null | while read conf; do
            DOMAIN=$(grep "server_name" "$conf" 2>/dev/null | head -1 | awk '{print $2}' | tr -d ';')
            PORT=$(grep "proxy_pass" "$conf" 2>/dev/null | head -1 | awk '{print $2}' | sed 's|.*:||g' | tr -d ';')
            if [ -n "$DOMAIN" ]; then
                echo "   📄 $(basename "$conf")"
                echo "      Domain: $DOMAIN"
                echo "      Proxy para porta: $PORT"
            fi
        done
    else
        echo "   ⚠️  Nenhum arquivo .conf em /etc/nginx/conf.d"
    fi
fi
echo ""

# ============================================================
# PARTE 4: VERIFICAR PROCESOS DAS APLICAÇÕES
# ============================================================
echo ""
echo "4️⃣  PROCESSOS DAS APLICAÇÕES DOS TENANTS"
echo "─────────────────────────────────────────────────────────"
echo ""

echo "Verificar PM2:"
if command -v pm2 &> /dev/null; then
    echo "   ✅ PM2 instalado"
    PM2_COUNT=$(pm2 list 2>/dev/null | grep -c "online\|stopped\|errored" || echo "0")
    if [ $PM2_COUNT -gt 0 ]; then
        echo "   Aplicações gerenciadas:"
        pm2 list 2>/dev/null | tail -n +3 | head -20 | sed 's/^/      /'
    else
        echo "   ⚠️  Nenhuma aplicação registrada em PM2"
    fi
else
    echo "   ❌ PM2 não instalado"
    echo "   Procurando processos Node.js manualmente:"
    NODE_PROCS=$(ps aux | grep -i "node" | grep -v grep | wc -l)
    if [ $NODE_PROCS -gt 0 ]; then
        ps aux | grep -i "node" | grep -v grep | sed 's/^/      /'
    else
        echo "      ⚠️  Nenhum processo Node.js encontrado"
    fi
fi
echo ""

# ============================================================
# PARTE 5: TESTAR RESOLUÇÃO DE NOMES
# ============================================================
echo ""
echo "5️⃣  RESOLUÇÃO DE NOMES (DNS)"
echo "─────────────────────────────────────────────────────────"
echo ""

# Pegar um domínio do banco para testar
TEST_DOMAIN=$(sqlite3 "$DB_FILE" "SELECT domain FROM tenants LIMIT 1" 2>/dev/null)

if [ -n "$TEST_DOMAIN" ]; then
    echo "Testando domínio: $TEST_DOMAIN"
    echo ""
    
    echo "   Resolução local (/etc/hosts):"
    grep "$TEST_DOMAIN" /etc/hosts || echo "      (não encontrado em /etc/hosts)"
    echo ""
    
    echo "   Resolução DNS:"
    nslookup "$TEST_DOMAIN" 8.8.8.8 2>/dev/null || echo "      (erro na resolução)"
    echo ""
    
    echo "   Teste HTTP ao domínio:"
    curl -s -o /dev/null -w "      HTTP Status: %{http_code}\n" "http://$TEST_DOMAIN/" 2>/dev/null || echo "      (erro ao conectar)"
fi
echo ""

# ============================================================
# PARTE 6: VERIFICAÇÃO DE WEBUZO
# ============================================================
echo ""
echo "6️⃣  WEBUZO PANEL"
echo "─────────────────────────────────────────────────────────"
echo ""

if command -v webuzo &> /dev/null; then
    echo "   ✅ WebuZO instalado"
    WEBUZO_VER=$(webuzo --version 2>/dev/null || echo "versão desconhecida")
    echo "   Versão: $WEBUZO_VER"
else
    echo "   ⚠️  WebuZO não encontrado no PATH"
fi

# Verificar se WebuZO está rodando na porta 2002
if netstat -tlnp 2>/dev/null | grep -q ":2002"; then
    echo "   ✅ Porta 2002 (WebuZO) está aberta"
else
    echo "   ⚠️  Porta 2002 (WebuZO) não está listening"
fi
echo ""

# ============================================================
# PARTE 7: RESUMO E AÇÕES
# ============================================================
echo ""
echo "════════════════════════════════════════════════════════════════"
echo "📋 RESUMO E PRÓXIMAS AÇÕES"
echo "════════════════════════════════════════════════════════════════"
echo ""

echo "🔍 Verificações Executadas:"
echo "   ✓ Status do serviço Cyzor"
echo "   ✓ Tenants no banco de dados"
echo "   ✓ Configuração Nginx"
echo "   ✓ Processos das aplicações"
echo "   ✓ Resolução de nomes"
echo "   ✓ Verificação WebuZO"
echo ""

echo "💡 Se algo está ❌:"
echo "   1. Verifique os logs:"
echo "      $ journalctl -u cyzor -n 50"
echo "      $ tail -50 /var/log/nginx/error.log"
echo "      $ tail -50 /var/log/cyzor.log"
echo ""
echo "   2. Reinicie serviços:"
echo "      $ systemctl restart cyzor"
echo "      $ systemctl restart nginx"
echo ""
echo "   3. Verifique configuração Nginx:"
echo "      $ nginx -t"
echo ""

echo "✨ Diagnóstico concluído em $(date)"
