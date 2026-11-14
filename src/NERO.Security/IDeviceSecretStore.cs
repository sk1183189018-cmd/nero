namespace NERO.Security;

/// <summary>Stores secrets in the current Windows user's protected credential vault.</summary>
public interface IDeviceSecretStore
{
    void Save(string targetName, string secret);
    string? Read(string targetName);
    bool Delete(string targetName);
}
