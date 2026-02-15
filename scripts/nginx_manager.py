#!/usr/bin/env python3
"""
Cyzor Nginx Manager - Gerencia configurações Nginx para tenants
"""

import os
import sys
import subprocess
import json
from pathlib import Path
from datetime import datetime

class NginxManager:
    def __init__(self):
        self.nginx_available = "/etc/nginx/sites-available"
        self.nginx_enabled = "/etc/nginx/sites-enabled"
        self.port_start = 6001
        self.port_step = 2
        self.domain_suffix = "cyzor.local"
        
    def get_next_available_port(self):
        """Encontrar próxima porta disponível"""
        port = self.port_start
        while self._port_in_use(port):
            port += self.port_step
        return port
    
    def _port_in_use(self, port):
        """Verificar se porta está em uso"""
        result = subprocess.run(
            f"ss -tlnp 2>/dev/null | grep -q ':{port}'",
            shell=True, capture_output=True
        )
        return result.returncode == 0
    
    def create_nginx_config(self, tenant_id, domain, port):
        """Criar arquivo de configuração Nginx"""
        if not domain.endswith(self.domain_suffix):
            domain = f"{domain}.{self.domain_suffix}"
        
        conf_file = f"{self.nginx_available}/{domain}.conf"
        
        config_content = f"""# Cyzor Tenant: {domain}
# ID: {tenant_id}
# Port: {port}
# Created: {datetime.now().isoformat()}

server {{
    listen 80;
    server_name {domain};

    # Redirect to HTTPS (after Let's Encrypt setup)
    # return 301 https://$server_name$request_uri;

    location / {{
        proxy_pass http://localhost:{port};
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_http_version 1.1;
        proxy_set_header Connection '';
        proxy_buffering off;
        proxy_request_buffering off;
    }}

    location /health {{
        proxy_pass http://localhost:{port}/;
        access_log off;
    }}
}}
"""
        
        try:
            with open(conf_file, 'w') as f:
                f.write(config_content)
            print(f"✅ Configuração criada: {conf_file}")
            
            # Criar symlink
            symlink_path = f"{self.nginx_enabled}/{os.path.basename(conf_file)}"
            if os.path.exists(symlink_path):
                os.remove(symlink_path)
            os.symlink(conf_file, symlink_path)
            print(f"✅ Symlink criado em sites-enabled")
            
            return conf_file
        except Exception as e:
            print(f"❌ Erro ao criar configuração: {e}")
            return None
    
    def validate_nginx(self):
        """Validar configuração Nginx"""
        result = subprocess.run("nginx -t 2>&1", shell=True, capture_output=True, text=True)
        return "successful" in result.stderr and result.returncode == 0
    
    def reload_nginx(self):
        """Recarregar Nginx"""
        result = subprocess.run("systemctl reload nginx", shell=True, capture_output=True)
        return result.returncode == 0
    
    def setup_tenant(self, tenant_id, domain, port=None):
        """Setup completo de um tenant"""
        print(f"\n🔧 Configurando tenant: {tenant_id}")
        print(f"   Domain: {domain}")
        
        # Determinar porta se não fornecida
        if port is None:
            port = self.get_next_available_port()
        print(f"   Port: {port}")
        
        # Criar configuração Nginx
        if not self.create_nginx_config(tenant_id, domain, port):
            return False
        
        # Validar
        if not self.validate_nginx():
            print("❌ Configuração inválida")
            return False
        print("✅ Configuração validada")
        
        # Recarregar
        if not self.reload_nginx():
            print("⚠️  Erro ao recarregar (mas configuração pode estar OK)")
            return False
        print("✅ Nginx recarregado")
        
        return True
    
    def list_tenants(self):
        """Listar todos os tenants configurados"""
        tenants = []
        
        for conf_file in Path(self.nginx_available).glob("*.conf"):
            # Extrair informações
            with open(conf_file) as f:
                content = f.read()
            
            # Extrair domain
            domain_match = [l for l in content.split('\n') if 'server_name' in l]
            domain = domain_match[0].split()[1].rstrip(';') if domain_match else 'unknown'
            
            # Extrair port
            port_match = [l for l in content.split('\n') if 'proxy_pass' in l and 'http://localhost' in l]
            port = port_match[0].split(':')[-1].rstrip(';').strip("'\"") if port_match else 'unknown'
            
            # Extrair ID
            id_match = [l for l in content.split('\n') if '# ID:' in l]
            tenant_id = id_match[0].split(':')[1].strip() if id_match else basename(conf_file).replace('.conf', '')
            
            tenants.append({
                'id': tenant_id,
                'domain': domain,
                'port': port,
                'config': str(conf_file)
            })
        
        return tenants
    
    def get_status(self):
        """Obter status geral"""
        status = {
            'nginx': 'unknown',
            'nginx_valid': False,
            'tenants_count': 0,
            'tenants': []
        }
        
        # Status Nginx
        result = subprocess.run("systemctl is-active nginx", shell=True, capture_output=True)
        status['nginx'] = 'active' if result.returncode == 0 else 'inactive'
        
        # Validação
        status['nginx_valid'] = self.validate_nginx()
        
        # Tenants
        tenants = self.list_tenants()
        status['tenants_count'] = len(tenants)
        status['tenants'] = tenants
        
        return status


def main():
    if len(sys.argv) < 2:
        print("Uso:")
        print(f"  {sys.argv[0]} setup <tenant-id> <domain> [port]")
        print(f"  {sys.argv[0]} list")
        print(f"  {sys.argv[0]} status")
        print(f"  {sys.argv[0]} validate")
        print(f"  {sys.argv[0]} reload")
        sys.exit(1)
    
    manager = NginxManager()
    command = sys.argv[1]
    
    if command == "setup":
        if len(sys.argv) < 4:
            print("❌ Parâmetros insuficientes")
            print(f"Uso: {sys.argv[0]} setup <tenant-id> <domain> [port]")
            sys.exit(1)
        
        tenant_id = sys.argv[2]
        domain = sys.argv[3]
        port = int(sys.argv[4]) if len(sys.argv) > 4 else None
        
        success = manager.setup_tenant(tenant_id, domain, port)
        sys.exit(0 if success else 1)
    
    elif command == "list":
        tenants = manager.list_tenants()
        print(json.dumps(tenants, indent=2))
    
    elif command == "status":
        status = manager.get_status()
        print(json.dumps(status, indent=2))
    
    elif command == "validate":
        if manager.validate_nginx():
            print("✅ Configuração Nginx válida")
            sys.exit(0)
        else:
            print("❌ Configuração Nginx inválida")
            sys.exit(1)
    
    elif command == "reload":
        if manager.reload_nginx():
            print("✅ Nginx recarregado")
            sys.exit(0)
        else:
            print("❌ Erro ao recarregar Nginx")
            sys.exit(1)
    
    else:
        print(f"❌ Comando desconhecido: {command}")
        sys.exit(1)


if __name__ == "__main__":
    main()
