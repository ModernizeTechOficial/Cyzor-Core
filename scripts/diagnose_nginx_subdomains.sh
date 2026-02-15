#!/bin/bash
# Diagnóstico Completo: Subdomínios, Nginx e Apontamentos

echo "════════════════════════════════════════════════════════════════"
echo "🔍 DIAGNÓSTICO CYZOR - SUBDOMÍNIOS E NGINX"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "Data: $(date)"
echo ""

# ============================================================
# 1. VERIFICAR CYZOR E BANCO DE DADOS
# ============================================================
echo "1️⃣  STATUS DA APLICAÇÃO CYZOR"
echo "───────────────────────────────────────────────────────────────"

# Verificar processo
if pgrep -f "Cyzor.Provisioning" > /dev/null; then
    echo "✅ Processo Cyzor está RODANDO"
    PID=$(pgrep -f "Cyzor.Provisioning")
    echo "   PID: $PID"
else
    echo "❌ Processo Cyzor NÃO ESTÁ RODANDO"
fi
echo ""

# Verificar porta 5000
if netstat -tlnp 2>/dev/null | grep -q ":5000"; then
    echo "✅ Porta 5000 está ABERTA"
    netstat -tlnp 2>/dev/null | grep ":5000" | sed 's/^/   /'
else
    echo "❌ Porta 5000 NÃO ESTÁ ABERTA"
fi
echo ""

# Testar HTTP
HTTP_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null)
if [ "$HTTP_RESPONSE" = "200" ]; then
    echo "✅ HTTP /health respondendo (200)"
else
    echo "❌ HTTP /health erro: $HTTP_RESPONSE"
fi
echo ""

# Verificar banco de dados
DB_FILE="/var/www/cyzor_dotnet/tenants.db"
echo "📊 Banco de dados:"
if [ -f "$DB_FILE" ]; then
    SIZE=$(du -h "$DB_FILE" | cut -f1)
    echo "   ✅ Encontrado: $DB_FILE"
    echo "   Tamanho: $SIZE"
else
    echo "   ❌ Não encontrado: $DB_FILE"
fi
echo ""

# ============================================================
# 2. VERIFICAR REGISTROS DO BANCO DE DADOS
# ============================================================
echo "2️⃣  REGISTROS DE SUBDOMÍNIOS NO BANCO DE DADOS"
echo "───────────────────────────────────────────────────────────────"

if [ -f "$DB_FILE" ]; then
    # Tentar consultar com sqlite3
    if command -v sqlite3 &> /dev/null; then
        echo "Tenants registrados:"
        sqlite3 "$DB_FILE" "SELECT id, domain, status, created_at FROM tenants LIMIT 20" 2>/dev/null || echo "   (erro ao ler banco)"
    else
        echo "⚠️  sqlite3 não está instalado"
    fi
fi
echo ""

# ============================================================
# 3. VERIFICAR CONFIGURAÇÕES NGINX
# ============================================================
echo "3️⃣  CONFIGURAÇÃO NGINX"
echo "───────────────────────────────────────────────────────────────"

# Verificar se Nginx está rodando
if systemctl is-active --quiet nginx; then
    echo "✅ Nginx está RODANDO"
else
    echo "⚠️  Nginx não está ativo"
fi
echo ""

# Listar sites habilitados
echo "📁 Sites habilitados no Nginx:"
if [ -d "/etc/nginx/sites-enabled" ]; then
    ls -la /etc/nginx/sites-enabled/ 2>/dev/null | grep -v "^total" | sed 's/^/   /'
else
    echo "   ⚠️  Diretório não existente"
fi
echo ""

# Verificar configurações de subdomínios
echo "📝 Verificando configurações de subdomínios:"
if [ -d "/etc/nginx/conf.d" ]; then
    CONF_FILES=$(find /etc/nginx/conf.d -name "*.conf" 2>/dev/null | wc -l)
    echo "   Arquivos .conf: $CONF_FILES"
    
    echo "   Conteúdo:"
    find /etc/nginx/conf.d -name "*.conf" -type f 2>/dev/null | head -10 | while read f; do
        echo ""
        echo "   📄 $f"
        head -20 "$f" | sed 's/^/      /'
    done
