# SafeHome — Software Router v2 : Time's On

> Application web ASP.NET Core MVC de contrôle parental réseau : gestion de profils famille, règles de pare-feu OPNsense, planification horaire et système de récompenses gamifié.

**Auteur :** Lucas Lordon  
**Établissement :** ETML — École Technique des Métiers de Lausanne  
**Contexte :** TPI (Travail Pratique Individuel) — Certification Informaticien CFC OrFo 2021  
**Période :** 27 avril – 28 mai 2026  
**Dépôt partagé avec :** cschafft  
**Dépôt GitHub :** https://github.com/LucasLordon/P-SoftwareRouteur

---

## Table des matières

1. [Présentation du projet](#1-présentation-du-projet)
2. [Stack technique](#2-stack-technique)
3. [Architecture](#3-architecture)
4. [Structure du projet](#4-structure-du-projet)
5. [Prérequis](#5-prérequis)
6. [Installation locale](#6-installation-locale)
7. [Configuration](#7-configuration)
8. [Compilation et build](#8-compilation-et-build)
9. [Lancement](#9-lancement)
10. [Composants principaux](#10-composants-principaux)
11. [API OPNsense — Endpoints consommés](#11-api-opnsense--endpoints-consommés)
12. [Base de données](#12-base-de-données)
13. [Internationalisation](#13-internationalisation)
14. [Données de test](#14-données-de-test)
15. [Déploiement sur infrastructure physique](#15-déploiement-sur-infrastructure-physique)
16. [Maintenance et mise à jour](#16-maintenance-et-mise-à-jour)
17. [Troubleshooting](#17-troubleshooting)

---

## 1. Présentation du projet

SafeHome est une application web de contrôle parental réseau. Elle permet à un administrateur de déclarer des appareils (clients réseau) et de les associer à des profils famille (parent ou enfant). Les parents configurent des règles de pare-feu, des plages horaires de blocage et des défis gamifiés pour leurs enfants. Lorsqu'un enfant réussit un défi, il obtient une récompense — une durée d'accès libre à Internet qui lève temporairement le blocage de ses appareils. Toutes les actions de blocage/autorisation sont appliquées en temps réel sur un pare-feu OPNsense via son API REST.

### Rôles utilisateurs

| Rôle | Accès | Description |
|------|-------|-------------|
| Admin | `/` — tableau de bord, `/admin/profiles`, `/client`, `/firewall` | Compte unique (username/password). Crée et gère les profils, les appareils et les règles de pare-feu globales. |
| Parent | `/parent/*` | Profil famille authentifié par code PIN. Gère les règles du profil enfant, les planifications, les autorisations temporaires et les défis. |
| Enfant | `/child/*` | Profil famille authentifié par code PIN (optionnel). Active ses récompenses, consulte ses défis et ses appareils. |

---

## 2. Stack technique

| Composant | Technologie | Version |
|-----------|-------------|---------|
| Framework | ASP.NET Core MVC | .NET 9.0 |
| Langage | C# | 13 |
| ORM | Entity Framework Core | 9.0.3 |
| Base de données | MySQL (Pomelo) | 9.0.0 (pilote Pomelo) |
| Authentification | ASP.NET Core Cookie Auth | inclus dans .NET 9 |
| Hachage | BCrypt.Net-Next | 4.1.0 |
| SSH/SCP client | Renci.SshNet.Async | 1.4.0 |
| Pare-feu | OPNsense | 26.1.2 |
| Virtualisation | VirtualBox | 7.1.x |
| OS serveur | Ubuntu Server 24.04 | — |
| Processus | systemd | — |

### Dépendances NuGet complètes

```xml
<ItemGroup>
  <PackageReference Include="BCrypt.Net-Next" Version="4.1.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.3" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.3">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.3">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="9.0.0" />
  <PackageReference Include="Renci.SshNet.Async" Version="1.4.0" />
</ItemGroup>
```

---

## 3. Architecture

L'infrastructure repose sur un mini-PC SpeedGoat (Ubuntu Server 24.04) hébergeant deux VMs VirtualBox : une VM OPNsense 26.1.2 assurant le routage NAT, le DHCP (plage 192.168.15.100–200), le DNS et le pare-feu via API REST ; et une VM Ubuntu Server (VM2, IP statique 192.168.15.10) hébergeant l'application ASP.NET Core et la base de données MySQL. Un switch Zyxel GS1920 et un access point TP-Link EAP225 complètent l'infrastructure physique, permettant aux clients WiFi d'accéder au réseau domestique simulé.

### Flux d'une requête

1. **Navigateur → Contrôleur ASP.NET Core** : requête HTTP authentifiée par cookie (Admin ou ProfileCookie).
2. **Contrôleur → EF Core / MySQL** : lecture/écriture de l'état applicatif (profils, règles, récompenses, planifications).
3. **Contrôleur / Service → OPNsenseService** : appel HTTP REST (Basic auth) vers l'API OPNsense pour créer, modifier ou supprimer des alias et des règles de pare-feu.
4. **Services en arrière-plan** : `RewardTimerService` (toutes les 5 s) et `SchedulerService` (toutes les 30 s) réévaluent l'état et poussent les modifications sur OPNsense de manière autonome.

### Services en arrière-plan

| Service | Fréquence | Rôle |
|---------|-----------|------|
| `RewardTimerService` | 5 secondes | Décrémente le compteur des récompenses actives. Bloque les appareils à l'expiration. Débloque les appareils à l'activation ou à la reprise. |
| `SchedulerService` | 30 secondes | Évalue les plages horaires de blocage et les autorisations temporaires de chaque profil enfant. Applique ou lève le blocage sur OPNsense selon l'état calculé. Priorité : récompense active > autorisation temporaire > plage horaire > règle globale. |

---

## 4. Structure du projet

```
SoftwareRouteur/
├── Controllers/          Contrôleurs MVC (9 fichiers)
├── Services/             Services applicatifs et services hébergés
├── Models/               Entités EF Core et view-models
├── Data/                 AppDbContext (configuration EF Core)
├── Migrations/           Historique des migrations EF Core (8 migrations)
├── Views/                Vues Razor organisées par contrôleur
│   ├── AdminProfiles/
│   ├── Auth/
│   ├── Child/
│   ├── Client/
│   ├── Firewall/
│   ├── Home/
│   ├── Parent/
│   ├── Profiles/
│   └── Shared/           Layouts (_Layout, _LayoutParent, _LayoutChild)
├── Resources/            Fichiers .resx (fr / en / de) par contrôleur et par vue
├── wwwroot/              Fichiers statiques
│   ├── css/              Feuilles de style (site.css)
│   ├── js/               Scripts JavaScript (site.js)
│   ├── lib/              Bootstrap, jQuery, validation
│   └── uploads/proofs/   Preuves de défis téléversées par les enfants
├── Properties/           launchSettings.json
├── appsettings.json      Configuration de base (production par défaut)
├── appsettings.Development.json  Surcharge pour développement local
├── Dockerfile            Image Docker multi-étapes (.NET 9 / Linux)
├── compose.yaml          Docker Compose (service unique)
└── SoftwareRouteur.csproj
```

---

## 5. Prérequis

### Poste de développement (Windows)

- .NET SDK 9.0 ou supérieur
- JetBrains Rider ou Visual Studio 2022+
- MySQL Server 8.x (local, port 3307 recommandé en dev pour éviter les conflits)
- VirtualBox 7.1.x avec VM OPNsense accessible
- OpenSSH client (inclus dans Windows 10/11)

### Infrastructure cible (production)

- Mini-PC Ubuntu Server 24.04 (SpeedGoat)
- VirtualBox 7.1.x
- OPNsense 26.1.2 avec API activée
- MySQL Server 8.x opérationnel
- .NET Runtime 9.0
- systemd

---

## 6. Installation locale

### 6.1 Cloner le dépôt

```bash
git clone https://github.com/LucasLordon/P-SoftwareRouteur.git
cd P-SoftwareRouteur/SoftwareRouteur
```

### 6.2 Configurer la variable d'environnement du mot de passe

L'application lit le mot de passe MySQL via la variable d'environnement `DB_PASSWORD` :

```powershell
# PowerShell (Windows)
$env:DB_PASSWORD = "votre_mot_de_passe_mysql"
```

```bash
# Bash (Linux/macOS)
export DB_PASSWORD="votre_mot_de_passe_mysql"
```

### 6.3 Appliquer les migrations

```bash
dotnet ef database update
```

### 6.4 Configurer appsettings.Development.json

Le fichier `appsettings.Development.json` est inclus dans le dépôt. Adapter les valeurs selon votre environnement local :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=3307;Database=db_firewall;Username=[REPLACE];Password=${DB_PASSWORD}"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## 7. Configuration

### Variables d'environnement / appsettings

| Clé | Type | Obligatoire | Description |
|-----|------|-------------|-------------|
| `ConnectionStrings:DefaultConnection` | string | Oui | Chaîne de connexion MySQL. Le mot de passe est injecté via `${DB_PASSWORD}`. |
| `DB_PASSWORD` | env var | Oui | Mot de passe MySQL. Ne jamais écrire en clair dans les fichiers de config versionnés. |
| `OPNsense:BaseUrl` | string | Oui | URL de base de l'API OPNsense (ex. `https://[IP-OPNSENSE]`). |
| `OPNsense:ApiKey` | string | Oui | Clé API OPNsense (Basic auth). |
| `OPNsense:ApiSecret` | string | Oui | Secret API OPNsense (Basic auth). |
| `OPNsense:SkipSslValidation` | bool | Non | Désactiver la validation du certificat SSL OPNsense (auto-signé). Défaut : `true`. |
| `Logging:LogLevel:Default` | string | Non | Niveau de log global. Défaut : `Information`. |
| `Logging:LogLevel:SoftwareRouteur` | string | Non | Niveau de log applicatif. Défaut : `Debug`. |
| `AllowedHosts` | string | Non | Filtre de hosts autorisés. Défaut : `*`. |

### appsettings.Production.json

> ⚠️ Ce fichier contient les credentials de production (API OPNsense, connection string MySQL). Il est **exclu du dépôt** via `.gitignore`. Il doit être créé manuellement sur le serveur cible.

Structure attendue :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=[REPLACE_WITH_MYSQL_HOST];Port=3306;Database=db_firewall;Username=[REPLACE_WITH_USERNAME];Password=${DB_PASSWORD}"
  },
  "OPNsense": {
    "BaseUrl": "https://[REPLACE_WITH_OPNSENSE_IP]",
    "ApiKey": "[REPLACE_WITH_API_KEY]",
    "ApiSecret": "[REPLACE_WITH_API_SECRET]",
    "SkipSslValidation": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "SoftwareRouteur": "Debug"
    }
  },
  "AllowedHosts": "*"
}
```

---

## 8. Compilation et build

### Build debug (développement local)

```bash
cd SoftwareRouteur
dotnet build
```

### Build production (publish)

```bash
dotnet publish -c Release -o ./publish
```

### Migrations EF Core

```bash
# Appliquer les migrations sur la base locale
dotnet ef database update

# Créer une nouvelle migration
dotnet ef migrations add NomDeLaMigration

# Lister les migrations existantes
dotnet ef migrations list
```

> **Important :** La variable d'environnement `DB_PASSWORD` doit être définie avant d'exécuter les commandes EF Core.

### Docker

```bash
# Build de l'image
docker build -t softwarerouteur .

# Lancer via Compose
docker compose up
```

---

## 9. Lancement

### Développement local

```bash
dotnet run --environment Development
```

URL locale :
- HTTP : `http://localhost:5219`
- HTTPS : `https://localhost:7028`

### Production (systemd)

```bash
# Vérifier le statut du service
sudo systemctl status safehome

# Démarrer / arrêter / redémarrer
sudo systemctl start safehome
sudo systemctl stop safehome
sudo systemctl restart safehome

# Voir les logs en temps réel
sudo journalctl -u safehome -f
```

Exemple de fichier de service systemd (`/etc/systemd/system/safehome.service`) :

```ini
[Unit]
Description=SafeHome ASP.NET Core Application
After=network.target mysql.service

[Service]
WorkingDirectory=/home/admin/safehome-app
ExecStart=/usr/bin/dotnet /home/admin/safehome-app/SoftwareRouteur.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=safehome
User=admin
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
EnvironmentFile=/home/admin/.safehome-env

[Install]
WantedBy=multi-user.target
```

---

## 10. Composants principaux

### AdminProfilesController

- **Route base :** `/admin/profiles`
- **Authentification requise :** `[Authorize]` (Admin)
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Index` | GET | `/admin/profiles` | Liste tous les profils avec le nombre d'appareils associés. |
| `Create` | POST | `/admin/profiles/create` | Crée un profil (Parent ou Enfant) avec validation du PIN BCrypt. |
| `Edit` | POST | `/admin/profiles/edit/{id}` | Modifie les informations et le PIN d'un profil. |
| `Delete` | POST | `/admin/profiles/delete/{id}` | Supprime le profil et nettoie les alias/règles OPNsense associés. |
| `AssignDevices` | POST | `/admin/profiles/devices/{profileId}` | Assigne ou désassigne des appareils à un profil. |

---

### AuthController

- **Route base :** `/Auth`
- **Authentification requise :** Aucune
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Login` | GET | `/Auth/Login` | Affiche le formulaire de connexion administrateur. |
| `Login` | POST | `/Auth/Login` | Authentifie l'administrateur (BCrypt, verrouillage après 5 échecs / 15 min). |
| `Logout` | POST | `/Auth/Logout` | Déconnecte les deux cookies (Admin + ProfileCookie). |
| `ProfileLogout` | POST | `/Auth/ProfileLogout` | Déconnecte uniquement le cookie de profil famille. Requiert `ProfileCookie`. |

---

### ChildController

- **Route base :** `/child`
- **Authentification requise :** `[RequireChildProfile]`
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Home` | GET | `/child/home` | Tableau de bord enfant : récompenses actives, appareils, défis disponibles. |
| `Timer` | GET | `/child/home/timer` | Endpoint JSON — état du compte à rebours de la récompense active. |
| `ActivateReward` | POST | `/child/home/activate/{rewardId}` | Démarre une récompense et débloque les appareils sur OPNsense. |
| `PauseReward` | POST | `/child/home/pause/{rewardId}` | Met en pause la récompense et re-bloque les appareils. |
| `ResumeReward` | POST | `/child/home/resume/{rewardId}` | Reprend une récompense en pause. |
| `Challenges` | GET | `/child/challenges` | Liste les défis disponibles et en cours. |
| `ChallengeSubmit` | POST | `/child/challenges/submit/{id}` | Soumet une preuve (photo ≤ 5 Mo, jpg/png/gif/webp ou déclaration). |
| `ChallengePropose` | POST | `/child/challenges/propose` | Propose un nouveau défi au premier parent du profil. |
| `Devices` | GET | `/child/devices` | Affiche les appareils assignés au profil enfant. |

---

### ClientController

- **Route base :** `/Client`
- **Authentification requise :** `[Authorize]` (Admin)
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Index` | GET | `/Client` | Liste les appareils avec pagination (défaut : page 1, 10 par page). |
| `Create` | POST | `/Client/Create` | Crée un appareil, valide l'IP, génère l'alias OPNsense + règles + whitelist. |
| `Edit` | POST | `/Client/Edit` | Modifie le nom d'hôte et l'IP, met à jour la description de l'alias OPNsense. |
| `Delete` | POST | `/Client/Delete` | Supprime l'appareil et retire ses règles/alias OPNsense. |

---

### CultureController

- **Route base :** `/Culture`
- **Authentification requise :** Aucune
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Set` | POST | `/Culture/Set` | Définit le cookie de langue et redirige vers l'URL d'origine. `[IgnoreAntiforgeryToken]`. |

---

### FirewallController

- **Route base :** `/Firewall`
- **Authentification requise :** `[Authorize]` (Admin)
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Index` | GET | `/Firewall` | Liste toutes les règles de pare-feu et les appareils. |
| `Create` | POST | `/Firewall/Create` | Crée une règle (deny/allow) avec détection de conflits au niveau client et profil. |
| `Delete` | POST | `/Firewall/Delete` | Supprime une règle et retire la destination de l'alias OPNsense. |
| `Edit` | POST | `/Firewall/Edit` | Modifie une règle et synchronise les alias OPNsense (ancien et nouvel état). |

---

### HomeController

- **Route base :** `/Home`
- **Authentification requise :** `[Authorize]` (Admin)
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Index` | GET | `/Home` | Tableau de bord admin : état en ligne/hors ligne des appareils, règles actives, trafic bloqué aujourd'hui. |

---

### ParentController

- **Route base :** `/parent`
- **Authentification requise :** `[RequireParentProfile]`
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Dashboard` | GET | `/parent/dashboard` | Vue d'ensemble parent : profils enfants et nombre d'appareils. |
| `Profils` | GET | `/parent/profils` | Liste les profils enfants avec leurs appareils et règles. |
| `AssignProfileDevices` | POST | `/parent/profils/devices/{profileId}` | Assigne/désassigne des appareils à un profil enfant. |
| `Challenges` | GET | `/parent/challenges` | Liste les défis actifs et en attente de validation. |
| `ChallengeCreate` | POST | `/parent/challenges/create` | Crée un défi (global ou limité à un site). |
| `ChallengeEdit` | POST | `/parent/challenges/edit/{id}` | Modifie un défi en attente. |
| `ChallengeDelete` | POST | `/parent/challenges/delete/{id}` | Supprime un défi et les fichiers de preuve associés. |
| `ChallengeApprove` | POST | `/parent/challenges/approve/{id}` | Approuve un défi soumis et crée la récompense. |
| `ChallengeReject` | POST | `/parent/challenges/reject/{id}` | Refuse un défi soumis. |
| `Regles` | GET | `/parent/regles` | Gestion des règles de pare-feu au niveau du profil. |
| `CreateProfileRule` | POST | `/parent/regles/profile/create` | Crée une règle de profil avec détection de conflits. |
| `Schedules` | GET | `/parent/schedules` | Gestion des plages horaires et des autorisations temporaires. |
| `ScheduleCreate` | POST | `/parent/schedules/create` | Crée une plage horaire de blocage. |
| `ScheduleEditGet` | GET | `/parent/schedules/edit/{id}` | Redirige vers la page Schedules (pré-sélection via query). |
| `ScheduleEdit` | POST | `/parent/schedules/edit/{id}` | Modifie une plage horaire existante et réinitialise l'état du planificateur. |
| `ScheduleDelete` | POST | `/parent/schedules/delete/{id}` | Supprime une plage horaire. |
| `TempAuthCreate` | POST | `/parent/schedules/tempauth/create` | Crée une autorisation temporaire (accès global ou limité à un site). |
| `TempAuthRevoke` | POST | `/parent/schedules/tempauth/revoke/{id}` | Révoque une autorisation temporaire. |
| `Security` | GET | `/parent/security` | Page de sécurité du profil parent. |

---

### ProfilesController

- **Route base :** `/profiles`
- **Authentification requise :** Aucune (accès public)
- **Actions principales :**

| Action | Méthode HTTP | Route | Description |
|--------|-------------|-------|-------------|
| `Index` | GET | `/profiles` | Sélection du profil famille. Redirige vers `/Home` si admin authentifié. |
| `Pin` | GET | `/profiles/pin/{id}` | Affiche la saisie du PIN ou redirige si aucun PIN configuré. |
| `Pin` | POST | `/profiles/pin/{id}` | Vérifie le PIN (BCrypt, verrouillage après 5 échecs / 15 min), crée le `ProfileCookie`, redirige vers `/child/home` ou `/parent/dashboard`. |

---

### Services applicatifs

#### OPNsenseService

- **Type :** Singleton
- **Rôle :** Interface unique avec l'API REST OPNsense. Gère la création, modification et suppression d'alias (block, allow, source) et de règles de pare-feu pour les appareils et les profils.
- **Méthodes publiques principales :**
  - `CreateAliasAsync(int clientId, string description)`
  - `AddToAliasAsync(int clientId, string destination)`
  - `RemoveFromAliasAsync(int clientId, string destination)`
  - `ListAliasContentAsync(int clientId)`
  - `DeleteAliasAsync(int clientId)`
  - `FlushAliasAsync(int clientId)`
  - `UpdateAliasDescriptionAsync(string aliasUuid, string description)`
  - `CreateWhitelistAliasAsync(int clientId, string description)`
  - `AddToWhitelistAsync(int clientId, string destination)`
  - `RemoveFromWhitelistAsync(int clientId, string destination)`
  - `DeleteWhitelistAliasAsync(int clientId)`
  - `CreateBlockRuleAsync(int clientId, string clientIp, string description)`
  - `CreateAllowRuleAsync(int clientId, string clientIp, string description)`
  - `DeleteFirewallRuleAsync(string ruleUuid)`
  - `ToggleFirewallRuleAsync(string ruleUuid, bool enabled)`
  - `SetDeviceBlockedAsync(string blockRuleUuid, bool blocked)`
  - `SearchRulesAsync(string searchPhrase)`
  - `ApplyFirewallRulesAsync()`
  - `ReconfigureAliasesAsync()`
  - `CreateProfileAliasesAndRulesAsync(int profileId, string profileName)`
  - `AddDeviceToProfileRulesAsync(string srcAliasUuid, string deviceIp)`
  - `RemoveDeviceFromProfileRulesAsync(string srcAliasUuid, string deviceIp)`
  - `AddToProfileBlocklistAsync(string aliasUuid, string destination)`
  - `RemoveFromProfileBlocklistAsync(string aliasUuid, string destination)`
  - `AddToProfileWhitelistAsync(string aliasUuid, string destination)`
  - `RemoveFromProfileWhitelistAsync(string aliasUuid, string destination)`
  - `DeleteProfileAliasesAndRulesAsync(int profileId, ...)`
- **Dépendances injectées :** `IOptions<OPNsenseSettings>`, `ILogger<OPNsenseService>`

---

#### RewardTimerService

- **Type :** Hosted Service (BackgroundService)
- **Rôle :** Décrémente les compteurs de récompenses actives toutes les 5 secondes. Débloque les appareils sur OPNsense à l'activation d'une récompense, les re-bloque à l'expiration. Reprend les récompenses actives au démarrage du service.
- **Méthodes publiques principales :**
  - `ExecuteAsync(CancellationToken)` — point d'entrée du service hébergé
- **Dépendances injectées :** `IServiceScopeFactory`, `OPNsenseService`, `ILogger<RewardTimerService>`

---

#### SchedulerService

- **Type :** Singleton + Hosted Service
- **Rôle :** Évalue toutes les 30 secondes les plages horaires de blocage et les autorisations temporaires pour chaque profil enfant. Applique ou lève le blocage (global ou limité à un site) sur OPNsense via les alias de profil. Expose `ResetProfileState(int profileId)` pour forcer une réévaluation immédiate après une modification.
- **Méthodes publiques principales :**
  - `ExecuteAsync(CancellationToken)` — point d'entrée du service hébergé
  - `ResetProfileState(int profileId)` — invalide le cache d'état d'un profil
- **Dépendances injectées :** `IServiceScopeFactory`, `OPNsenseService`, `ILogger<SchedulerService>`

---

#### RewardOPNsenseHelper

- **Type :** Classe statique utilitaire
- **Rôle :** Applique ou annule le blocage OPNsense pour tous les appareils d'un profil lors des transitions de récompense. Gère les récompenses globales (toggle des règles d'appareils) et les récompenses limitées à un site (ajout/retrait de la whitelist de profil).
- **Méthodes publiques principales :**
  - `SetProfileDevicesBlockedAsync(AppDbContext, OPNsenseService, ILogger, Reward, bool blocked)`

---

## 11. API OPNsense — Endpoints consommés

| # | Méthode | Endpoint OPNsense | Description |
|---|---------|-------------------|-------------|
| 1 | POST | `/api/firewall/alias/addItem` | Créer un alias (block, allow, source, profil) |
| 2 | POST | `/api/firewall/alias/setItem/{uuid}` | Mettre à jour le contenu d'un alias (ajouter / retirer des entrées) |
| 3 | GET | `/api/firewall/alias/getItem/{uuid}` | Lire le contenu actuel d'un alias |
| 4 | GET | `/api/firewall/alias_util/list/{aliasName}` | Lister les entrées résolues d'un alias |
| 5 | POST | `/api/firewall/alias/delItem/{uuid}` | Supprimer un alias |
| 6 | POST | `/api/firewall/alias_util/flush/{aliasName}` | Vider entièrement un alias |
| 7 | GET | `/api/firewall/alias/getAliasUUID/{name}` | Obtenir l'UUID d'un alias par son nom |
| 8 | POST | `/api/firewall/filter/addRule` | Créer une règle de pare-feu (block ou pass) |
| 9 | POST | `/api/firewall/filter/delRule/{ruleUuid}` | Supprimer une règle de pare-feu |
| 10 | POST | `/api/firewall/filter/toggleRule/{ruleUuid}/{0\|1}` | Activer ou désactiver une règle (blocage/déblocage appareil) |
| 11 | GET | `/api/firewall/filter/searchRule?current=1&rowCount=-1&searchPhrase={q}` | Rechercher des règles par phrase |
| 12 | POST | `/api/firewall/filter/apply` | Appliquer les modifications de règles |
| 13 | POST | `/api/firewall/alias/reconfigure` | Reconfigurer tous les alias |

**Configuration de l'authentification OPNsense :**

- Type : HTTP Basic Auth (ApiKey : ApiSecret)
- Credentials injectés via `appsettings.Production.json` (clés `OPNsense:ApiKey` et `OPNsense:ApiSecret`)
- Validation SSL : désactivée (`SkipSslValidation: true`) — certificat auto-signé sur réseau interne

---

## 12. Base de données

### Schéma — Tables

| Table EF Core | Entité C# | Description |
|---------------|-----------|-------------|
| `Clients` | `Client` | Appareils réseau (hostname, IP, alias OPNsense UUIDs) |
| `FirewallRules` | `FirewallRule` | Règles de pare-feu au niveau appareil |
| `ProfileFirewallRules` | `ProfileFirewallRule` | Règles de pare-feu au niveau profil enfant |
| `AdminUsers` | `AdminUser` | Compte(s) administrateur (username, hash BCrypt, lockout) |
| `Monitorings` | `Monitoring` | Données de supervision réseau |
| `BlockedTraffics` | `BlockedTraffic` | Historique du trafic bloqué |
| `Profiles` | `Profile` | Profils famille (Admin, Parent, Enfant — PIN BCrypt, alias OPNsense UUIDs) |
| `Challenges` | `Challenge` | Défis gamifiés (titre, durée récompense, portée global/site) |
| `ChallengeProofs` | `ChallengeProof` | Preuves soumises par les enfants (photo ou déclaration) |
| `Rewards` | `Reward` | Récompenses actives/consommées (compteur en secondes, état) |
| `Schedules` | `Schedule` | Plages horaires de blocage (jours, heures, portée, destination) |
| `TempAuthorizations` | `TempAuthorization` | Autorisations temporaires (durée, portée, destination, expiration) |

### Historique des migrations

| # | Nom de la migration | Description |
|---|--------------------|--------------------|
| 1 | `20260212125704_Initial` | Schéma initial (clients, règles, utilisateurs admin, monitoring) |
| 2 | `20260430123655_AddProfilesAndPinSupport` | Profils famille et authentification PIN |
| 3 | `20260506072220_AddClientWhitelistUuids` | UUIDs de la whitelist OPNsense par appareil |
| 4 | `20260507000000_AddGamificationTables` | Défis, preuves et récompenses (système gamifié) |
| 5 | `20260511104016_AddProfileFirewallRules` | Règles de pare-feu au niveau profil |
| 6 | `20260513134349_AddSchedulesAndTempAuthorizations` | Plages horaires et autorisations temporaires |
| 7 | `20260518065348_AddScheduleBlockDestination` | Destination de blocage spécifique par plage horaire |
| 8 | `20260518111722_AddTempAuthAllowDestination` | Destination d'autorisation spécifique pour les TempAuths |

### Connexion locale (développement)

```
Host=127.0.0.1
Port=3307
Database=db_firewall
Username=[REPLACE_WITH_USERNAME]
Password=[via variable d'environnement DB_PASSWORD]
```

---

## 13. Internationalisation

**Langues supportées :** Français (`fr`, défaut), Anglais (`en`), Allemand (`de`)

La langue est sélectionnable via un menu déroulant dans l'en-tête. Le choix est persisté dans un cookie de culture. Le changement de langue appelle `POST /Culture/Set`.

**Fichiers de ressources :**

```
Resources/
├── Controllers/
│   ├── AdminProfilesController.{fr,en,de}.resx
│   ├── AuthController.{fr,en,de}.resx
│   ├── ChildController.{fr,en,de}.resx
│   ├── ClientController.{fr,en,de}.resx
│   ├── FirewallController.{fr,en,de}.resx
│   ├── ParentController.{fr,en,de}.resx
│   └── ProfilesController.{fr,en,de}.resx
└── Views/
    ├── AdminProfiles/Index.{fr,en,de}.resx
    ├── Auth/Login.{fr,en,de}.resx
    ├── Child/Challenges.{fr,en,de}.resx
    ├── Child/Devices.{fr,en,de}.resx
    ├── Child/Home.{fr,en,de}.resx
    ├── Client/Index.{fr,en,de}.resx
    ├── Firewall/Index.{fr,en,de}.resx
    ├── Home/Index.{fr,en,de}.resx
    ├── Parent/Challenges.{fr,en,de}.resx
    ├── Parent/Dashboard.{fr,en,de}.resx
    ├── Parent/Profils.{fr,en,de}.resx
    ├── Parent/Regles.{fr,en,de}.resx
    ├── Parent/Schedules.{fr,en,de}.resx
    ├── Parent/Security.{fr,en,de}.resx
    ├── Profiles/Index.{fr,en,de}.resx
    ├── Profiles/Pin.{fr,en,de}.resx
    ├── Shared/_Layout.{fr,en,de}.resx
    ├── Shared/_LayoutChild.{fr,en,de}.resx
    └── Shared/_LayoutParent.{fr,en,de}.resx
```

---

## 14. Données de test

**Compte administrateur :**

> Les mots de passe sont hachés avec BCrypt.Net-Next (`$2b$`, work factor 12).  
> Le compte admin initial est créé directement en base de données (aucun seeder automatique présent dans le code).

```sql
-- Exemple d'insertion manuelle d'un compte admin (à adapter)
INSERT INTO AdminUsers (Username, PasswordHash, FailedAttempts, LockoutEnd)
VALUES ('admin', '[HASH_BCRYPT]', 0, NULL);
```

**Dump de base de données :**

Un fichier `safehome-dump.sql` existe sur le poste de développement (hors dépôt). Il contient les données de test complètes. À importer manuellement en environnement de développement :

```bash
mysql -u [username] -p db_firewall < safehome-dump.sql
```

**Profils de test disponibles dans le dump :**

| Profil | Rôle | PIN |
|--------|------|-----|
| Marie Dubois | Parent | Activé |
| Lucas | Enfant | Optionnel |
| Emma | Enfant | Optionnel |
| Noah | Enfant | Optionnel |

**Appareils de test :**

| Hostname | IP |
|----------|----|
| iPhone-Lucas | 192.168.15.101 |
| PC-Bureau | 192.168.15.102 |
| iPad-Salon | 192.168.15.103 |

---

## 15. Déploiement sur infrastructure physique

> ⚠️ **Prérequis :** être connecté au VPN SafeHome via OpenVPN Connect (`62.2.127.6`) depuis une connexion hors réseau ETML (partage iPhone recommandé). Voir la documentation projet §2.1 pour la procédure VPN complète.

### Configuration SSH (fichier `~/.ssh/config` sur le poste développeur)

```
Host speedgoat
    HostName 172.21.14.164
    User adminsys

Host sg-opnsense
    HostName 192.168.56.2
    User root
    ProxyJump speedgoat

Host sg-vm2
    HostName 192.168.15.10
    User admin
    ProxyJump sg-opnsense
```

### Procédure de déploiement

**Étape 1 — Compiler et publier (poste développeur)**

```bash
cd SoftwareRouteur
dotnet publish -c Release -o ./publish
```

**Étape 2 — Transférer vers VM2 via SCP**

```bash
scp -r ./publish/* sg-vm2:/home/admin/safehome-app/
```

**Étape 3 — Vérifier/créer appsettings.Production.json sur VM2**

```bash
ssh sg-vm2 "nano /home/admin/safehome-app/appsettings.Production.json"
# S'assurer que ApiKey, ApiSecret, ConnectionString et DB_PASSWORD sont corrects
```

**Étape 4 — Redémarrer le service**

```bash
ssh sg-vm2 "sudo systemctl restart safehome"
```

**Étape 5 — Vérifier**

```bash
ssh sg-vm2 "sudo systemctl status safehome"
ssh sg-vm2 "sudo journalctl -u safehome -f --since '1 min ago'"
```

### Vérification du démarrage des VMs (sur le SpeedGoat)

```bash
ssh speedgoat "VBoxManage list runningvms"
# Résultat attendu :
# "OPNsense" {762a3f83-56ac-4552-a1a7-67c1b90113e3}
# "ubuntu-server-vm2" {3a193e58-afb4-453d-affb-6caf0ebb12b0}
```

### Prérequis côté serveur (VM2)

- .NET Runtime 9.0 installé
- MySQL Server 8.x opérationnel et base `db_firewall` créée
- Variable d'environnement `DB_PASSWORD` définie (via `EnvironmentFile` systemd)
- Service systemd `safehome` configuré (voir §9)
- `appsettings.Production.json` présent dans `/home/admin/safehome-app/` avec les credentials corrects
- Accès réseau de VM2 vers OPNsense sur `192.168.15.1:443`

---

## 16. Maintenance et mise à jour

### Appliquer une mise à jour applicative

```bash
# 1. Sur le poste de développement
dotnet publish -c Release -o ./publish

# 2. Transférer les fichiers
scp -r ./publish/* sg-vm2:/home/admin/safehome-app/

# 3. Sur VM2 — redémarrer le service
ssh sg-vm2 "sudo systemctl restart safehome"

# 4. Vérifier
ssh sg-vm2 "sudo systemctl status safehome"
ssh sg-vm2 "sudo journalctl -u safehome -f --since '1 min ago'"
```

### Appliquer une migration de base de données

```bash
# Option A — via EF Core Tools sur la VM (si SDK installé)
dotnet ef database update

# Option B — exporter le script SQL et l'appliquer manuellement
dotnet ef migrations script [DerniereMigration] [NouvelleMigration] -o migration.sql
mysql -u [username] -p db_firewall < migration.sql
```

### Sauvegarder la base de données

```bash
mysqldump -u [username] -p db_firewall > backup_$(date +%Y%m%d_%H%M%S).sql
```

### Ajouter une langue

1. Dupliquer tous les fichiers `.resx` existants en `.{code_langue}.resx`
2. Ajouter la culture dans `Program.cs` : `supportedCultures` et `RequestLocalizationOptions`
3. Ajouter l'option dans la `<select>` de sélection de langue des layouts (`_Layout.cshtml`, `_LayoutParent.cshtml`, `_LayoutChild.cshtml`)

---

## 17. Troubleshooting

### Le service ne démarre pas

```bash
sudo journalctl -u safehome -n 100 --no-pager
```

Causes fréquentes :
- `DB_PASSWORD` non défini → `FormatException` ou `MySqlException` au démarrage
- `appsettings.Production.json` absent → connexion MySQL et OPNsense avec valeurs vides
- Port 5219/7028 déjà utilisé → changer le port dans `appsettings.Production.json`
- Permissions insuffisantes sur `wwwroot/uploads/proofs/` → les téléversements de preuves échouent silencieusement

### Les appels OPNsense échouent silencieusement

```bash
sudo journalctl -u safehome -f | grep -i "opnsense\|warn\|error\|401\|timeout"
```

Causes fréquentes :
- Certificat SSL auto-signé non ignoré → vérifier `OPNsense:SkipSslValidation: true`
- Credentials incorrects → code `401` dans les logs, vérifier `ApiKey` et `ApiSecret`
- VM OPNsense non démarrée → timeout de connexion, vérifier avec `VBoxManage list runningvms`
- UUID d'alias null sur le profil → un profil créé avant la migration `AddProfileFirewallRules` peut avoir des UUIDs manquants ; supprimer et recréer le profil

### Les migrations EF Core échouent

```bash
# Vérifier que MySQL est démarré
sudo systemctl status mysql

# Appliquer avec logs verbeux
dotnet ef database update --verbose

# Vérifier la variable de mot de passe
echo $DB_PASSWORD
```

### Le planificateur ne bloque/débloque pas les appareils

- Vérifier que `SchedulerService` est enregistré comme **Singleton** ET **HostedService** dans `Program.cs`
- Vérifier les logs : `sudo journalctl -u safehome -f | grep -i "scheduler\|profile"`
- Forcer une réévaluation en modifiant puis sauvegardant une plage horaire (appelle `ResetProfileState`)

### Le compte est verrouillé (admin ou profil)

Le verrouillage dure 15 minutes après 5 échecs consécutifs. Pour débloquer manuellement :

```sql
-- Admin
UPDATE AdminUsers SET FailedAttempts = 0, LockoutEnd = NULL WHERE Username = '[username]';

-- Profil famille
UPDATE Profiles SET FailedAttempts = 0, LockoutEnd = NULL WHERE Id = [profileId];
```

### Les récompenses ne se débloquent pas à l'activation

- Vérifier que `RewardTimerService` est en cours d'exécution : `sudo journalctl -u safehome | grep -i "reward"`
- Vérifier que le profil enfant possède un `OpnsenseBlockAliasUuid` valide (non null)
- Si la récompense est de portée `site`, vérifier que `OpnsenseAllowAliasUuid` est valide

### Accès RDP d'urgence aux VMs (si SSH indisponible)

En cas d'impossibilité d'accéder aux VMs via SSH, activer temporairement le VRDE (RDP VirtualBox) :

```bash
# Depuis la connexion SSH au SpeedGoat
VBoxManage controlvm "ubuntu-server-vm2" vrde on
VBoxManage controlvm "ubuntu-server-vm2" vrdeport 5001

VBoxManage controlvm "OPNsense" vrde on
VBoxManage controlvm "OPNsense" vrdeport 5000
```

Puis depuis le poste développeur, ouvrir un tunnel SSH et se connecter en RDP :

```bash
# Terminal 1 — Tunnel SSH
ssh -L 5001:localhost:5001 speedgoat

# Terminal 2 (Win + R)
mstsc /v:localhost:5001
```

Désactiver le VRDE une fois le problème SSH résolu.

---

*Documentation générée le 27 mai 2026 — TPI Software Router 2 : Time's On — Lucas Lordon — ETML 2026*
