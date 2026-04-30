using System;
using System.Collections.Generic;
using Microsoft.Maui.Graphics;

namespace Pokedex1.Models;

public class Evolution
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class AbilityDetail
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class Pokemon
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    // Flavor text / species description (English)
    public string Description { get; set; } = string.Empty;

    // Color for UI
    public Color TypeColor { get; set; }

    // Normalized base stats 0..1 for progress bars
    public double HP { get; set; }
    public double Attack { get; set; }
    public double Defense { get; set; }
    public double SpecialAttack { get; set; }
    public double SpecialDefense { get; set; }
    public double Speed { get; set; }

    // Raw base stat integers (for display)
    public int RawHP { get; set; }
    public int RawAttack { get; set; }
    public int RawDefense { get; set; }
    public int RawSpecialAttack { get; set; }
    public int RawSpecialDefense { get; set; }
    public int RawSpeed { get; set; }

    // Abilities names and detailed descriptions
    public List<string> Abilities { get; set; } = new();
    public List<AbilityDetail> AbilityDetails { get; set; } = new();

    // Evolution chain for details page
    public List<Evolution> EvolutionChain { get; set; } = new();

    // Leveling info
    public int BaseExperience { get; set; }
    public string GrowthRate { get; set; } = string.Empty;
    public string LevelUpGuide { get; set; } = string.Empty;
}
