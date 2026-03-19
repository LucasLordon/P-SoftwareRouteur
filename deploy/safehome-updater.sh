#!/bin/bash
set -e

# === CONFIGURATION ===
GITHUB_REPO="LucasLordon/P-APRPO-SoftwareRouteur"   # <-- ADAPTER
VM2_USER="admin"
VM2_IP="192.168.15.1"
LOCAL_DIR="/home/adminsys/safehome"
BACKUP_DIR="/home/adminsys/safehome-backup"
LOG_FILE="/var/log/safehome-updater.log"
VERSION_FILE="$LOCAL_DIR/version.json"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# === VÉRIFIER LA DERNIÈRE VERSION SUR GITHUB ===
log "=== Vérification des mises à jour ==="

LATEST_RELEASE=$(curl -sf "https://api.github.com/repos/$GITHUB_REPO/releases/latest" 2>/dev/null)

if [ -z "$LATEST_RELEASE" ]; then
    log "ERREUR: Impossible de contacter GitHub. Pas de connexion Internet ?"
    exit 1
fi

LATEST_TAG=$(echo "$LATEST_RELEASE" | grep -o '"tag_name": "[^"]*"' | head -1 | cut -d'"' -f4)
LATEST_VERSION=${LATEST_TAG#v}
DOWNLOAD_URL=$(echo "$LATEST_RELEASE" | grep -o '"browser_download_url": "[^"]*\.tar\.gz"' | head -1 | cut -d'"' -f4)

if [ -z "$LATEST_TAG" ] || [ -z "$DOWNLOAD_URL" ]; then
    log "ERREUR: Pas de release trouvée sur GitHub"
    exit 1
fi

# === COMPARER AVEC LA VERSION LOCALE ===
CURRENT_VERSION="0.0.0"
if [ -f "$VERSION_FILE" ]; then
    CURRENT_VERSION=$(grep -o '"version": "[^"]*"' "$VERSION_FILE" | head -1 | cut -d'"' -f4)
fi

log "Version actuelle : $CURRENT_VERSION"
log "Dernière version : $LATEST_VERSION"

if [ "$CURRENT_VERSION" = "$LATEST_VERSION" ]; then
    log "Déjà à jour."
    exit 0
fi

log ">>> Nouvelle version disponible : $CURRENT_VERSION → $LATEST_VERSION"

# === TÉLÉCHARGER ===
TEMP_DIR=$(mktemp -d)
log "Téléchargement..."
curl -sL "$DOWNLOAD_URL" -o "$TEMP_DIR/release.tar.gz"

if [ ! -f "$TEMP_DIR/release.tar.gz" ]; then
    log "ERREUR: Échec du téléchargement"
    rm -rf "$TEMP_DIR"
    exit 1
fi

tar -xzf "$TEMP_DIR/release.tar.gz" -C "$TEMP_DIR"
log "Téléchargement OK"

# === VÉRIFIER QUE VM2 EST ACCESSIBLE ===
if ! ssh -o ConnectTimeout=10 -o BatchMode=yes "$VM2_USER@$VM2_IP" "echo ok" &>/dev/null; then
    log "ERREUR: VM2 ($VM2_IP) inaccessible en SSH. Mise à jour annulée."
    rm -rf "$TEMP_DIR"
    exit 1
fi

# === SAUVEGARDER LA VERSION ACTUELLE ===
if [ -d "$LOCAL_DIR" ]; then
    log "Sauvegarde de la version actuelle..."
    rm -rf "$BACKUP_DIR"
    cp -r "$LOCAL_DIR" "$BACKUP_DIR"
fi
mkdir -p "$LOCAL_DIR"

# === DÉPLOYER LA WEBAPP SUR VM2 ===
log "Arrêt de l'application..."
ssh "$VM2_USER@$VM2_IP" "sudo systemctl stop safehome" 2>/dev/null || true

log "Copie des fichiers..."
scp -r "$TEMP_DIR/webapp/"* "$VM2_USER@$VM2_IP:/home/$VM2_USER/safehome-app/" 2>/dev/null

log "Redémarrage de l'application..."
ssh "$VM2_USER@$VM2_IP" "sudo systemctl start safehome"

# Vérifier que l'app répond
sleep 5
if ssh "$VM2_USER@$VM2_IP" "curl -sf -o /dev/null http://localhost:5000" &>/dev/null; then
    log "Application redémarrée avec succès"
else
    log "WARNING: L'application ne répond pas après le redémarrage"
    log "Tentative de rollback..."

    # Rollback
    if [ -d "$BACKUP_DIR/webapp" ]; then
        ssh "$VM2_USER@$VM2_IP" "sudo systemctl stop safehome" 2>/dev/null || true
        scp -r "$BACKUP_DIR/webapp/"* "$VM2_USER@$VM2_IP:/home/$VM2_USER/safehome-app/" 2>/dev/null
        ssh "$VM2_USER@$VM2_IP" "sudo systemctl start safehome"
        log "Rollback effectué vers $CURRENT_VERSION"
    fi

    rm -rf "$TEMP_DIR"
    exit 1
fi

# === METTRE À JOUR LA VERSION LOCALE ===
cp "$TEMP_DIR/version.json" "$VERSION_FILE"
cp -r "$TEMP_DIR/"* "$LOCAL_DIR/" 2>/dev/null || true

# === NETTOYAGE ===
rm -rf "$TEMP_DIR"

log ">>> Mise à jour terminée : $CURRENT_VERSION → $LATEST_VERSION"
log ""