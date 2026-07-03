namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Linha exibida nos DataGrids de origem/destino (POCO — recriada a cada refresh).</summary>
public sealed class FileRow
{
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Modified { get; set; } = "";
    public bool Highlight { get; set; }
    public bool IsDir { get; set; }
    public bool IsUp { get; set; }
    public string RealName { get; set; } = "";
}