else
    echo "   ⚠️  /etc/nginx/conf.d não existe"
fi
echo ""

# ============================================================
# 4. VERIFICAR DIRETÓRIOS DE APLICAÇÕES
# ============================================================
echo "4️⃣  DIRETÓRIOS DE APLICAÇÕES DOS TENANTS"
echo "───────────────────────────────────────────────────────────────"

APPS_DIR="/app"
if [ -d "$APPS_DIR" ]; then
    echo "✅ Diretório existe: $APPS_DIR"
    echo "   Subdirectórios:"
    ls -la "$APPS_DIR" 2>/dev/null | grep "^d" | sed 's/^/      /'
    
    echo ""
    echo "   Total de aplicações:"
    COUNT=$(ls -1d "$APPS_DIR"/*/ 2>/dev/null | wc -l)
    echo "   $COUNT aplicações encontradas"
else
    echo "❌ Diretório NÃO EXISTS: $APPS_DIR"
    echo "   Verificando alternativas..."
    
    # Procurar em localizações comuns
    for DIR in "/var/www/apps" "/home/apps" "/opt/apps"; do
        if [ -d "$DIR" ]; then
            echo "   ✅ Encontrado em: $DIR"
            ls -la "$DIR" | head -20 | sed 's/^/      /'
        fi
    done
fi
echo ""

# ============================================================
# 5. VERIFICAR PROCESSOS PM2
# ============================================================
echo "5️⃣  PROCESSOS PM2 DOS TENANTS"
echo "───────────────────────────────────────────────────────────────"

if command -v pm2 &> /dev/null; then
    echo "✅ PM2 instalado"
    echo "   Processos:"
    pm2 list 2>/dev/null || echo "   (erro ao listar)"
else
    echo "❌ PM2 não está instalado"
    echo "   Verificando processos Node.js:"
    ps aux | grep -i "node" | grep -v grep | sed 's/^/   /'
fi
echo ""

# ============================================================
# 6. VERIFICAR DNS LOCAL
# ============================================================
echo "6️⃣  CONFIGURAÇÃO DNS LOCAL"
echo "───────────────────────────────────────────────────────────────"

echo "📄 /etc/hosts:"
cat /etc/hosts | sed 's/^/   /'
echo ""

# ============================================================
# 7. VERIFICAR PORTA HTTP/HTTPS
# ============================================================
echo "7️⃣  PORTAS HTTP/HTTPS"
echo "───────────────────────────────────────────────────────────────"

echo "Porta 80 (HTTP):"
if netstat -tlnp 2>/dev/null | grep -q ":80 "; then
    echo "   ✅ ABERTA"
    netstat -tlnp 2>/dev/null | grep ":80 " | sed 's/^/      /'
else
    echo "   ❌ FECHADA"
fi
echo ""

echo "Porta 443 (HTTPS):"
if netstat -tlnp 2>/dev/null | grep -q ":443 "; then
    echo "   ✅ ABERTA"
    netstat -tlnp 2>/dev/null | grep ":443 " | sed 's/^/      /'
else
    echo "   ❌ FECHADA"
fi
echo ""

# ============================================================
# 8. TESTE DE CRIAÇÃO DE TENANT
# ============================================================
echo "8️⃣  TESTE API CYZOR"
echo "───────────────────────────────────────────────────────────────"

# Tentar listar tenants via API (sem auth para teste)
echo "Tentando consultar tenants..."
curl -s http://localhost:5000/api/status/test 2>/dev/null || echo "   (erro ao conectar)"
echo ""

# ============================================================
echo ""
echo "════════════════════════════════════════════════════════════════"
echo "✨ FIM DO DIAGNÓSTICO"
echo "════════════════════════════════════════════════════════════════"
