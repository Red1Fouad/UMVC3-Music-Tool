using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Umvc3MusicTool.ViewModels;

namespace Umvc3MusicTool.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType AudioFilter = new("Audio files")
    {
        Patterns =
        [
            "*.mp3", "*.wav", "*.flac", "*.ogg", "*.oga", "*.m4a", "*.aac",
            "*.wma", "*.opus", "*.aiff", "*.aif", "*.ape", "*.sngw",
        ],
    };

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null || files.Length == 0)
            return;

        var paths = files.Select(f => f.Path.LocalPath).ToList();
        if (paths.Count > 0)
            Vm?.AddFiles(paths);
    }

    private async void OnAddFilesClick(object? sender, RoutedEventArgs e)
    {
        var files = await PickAudioFilesAsync();
        if (files.Count > 0)
            Vm?.AddFiles(files);
    }

    private void OnRemoveSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedFile is { } selected)
            Vm.RemoveSelected(selected);
    }

    private async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose output folder",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
            Vm!.OutputDirectory = folders[0].Path.LocalPath;
    }

    private async Task<List<string>> PickAudioFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose audio files",
            AllowMultiple = true,
            FileTypeFilter = [AudioFilter, FilePickerFileTypes.All],
        });

        return files.Select(f => f.Path.LocalPath).ToList();
    }

    private async Task<string?> PickSingleAudioFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose audio file",
            AllowMultiple = false,
            FileTypeFilter = [AudioFilter, FilePickerFileTypes.All],
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async void OnBrowseDynamicFile1Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickSingleAudioFileAsync();
        if (path is not null)
            Vm!.DynamicFile1 = path;
    }

    private async void OnBrowseDynamicFile2Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickSingleAudioFileAsync();
        if (path is not null)
            Vm!.DynamicFile2 = path;
    }

    private async void OnBgmGuideClick(object? sender, RoutedEventArgs e)
    {
        await new BgmGuideWindow().ShowDialog(this);
    }
}
