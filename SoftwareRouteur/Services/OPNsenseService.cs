using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SoftwareRouteur.Models;

namespace SoftwareRouteur.Services;

public class OPNsenseService
{
    private readonly HttpClient _http;
    private readonly OPNsenseSettings _settings;
    private readonly ILogger<OPNsenseService> _logger;

    public OPNsenseService(
        IOptions<OPNsenseSettings> settings,
        ILogger<OPNsenseService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var handler = new HttpClientHandler();
        if (_settings.SkipSslValidation)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(_settings.BaseUrl)
        };

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settings.ApiKey}:{_settings.ApiSecret}"));
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }

    // ================================================================
    // ALIASES — Chaque client a un alias contenant ses destinations bloquées
    // ================================================================

    /// <summary>
    /// Crée un alias de type Host pour un client SafeHome.
    /// L'alias peut contenir des FQDN, IPs et CIDR.
    /// Nom convention : safehome_block_{clientId}
    /// </summary>
    public async Task<string?> CreateAliasAsync(int clientId, string description)
    {
        var aliasName = $"safehome_block_{clientId}";
        var data = new
        {
            alias = new
            {
                name = aliasName,
                type = "host",
                description = description,
                content = "",
                enabled = "1"
            }
        };

        var response = await PostAsync("/api/firewall/alias/addItem", data);
        if (response == null) return null;

        var uuid = ExtractUuid(response);
        if (uuid != null)
        {
            _logger.LogInformation("Alias créé : {Name} (UUID: {Uuid})", aliasName, uuid);
            await ReconfigureAliasesAsync();
        }
        return uuid;
    }

    private async Task<bool> SetAliasContentAsync(string uuid, List<string> destinations)
    {
        var data = new
        {
            alias = new
            {
                content = string.Join("\n", destinations)
            }
        };

        var response = await PostAsync($"/api/firewall/alias/setItem/{uuid}", data);
        return response != null;
    }

    public async Task<bool> AddToAliasAsync(int clientId, string destination)
    {
        var aliasName = $"safehome_block_{clientId}";

        var uuid = await GetAliasUuidByNameAsync(aliasName);
        if (uuid == null)
        {
            _logger.LogWarning("Alias introuvable : {Alias}", aliasName);
            return false;
        }

        var entries = await GetAliasContentAsListAsync(uuid);
        if (entries == null) return false;

        if (!entries.Contains(destination))
            entries.Add(destination);

        var ok = await SetAliasContentAsync(uuid, entries);
        if (ok)
        {
            _logger.LogInformation("Ajouté {Dest} à l'alias {Alias}", destination, aliasName);
            await ReconfigureAliasesAsync();
        }
        return ok;
    }

    public async Task<bool> RemoveFromAliasAsync(int clientId, string destination)
    {
        var aliasName = $"safehome_block_{clientId}";

        var uuid = await GetAliasUuidByNameAsync(aliasName);
        if (uuid == null)
        {
            _logger.LogWarning("Alias introuvable : {Alias}", aliasName);
            return false;
        }

        var entries = await GetAliasContentAsListAsync(uuid);
        if (entries == null) return false;

        var removed = entries.RemoveAll(e =>
            string.Equals(e.TrimEnd('.'), destination.TrimEnd('.'),
            StringComparison.OrdinalIgnoreCase)) > 0;

        if (!removed)
        {
            _logger.LogWarning("Destination {Dest} introuvable dans {Alias}", destination, aliasName);
            return false;
        }

        var ok = await SetAliasContentAsync(uuid, entries);
        if (ok)
        {
            _logger.LogInformation("Retiré {Dest} de l'alias {Alias}", destination, aliasName);
            await ReconfigureAliasesAsync();
        }
        return ok;
    }
    
    private async Task<List<string>?> GetAliasContentAsListAsync(string uuid)
    {
        var current = await GetAsync($"/api/firewall/alias/getItem/{uuid}");
        if (current == null) return null;

        try
        {
            var doc = JsonDocument.Parse(current);
            var content = doc.RootElement
                .GetProperty("alias")
                .GetProperty("content");

            var entries = new List<string>();
            foreach (var prop in content.EnumerateObject())
            {
                if (string.IsNullOrEmpty(prop.Name)) continue;
                if (prop.Name == "Array") continue;
                if (!prop.Value.TryGetProperty("selected", out var sel)) continue;
                if (sel.GetInt32() != 1) continue;

                entries.Add(prop.Name);
            }
            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur parsing alias content");
            return null;
        }
    }

    /// <summary>
    /// Liste le contenu actuel d'un alias.
    /// </summary>
    public async Task<List<string>> ListAliasContentAsync(int clientId)
    {
        var aliasName = $"safehome_block_{clientId}";
        var response = await GetAsync($"/api/firewall/alias_util/list/{aliasName}");
        if (response == null) return new List<string>();

        try
        {
            var doc = JsonDocument.Parse(response);
            var rows = doc.RootElement.GetProperty("rows");
            var items = new List<string>();
            foreach (var row in rows.EnumerateArray())
            {
                if (row.TryGetProperty("ip", out var ip))
                    items.Add(ip.GetString() ?? "");
            }
            return items;
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Supprime un alias complet (quand un client est supprimé de SafeHome).
    /// </summary>
    public async Task<bool> DeleteAliasAsync(int clientId)
    {
        var aliasName = $"safehome_block_{clientId}";
        
        var uuid = await GetAliasUuidByNameAsync(aliasName);
        if (uuid == null) return false;

        var response = await PostAsync($"/api/firewall/alias/delItem/{uuid}", new { });
        if (response != null)
        {
            _logger.LogInformation("Alias supprimé : {Alias}", aliasName);
            await ReconfigureAliasesAsync();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Vide un alias (retire toutes les destinations).
    /// </summary>
    public async Task<bool> FlushAliasAsync(int clientId)
    {
        var aliasName = $"safehome_block_{clientId}";
        var response = await PostAsync($"/api/firewall/alias_util/flush/{aliasName}", new { });
        return response != null;
    }

    // ================================================================
    // FIREWALL RULES — Une règle de blocage par client
    // ================================================================

    /// <summary>
    /// Crée une règle firewall qui bloque le trafic du client vers son alias.
    /// La règle est : BLOCK traffic FROM client_ip TO alias safehome_block_{clientId}
    /// </summary>
    public async Task<string?> CreateBlockRuleAsync(int clientId, string clientIp, string description)
    {
        var aliasName = $"safehome_block_{clientId}";

        var data = new
        {
            rule = new
            {
                enabled = "1",
                action = "block",
                quick = "1",
                interface_field = "lan",
                direction = "in",
                ipprotocol = "inet",
                protocol = "any",
                source_net = clientIp,
                source_not = "0",
                destination_net = aliasName,
                destination_not = "0",
                description = $"SafeHome: {description}",
                log = "1"
            }
        };

        var response = await PostAsync("/api/firewall/filter/addRule", data);
        if (response == null) return null;

        var uuid = ExtractUuid(response);
        if (uuid != null)
        {
            _logger.LogInformation("Règle créée pour client {Ip} → alias {Alias} (UUID: {Uuid})",
                clientIp, aliasName, uuid);
            await ApplyFirewallRulesAsync();
        }
        return uuid;
    }

    /// <summary>
    /// Supprime une règle firewall par UUID.
    /// </summary>
    public async Task<bool> DeleteFirewallRuleAsync(string ruleUuid)
    {
        var response = await PostAsync($"/api/firewall/filter/delRule/{ruleUuid}", new { });
        if (response != null)
        {
            await ApplyFirewallRulesAsync();
            _logger.LogInformation("Règle supprimée : {Uuid}", ruleUuid);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Active ou désactive une règle.
    /// </summary>
    public async Task<bool> ToggleFirewallRuleAsync(string ruleUuid, bool enabled)
    {
        var response = await PostAsync(
            $"/api/firewall/filter/toggleRule/{ruleUuid}/{(enabled ? "1" : "0")}", new { });
        if (response != null)
        {
            await ApplyFirewallRulesAsync();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Recherche une règle par description (pour retrouver les règles SafeHome).
    /// </summary>
    public async Task<List<(string Uuid, string Description, bool Enabled)>> SearchRulesAsync(
        string searchPhrase = "SafeHome")
    {
        var response = await GetAsync(
            $"/api/firewall/filter/searchRule?current=1&rowCount=-1&searchPhrase={searchPhrase}");
        var rules = new List<(string, string, bool)>();

        if (response == null) return rules;

        try
        {
            var doc = JsonDocument.Parse(response);
            foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
            {
                var uuid = row.GetProperty("uuid").GetString() ?? "";
                var desc = row.GetProperty("description").GetString() ?? "";
                var enabled = row.GetProperty("enabled").GetString() == "1";
                rules.Add((uuid, desc, enabled));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur parsing des règles OPNsense");
        }
        return rules;
    }

    // ================================================================
    // APPLY — Appliquer les changements
    // ================================================================

    /// <summary>
    /// Applique les changements de règles firewall (équivalent du bouton "Apply").
    /// </summary>
    public async Task<bool> ApplyFirewallRulesAsync()
    {
        var response = await PostAsync("/api/firewall/filter/apply", new { });
        return response != null;
    }

    /// <summary>
    /// Reconfigure les aliases (nécessaire après ajout/suppression d'alias).
    /// </summary>
    public async Task<bool> ReconfigureAliasesAsync()
    {
        var response = await PostAsync("/api/firewall/alias/reconfigure", new { });
        return response != null;
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private async Task<string?> GetAliasUuidByNameAsync(string name)
    {
        var response = await GetAsync($"/api/firewall/alias/getAliasUUID/{name}");
        if (response == null) return null;

        try
        {
            var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("uuid", out var uuid))
                return uuid.GetString();
        }
        catch { }
        return null;
    }

    private string? ExtractUuid(string jsonResponse)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonResponse);
            if (doc.RootElement.TryGetProperty("uuid", out var uuid))
                return uuid.GetString();
        }
        catch { }
        return null;
    }

    private async Task<string?> PostAsync(string endpoint, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(endpoint, content);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OPNsense API POST {Endpoint} → {Status}: {Body}",
                    endpoint, response.StatusCode, body);
                return null;
            }

            return body;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur appel OPNsense API POST {Endpoint}", endpoint);
            return null;
        }
    }

    private async Task<string?> GetAsync(string endpoint)
    {
        try
        {
            var response = await _http.GetAsync(endpoint);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OPNsense API GET {Endpoint} → {Status}: {Body}",
                    endpoint, response.StatusCode, body);
                return null;
            }

            return body;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur appel OPNsense API GET {Endpoint}", endpoint);
            return null;
        }
    }
}