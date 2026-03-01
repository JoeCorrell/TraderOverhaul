<div align="center">

# Haldor Trading Overhaul

Transform Haldor into a full-service merchant with buy/sell support, progression-based item unlocks, and an integrated bank tab.

[![Version](https://img.shields.io/badge/Version-1.0.20-blue?style=for-the-badge)](https://github.com/JoeCorrell/TraderOverhaul/releases)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4.2200+-orange?style=for-the-badge)](#requirements)
[![Items](https://img.shields.io/badge/Items-590+-green?style=for-the-badge)](#features)

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

---

## Features

Buy 590+ items from Haldor with category grouping and search<br/>
Sell items back to Haldor with balanced sell pricing<br/>
Progression-gated unlocks based on defeated bosses<br/>
Full controller support for tabs, lists, and actions<br/>
Compatible bank workflow: purchases draw from bank and selling deposits into bank

<hr/>

## Bank

Haldor can be used as a personal banker:

`Deposit` moves coins from inventory into your bank balance<br/>
`Withdraw` moves coins from bank back to inventory<br/>
Buy and sell operations use the bank flow automatically<br/>
Bank balance is synced with HildirOverhaul when both mods are installed

<hr/>

## Compatible Mods

<p align="center">
<a href="https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/">
<img src="https://raw.githubusercontent.com/JoeCorrell/TraderOverhaul/main/Screenshots/EpicLoot.jpg" alt="Epic Loot" width="300"/>
</a>
</p>

**[Epic Loot](https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/)** is fully integrated. Buy and sell Magic, Rare, Epic, Legendary, and Mythic rarity items with proper enchantments, stat bonuses, colored icon backgrounds, rarity sub-categories, and scaled pricing. TraderOverhaul fully overrides Epic Loot's Haldor UI.

<p align="center">
<a href="https://thunderstore.io/c/valheim/p/Azumatt/BowsBeforeHoes/">
<img src="https://raw.githubusercontent.com/JoeCorrell/TraderOverhaul/main/Screenshots/BowsBeforeHoes.png" alt="Bows Before Hoes" width="300"/>
</a>
</p>

**[Bows Before Hoes](https://thunderstore.io/c/valheim/p/Azumatt/BowsBeforeHoes/)** is supported. Bows, quivers, and arrows from that mod are integrated into the Haldor shop and follow the same progression model.

<hr/>

## Compatibility Notes

This mod targets **Haldor only** by prefab name.

Other traders (Hildir and modded trader NPCs) continue using vanilla `StoreGui`<br/>
Mods that replace or heavily patch Haldor may conflict<br/>
Mods that intercept `StoreGui.Show` before this mod may block the custom UI

If you see issues around trader UI opening, disable other Haldor-focused mods one by one to identify conflicts.

<hr/>

## Requirements

Valheim<br/>
BepInEx 5.4.2200 or newer

<hr/>

## Advanced Script

`generate.py` is an advanced config generation script included in this release.

Extracts item and recipe data from Valheim/Jotunn item sources<br/>
Builds from prefab data while applying aggressive exclusion rules for junk entries (attacks, spawners, VFX/SFX, creatures, debug rows, and other non-item prefabs)<br/>
Uses whitelist and validation logic to keep output focused on real tradeable items<br/>
Balances price through multiple layers: biome progression, category multipliers, rarity overrides, recipe ingredient cost analysis, crafting markup, and sell multipliers<br/>
Includes price sanity checks to flag suspiciously cheap or expensive results

Run it manually with Python if you want to regenerate buy/sell configs:

```bash
python generate.py
```

Optional custom output directory:

```bash
python generate.py "C:/Path/To/BepInEx/config"
```

<hr/>

## Installation

Install BepInEx<br/>
Download the latest release<br/>
Extract to `BepInEx/plugins/TraderOverhaul/`<br/>
Copy `TraderOverhaul.buy.json` and `TraderOverhaul.sell.json` into `BepInEx/config/`<br/>
Launch the game

<hr/>

## Credits

Inspired by [TradersExtended](https://thunderstore.io/c/valheim/p/shudnal/TradersExtended/)<br/>
Item and recipe source data via [Jotunn Library docs](https://valheim-modding.github.io/Jotunn/)

[![GitHub](https://img.shields.io/badge/GitHub-Issues-181717?style=for-the-badge&logo=github)](https://github.com/JoeCorrell/TraderOverhaul/issues)
[![Discord](https://img.shields.io/badge/Discord-@profmags-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.com)

</div>
