<div align="center">

# Trader Overhaul

One custom trader UI for **Haldor**, **Hildir**, and **Bog Witch**.<br/>
Shared configs + unified buy/sell/bank workflow.

[![Version](https://img.shields.io/badge/Version-0.0.2-blue?style=for-the-badge)](https://github.com/JoeCorrell/TraderOverhaul/releases)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4.2200+-orange?style=for-the-badge)](#requirements)
[![Traders](https://img.shields.io/badge/Traders-3-green?style=for-the-badge)](#features)

---

<p align="center">
<a href="https://ko-fi.com/profmags">
<img src="https://storage.ko-fi.com/cdn/kofi3.png?v=3" alt="Support me on Ko-fi" width="300" style="border-radius: 0;"/>
</a>
</p>

<h3>Buy</h3>
<p align="center">
<img src="https://raw.githubusercontent.com/JoeCorrell/TraderOverhaul/main/Screenshots/Buy.png" alt="Buy Tab" width="600"/>
</p>

<hr/>

<h3>Sell</h3>
<p align="center">
<img src="https://raw.githubusercontent.com/JoeCorrell/TraderOverhaul/main/Screenshots/Sell.png" alt="Sell Tab" width="600"/>
</p>

<hr/>

<h3>Bank</h3>
<p align="center">
<img src="https://raw.githubusercontent.com/JoeCorrell/TraderOverhaul/main/Screenshots/Bank.png" alt="Bank Tab" width="600"/>
</p>

<hr/>

## Features

<p align="center">
One unified custom UI for Haldor, Hildir, and Bog Witch with shared Buy, Sell, and Bank tabs; per-trader toggle to enable or disable the custom UI individually; a shared bank balance across all three traders with deposit and withdraw support; bank-backed transactions where buying spends bank coins and selling deposits into bank coins; shared config files (`TraderOverhaul.buy.json` and `TraderOverhaul.sell.json`) for all traders; Haldor config-driven buy stock while Hildir and Bog Witch keep vanilla stock plus config overrides; sell entries that only appear when inventory and stack requirements match config; progression gating through `must_defeated_boss`; built-in search, category filters, collapsible groups, and item details; full mouse/controller support; player preview on Buy, trader preview on Sell; and automatic config creation/validation when files are missing.
</p>

<hr/>

## Advanced Script

`Release/generate.py` generates the new shared config names:<br/>
`TraderOverhaul.buy.json` and `TraderOverhaul.sell.json`

Run manually:

```bash
python generate.py
```

Optional output directory:

```bash
python generate.py "C:/Path/To/BepInEx/config"
```

<hr/>

## Installation

Install BepInEx<br/>
Download latest release<br/>
Extract to `BepInEx/plugins/TraderOverhaul/`<br/>
Copy `TraderOverhaul.buy.json` and `TraderOverhaul.sell.json` into `BepInEx/config/`<br/>
Launch the game

<hr/>

## Requirements

Valheim<br/>
BepInEx 5.4.2200 or newer<br/>
ValheimModding-JsonDotNET-13.0.4

<hr/>

## Per-Trader Toggle

Each trader can be individually enabled or disabled for the custom UI via `BepInEx/config/com.profmags.traderoverhaul.cfg`:

```ini
[Custom UI]
EnableHaldor = true
EnableHildir = true
EnableBogWitch = true
```

Set any to `false` and that trader will use the vanilla `StoreGui` instead.

<hr/>

<h3>Epic Loot</h3>
<p align="center">
<img src="https://raw.githubusercontent.com/JoeCorrell/TraderOverhaul/main/Screenshots/EpicLoot.jpg" alt="Epic Loot Integration" width="600"/>
</p>

<hr/>

## Compatible Mods

**[Epic Loot](https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/)** — Full integration. Buy and sell Magic, Rare, Epic, Legendary, and Mythic rarity items with proper enchantments, stat bonuses, colored icon backgrounds, rarity sub-categories, and scaled pricing. TraderOverhaul fully overrides Epic Loot's Haldor UI.

<hr/>

## Compatibility Notes

Targets these trader prefabs: `Haldor`, `Hildir`, `BogWitch`.<br/>
Other trader prefabs fall back to vanilla `StoreGui`.<br/>
Disabled traders also fall back to vanilla `StoreGui`.<br/>
Mods that replace or heavily patch those trader interactions may conflict.

<hr/>

## Credits

Inspired by [TradersExtended](https://thunderstore.io/c/valheim/p/shudnal/TradersExtended/)<br/>
Item and recipe source data via [Jotunn Library docs](https://valheim-modding.github.io/Jotunn/)

[![GitHub](https://img.shields.io/badge/GitHub-Issues-181717?style=for-the-badge&logo=github)](https://github.com/JoeCorrell/TraderOverhaul/issues)
[![Discord](https://img.shields.io/badge/Discord-@profmags-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.com)

</div>
