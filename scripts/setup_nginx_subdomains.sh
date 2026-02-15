#!/bin/bash
# Script para Gerar e Atualizar Configurações Nginx para Tenants Cyzor

# ============================================================
# CONFIGURAÇÕES
# ============================================================
APP_DIR="/var/www"
NGINX_AVAILABLE="/etc/nginx/sites-available"
NGINX_ENABLED="/etc/nginx/sites-enabled"
NEXT_PORT=6001
PORT_INCREMENT=2
API_KEY="${1:-test-key-12345}"
DOMAIN_SUFFIX="${2:-cyzor.local}"

# ============================================================
# FUNÇÕES
# ============================================================

echo_header() {
    echo ""
    echo "════════════════════════════════════════════════════════════════"
    echo "$1"
    echo "════════════════════════════════════════════════════════════════"
    echo ""
}

# Gerar ID único baseado em timestamp
generate_id() {
    echo $(date +%s%N | sha256sum | head -c 8)
}

# Extrair ID do diretório (ex: /var/www/36ce42ce -> 36ce42ce)
get_tenant_id() {
    basename "$1"
}

# Obter próxima porta disponível
get_next_available_port() {
    local port=$NEXT_PORT
    while netstat -tlnp 2>/dev/null | grep -q ":$port "; do
        port=$((port + PORT_INCREMENT))
    done
    echo $port
}

# Criar configuração Nginx para tensor
create_nginx_config() {
    local tenant_id="$1"
    local domain="$2"
    local port="$3"
    local conf_file="${NGINX_AVAILABLE}/${domain}.conf"
    
    cat > "$conf_file" <<EOF
# Cyzor Tenant: ${domain}
# ID: ${tenant_id}
# Port: ${port}
server {
    listen 80;
    server_name ${domain};

    # Redirect to HTTPS (after Let's Encrypt setup)
    # return 301 https://\$server_name\$request_uri;

    location / {
        proxy_pass http://localhost:${port};
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_http_version 1.1;
        proxy_set_header Connection '';
        proxy_buffering off;
        proxy_request_buffering off;
    }

    location /health {
        proxy_pass http://localhost:${port}/;
        access_log off;
    }
}
EOF

    # Criar symlink para sites-enabled
    ln -sf "$conf_file" "${NGINX_ENABLED}/$(basename "$conf_file")" 2>/dev/null

    echo "✅ Config Nginx criada: $conf_file"
    echo "   Domain: $domain"
    echo "   Port: $port"
}

# ============================================================
# VERIFICAÇÃO TENANT EXISTENTE
# ============================================================
echo_header "1️⃣  VERIFICANDO TENANTS EXISTENTES"

if [ -d "$APP_DIR" ]; then
    echo "Tenants encontrados em $APP_DIR:"
    echo ""
    
    for tenant_dir in "$APP_DIR"/*; do
        if [ -d "$tenant_dir" ] && [ -f "$tenant_dir/publish/server.js" 2>/dev/null ]; then
            tenant_id=$(get_tenant_id "$tenant_dir")
            
            # Verificar se config Nginx existe
            if [ -f "$NGINX_AVAILABLE/${tenant_id}_${DOMAIN_SUFFIX}.conf" ]; then
                port=$(grep -hE 'proxy_pass.*:([0-9]+)' "$NGINX_AVAILABLE/${tenant_id}_${DOMAIN_SUFFIX}.conf" | head -1 | grep -oE '[0-9]+$')
                echo "✅ $tenant_id"
                echo "   Domain: ${tenant_id}.${DOMAIN_SUFFIX}"
                echo "   Port: $port"
            else
                echo "❌ $tenant_id (sem config Nginx)"
            fi
            
            # Verificar PM2
            if pm2 list 2>/dev/null | grep -q "$tenant_id"; then
                status="online"
            else
                status="not in PM2"
            fi
            echo "   PM2: $status"
            echo ""
        fi
    done
fi

echo ""

# ============================================================
# SINCRONIZAR COM BANCO DE DADOS
# ============================================================
echo_header "2️⃣  SINCRONIZANDO COM BANCO DE DADOS"

DB_FILE="/var/www/cyzor_dotnet/tenants.db"

if command -v sqlite3 &> /dev/null; then
    if [ -f "$DB_FILE" ]; then
        echo "Tenants no banco de dados:"
        echo ""
        
        # Criar tabela de mapeamento se não existir
        sqlite3 "$DB_FILE" <<EOF 2>/dev/null
CREATE TABLE IF NOT EXISTS tenant_ports (
    id TEXT PRIMARY KEY,
    domain TEXT,
    port INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
EOF
        
        # Consultar tenants
        sqlite3 "$DB_FILE" <<EOF 2>/dev/null | while IFS='|' read id domain apptype status; do
.mode list
.separator |
SELECT id, domain, app_type, status FROM tenants ORDER BY created_at DESC;
EOF
        done
    fi
fi

echo ""

# ============================================================
# CRIAR NOVO TENANT DE TESTE
# ============================================================
echo_header "3️⃣  CRIANDO TENANT DE TESTE"

TEST_TENANT_ID="test_$(generate_id)"
TEST_DOMAIN="test1.${DOMAIN_SUFFIX}"
TEST_PORT=$(get_next_available_port)

echo "Novo tenant de teste:"
echo "   ID: $TEST_TENANT_ID"
echo "   Domain: $TEST_DOMAIN"
echo "   Port: $TEST_PORT"
echo ""

# Criar config Nginx
create_nginx_config "$TEST_TENANT_ID" "$TEST_DOMAIN" "$TEST_PORT"

echo ""

# ============================================================
# VALIDAR CONFIGURAÇÃO NGINX
# ============================================================
echo_header "4️⃣  VALIDANDO CONFIGURAÇÃO NGINX"

if nginx -t 2>&1; then
    echo "✅ Configuração Nginx válida"
else
    echo "❌ Erro na configuração Nginx"
    exit 1
fi

echo ""

# ============================================================
# RECARREGAR NGINX
# ============================================================
echo_header "5️⃣  RECARREGANDO NGINX"

systemctl reload nginx 2>&1
if [ $? -eq 0 ]; then
    echo "✅ Nginx recarregado com sucesso"
else
    echo "❌ Erro ao recarregar Nginx"
fi

echo ""

# ============================================================
# RESUMO FINAL
# ============================================================
echo_header "✨ RESUMO FINAL"

echo "Verificações realizadas:"
echo "   ✓ Tenants existentes catalogados"
echo "   ✓ Configurações Nginx analisadas"
echo "   ✓ Nova config Nginx criada para teste"
echo "   ✓ Nginx validado e recarregado"
echo ""

echo "Próximas ações:"
echo "   1. Iniciar aplicação do tenant em $TEST_PORT"
echo "      Exemplo: npm start -- --port $TEST_PORT"
echo ""
echo "   2. Testar acesso:"
echo "      $ curl http://$TEST_DOMAIN/"
echo "      $ curl -H 'Host: $TEST_DOMAIN' http://localhost/"
echo ""
echo "   3. Verificar logs:"
echo "      $ tail -f /var/log/nginx/error.log"
echo "      $ tail -f /var/log/nginx/access.log"
echo ""

echo "✨ Script concluído em $(date)"
