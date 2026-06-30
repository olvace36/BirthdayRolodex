# Birthday Rolodex

Do you have a lot of modded NPCs in your Stardew Valley game? Then they
probably have overlapping birthdays, and you have probably seen on the calendar
that only one NPC's sprite is displayed on the date. Well, this mod addresses
that problem by displaying all of them in a rotating queue.

When you mouseover each date, the lineup of NPC images will start automatically
rotating. You can also left-click or scroll your mouse wheel to rotate them
manually, which will pause the automatic rotation for a time.

This mod has no other effects and won't meaningfully change your gameplay, but
I think it makes looking at the calendar a bit nicer.


## Configuration

You can tweak most of this mod's behavior via its config. As usual, use of
[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) is
recommended.

- **CycleTime**: (integer, minimum 500) how long, in milliseconds, to wait
  between each movement when automatically rotating the queue. *Default: 1000*.
- **PauseTime**: (integer, minimum 1000) how long, in milliseconds, to wait
  after the player manually rotates the queue before restarting the automatic
  rotation. *Default: 4000*.
- **StaggerDistance**: (integer, 4-16) how far, in screen pixels, each
  additional icon is offset from the main draw position. *Default: 12*.
- **QueueBrightness**: (integer, 0-255) how brightly each additional icon is
  drawn. *Default: 108*.
- **CycleSoundVolume**: (float, 0.0-1.0) How loudly, relative to normal SFX
  volume, to play the page sound effect when manually rotating the queue. Set
  to 0 to disable the sound. *Default: 0.33*.
- **AlwaysCycle**: (boolean) Whether to automatically rotate the queue even
  when not hovering over the date. *Default: false*.
- **InvertScrollDirection**: (boolean) Whether to flip the default mapping of
  scroll wheel directions to queue rotation directions. *Default: false*.


## Compatibility

No known conflicts at this time. This mod doesn't ship any of its own assets,
so it should seamlessly work with any interface mods, character sprite edits,
etc.

This mod isn't specifically written for birthdays, and should work with any day
event that defines its own texture, which is how birthdays are set up to behave
on the calendar. No other day events do this, to my knowledge.


## Special Thanks

[mushymato](https://github.com/mushymato), for the snipe (and the name)
[scarlett](https://www.nexusmods.com/profile/scarlett28), for beta/stress testing
