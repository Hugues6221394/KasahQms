# KASAH QMS Deployment Guide

## Prerequisites

Before deploying the KASAH QMS system, ensure you have:

- [ ] **Server/Hosting Provider** (Linux recommended - Ubuntu 20.04 LTS or later)
- [ ] **Domain Name** (e.g., qms.yourcompany.com)
- [ ] **SSL Certificate** (Let's Encrypt recommended)
- [ ] **PostgreSQL Database** (Version 13 or later)
- [ ] **SMTP Email Server** credentials (for notifications)
- [ ] **.NET 8 Runtime** installed on server
- [ ] **Nginx** or **Apache** for reverse proxy

---

## Deployment Options

### Option A: Deploy to Linux Server (Recommended)
### Option B: Deploy to Azure App Service
### Option C: Deploy to AWS
### Option D: Deploy to Docker Container

---

## Option A: Deploy to Linux Server (Recommended)

### Step 1: Prepare Your Server

```bash
# Update system packages
sudo apt update && sudo apt upgrade -y

# Install .NET 8 Runtime
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0 --runtime aspnetcore

# Install PostgreSQL
sudo apt install postgresql postgresql-contrib -y

# Install Nginx
sudo apt install nginx -y
```

### Step 2: Setup PostgreSQL Database

```bash
# Switch to postgres user
sudo -u postgres psql

# Create database and user
CREATE DATABASE kasahqms;
CREATE USER qmsadmin WITH ENCRYPTED PASSWORD 'your-secure-password';
GRANT ALL PRIVILEGES ON DATABASE kasahqms TO qmsadmin;
\q
```

### Step 3: Configure Database Connection

On your **local machine**, update the connection string:

**File:** `src/Presentation/KasahQMS.Web/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-server-ip;Database=kasahqms;Username=qmsadmin;Password=your-secure-password;Port=5432"
  }
}
```

### Step 4: Configure Email Service

Update email settings in `appsettings.json`:

```json
{
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@company.com",
    "SmtpPassword": "your-app-specific-password",
    "FromEmail": "noreply@company.com",
    "FromName": "KASAH QMS"
  }
}
```

**Note for Gmail:** 
- Use App Passwords (not your regular password)
- Enable 2FA, then generate App Password at: https://myaccount.google.com/apppasswords

### Step 5: Build and Publish the Application

On your **local machine**:

```bash
cd "src"

# Build in Release mode
dotnet build -c Release

# Publish the application
dotnet publish Presentation/KasahQMS.Web/KasahQMS.Web.csproj -c Release -o ./publish

# Create a zip file for transfer
tar -czf kasahqms.tar.gz -C ./publish .
```

### Step 6: Transfer Files to Server

```bash
# From your local machine
scp kasahqms.tar.gz username@your-server-ip:/var/www/

# On the server
ssh username@your-server-ip
cd /var/www
sudo tar -xzf kasahqms.tar.gz -C /var/www/kasahqms
sudo chown -R www-data:www-data /var/www/kasahqms
```

### Step 7: Create Systemd Service

Create service file:

```bash
sudo nano /etc/systemd/system/kasahqms.service
```

Add the following content:

```ini
[Unit]
Description=KASAH QMS Application
After=network.target

[Service]
WorkingDirectory=/var/www/kasahqms
ExecStart=/home/username/.dotnet/dotnet /var/www/kasahqms/KasahQMS.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=kasahqms
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Enable and start the service:

```bash
sudo systemctl enable kasahqms
sudo systemctl start kasahqms
sudo systemctl status kasahqms
```

### Step 8: Configure Nginx Reverse Proxy

```bash
sudo nano /etc/nginx/sites-available/kasahqms
```

Add configuration:

```nginx
server {
    listen 80;
    server_name qms.yourcompany.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # WebSocket support for SignalR
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 86400;
    }

    client_max_body_size 50M;
}
```

Enable the site:

```bash
sudo ln -s /etc/nginx/sites-available/kasahqms /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### Step 9: Install SSL Certificate (Let's Encrypt)

```bash
# Install Certbot
sudo apt install certbot python3-certbot-nginx -y

# Get SSL certificate
sudo certbot --nginx -d qms.yourcompany.com

# Auto-renewal is configured automatically
# Test renewal
sudo certbot renew --dry-run
```

### Step 10: Run Database Migrations

```bash
cd /var/www/kasahqms

# Apply migrations
dotnet KasahQMS.Infrastructure.Persistence.dll --migrate

# Or run from the DLL
dotnet ef database update --connection "Host=localhost;Database=kasahqms;Username=qmsadmin;Password=your-password"
```

### Step 11: Seed Initial Data

The system will automatically seed:
- Default tenant
- System admin user (check `SEEDED_TEST_USERS.md` for credentials)
- Default roles and permissions

