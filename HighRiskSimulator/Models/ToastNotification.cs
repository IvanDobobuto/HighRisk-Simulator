
using System;

namespace HighRiskSimulator.Models;

/// <summary>
/// Notificación visual breve para confirmaciones no intrusivas.
/// </summary>
public sealed class ToastNotification
{
    public ToastNotification(string title, string message, string accentColor, string iconGlyph)
    {
        Title = title;
        Message = message;
        AccentColor = accentColor;
        IconGlyph = iconGlyph;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string Title { get; }

    public string Message { get; }

    public string AccentColor { get; }

    public string IconGlyph { get; }

    public DateTime CreatedAtUtc { get; }
}
