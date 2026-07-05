using StardewModdingAPI;
using System;

namespace GenericModConfigMenu
{
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string> formatValue = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string> tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string> formatValue = null, string fieldId = null);
    }
}

namespace BirthdayRolodex
{
    // Soft dependency: LookupAnythingMobileSearch implements a matching
    // ShowNpcByName(string) method on its own ModEntry (which also
    // implements this same interface shape). SMAPI's GetApi<T> builds a
    // proxy by matching method signatures, so the two mods never need to
    // reference each other's assemblies.
    public interface ILookupAnythingMobileSearchApi
    {
        bool ShowNpcByName(string npcInternalName);
    }
}