**Default Admin Credentials:**
- Username: `admin`
- Password: Check the `SEEDED_TEST_USERS.md` file or set in seed data

⚠️ **IMPORTANT:** Change admin password immediately after first login!

### Step 12: Configure Firewall

```bash
# Allow HTTP and HTTPS
sudo ufw allow 'Nginx Full'

# Allow SSH
sudo ufw allow OpenSSH

# Enable firewall
sudo ufw enable
```

### Step 13: Verify Deployment

1. Visit: `https://qms.yourcompany.com`
2. Login with admin credentials
3. Check:
   - [ ] Login works
   - [ ] Dashboard loads
   - [ ] Database connection is working
   - [ ] Email notifications work (test forgot password)
   - [ ] File uploads work

---

## Option B: Deploy to Azure App Service

### Step 1: Create Azure Resources

```bash
# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Login to Azure
az login

# Create resource group
az group create --name KasahQMS --location eastus

# Create PostgreSQL server
az postgres flexible-server create \
  --resource-group KasahQMS \
  --name kasahqms-db \
  --location eastus \
  --admin-user qmsadmin \
  --admin-password "Your-Secure-Password-123!" \
  --sku-name Standard_B1ms \
  --version 13

# Create database
az postgres flexible-server db create \
  --resource-group KasahQMS \
  --server-name kasahqms-db \
  --database-name kasahqms

# Create App Service Plan
az appservice plan create \
  --name KasahQMSPlan \
  --resource-group KasahQMS \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --resource-group KasahQMS \
  --plan KasahQMSPlan \
  --name kasahqms-app \
  --runtime "DOTNETCORE|8.0"
```

### Step 2: Configure App Settings

```bash
# Set connection string
az webapp config connection-string set \
  --resource-group KasahQMS \
  --name kasahqms-app \
  --settings DefaultConnection="Host=kasahqms-db.postgres.database.azure.com;Database=kasahqms;Username=qmsadmin;Password=Your-Secure-Password-123!;SSL Mode=Require" \
  --connection-string-type PostgreSQL

# Set app settings
az webapp config appsettings set \
  --resource-group KasahQMS \
  --name kasahqms-app \
  --settings ASPNETCORE_ENVIRONMENT=Production
```

### Step 3: Deploy Application

```bash
# Build and publish
cd src
dotnet publish Presentation/KasahQMS.Web/KasahQMS.Web.csproj -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../kasahqms.zip .

# Deploy to Azure
az webapp deployment source config-zip \
  --resource-group KasahQMS \
  --name kasahqms-app \
  --src ../kasahqms.zip
```

### Step 4: Configure Custom Domain and SSL

```bash
# Add custom domain
az webapp config hostname add \
  --resource-group KasahQMS \
  --webapp-name kasahqms-app \
  --hostname qms.yourcompany.com

# Enable HTTPS
az webapp update \
  --resource-group KasahQMS \
  --name kasahqms-app \
  --https-only true
```

---

## Option C: Deploy with Docker

### Step 1: Create Dockerfile

Already exists at: `src/Dockerfile`

### Step 2: Create docker-compose.yml

Already exists at: `src/docker-compose.yml`

### Step 3: Build and Run

```bash
cd src

# Build images
docker-compose build

# Start services
docker-compose up -d

# Check logs
docker-compose logs -f web

# Stop services
docker-compose down
```

### Step 4: Access Application

- Application: http://localhost:5000
- Database: localhost:5432

---

## Post-Deployment Checklist

### Security

- [ ] Change default admin password
- [ ] Enable HTTPS/SSL
- [ ] Configure firewall rules
- [ ] Set up regular backups
- [ ] Review and update CORS settings
- [ ] Enable rate limiting
- [ ] Configure CSP headers

### Database

- [ ] Create database backups schedule
- [ ] Optimize database settings
- [ ] Set up monitoring
- [ ] Create read replicas (if needed)

### Application

- [ ] Test all major features
- [ ] Verify email notifications
- [ ] Test file uploads
- [ ] Check audit logs
- [ ] Verify user roles and permissions
- [ ] Test on multiple browsers

### Monitoring

- [ ] Set up application logging
- [ ] Configure error tracking (e.g., Sentry)
- [ ] Set up uptime monitoring
- [ ] Configure performance monitoring
- [ ] Set up alerts for errors

### Backup Strategy

```bash
# PostgreSQL backup script
#!/bin/bash
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/var/backups/kasahqms"
DB_NAME="kasahqms"
DB_USER="qmsadmin"

# Create backup directory
mkdir -p $BACKUP_DIR

# Backup database
pg_dump -U $DB_USER $DB_NAME > $BACKUP_DIR/kasahqms_$DATE.sql

# Compress backup
gzip $BACKUP_DIR/kasahqms_$DATE.sql

# Delete backups older than 30 days
find $BACKUP_DIR -name "*.sql.gz" -mtime +30 -delete

echo "Backup completed: kasahqms_$DATE.sql.gz"
```

