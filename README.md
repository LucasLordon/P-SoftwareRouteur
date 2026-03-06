# SafeHome — Software Router

Routeur logiciel avec filtrage Internet pour réseau domestique, développé dans le cadre du TPI à l'ETML (février–mars 2026).

## Présentation

SafeHome permet à un parent ou administrateur de bloquer l'accès à certains sites web (par domaine, IP ou CIDR) pour des appareils spécifiques du réseau domestique, via une interface web accessible depuis un navigateur.

## Architecture

L'infrastructure repose sur 3 machines virtuelles VirtualBox :

| VM | OS | Rôle |
|---|---|---|
| VM1 | Ubuntu Server 24.04 | DHCP, DNS, DDNS (isc-dhcp-server, BIND9) |
| VM2 | Ubuntu Server 24.04 | Routeur NAT (iptables), MySQL, webapp ASP.NET Core, scripts Python |
| Client-Test | Ubuntu Desktop 22.04 | Poste client de test |

Réseau interne : `192.168.15.0/24` via VirtualBox Internal Network.

## Technologies

- **Backend** : ASP.NET Core 8.0 MVC, Entity Framework Core, Pomelo MySQL
- **Base de données** : MySQL 8.0 (6 tables)
- **Authentification** : BCrypt (BCrypt.Net-Next)
- **Scripts** : Python 3 (pymysql, subprocess)
- **Réseau** : iptables, isc-dhcp-server, BIND9, Netplan

## Structure du projet

```
├── Controllers/
│   ├── AuthController.cs        # Login / Logout (BCrypt)
│   ├── HomeController.cs        # Dashboard
│   ├── ClientController.cs      # CRUD clients
│   └── FirewallController.cs    # CRUD règles de filtrage
├── Models/
│   ├── Client.cs
│   ├── FirewallRule.cs
│   ├── AdminUser.cs
│   ├── Monitoring.cs
│   └── BlockedTraffic.cs
├── Views/
│   ├── Auth/Login.cshtml
│   ├── Home/Index.cshtml
│   ├── Client/Index.cshtml
│   ├── Firewall/Index.cshtml
│   └── Shared/_Layout.cshtml
├── Data/
│   └── AppDbContext.cs
├── wwwroot/css/
│   ├── firewall.css
│   └── dashboard.css
├── Program.cs
├── appsettings.json
└── scripts/                     # Sur VM2, hors du projet .NET
    ├── sync_firewall.py         # Synchronisation iptables ↔ MySQL (cron */5)
    └── monitor.py               # Monitoring clients + trafic bloqué (cron */2)
```

## Déploiement rapide

Le manuel d'installation complet est disponible dans le rapport TPI. En résumé :

```bash
# VM2 — Installer le runtime .NET
sudo apt install aspnet-runtime-8.0 -y

# Publier et copier l'application
dotnet publish -c Release -o ./publish
scp -r ./publish admin@<IP-VM2>:/home/admin/safehome-app

# Activer le service
sudo systemctl enable safehome
sudo systemctl start safehome
```

L'application est accessible sur `http://192.168.15.1:5000` (réseau interne) ou `http://<IP-bridge-VM2>:5000` (PC hôte).

## Auteur

Lucas Lordon — ETML, TPI 2026
