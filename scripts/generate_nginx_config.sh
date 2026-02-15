#!/bin/bash
# Script para Gerar Configuração Nginx Automaticamente para Novo Tenant
# Uso: ./generate_nginx_config.sh <tenant-id> <domain> <port>

TENANT_ID="$1"
DOMAIN="$2"
PORT="$3"
NGINX_AVAILABLE="/etc/nginx/sites-available"
NGINX_ENABLED="/etc/nginx/sites-enabled"

if [ -z "$TENANT_ID" ] || [ -z "$DOMAIN" ] || [ -z "$PORT" ]; then
    echo "❌ Erro: Parâmetros insuficientes"
    echo "Uso: $0 <tenant-id> <domain> <port>"
    echo "Exemplo: $0 abc123def456 app.example.com 6001"
    exit 1
fi

CONF_FILE="${NGINX_AVAILABLE}/${DOMAIN}.conf"

echo "📝 Gerando configuração Nginx..."
echo "   Tenant: $TENANT_ID"
echo "   Domain: $DOMAIN"
echo "   Port: $PORT"
echo ""

# Criar configuração
cat > "$CONF_FILE" <<EOF
# Cyzor Tenant: ${DOMAIN}
# ID: ${TENANT_ID}
# Port: ${PORT}
# Created: $(date)

server {
    listen 80;
    server_name ${DOMAIN};

    # Redirect to HTTPS (after Let's Encrypt setup)
    # return 301 https://\$server_name\$request_uri;

    location / {
        proxy_pass http://localhost:${PORT};
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
        proxy_pass http://localhost:${PORT}/;
        access_log off;
    }
}
EOF

if [ $? -eq 0 ]; then
    echo "✅ Arquivo criado: $CONF_FILE"
else
    echo "❌ Erro ao criar arquivo"
    exit 1
fi

# Criar symlink para sites-enabled
ln -sf "$CONF_FILE" "${NGINX_ENABLED}/$(basename "$CONF_FILE")" 2>/dev/null
if [ $? -eq 0 ]; then
    echo "✅ Symlink criado em sites-enabled"
else
    echo "⚠️  Erro ao criar symlink (pode já existir)"
fi

# Validar configuração Nginx
echo ""
echo "🔍 Validando configuração Nginx..."
if nginx -t 2>&1 | grep -q "successful"; then
    echo "✅ Configuração válida"
    
    # Recarregar Nginx
    echo ""
    echo "🔄 Recarregando Nginx..."
    systemctl reload nginx 2>&1
    if [ $? -eq 0 ]; then
        echo "✅ Nginx recarregado com sucesso"
    else
        echo "⚠️  Erro ao recarregar (verifique com: systemctl restart nginx)"
    fi
else
    echo "❌ Configuração inválida - verifique com: nginx -t"
    exit 1
fi

echo ""
echo "✨ Configuração concluída!"
echo ""
echo "📋 Próximos passos:"
echo "   1. Iniciar aplicação em porta $PORT"
echo "   2. Testar acesso: curl http://$DOMAIN/"
echo "   3. Ver logs: tail -f /var/log/nginx/error.log"
