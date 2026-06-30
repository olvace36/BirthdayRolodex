using GenericModConfigMenu;
using ichortower.TowerCore;
using StardewModdingAPI.Events;
using System;

using TR = ichortower.TowerCore.Translation;

namespace BirthdayRolodex;

public class ModConfig
{
    /*
     * How long (milliseconds) to wait between automatic cyclings when
     * hovering over a day with multiple event textures.
     */
    public int CycleTime {
        get {
            return _cycleTime;
        }
        set {
            _cycleTime = Math.Max(500, value);
        }
    }

    /*
     * How long (milliseconds) to wait after manual cycling to resume
     * automatic cycling.
     */
    public int PauseTime {
        get {
            return _pauseTime;
        }
        set {
            _pauseTime = Math.Max(1000, value);
        }
    }

    /*
     * How far (in screen pixels) each additional texture is offset from
     * the main draw position. I recommend not changing this.
     */
    public int StaggerDistance {
        get {
            return _staggerDistance;
        }
        set {
            _staggerDistance = Math.Max(4, Math.Min(value, 16));
        }
    }

    /*
     * How brightly (0-255) the characters behind the front one will be
     * rendered.
     */
    public int QueueBrightness {
        get {
            return _queueBrightness;
        }
        set {
            _queueBrightness = Math.Max(0, Math.Min(value, 255));
        }
    }

    /*
     * How loudly (0.0-1.0) the sound effect plays when the icons are manually
     * rotated. Set to 0 to disable the sound.
     */
    public float CycleSoundVolume {
        get {
            return _cycleSoundVolume;
        }
        set {
            _cycleSoundVolume = MathF.Max(0f, MathF.Min(value, 1.0f));
        }
    }

    /*
     * By default, textures can only be cycled through player interaction:
     * either hovering for automatic cycling, or clicking/scrolling for manual.
     * Set this flag to allow automatic cycling to occur even when not hovering.
     */
    public bool AlwaysCycle = false;

    /*
     * By default, scroll wheel down moves to the next texture in the
     * order, and scroll wheel up moves to the previous one (in the back).
     * Set this flag to invert this mapping.
     */
    public bool InvertScrollDirection = false;



    /*
     * backing fields
     */
    private int _cycleTime = 1000;
    private int _pauseTime = 4000;
    private int _staggerDistance = 12;
    private int _queueBrightness = 108;
    private float _cycleSoundVolume = 0.33f;


    [SmapiEvent]
    internal static void GameLaunched(object sender, GameLaunchedEventArgs e)
    {
        var gmcm = Main.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
                "spacechase0.GenericModConfigMenu");
        if (gmcm is null) {
            return;
        }

        gmcm.Register(
            mod: Main.Manifest,
            reset: () => ModMain.Config = new ModConfig(),
            save: () => {
                Main.Helper.WriteConfig(ModMain.Config);
            }
        );

        gmcm.AddNumberOption(
            mod: Main.Manifest,
            name: () => TR.Get("gmcm.CycleTime.name"),
            tooltip: () => TR.Get("gmcm.CycleTime.tooltip"),
            min: 500,
            getValue: () => ModMain.Config.CycleTime,
            setValue: value => ModMain.Config.CycleTime = value
        );
        gmcm.AddNumberOption(
            mod: Main.Manifest,
            name: () => TR.Get("gmcm.PauseTime.name"),
            tooltip: () => TR.Get("gmcm.PauseTime.tooltip"),
            min: 1000,
            getValue: () => ModMain.Config.PauseTime,
            setValue: value => ModMain.Config.PauseTime = value
        );
        gmcm.AddNumberOption(
            mod: Main.Manifest,
            name: () => TR.Get("gmcm.StaggerDistance.name"),
            tooltip: () => TR.Get("gmcm.StaggerDistance.tooltip"),
            min: 4,
            max: 16,
            getValue: () => ModMain.Config.StaggerDistance,
            setValue: value => ModMain.Config.StaggerDistance = value
        );
        gmcm.AddNumberOption(
            mod: Main.Manifest,
            name: () => TR.Get("gmcm.QueueBrightness.name"),
            tooltip: () => TR.Get("gmcm.QueueBrightness.tooltip"),
            min: 0,
            max: 255,
            getValue: () => ModMain.Config.QueueBrightness,
            setValue: value => ModMain.Config.QueueBrightness = value
        );
        gmcm.AddNumberOption(
            mod: Main.Manifest,
            name: () => TR.Get("gmcm.CycleSoundVolume.name"),
            tooltip: () => TR.Get("gmcm.CycleSoundVolume.tooltip"),
            min: 0.0f,
            max: 1.0f,
            interval: 0.01f,
            formatValue: f => f.ToString("0.00"),
            getValue: () => ModMain.Config.CycleSoundVolume,
            setValue: value => ModMain.Config.CycleSoundVolume = value
        );
        gmcm.AddBoolOption(
            mod: Main.Manifest,
            name: () => TR.Get("gmcm.AlwaysCycle.name"),
            tooltip: () => TR.Get("gmcm.AlwaysCycle.tooltip"),
            getValue: () => ModMain.Config.AlwaysCycle,
            setValue: value => ModMain.Config.AlwaysCycle = value
        );
        gmcm.AddBoolOption(
            mod: Main.Manifest,
            name: () => TR.Get("gmcm.InvertScrollDirection.name"),
            tooltip: () => TR.Get("gmcm.InvertScrollDirection.tooltip"),
            getValue: () => ModMain.Config.InvertScrollDirection,
            setValue: value => ModMain.Config.InvertScrollDirection = value
        );
    }
}
