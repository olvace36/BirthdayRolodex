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

        /*
         * Long-press support (for LookupAnythingMobileSearch integration):
         * a quick tap still cycles through NPCs as before. Holding the
         * touch down on the same cell past LongPressThresholdMs instead
         * opens the Lookup Anything viewer for whichever NPC is currently
         * on top of the stack (CycleIndex), without also cycling.
         *
         * This is driven by real touch-down/touch-up events
         * (receiveLeftClick / releaseLeftClick), NOT by polling
         * performHoverAction's x,y every frame - on Android, the hover
         * position reported after a touch is released doesn't clear or
         * move away, it just stays wherever the finger last was. Polling
         * position alone made the timer keep accumulating well after the
         * finger had already lifted, firing a "long press" from an
         * ordinary quick tap.
         */
        public static int LongPressThresholdMs = 400;
        private long? _pressStartTicks = null;

        public void BeginPress()
        {
            _pressStartTicks = Environment.TickCount64;
        }

        // Called on touch-up for this cell. Returns true if this was a
        // long press (already opened the lookup viewer, so the caller
        // should NOT also cycle to the next NPC).
        public bool EndPress()
        {
            if (_pressStartTicks is null) {
                return false;
            }
            long elapsed = Environment.TickCount64 - _pressStartTicks.Value;
            _pressStartTicks = null;
            if (elapsed >= LongPressThresholdMs) {
                TriggerLookup();
                return true;
            }
            return false;
        }

        private void TriggerLookup()
        {
            if (Events.Count == 0 || Rolodex.LookupApi is null) {
                return;
            }
            Billboard.BillboardEvent evt = Events[CycleIndex];
            if (evt.Type != Billboard.BillboardEventType.Birthday
                    || evt.Arguments is null || evt.Arguments.Length == 0) {
                return;
            }
            // Close the calendar outright instead of letting Lookup
            // Anything hide it underneath its own menu stack - that path
            // was leaving stale/blank calendar state and letting other
            // mods' calendar-tied tooltips bleed through. We reopen a
            // fresh Billboard ourselves once Lookup Anything closes (see
            // Rolodex.MenuChanged).
            Rolodex.PendingLookupNpcName = evt.Arguments[0];
            Rolodex.ReopenCalendarAfterLookup = true;
            Game1.activeClickableMenu?.exitThisMenu(false);
        }
    }

    internal static Color UnfocusedColor = LerpColor(0f);
    internal static Color FocusedColor = Color.White;

    // Soft dependency on LookupAnythingMobileSearch, fetched via SMAPI's
    // GetApi<T> so this mod compiles/runs fine even if that mod isn't
    // installed (LookupApi just stays null and long-press does nothing).
    internal static ILookupAnythingMobileSearchApi LookupApi = null;

    // Set by a long-press; consumed on the next tick once the calendar
    // menu has actually finished closing. Never call the lookup API
    // synchronously from inside a Billboard patch - see TriggerLookup.
    internal static string PendingLookupNpcName = null;

    // True from the moment we close the calendar for a long-press lookup
    // until we've reopened a fresh one after Lookup Anything closes.
    internal static bool ReopenCalendarAfterLookup = false;

    [SmapiEvent]
    internal static void GameLaunched(object sender, GameLaunchedEventArgs e)
    {
        LookupApi = Main.Helper.ModRegistry.GetApi<ILookupAnythingMobileSearchApi>(
                "olvace36.LookupAnythingMobileSearch");
        if (LookupApi is not null) {
            Log.DebugWarn("LookupAnythingMobileSearch found; long-press lookup enabled.");
        }
    }

    [SmapiEvent]
    internal static void UpdateTicked(object sender, UpdateTickedEventArgs e)
    {
        if (PendingLookupNpcName is null || LookupApi is null) {
            return;
        }
        // Wait until the calendar has actually finished closing before
        // opening Lookup Anything - otherwise Lookup Anything would still
        // see Billboard as the active menu and hide it underneath its own
        // menu stack instead of opening cleanly on its own.
        if (Game1.activeClickableMenu is Billboard) {
            return;
        }
        string npcName = PendingLookupNpcName;
        PendingLookupNpcName = null;
        LookupApi.ShowNpcByName(npcName);
    }

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
        const float MarginFactor = 0.85f; // icon size as a fraction of the cell
        Billboard.BillboardEvent firstEvt = day.Events[0];
        float scaleByHeight = (dayCtc.bounds.Height * MarginFactor) / firstEvt.TextureSourceRect.Height;
        float scaleByWidth = (dayCtc.bounds.Width * MarginFactor) / firstEvt.TextureSourceRect.Width;
        // Use whichever is smaller so the icon can never exceed the cell in
        // either dimension (previously height-only, which overflowed
        // sideways on non-square cells).
        float scale = Math.Min(scaleByHeight, scaleByWidth);

        for (int i = start; i < count; ++i) {
            int g = (count - 1 - i + day.CycleIndex) % count;
            Billboard.BillboardEvent evt = day.Events[g];
            if (i == count - 1) {
                drawColor = LerpColor(1f - (float)Math.Abs(day.SlideOffset) / (float)ModMain.Config.StaggerDistance);
            }
            int iconWidth = (int)(evt.TextureSourceRect.Width * scale);
            int iconHeight = (int)(evt.TextureSourceRect.Height * scale);
            // Right-align the front icon within the cell (base = rightmost
            // possible position), so staggering only ever moves it left,
            // never past either edge of the cell.
            int baseStagger = Math.Max(0, dayCtc.bounds.Width - iconWidth);
            int stagger = Math.Max(0, Math.Min(baseStagger,
                    baseStagger + day.SlideOffset - ModMain.Config.StaggerDistance * (count - 1 - i)));
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
        // We close the calendar ourselves before opening Lookup Anything
        // (see Day.TriggerLookup), so once Lookup Anything's viewer fully
        // closes back to nothing, reopen a fresh calendar automatically.
        if (ReopenCalendarAfterLookup && e.NewMenu is null && e.OldMenu is not null
                && (e.OldMenu.GetType().Namespace ?? "").StartsWith("Pathoschild.Stardew.LookupAnything")) {
            ReopenCalendarAfterLookup = false;
            Game1.activeClickableMenu = new Billboard(false);
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

    // Marks the moment a touch/click goes down on a cell. The actual
    // decision (tap = cycle, long-press = lookup) happens on release, in
    // ReleaseLeftClick_Postfix below - see the Day class's press-tracking
    // notes for why this can't be driven from performHoverAction on Android.
    [TargetMethod(typeof(Billboard), nameof(Billboard.receiveLeftClick))]
    [PatchType(PatchTypes.Postfix)]
    public static void Billboard_receiveLeftClick_Postfix(Billboard __instance, int x, int y)
    {
        if (__instance.calendarDays is null) {
            return;
        }
        foreach (ClickableTextureComponent c in __instance.calendarDays) {
            if (c.bounds.Contains(x, y)) {
                Data[c.myID - 1]?.BeginPress();
                break;
            }
        }
    }

    // Touch-up counterpart to receiveLeftClick above. A quick tap cycles
    // the NPC as before; a hold past the threshold instead opens the
    // lookup viewer (handled inside EndPress) and skips the cycle.
    [TargetMethod(typeof(IClickableMenu), nameof(IClickableMenu.releaseLeftClick))]
    [PatchType(PatchTypes.Postfix)]
    public static void IClickableMenu_releaseLeftClick_Postfix(IClickableMenu __instance, int x, int y)
    {
        if (__instance is not Billboard menu || menu.calendarDays is null) {
            return;
        }
        foreach (ClickableTextureComponent c in menu.calendarDays) {
            if (c.bounds.Contains(x, y)) {
                Day day = Data[c.myID - 1];
                if (day is not null && !day.EndPress()) {
                    day.Click();
                }
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
