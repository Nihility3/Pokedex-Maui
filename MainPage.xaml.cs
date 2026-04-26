using Pokedex1.Models;
using Pokedex1.Services;
using System.Linq; // REQUIRED: This fixes the ".Where" error

namespace Pokedex1;

public partial class MainPage : ContentPage
{
    // Class-level field so all methods can see the data
    List<Pokemon> allPokemon;

    public MainPage()
    {
        InitializeComponent(); 

        // Initialize the list using the service
        allPokemon = PokemonService.GetPokemon();
        PokeCollection.ItemsSource = allPokemon;
    }

    // Feature: Real-time Search Logic
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var searchTerm = e.NewTextValue;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            PokeCollection.ItemsSource = allPokemon;
        }
        else
        {
            // .ToList() ensures the CollectionView receives a compatible list format
            PokeCollection.ItemsSource = allPokemon
                .Where(p => p.Name.ToLower().Contains(searchTerm.ToLower()))
                .ToList();
        }
    }

    // Feature: Navigation to Detail
    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Pokemon selected)
        {
            // Deselect item to allow re-clicking later
            ((CollectionView)sender).SelectedItem = null;

            // Navigate to the DetailPage
            await Navigation.PushAsync(new DetailPage(selected));
        }
    }
}