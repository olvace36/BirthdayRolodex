using StardewModdingAPI;

using Main = ichortower.TowerCore.Main;

namespace BirthdayRolodex;

public class ModMain : Mod
{
    public static ModConfig Config = null;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        Main.Init(this);
    }
}
