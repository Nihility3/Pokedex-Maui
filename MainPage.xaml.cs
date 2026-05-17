using Pokedex1.Models;
using Pokedex1.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pokedex1;

public partial class MainPage : ContentPage
{
    public ObservableCollection<Pokemon> allPokemon { get; set; } = new ObservableCollection<Pokemon>();
    public ObservableCollection<Pokemon> VisiblePokemon { get; set; } = new ObservableCollection<Pokemon>();
    private CancellationTokenSource? _cts;

    // paging state
    private int _offset = 0;
    private const int PageSize = 30;
    private bool _isLoadingMore = false;
    private bool _hasMore = true;
    private bool _hasLoadedInitialPage = false;
    private bool _isLoadingAll = false;

    public MainPage()
    {
        InitializeComponent();
        PokeCollection.ItemsSource = VisiblePokemon;
        TypePicker.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_hasLoadedInitialPage)
            _ = LoadInitialAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts?.Cancel();
    }

    private async Task LoadInitialAsync()
    {
        _offset = 0;
        _hasMore = true;
        allPokemon.Clear();
        VisiblePokemon.Clear();
        await LoadMoreAsync(force: true).ConfigureAwait(false);
        _hasLoadedInitialPage = true;
    }



    // Called by RemainingItemsThresholdReached
    private async void OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
            if (_hasMore && !IsFilterActive())
            {
                await LoadMoreAsync();
            }
    }

    private async Task LoadMoreAsync(bool force = false)
    {
        if (_isLoadingMore || !_hasMore)
            return;

        _isLoadingMore = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
            });

            var page = await PokemonService.GetPokemonPageAsync(_offset, PageSize, _cts.Token, force).ConfigureAwait(false);

            // Inside your LoadMoreAsync method
            if (page.Count > 0)
            {
                _offset += page.Count;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (PokeCollection != null)
                    {
                        foreach (var pokemon in page)
                        {
                            allPokemon.Add(pokemon);
                            if (PokemonMatchesFilter(pokemon))
                                VisiblePokemon.Add(pokemon);
                        }
                    }
                });
            }

            if (page.Count < PageSize)
                _hasMore = false;
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Error", "Failed to load Pokémon: " + ex.Message, "OK");
            });
        }
        finally
        {
            _isLoadingMore = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            });
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyCombinedFilter();
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Pokemon selected)
        {
            ((CollectionView)sender).SelectedItem = null;
            await Navigation.PushAsync(new DetailPage(selected));
        }
    }

    private void OnTypeFilterChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        int selectedIndex = picker.SelectedIndex;

        if (selectedIndex == -1) return;

        ApplyCombinedFilter();
    }
    private void ApplyCombinedFilter()
    {
        VisiblePokemon.Clear();

        foreach (var pokemon in allPokemon.Where(PokemonMatchesFilter))
        {
            VisiblePokemon.Add(pokemon);
        }
    }

    private bool PokemonMatchesFilter(Pokemon pokemon)
    {
        string searchText = PokemonSearchBar?.Text?.Trim() ?? string.Empty;
        string selectedType = TypePicker?.SelectedIndex >= 0
            ? TypePicker.Items[TypePicker.SelectedIndex]
            : "All";

        bool matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                             pokemon.Name.Contains(searchText, System.StringComparison.OrdinalIgnoreCase);

        bool matchesType = selectedType == "All" ||
                           pokemon.Types.Any(t => t.Equals(selectedType, System.StringComparison.OrdinalIgnoreCase)) ||
                           pokemon.Type.Equals(selectedType, System.StringComparison.OrdinalIgnoreCase);

        return matchesSearch && matchesType;
    }

    private bool IsFilterActive()
    {
        var selectedType = TypePicker?.SelectedIndex >= 0 ? TypePicker.Items[TypePicker.SelectedIndex] : "All";
        return !string.IsNullOrWhiteSpace(PokemonSearchBar?.Text) || selectedType != "All";
    }

    private async void OnLoadAllClicked(object sender, EventArgs e)
    {
        if (_isLoadingAll)
            return;

        _isLoadingAll = true;
        LoadAllButton.IsEnabled = false;
        LoadAllButton.Text = "Loading...";

        try
        {
            while (_hasMore)
            {
                await LoadMoreAsync();
                await Task.Delay(75);
            }
        }
        finally
        {
            _isLoadingAll = false;
            LoadAllButton.Text = "All Pokemon Loaded";
        }
    }
    private async void OnViewTeamClicked(object sender, EventArgs e)
    {
        // Pass the global team list to the TeamPage
        await Navigation.PushAsync(new TeamPage(PokemonService.MyTeam));
    }
}
