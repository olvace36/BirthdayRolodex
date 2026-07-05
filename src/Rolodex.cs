using HarmonyLib;
using ichortower.TowerCore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace BirthdayRolodex;

public class Rolodex
{
    public class Day
    {
        public static int CycleDuration = 1000;
        public static int PauseDuration = 5000;
        public static int StaggerDistance = 12;

        public List<Billboard.BillboardEvent> Events = new();
        public int CycleIndex = 0;
        public int CycleTimer = ModMain.Config.CycleTime;
        public int SlideOffset = 0;

        public void Next() {
            if (Events.Count == 0) {
                CycleIndex = 0;
                SlideOffset = 0;
            }
            else {
                CycleIndex = (CycleIndex + 1) % Events.Count;
                CycleTimer = ModMain.Config.CycleTime;
                SlideOffset = -1 * ModMain.Config.StaggerDistance;
            }
        }

        public void Previous() {
            if (Events.Count == 0) {
                CycleIndex = 0;
                SlideOffset = 0;
            }
            else {
                CycleIndex = (CycleIndex + Events.Count - 1) % Events.Count;
                CycleTimer = ModMain.Config.CycleTime;
                SlideOffset = ModMain.Config.StaggerDistance;
            }
        }

        public void Hover() {
            if (Events.Count <= 1) {
                return;
            }
            CycleTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            if (CycleTimer <= 0) {
                Next();
            }
        }

        public void ResetTimer() {
            CycleTimer = ModMain.Config.CycleTime;
        }

        public void Click() {
            if (Events.Count <= 1) {
                return;
            }
            if (ModMain.Config.CycleSoundVolume > 0.0f) {
                _ = Game1.playSound("shwip", out ICue cue);
                ApplyCueVolume(cue, ModMain.Config.CycleSoundVolume);
            }
            Next();
            CycleTimer = ModMain.Config.PauseTime;
        }

        public void Wheel(int direction) {
            if (Events.Count <= 1) {
                return;
            }
            if (ModMain.Config.InvertScrollDirection) {
                direction *= -1;
            }
            if (ModMain.Config.CycleSoundVolume > 0.0f) {
                _ = Game1.playSound("shwip", out ICue cue);
                ApplyCueVolume(cue, ModMain.Config.CycleSoundVolume);
            }
            if (Math.Sign(direction) > 0) {
                Previous();
            }
            else {
                Next();
            }
            CycleTimer = ModMain.Config.PauseTime;
        }

        // NOTE: the Android build's ICue doesn't expose a settable "Volume"
        // property the way PC's does (compile error CS1061). Rather than
        // hardcode against one platform's shape, use reflection to apply the
        // volume however this platform's ICue supports it (a "Volume"
        // property if present, otherwise a SetVariable(string, float)
        // method commonly used by the underlying audio engine). If neither
        // is found, this silently does nothing instead of failing to build.
        private static void ApplyCueVolume(ICue cue, float multiplier)
        {
            if (cue is null) {
                return;
            }
            Type t = cue.GetType();
            PropertyInfo volumeProp = t.GetProperty("Volume",
                    BindingFlags.Public | BindingFlags.Instance);
            if (volumeProp is not null && volumeProp.CanRead && volumeProp.CanWrite) {
                float current = (float)volumeProp.GetValue(cue);
                volumeProp.SetValue(cue, current * multiplier);
                return;
            }
            MethodInfo setVariable = t.GetMethod("SetVariable",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new Type[] { typeof(string), typeof(float) }, null);
            setVariable?.Invoke(cue, new object[] { "Volume", multiplier });
        }
    }

    internal static Color UnfocusedColor = LerpColor(0f);
    internal static Color FocusedColor = Color.White;

    internal static Color LerpColor(float t)
    {
        int v = (int)Utility.Lerp(ModMain.Config.QueueBrightness, 255, t);
        return new Color(v, v, v);
    }

    private static Day[] _Data = null;
    internal static Day[] Data {
        get {
            _Data ??= Crunch(Game1.activeClickableMenu);
            return _Data;
        }
    }

    public static Day[] Crunch(IClickableMenu icm)
    {
        Day[] ret = new Day[StardewValley.WorldDate.DaysPerMonth];
        for (int i = 0; i < ret.Length; ++i) {
            ret[i] = new();
        }
        if (icm is not Billboard menu) {
            return ret;
        }
        foreach (var entry in menu.calendarDayData) {
            int idx = entry.Key - 1;
            foreach (Billboard.BillboardEvent evt in entry.Value.Events) {
                if (evt.Texture is not null) {
                    ret[idx].Events.Add(evt);
                }
            }
        }
        return ret;
    }

