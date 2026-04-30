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
}
