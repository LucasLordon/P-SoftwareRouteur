namespace SoftwareRouteur.Models;

public class OPNsenseSettings
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public bool SkipSslValidation { get; set; } = true;
}