    public static void Draw(SpriteBatch sb, ClickableTextureComponent dayCtc, int idx)
    {
        if (Data is null || Data[idx].Events.Count == 0) {
            Log.DebugWarn("missed frame");
            return;
        }
        Day day = Data[idx];
        int count = day.Events.Count;
        int start = 0;//(day.SlideOffset != 0 ? 1 : 0);
        Color drawColor = LerpColor(0f);

        // NOTE: previously this used hardcoded pixel constants (scale 4f,
        // y-offset 28, base stagger 48) tuned to PC's calendar cell size and
        // portrait source-rect dimensions. On Android, Billboard's cell
        // bounds and/or the portrait source rect can differ in size, so
        // those fixed constants no longer match the cell and the icon draws
        // oversized/misplaced, overflowing into neighboring rows.
        //
        // Fix: derive scale and position from the actual cell bounds
        // (dayCtc.bounds) and the actual source rect of the texture being
        // drawn, so the icon always fits inside the cell regardless of
        // platform-specific sizing. Tweak MarginFactor below if you want
        // the icon a bit smaller/larger relative to the cell.
        const float MarginFactor = 0.85f; // icon height as a fraction of cell height
        Billboard.BillboardEvent firstEvt = day.Events[0];
        float scale = (dayCtc.bounds.Height * MarginFactor) / firstEvt.TextureSourceRect.Height;

        for (int i = start; i < count; ++i) {
            int g = (count - 1 - i + day.CycleIndex) % count;
            Billboard.BillboardEvent evt = day.Events[g];
            if (i == count - 1) {
                drawColor = LerpColor(1f - (float)Math.Abs(day.SlideOffset) / (float)ModMain.Config.StaggerDistance);
            }
            int iconHeight = (int)(evt.TextureSourceRect.Height * scale);
            int baseStagger = (int)(evt.TextureSourceRect.Width * scale);
            int stagger = Math.Max(0, baseStagger + day.SlideOffset - ModMain.Config.StaggerDistance * (count - 1 - i));
            int x = dayCtc.bounds.X + stagger;
            int y = dayCtc.bounds.Y + dayCtc.bounds.Height - iconHeight;
            sb.Draw(evt.Texture, new Vector2(x, y), evt.TextureSourceRect, drawColor, 0f,
                    Vector2.Zero, scale, SpriteEffects.None, 1f);
        }
        if (day.SlideOffset != 0) {
            day.SlideOffset -= Math.Sign(day.SlideOffset);
        }
    }

    public static void CleanUp()
    {
        _Data = null;
    }

    [SmapiEvent]
    public static void MenuChanged(object sender, MenuChangedEventArgs e)
    {
        if (e.OldMenu is Billboard && e.NewMenu is not Billboard) {
            CleanUp();
        }
    }

    [TargetMethod(typeof(Billboard), nameof(Billboard.draw),
                  new Type[] {typeof(SpriteBatch)})]
    [PatchType(PatchTypes.Transpiler)]
    public static IEnumerable<CodeInstruction> Billboard_draw_Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator,
            MethodBase original)
    {
        // This replaces the call to draw the birthday person's mugshot with a call to
        // our draw, which draws all of them (as well as any other day event which has a
        // specified texture, although in vanilla it's only birthdays).
        MethodInfo RolodexDraw = typeof(Rolodex).GetMethod(nameof(Rolodex.Draw),
                BindingFlags.Public | BindingFlags.Static);
        CodeMatcher cm = new(instructions);
        cm.MatchStartForward(
            new CodeMatch(OpCodes.Ldarg_1),
            new CodeMatch(i => i.opcode == OpCodes.Ldloc_S &&
                    (short)((LocalBuilder)i.operand).LocalIndex == (short)4),
            new CodeMatch(OpCodes.Callvirt))
        .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldloc_3),
            new CodeInstruction(OpCodes.Ldloc_2),
            new CodeInstruction(OpCodes.Call, RolodexDraw));
        int b = cm.Pos;
        cm.MatchEndForward(
            new CodeMatch(OpCodes.Ldc_I4_0),
            new CodeMatch(OpCodes.Ldc_R4),
            new CodeMatch(OpCodes.Callvirt))
        .RemoveInstructionsInRange(b, cm.Pos);
        return cm.InstructionEnumeration();
    }


    // this one should maybe be another transpiler, for perf concerns
    [TargetMethod(typeof(Billboard), nameof(Billboard.performHoverAction))]
    [PatchType(PatchTypes.Postfix)]
    public static void Billboard_performHoverAction_Postfix(Billboard __instance, int x, int y)
    {
        if (__instance.calendarDays is null) {
            return;
        }
        foreach (ClickableTextureComponent c in __instance.calendarDays) {
            // for Happy Birthday, which adds the player's birthday to the
            // calendar in a somewhat broken way
            if (c.myID < 1 || c.myID > StardewValley.WorldDate.DaysPerMonth) {
                continue;
            }
            if (ModMain.Config.AlwaysCycle || c.bounds.Contains(x, y)) {
                Data[c.myID - 1]?.Hover();
            }
            else {
                Data[c.myID - 1]?.ResetTimer();
            }
        }
    }


    // this one is fine, since there isn't an existing contains loop that this
    // duplicates. we have to do the work regardless
    [TargetMethod(typeof(Billboard), nameof(Billboard.receiveLeftClick))]
    [PatchType(PatchTypes.Postfix)]
    public static void Billboard_receiveLeftClick_Postfix(Billboard __instance, int x, int y)
    {
        if (__instance.calendarDays is null) {
            return;
        }
        foreach (ClickableTextureComponent c in __instance.calendarDays) {
            if (c.bounds.Contains(x, y)) {
                Data[c.myID - 1]?.Click();
                break;
            }
        }
    }

    // same, should be fine. not duplicating work
    [TargetMethod(typeof(IClickableMenu), nameof(IClickableMenu.receiveScrollWheelAction))]
    [PatchType(PatchTypes.Postfix)]
    public static void IClickableMenu_receiveScrollWheelAction_Postfix(IClickableMenu __instance, int direction)
    {
        if (__instance is not Billboard menu) {
            return;
        }
        if (menu.calendarDays is null) {
            return;
        }
        int x = Game1.getMouseX();
        int y = Game1.getMouseY();
        foreach (ClickableTextureComponent c in menu.calendarDays) {
            if (c.bounds.Contains(x, y)) {
                Data[c.myID - 1]?.Wheel(direction);
                break;
            }
        }
    }
}

