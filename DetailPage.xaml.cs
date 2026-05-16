using Pokedex1.Models;
using Pokedex1.Services;

namespace Pokedex1;

public partial class DetailPage : ContentPage
{
    private readonly Pokemon _initial;

    public DetailPage(Pokemon pokemon)
    {
        InitializeComponent();
        _initial = pokemon;
        BindingContext = _initial;

        // Load species/flavor text, evolution chain and ability descriptions
        _ = LoadFullDetailsAsync();
    }

    private async Task LoadFullDetailsAsync()
    {
        try
        {
            var detailed = await PokemonService.GetPokemonDetailsAsync(_initial.Id).ConfigureAwait(false);
            if (detailed is null)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // copy/update fields onto the bound instance
                _initial.Description = string.IsNullOrEmpty(detailed.Description) ? _initial.Description : detailed.Description;
                _initial.Types = detailed.Types;
                _initial.Type = detailed.Type;
                _initial.CryUrl = detailed.CryUrl;
                _initial.EvolutionChain = detailed.EvolutionChain ?? new List<Evolution>();
                _initial.BaseExperience = detailed.BaseExperience;
                _initial.GrowthRate = detailed.GrowthRate;
                _initial.LevelUpGuide = detailed.LevelUpGuide;

                // stats (raw and normalized)
                _initial.RawHP = detailed.RawHP;
                _initial.RawAttack = detailed.RawAttack;
                _initial.RawDefense = detailed.RawDefense;
                _initial.RawSpecialAttack = detailed.RawSpecialAttack;
                _initial.RawSpecialDefense = detailed.RawSpecialDefense;
                _initial.RawSpeed = detailed.RawSpeed;

                _initial.HP = detailed.HP;
                _initial.Attack = detailed.Attack;
                _initial.Defense = detailed.Defense;
                _initial.SpecialAttack = detailed.SpecialAttack;
                _initial.SpecialDefense = detailed.SpecialDefense;
                _initial.Speed = detailed.Speed;

                if (detailed.AbilityDetails?.Count > 0)
                    _initial.AbilityDetails = detailed.AbilityDetails;

                // refresh binding so UI updates
                BindingContext = null;
                BindingContext = _initial;
            });
        }
        catch
        {
            // ignore errors silently
        }
    }
    private async void OnAddToTeamClicked(object sender, EventArgs e)
    {
        var pokemon = (Pokemon)BindingContext;

        if (PokemonService.MyTeam.Count < 6)
        {
            PokemonService.MyTeam.Add(pokemon);
            await DisplayAlert("Success", $"{pokemon.Name} added to team!", "OK");
        }
        else
        {
            await DisplayAlert("Team Full", "You can only have 6 Pokémon.", "OK");
        }
    }

    private async void OnNextPokemonClicked(object sender, EventArgs e)
    {
        var next = await PokemonService.GetPokemonDetailsAsync(_initial.Id + 1);
        if (next is null)
        {
            await DisplayAlert("Not Found", "There is no next Pokemon available.", "OK");
            return;
        }

        await Navigation.PushAsync(new DetailPage(next));
    }

    private async void OnPlayCryClicked(object sender, EventArgs e)
    {
        var pokemon = (Pokemon)BindingContext;
        var cryUrl = pokemon.CryUrl;

        if (string.IsNullOrWhiteSpace(cryUrl))
        {
            var detailed = await PokemonService.GetPokemonDetailsAsync(pokemon.Id);
            cryUrl = detailed?.CryUrl ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(cryUrl))
        {
            await DisplayAlert("No Cry", "No cry audio is available for this Pokemon.", "OK");
            return;
        }

        await Launcher.OpenAsync(cryUrl);
    }
}