Add to crontab for daily backups:
```bash
crontab -e
# Add line:
0 2 * * * /path/to/backup-script.sh
```

---

## Troubleshooting

### Application won't start

```bash
# Check service status
sudo systemctl status kasahqms

# Check logs
sudo journalctl -u kasahqms -n 100 --no-pager

# Check .NET runtime
dotnet --info
```

### Database connection fails

```bash
# Test PostgreSQL connection
psql -h localhost -U qmsadmin -d kasahqms

# Check PostgreSQL is running
sudo systemctl status postgresql

# Check connection string in appsettings.json
```

### Email notifications not working

- Verify SMTP credentials
- Check firewall allows SMTP port (587/465)
- Test with a simple email tool
- Check application logs for errors

### 502 Bad Gateway (Nginx)

```bash
# Check if application is running
sudo systemctl status kasahqms

# Check Nginx error logs
sudo tail -f /var/log/nginx/error.log

# Verify proxy_pass URL in Nginx config
```

---

## Updating the Application

### Deploy New Version

```bash
# On local machine - build new version
cd src
dotnet publish Presentation/KasahQMS.Web/KasahQMS.Web.csproj -c Release -o ./publish
tar -czf kasahqms-update.tar.gz -C ./publish .

# Transfer to server
scp kasahqms-update.tar.gz username@server:/tmp/

# On server
sudo systemctl stop kasahqms
cd /var/www/kasahqms
sudo tar -xzf /tmp/kasahqms-update.tar.gz -C /var/www/kasahqms
sudo chown -R www-data:www-data /var/www/kasahqms
sudo systemctl start kasahqms
```

---

## Maintenance

### Database Maintenance

```bash
# Vacuum database
sudo -u postgres vacuumdb -d kasahqms -z -v

# Reindex
sudo -u postgres reindexdb -d kasahqms
```

### Log Rotation

```bash
# Create logrotate config
sudo nano /etc/logrotate.d/kasahqms
```

```
/var/log/kasahqms/*.log {
    daily
    missingok
    rotate 14
    compress
    delaycompress
    notifempty
    create 0640 www-data www-data
}
```

---

## Support and Documentation

- **Application Logs:** `/var/log/kasahqms/`
- **Nginx Logs:** `/var/log/nginx/`
- **Database Logs:** `/var/log/postgresql/`

For issues, check:
1. Application logs first
2. Database connection
3. Nginx/reverse proxy configuration
4. Firewall settings
5. SSL certificate validity

---

## Production Environment Variables

Create a production `appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "qms.yourcompany.com",
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-db-server;Database=kasahqms;Username=qmsadmin;Password=your-password"
  },
  "EmailSettings": {
    "SmtpHost": "smtp.yourprovider.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email",
    "SmtpPassword": "your-password",
    "FromEmail": "noreply@yourcompany.com",
    "FromName": "KASAH QMS"
  },
  "Security": {
    "RequireHttps": true,
    "UseHsts": true
  }
}
```

---

## Scaling Considerations

### For High Traffic:

1. **Database:** 
   - Use connection pooling
   - Add read replicas
   - Consider Azure PostgreSQL or AWS RDS

2. **Application:**
   - Deploy multiple instances
   - Use load balancer (Azure Load Balancer, AWS ELB, or Nginx)
   - Enable distributed caching (Redis)

3. **Static Files:**
   - Use CDN for static assets
   - Enable Nginx/Apache caching
   - Use Azure Blob Storage or AWS S3 for uploads

---

## Cost Estimate (Monthly)

### Small Business (< 50 users):
- **Option 1 - VPS:** $20-50/month (DigitalOcean, Linode)
- **Option 2 - Azure:** $50-100/month (Basic tier)
- **Domain + SSL:** Free (Let's Encrypt)

### Medium Business (50-200 users):
- **VPS/Cloud:** $100-200/month
- **Database:** Included or $30-50/month
- **Backups:** $10-20/month

### Enterprise (200+ users):
- **Custom pricing** based on requirements
- Consider dedicated servers or premium cloud tiers

---

## Success! 🎉

Your KASAH QMS system should now be deployed and accessible at your domain!

**Next Steps:**
1. Login and change admin password
2. Configure company settings
3. Add users and departments
4. Import initial documents/templates
5. Train users on the system

**Need Help?**
- Check application logs
- Review this guide
- Contact support

---

**Document Version:** 1.0  
**Last Updated:** March 2026  
**Platform:** KASAH QMS v1.0
