using System.Windows;
using ModernWpf.Controls;
using VideoAnalysis.App.ViewModels;
using VideoAnalysis.Core.Spansh.Models;
using VideoAnalysis.Core.Storage;

namespace VideoAnalysis.App.Views;

public partial class VideoUploadMetadataWindow : Window
{
    private readonly VideoUploadMetadataViewModel _viewModel;
    private readonly string _filePath;
    private CancellationTokenSource? _searchDebounceCts;

    public VideoLibraryEntry? ResultEntry { get; private set; }

    public VideoUploadMetadataWindow(VideoUploadMetadataViewModel viewModel, string filePath)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _filePath = filePath;
        DataContext = _viewModel;
    }

    private async void SystemSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        _searchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);
            await _viewModel.RefreshSuggestionsAsync(sender.Text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke
        }
    }

    private async void SystemSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SpanshSearchSystem system)
        {
            await _viewModel.SelectSystemAsync(system);
        }
    }

    private async void SystemSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var chosen = args.ChosenSuggestion as SpanshSearchSystem;
        await _viewModel.SelectSystemAsync(chosen);
    }

    private async void LookUpButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SelectSystemAsync(null);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SystemQuery.Trim().Length == 0)
        {
            _viewModel.ErrorMessage = "Enter a system name.";
            return;
        }

        ResultEntry = _viewModel.BuildEntry(_filePath);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
