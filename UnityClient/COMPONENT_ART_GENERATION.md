# Component art generation

The Unity component catalog mirrors `../outputs/balance_configs/component_config.csv`:
five slots, five rarities, five authored variants per slot/rarity, and 125 unique
component IDs. The same table is packaged for builds at
`Assets/_Project/Resources/Data/component_catalog_v2.csv`. Each record owns a
distinct name, effect, specialization, price, values, and narrative description.

Runtime textures live in `Assets/_Project/Resources/ComponentArt`; the five
untouched source atlases are kept in
`Assets/_Project/Editor/ArchivedArt/ComponentArt/Atlases`. The `art_source_id`
column deliberately maps the four new gameplay variants in each slot/rarity group
to its existing compatible art archetype. This keeps modular-ship anchors stable
while allowing future unique textures to be introduced one row at a time.

After editing the canonical balance table, refresh the packaged Unity copy with
`node work/cargo_config_builder/sync_component_catalog_to_unity.mjs` from the
repository root. The script rejects missing columns or any count other than 125.

## Image generation mode

Built-in OpenAI ImageGen was used. Each slot was generated as one 3-by-2 pixel-art
atlas: five filled cells in reading order and one empty bottom-right cell. The
atlases were then deterministically cropped into 512-by-512 PNG textures.

## Shared final prompt recipe

Create a production-ready 16-bit pixel-art sprite sheet for a dark nautical
roguelike Unity game. Use a single 3-column by 2-row sheet with six equal square
cells, thin dark-iron borders, deep navy-black panels, no gaps, and no text,
letters, numerals, logos, labels, captions, badges, or watermark. Fill the first
five cells in reading order and leave the sixth cell empty. Center one large,
clearly distinct ship component in each filled cell. Use crisp intentional pixel
clusters, strong silhouettes, dramatic rim light, weathered nautical materials,
and a cohesive premium retro strategy-game inventory style. Do not show people,
full ships, scenery, or extra objects.

Apply these surrounding glows in reading order, increasing in intensity:

- Common: silver gray (`#B8C0C8`)
- Uncommon: emerald green (`#48D66B`)
- Rare: azure blue (`#3B8BFF`)
- Epic: violet purple (`#B05CFF`)
- Legendary: gold orange (`#FFB52E`)

## Slot subject blocks

- Bow: oak wave-breaking ram; reef-cutter prow; gilded mermaid figurehead;
  abyssal whale-fang ram; leviathan-king armored ram.
- Sail: patched lateen sail; reinforced storm canvas; guild golden-silk sail;
  moonweave crescent sail; paired tempest-wing sail.
- Cannon: deck swivel gun; bronze long gun; chain-shot repeater; three-gun
  starfire battery; pirate-queen broadside battery.
- Plating: cross-braced ribs; iron-banded hull; gilded bulwark; moonsilver skin;
  living-tide hull.
- Instrument: brass compass; tideglass; guild route engine; lunar astrolabe;
  all-seeing sextant.

## Modular ship layers

The active light sloop is rendered from `Resources/ModularShip/light_sloop_base`
plus one transparent texture from each of the five component slots. The 25 art
archetype filenames are referenced by the balance table's `art_source_id` column.
The untouched source atlases are stored under `Resources/ModularShip/Atlases`.

Built-in OpenAI ImageGen prompt recipe:

> Create a modular Unity game ship layer in crisp premium 16-bit pixel art for a
> dark nautical roguelike. Use a fixed starboard three-quarter view with the bow
> pointing right. Preserve genuine transparency outside the subject and a
> consistent camera, scale, and anchor. For category atlases, use a 3-by-2 sheet,
> place the five rarity variants in reading order, and leave the sixth cell fully
> transparent. Apply a tight rarity edge light in silver-gray, emerald, azure,
> violet, and gold-orange order. Include only the requested ship attachment; no
> complete ship, water, backdrop, frame, text, logo, or watermark.

The neutral base prompt requests a compact dark-oak light sloop with bare mast,
plain teal structural trim, and empty mounting points, while explicitly excluding
sails, cannons, bow ornaments, hull plating, navigation instruments, scenery, and
rarity glow.
