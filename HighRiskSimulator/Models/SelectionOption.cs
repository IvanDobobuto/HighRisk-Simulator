namespace HighRiskSimulator.Models;

/// <summary>
/// Opción simple para ComboBox de la UI.
/// </summary>
public sealed class SelectionOption
{
    public SelectionOption(string id, string displayName, string description)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public override string ToString()
    {
        return DisplayName;
    }
}
