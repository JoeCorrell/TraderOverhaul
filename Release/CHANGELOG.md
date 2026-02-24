# Changelog

## 0.0.1
- Unified Haldor, Hildir, and Bog Witch into one custom trader UI mod
- Switched to shared configs: `TraderOverhaul.buy.json` and `TraderOverhaul.sell.json`
- Preserved trader-specific preview camera profiles (including Bog Witch FOV 61)
- Updated package/plugin/docs version metadata to 0.0.1

## 1.0.19
- Added shared bank balance sync with HildirOverhaul (both traders now read/write the same bank balance)
- Internal bank persistence now migrates and mirrors legacy Haldor/Hildir keys into a shared key

## 1.0.18
- Fixed custom trader UI so it only applies to Haldor
- Other traders (Hildir and modded NPCs) now use the vanilla `StoreGui`
- Improved compatibility with mods that add their own traders

## 1.0.17
- Improved trader UI layout and visual polish

## 1.0.16
- Removed boss progression gates from treasure sell items (`Amber`, `AmberPearl`, `Ruby`, `SilverNecklace`, `GoldRuby`)
- Players can now sell treasure to Haldor at any point in progression

## 1.0.15
- Added custom UI sprites for search bar and category buttons (`SearchBarBackground.png`, `CategoryBackground.png`)
- Fixed item category classification to use enum comparison instead of string matching
- Fixed panel tinting so panels use a clean grey tint instead of brownish sprite-based coloring
- Fixed TMP font warnings by deferring TextMeshProUGUI initialization until a font is assigned
- Updated category button icons (bronze axe, troll leather helmet, wooden shield, stamina mead, bronze)
- Added a grey tint overlay on the Buy/Sell action button to match panel styling

## 1.0.14
- Added BowsBeforeHoes mod support
- Added 10 BowsBeforeHoes items to buy/sell configs (3 bows, 4 quivers, 3 arrows) with recipe-based pricing
- Doubled list panel scroll speed

## 1.0.13
- Fixed standalone bank UI cursor being locked when opened with the Z key
- Fixed bank UI registration so it behaves as a store UI (camera and input now work correctly)
- Removed Z key shortcut for bank; bank access now happens through the trader UI
- Added full controller support to the standalone bank panel

## 1.0.12
- Bug fixes and stability improvements

## 1.0.11
- Removed CurrencyPocket dependency requirement

## 1.0.10
- Bug fixes and internal maintenance updates

## 1.0.9
- Added Haldor's Bank system; bank balance funds purchases and selling deposits directly into the bank
- Added Bank tab to the trader UI and a standalone bank panel (Z key)
- Added `setbankbalance` console command (fixed registration so it now works in-game)
- Reverted bank panel to a clean text layout
- Additional bug fixes and performance improvements

## 1.0.8
- Fixed controller navigation issues with item list scrolling and selection highlighting
- Fixed controller inability to select category headers for expand/collapse
- Fixed scroll position not resetting when switching tabs
- Fixed multiple UI bugs and layout issues
- Suppressed Haldor talk bubbles while the trading UI is open
- Added `setcoins` console command for testing
- Updated UI design

## 1.0.7
- Updated trader UI visuals
- Bug fixes and interaction improvements

## 1.0.6
- Updated UI design
- Updated price generator script
- Updated config files
- Various bug fixes

## 1.0.5
- Added JsonDotNET and CurrencyPocket as required dependencies

## 1.0.4
- Updated dependency configuration and packaging metadata

## 1.0.3
- Added capes to the buy menu

## 1.0.2
- Documentation updates in `README.md`

## 1.0.1
- Documentation updates in `README.md`

## 1.0.0
- Initial release
