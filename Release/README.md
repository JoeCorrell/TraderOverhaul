<div align="center">

# Trader Overhaul

One custom trader UI for **Haldor**, **Hildir**, and **Bog Witch**.<br/>
Shared configs + unified buy/sell/bank workflow.

[![Version](https://img.shields.io/badge/Version-0.0.1-blue?style=for-the-badge)](https://github.com/JoeCorrell/TraderOverhaul/releases)
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

<p align="left">
<strong>Unified UI:</strong> One custom interface for Haldor, Hildir, and Bog Witch.<br/><br/>

<strong>Scoped replacement:</strong> Overrides `StoreGui` only for `Haldor`, `Hildir`, and `BogWitch`; unknown traders stay vanilla.<br/><br/>

<strong>Single workflow:</strong> Shared tab system for <strong>Buy</strong>, <strong>Sell</strong>, and <strong>Bank</strong>.<br/><br/>

<strong>Shared bank balance:</strong> One coin balance used by all 3 traders, with deposit/withdraw and total wealth display.<br/><br/>

<strong>Bank-backed transactions:</strong> Buying always spends from bank balance, and selling always deposits into bank balance.<br/><br/>

<strong>Legacy bank sync:</strong> Old per-trader bank keys are migrated/mirrored into one shared balance key.<br/><br/>

<strong>Shared buy config:</strong> All traders read from `TraderOverhaul.buy.json`.<br/><br/>

<strong>Per-trader buy behavior:</strong> Haldor is config-driven; Hildir and Bog Witch keep vanilla stock plus config overrides/additions.<br/><br/>

<strong>Shared sell config:</strong> All traders read from `TraderOverhaul.sell.json`, and only valid inventory items that meet configured stack rules appear.<br/><br/>

<strong>Progression gates:</strong> Config entries respect `must_defeated_boss` unlock requirements.<br/><br/>

<strong>Item navigation:</strong> Search box, category filters, collapsible category groups, and item detail panel.<br/><br/>

<strong>Input support:</strong> Mouse and controller support across tabs, lists, filters, and bank actions.<br/><br/>

<strong>Preview systems:</strong> Buy tab shows player equipment preview; sell tab shows the active trader preview/camera profile.<br/><br/>

<strong>Config lifecycle:</strong> Missing config files are auto-created and config entries are validated on load.
</p>

<hr/>

## Config Files

`TraderOverhaul.buy.json`<br/>
`TraderOverhaul.sell.json`

`buy.json` applies to all traders.<br/>
Hildir and Bog Witch still include their vanilla buy stock by default.<br/>
Config entries with matching prefab names override the vanilla entry values in the UI.<br/>

`sell.json` applies sell pricing/stack rules to all traders.

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

## Compatibility Notes

Targets these trader prefabs: `Haldor`, `Hildir`, `BogWitch`.<br/>
Other trader prefabs fall back to vanilla `StoreGui`.<br/>
Mods that replace or heavily patch those trader interactions may conflict.

<hr/>

## Credits

Inspired by [TradersExtended](https://thunderstore.io/c/valheim/p/shudnal/TradersExtended/)<br/>
Item and recipe source data via [Jotunn Library docs](https://valheim-modding.github.io/Jotunn/)

[![GitHub](https://img.shields.io/badge/GitHub-Issues-181717?style=for-the-badge&logo=github)](https://github.com/JoeCorrell/TraderOverhaul/issues)
[![Discord](https://img.shields.io/badge/Discord-@profmags-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.com)

</div>
