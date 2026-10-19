// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>
    /// Defines the <see cref="IsImageSaveEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsImageSaveEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(IsImageSaveEnabled), true);

    /// <summary>
    /// Identifies the <see cref="SavingImage"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<SavingImageEventArgs> SavingImageEvent =
        RoutedEvent.Register<MarkdownViewer, SavingImageEventArgs>(nameof(SavingImage), RoutingStrategies.Bubble);

    /// <summary>
    /// Gets or sets whether images and Mermaid diagrams can be saved to a file: from the lightbox's save button
    /// or Ctrl+S (Cmd+S on macOS), and from the item "Save image as…" of the context menu opened on one. A
    /// diagram, or an image of the Svg package, is saved as SVG or PNG; any other image as PNG. Saving raises
    /// <see cref="SavingImage"/>, then opens the platform's save picker (in a browser without one, the file is
    /// downloaded). Defaults to <c>true</c>. Can be set from a style, e.g. for every viewer of an application.
    /// </summary>
    public bool IsImageSaveEnabled
    {
        get => GetValue(IsImageSaveEnabledProperty);
        set => SetValue(IsImageSaveEnabledProperty, value);
    }

    /// <summary>
    /// Raised when the user asks to save an image or a diagram, before the save picker opens. A handler can
    /// change <see cref="SavingImageEventArgs.SuggestedFileName"/>, or set <see cref="SavingImageEventArgs.Cancel"/>
    /// and save the picture itself with <see cref="SavingImageEventArgs.WriteAsync"/>. Bubbles up the visual tree.
    /// </summary>
    public event EventHandler<SavingImageEventArgs>? SavingImage
    {
        add => AddHandler(SavingImageEvent, value);
        remove => RemoveHandler(SavingImageEvent, value);
    }

    /// <summary>Whether <paramref name="image"/> can be saved now.</summary>
    private bool CanSaveImage(Image? image) => IsImageSaveEnabled && image is not null && ImageExport.CanSave(image);

    /// <summary>Shows the lightbox's save button while its picture can be saved, and saves it on request.</summary>
    private void SetUpLightboxSave(ImageLightbox lightbox)
    {
        ShowLightboxSaveButton(lightbox);
        lightbox.SaveRequested += () => _ = SaveImageAsync(lightbox.Picture);
        lightbox.AddHandler(ContextRequestedEvent, (_, _) => _contextImage = lightbox.Picture);
    }

    // Also when IsImageSaveEnabled changes while the lightbox is open.
    private void ShowLightboxSaveButton(ImageLightbox lightbox) =>
        lightbox.SaveButton.IsVisible = CanSaveImage(lightbox.Picture);

    /// <summary>
    /// Saves <paramref name="image"/>'s picture: raises <see cref="SavingImage"/>, then, unless a handler cancels,
    /// asks where to save it with the window's save picker and writes the file. Returns whether a file was written.
    /// </summary>
    internal async Task<bool> SaveImageAsync(Image image)
    {
        if (!CanSaveImage(image) || image.Source is not { } picture) return false;
        var background = ImageZoom.GetBackground(image);
        var e = new SavingImageEventArgs(ImageExport.GetFileName(image)!, ImageExport.GetFormats(picture),
            (format, stream) => ImageExport.WriteAsync(picture, background, format, stream))
        {
            RoutedEvent = SavingImageEvent,
        };
        RaiseEvent(e);
        if (e.Cancel || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage) return false;

        var options = CreateSaveOptions(e);
        var result = await storage.SaveFilePickerWithResultAsync(options);
        if (result.File is not { } file) return false;
        using (file)
            return await WriteFileAsync(e, file.Name, result.SelectedFileType?.Name, file.OpenWriteAsync);
    }

    /// <summary>
    /// The save picker's options for <paramref name="e"/>: its suggested name with the extension of its first
    /// format, and one file type per format, the first suggested.
    /// </summary>
    internal static FilePickerSaveOptions CreateSaveOptions(SavingImageEventArgs e)
    {
        var types = e.Formats.Select(format => new FilePickerFileType(FileTypeName(format))
        {
            Patterns = [$"*.{ImageExport.GetExtension(format)}"],
            MimeTypes = [ImageExport.GetMimeType(format)],
        }).ToList();
        var extension = ImageExport.GetExtension(e.Formats[0]);
        return new FilePickerSaveOptions
        {
            SuggestedFileName = $"{e.SuggestedFileName}.{extension}",
            DefaultExtension = extension,
            FileTypeChoices = types,
            SuggestedFileType = types[0],
            ShowOverwritePrompt = true,
        };
    }

    /// <summary>
    /// Writes <paramref name="e"/>'s picture to the stream <paramref name="openWrite"/> opens, in the format
    /// <see cref="FormatOf"/> gives the file named <paramref name="fileName"/> with the file type named
    /// <paramref name="pickedType"/> picked. Returns whether it was written.
    /// </summary>
    internal static async Task<bool> WriteFileAsync(SavingImageEventArgs e, string fileName, string? pickedType,
        Func<Task<Stream>> openWrite)
    {
        var format = FormatOf(fileName, pickedType, e.Formats);
        try
        {
            await using var stream = await openWrite();
            await e.WriteAsync(format, stream);
            return true;
        }
        // The file could not be written: nothing more can be done about it here.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// The format a file named <paramref name="fileName"/> is saved in: the one of its extension, else the one of
    /// the file type picked (named <paramref name="pickedType"/>), else the first of <paramref name="formats"/>.
    /// </summary>
    internal static MarkdownImageFormat FormatOf(string fileName, string? pickedType, IReadOnlyList<MarkdownImageFormat> formats)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.');
        foreach (var format in formats)
        {
            if (string.Equals(ImageExport.GetExtension(format), extension, StringComparison.OrdinalIgnoreCase))
                return format;
        }
        foreach (var format in formats)
        {
            if (string.Equals(FileTypeName(format), pickedType, StringComparison.Ordinal))
                return format;
        }
        return formats[0];
    }

    private static string FileTypeName(MarkdownImageFormat format) => format.ToString().ToUpperInvariant();
}
