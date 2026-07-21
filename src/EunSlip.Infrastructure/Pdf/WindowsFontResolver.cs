using PdfSharp.Fonts;

namespace EunSlip.Infrastructure.Pdf;

public sealed class WindowsFontResolver : IFontResolver
{
    private static readonly string FontsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    public byte[]? GetFont(string faceName)
    {
        string fileName = faceName switch
        {
            "Arial#" => "arial.ttf",
            "Arial#b" => "arialbd.ttf",
            _ => "arial.ttf",
        };

        string path = Path.Combine(FontsDirectory, fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        return familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase)
            ? new FontResolverInfo(bold ? "Arial#b" : "Arial#")
            : new FontResolverInfo("Arial#");
    }
}
