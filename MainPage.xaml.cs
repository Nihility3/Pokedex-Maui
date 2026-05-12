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
    private CancellationTokenSource? _cts;

    // paging state
    private int _offset = 0;
    private const int PageSize = 30;
    private bool _isLoadingMore = false;
    private bool _hasMore = true;

    public MainPage()
    {
        InitializeComponent();
        PokeCollection.ItemsSource = allPokemon;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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
        await LoadMoreAsync(force: true).ConfigureAwait(false);
    }



    // Called by RemainingItemsThresholdReached
    private async void OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
        if (_hasMore)
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
                        }
                    }
                });
            }
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
        var searchTerm = e.NewTextValue ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            PokeCollection.ItemsSource = allPokemon;
        }
        else
        {
            PokeCollection.ItemsSource = allPokemon
                .Where(p => p.Name?.IndexOf(searchTerm, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }
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

        // Use .Items[selectedIndex] instead of .ItemsSource
        string selectedType = picker.Items[selectedIndex];

        if (selectedType == "All")
        {
            PokeCollection.ItemsSource = allPokemon;
        }
        else
        {
            var filteredList = allPokemon
                .Where(p => p.Type == selectedType)
                .ToList();

            PokeCollection.ItemsSource = filteredList;
        }
        ApplyCombinedFilter();
    }
    private void ApplyCombinedFilter()
    {
        // Get the current search text (handle nulls and make it lowercase)
        string searchText = PokemonSearchBar.Text?.ToLower() ?? "";

        // Get the current selected type (handle the "All" case)
        string selectedType = TypePicker.SelectedIndex != -1
                              ? TypePicker.Items[TypePicker.SelectedIndex]
                              : "All";

        // Filter the list based on BOTH conditions
        var filteredList = allPokemon.Where(p =>
        {
            bool matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                                 p.Name.ToLower().Contains(searchText);

            bool matchesType = selectedType == "All" ||
                               p.Type == selectedType;

            return matchesSearch && matchesType;
        }).ToList();

        // Update the UI
        PokeCollection.ItemsSource = filteredList;
    }
    private async void OnViewTeamClicked(object sender, EventArgs e)
    {
        // Pass the global team list to the TeamPage
        await Navigation.PushAsync(new TeamPage(PokemonService.MyTeam));
    }
}