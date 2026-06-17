using System.Globalization;

namespace Reader.Controls;

// Empty/blank cover url -> bundled placeholder image.
public class CoverConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
    {
        var s = v as string;
        if (string.IsNullOrWhiteSpace(s)) return ImageSource.FromFile("placeholder.png");
        // Chemin local (couverture personnalisée sans connexion).
        if (File.Exists(s)) return ImageSource.FromFile(s);
        // URL distante (Supabase Storage ou couverture scraped).
        return Uri.TryCreate(s, UriKind.Absolute, out var uri)
            ? ImageSource.FromUri(uri)
            : ImageSource.FromFile("placeholder.png");
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

// Builds a 3-span FormattedString (before · highlighted word · after) for the reader.
// Cheap to rebuild per word for chapter-sized text.
public class HighlightConverter : IMultiValueConverter
{
    public object Convert(object[] v, Type t, object? p, CultureInfo c)
    {
        var text = v[0] as string ?? "";
        int start = v[1] is int a ? a : 0;
        int len = v[2] is int b ? b : 0;
        var fs = new FormattedString();
        if (len <= 0 || start < 0 || start + len > text.Length)
        {
            fs.Spans.Add(new Span { Text = text });
            return fs;
        }
        var hl = (Color)Application.Current!.Resources["Highlight"];
        fs.Spans.Add(new Span { Text = text[..start] });
        fs.Spans.Add(new Span { Text = text.Substring(start, len), BackgroundColor = hl, TextColor = Colors.Black });
        fs.Spans.Add(new Span { Text = text[(start + len)..] });
        return fs;
    }
    public object[] ConvertBack(object? v, Type[] t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class InvertBool : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is bool b && !b;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => v is bool b && !b;
}
