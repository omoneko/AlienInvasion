# Steam Workshop publishing guide - Alien Invasion

The material and the steps for publishing a Cities: Skylines (2015) code mod to the Workshop.

## 1. Title and basics

- **Workshop title:** `Alien Invasion — War of the Worlds`
  (the in-game mod name is `Alien Invasion`; the Workshop title is better with the subtitle)
- **Visibility:** publish first as `Friends only` or `Unlisted` to test, then `Public` once it
  looks right
- **Tags:** `Mod`
- **DLC requirements:** already stated in the description - Natural Disasters recommended, and
  After Dark for the night-time glow

## 2. Preview image (the thumbnail)

- **In use:** [`preview.png`](preview.png), a cover image made to match the NuclearMeltdown mod's
  preview: 1024 by 1024, in two panels.
  - Top: the mothership from above, based on [`cover-ufo.png`](cover-ufo.png), with the title
    "ALIEN / INVASION" and the mothership icon
  - Middle: the yellow and black hazard band
  - Bottom: three tripods attacking a city, based on
    [`cover-tripods.png`](cover-tripods.png), labelled "TRIPOD ASSAULT"
  - The same tone as the author's NuclearMeltdown mod, so they read as a series.
- **Regenerating it:** [`make_preview.py`](make_preview.py), which uses Pillow. The two images,
  the title colour and the labels can all be changed and it rebuilt.
  Run it with `python docs/workshop/make_preview.py`
- Steam shows the preview small and crops it square in places. This image is already square, so
  that is fine.

## 3. Gallery images (in the order they go on the Workshop page)

1. [`screenshot-1-mothership.png`](screenshot-1-mothership.png) - the mothership arriving over
   the city centre, with the crater and the lightning
2. [`screenshot-2-night-tripod.png`](screenshot-2-night-tripod.png) - a tripod glowing red at
   night, which is where the night-time glow shows best
3. [`screenshot-3-tripod-highway.png`](screenshot-3-tripod-highway.png) - a tripod straddling an
   interchange, with everything on fire
4. [`screenshot-4-street-attack.png`](screenshot-4-street-attack.png) - the mothership and a
   tripod wrecking a street

## 4. Description

- The text, in BBCode and ready to paste:
  [`steam-description.txt`](steam-description.txt)
- In English, with the headings and lists written in Steam's BBCode (`[h1]`, `[list]`, `[b]`,
  `[i]`).

## 5. Publishing (from the in-game Content Manager)

1. Confirm it is built and deployed - run `build.ps1` and check
   `...\Addons\Mods\AlienInvasion`.
2. Start Cities: Skylines and open **Content Manager -> Mods** from the main menu.
3. Click **Share** on the `Alien Invasion` row.
4. On the upload screen:
   - Fill in the **Title** and **Description**, using the title above and the text from
     `steam-description.txt`
   - Set the **Preview image** to `docs/workshop/preview.png`
   - Choose the **Visibility** - Friends only is a good choice for testing
5. Upload. Once it appears, add the four **gallery images** on the Steam Workshop page.

> Note that a code mod can be shared straight from the Content Manager as long as everything it
> consists of - `AlienInvasion.dll` plus `Models/` and `Sounds/` - is in the mod folder. Updating
> it re-uploads through the same Share button.

## 6. Checklist before publishing

- [ ] Everything works in game: summoning, the destruction, the tripods, the contamination, the
      night-time glow, the sounds and the pause
- [ ] `preview.png` is the image you intended
- [ ] The DLC notes in the description are right (Natural Disasters and After Dark)
- [ ] Publish as Friends only or Unlisted first, then make it Public once it looks right
