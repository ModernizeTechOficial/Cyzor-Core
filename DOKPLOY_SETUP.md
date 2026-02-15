# Cyzor Ecossistem - Dokploy Deployment Guide

## Arquivos Criados

### 1. **Dockerfile**
- Build multi-stage otimizado para .NET 8
- Contém health check integrado
- Reduz tamanho final da imagem

### 2. **docker-compose.yml**
- 3 serviços: Provisioning API, Webhook Receiver, Nginx
- Networking interno
- Environment variables já preenchidas com dados reais
- Health checks para todos os serviços

### 3. **dokploy.json**
- Configuração completa do Dokploy
- 3 serviços definidos
- Domínios, SSL, recursos, backups e monitoring
- Pronto para importar no Dokploy

### 4. **nginx-docker.conf**
- Reverse proxy otimizado
- Rate limiting (100 req/s para API, 50 req/s para webhooks)
- Multi-tenant routing automático
- Gzip compression
- SSL ready

### 5. **.dockerignore**
- Otimiza build do Docker
- Exclui arquivos desnecessários

---

## Como Usar

### **Opção 1: Docker Compose (Local/Dev)**
```bash
docker-compose up -d
```

Acesso:
- API: http://localhost:5000
- Webhooks: http://localhost:7000
- Nginx: http://localhost:80

---

### **Opção 2: Dokploy (Production)**

1. **Acesse seu Dokploy dashboard**
2. **Importe o projeto**:
   - Git: `https://seu-repo/cyzor_ecossistem.git`
   - Branch: `main`
3. **Carregue a config**: Copie conteúdo de `dokploy.json`
4. **Deploy automático**

---

## Variáveis de Ambiente Reais

```env
# Provisioning
Provisioning__Host=72.60.247.117
Provisioning__User=root
Provisioning__Password=A@ndr0m3d434513754
Provisioning__BuildsBasePath=/var/www/builds
Provisioning__AppsBasePath=/var/www

# Webhook
CYZOR_WEBHOOK_SECRET=cyzor_webhook_secret_2024

# Logging
Logging__LogLevel__Default=Information
```

---

## Portas

| Serviço | Porta |
|---------|-------|
| Provisioning API | 5000 |
| Webhook Receiver | 7000 |
| Nginx HTTP | 80 |
| Nginx HTTPS | 443 |

---

## Domínios

- `api.cyzor.com.br` → Provisioning (porta 5000)
- `webhooks.cyzor.com.br` → Webhook Receiver (porta 7000)
- `*.cyzor.com.br` → Multi-tenant routing (auto-proxyng)

---

## Health Checks

Todos os serviços têm health checks habilitados:

```bash
# API
curl http://localhost:5000/health

# Webhooks
curl http://localhost:7000/health

# Nginx
curl http://localhost:80/health
```

---

## Próximos Passos

1. ✅ Configurar SSL (~Let's Encrypt via Dokploy)
2. ✅ Atualizar SLACK_WEBHOOK_URL em `.env`
3. ✅ Deploy no servidor VPS (72.60.247.117)
4. ✅ Testar multi-tenant routing
5. ✅ Configurar backups automáticos

---

## Troubleshooting

**Porta 80 em uso?**
```bash
sudo lsof -i :80
# Mude EXTERNAL no docker-compose.yml
```

**Container não inicia?**
```bash
docker-compose logs -f cyzor-provisioning
```

**Nginx não está roteando?**
```bash
docker exec cyzor-nginx nginx -t
```

---

## Segurança

⚠️ **IMPORTANTE**: 
- Mude as senhas antes de produção
- Não versione o `.env` (já está no .gitignore)
- Habilite SSL/TLS
- Configure firewall

---

Pronto para deploy! 🚀
