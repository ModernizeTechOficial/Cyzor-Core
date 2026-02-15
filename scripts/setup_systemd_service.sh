#!/bin/bash
# Criar systemd service para garantir auto-start do Cyzor

SERVICE_NAME="cyzor"
SERVICE_PATH="/etc/systemd/system/${SERVICE_NAME}.service"
APP_DIR="/var/www/cyzor_dotnet"
APP_BINARY="${APP_DIR}/Cyzor.Provisioning"

echo "🔧 Criando systemd service para $SERVICE_NAME..."

# Criar arquivo de serviço
sudo tee $SERVICE_PATH > /dev/null <<EOF
[Unit]
Description=Cyzor Provisioning API
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=$APP_DIR
ExecStart=${APP_BINARY}
Restart=on-failure
RestartSec=10
StandardOutput=append:/var/log/${SERVICE_NAME}.log
StandardError=append:/var/log/${SERVICE_NAME}.log

[Install]
WantedBy=multi-user.target
EOF

echo "✅ Service file criado em: $SERVICE_PATH"

# Recarregar systemd daemon
sudo systemctl daemon-reload
echo "✅ Daemon recarregado"

# Habilitar serviço para iniciar na boot
sudo systemctl enable $SERVICE_NAME
echo "✅ Service habilitado para auto-start"

# Iniciar serviço agora
sudo systemctl start $SERVICE_NAME
echo "✅ Service iniciado"

# Verificar status
echo ""
echo "📊 Status do Service:"
sudo systemctl status $SERVICE_NAME --no-pager

# Testar endpoint
echo ""
echo "🔍 Testando endpoint..."
sleep 2
curl -s http://localhost:5000/health || echo "❌ Serviço ainda não responde"

echo ""
echo "✨ Setup completo!"
