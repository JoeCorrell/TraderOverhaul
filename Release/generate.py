#!/usr/bin/env python3
"""
Valheim Trader Configuration Generator v1.0
============================================
- Fetches data from Jotunn documentation URLs
- Complete whitelist-based item database - every item explicitly defined
- Every item has a biome tier and boss key
- Zero fake prefabs guaranteed
- Enhanced pricing with category and rarity multipliers
- Automatic price validation and balance warnings

Output JSON format:
{
    "item_prefab": "Wood",
    "item_quantity": 50,
    "item_price": 25,
    "must_defeated_boss": ""
}
"""

import json
import re
import sys
import urllib.request
import ssl
from pathlib import Path
from html.parser import HTMLParser
from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple
from enum import Enum
from collections import defaultdict

# ============================================================================
# CONFIGURATION
# ============================================================================

SELL_MULTIPLIER = 0.30
CRAFTING_MARKUP = 1.15
MIN_PRICE = 5
MAX_PRICE = 99999

# ============================================================================
# ITEM CATEGORY MULTIPLIERS
# Applied on top of biome multiplier for better balance
# ============================================================================

CATEGORY_MULTIPLIERS = {
    'weapon_2h': 1.3,       # Two-handed weapons are premium
    'weapon_1h': 1.0,       # Standard weapons
    'bow': 1.1,             # Bows slightly more
    'crossbow': 1.25,       # Crossbows are rare
    'staff': 1.35,          # Magic staves are premium
    'shield': 0.9,          # Shields slightly less
    'armor_heavy': 1.35,    # Heavy armor premium
    'armor_light': 1.2,     # Standard armor
    'cape': 0.85,           # Capes are cheaper
    'tool': 0.8,            # Tools are utility, cheaper
    'ammo': 1.0,            # Ammo - priced by ingredients
    'food_raw': 0.7,        # Raw food is cheap
    'food_cooked': 1.0,     # Cooked food standard
    'food_mead': 1.2,       # Meads are premium consumables
    'material_common': 0.8, # Common materials
    'material_rare': 1.5,   # Rare drops (chain, cores, etc.)
    'material_boss': 2.5,   # Boss drops are very valuable
    'trophy_common': 0.9,   # Common trophies
    'trophy_rare': 1.3,     # Rare enemy trophies
    'trophy_boss': 3.0,     # Boss trophies are collectibles
    'treasure': 1.0,        # Sell-only treasure items
    'key': 2.0,             # Keys are valuable progression items
    'egg': 1.5,             # Eggs for taming/summoning
    'cosmetic': 0.7,        # Cosmetic items
}

# ============================================================================
# RARITY MULTIPLIERS
# Some items within a category are rarer than others
# ============================================================================

RARITY_OVERRIDES = {
    # Rare dungeon/special drops - these are hard to farm
    'Chain': 1.8,
    'SurtlingCore': 1.6,
    'Ectoplasm': 1.4,
    'Wisp': 1.5,
    'BlackCore': 1.7,
    'DragonTear': 2.0,
    'Thunderstone': 1.6,

    # Boss drops - unique progression items
    'HardAntler': 2.0,        # Eikthyr drop - first pickaxe
    'CryptKey': 2.2,          # The Elder drop - Swamp Key
    'Wishbone': 2.0,          # Bonemass drop - silver finder
    'YagluthDrop': 2.0,       # Yagluth drop
    'QueenDrop': 2.2,         # Seeker Queen drop
    'FaderDrop': 2.5,         # Fader drop

    # Boss summoning items
    'DragonEgg': 2.5,
    'WitheredBone': 1.3,
    'FulingTotem': 1.4,
    'GoblinTotem': 1.4,

    # Rare crafting materials
    'YmirRemains': 1.8,  # Sold by Haldor originally
    'FenrisClaw': 1.5,
    'FenrisHair': 1.4,
    'RoyalJelly': 1.6,
    'MorgenHeart': 1.7,
    'CelestialFeather': 1.8,
    'BellFragment': 2.0,

    # High-tier refined materials
    'Eitr': 1.8,  # Magic fuel is crucial
    'LinenThread': 1.2,

    # Special utility items
    'QueenBee': 1.5,
    'FishingRod': 1.3,
    'BarberKit': 1.5,
    'SaddleLox': 1.4,
    'DvergrKey': 1.8,

    # Dyrnwyn fragments (legendary weapon)
    'DyrnwynBladeFragment': 2.5,
    'DyrnwynHiltFragment': 2.5,
    'DyrnwynTipFragment': 2.5,
}

# URLs for data sources
ITEM_LIST_URL = "https://valheim-modding.github.io/Jotunn/data/objects/item-list.html"
RECIPE_LIST_URL = "https://valheim-modding.github.io/Jotunn/data/objects/recipe-list.html"

# Output directory - falls back to local if Steam path doesn't exist
CONFIG_OUTPUT_DIR = Path(r"C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/config")

# ============================================================================
# BIOME SYSTEM
# ============================================================================

class Biome(Enum):
    MEADOWS      = ("Meadows",     1.5,   "",                   1)   # Increased from 1.25
    BLACK_FOREST = ("BlackForest", 2.75,  "defeated_eikthyr",   2)   # Increased from 2.25
    SWAMP        = ("Swamp",       4.5,   "defeated_gdking",    3)   # Increased from 3.75
    MOUNTAIN     = ("Mountain",    7.5,   "defeated_bonemass",  4)   # Increased from 6.25
    PLAINS       = ("Plains",      12.0,  "defeated_dragon",    5)   # Increased from 10.0
    MISTLANDS    = ("Mistlands",   20.0,  "defeated_goblinking",6)   # Increased from 17.5
    ASHLANDS     = ("Ashlands",    32.0,  "defeated_queen",     7)   # Increased from 27.5
    DEEP_NORTH   = ("DeepNorth",   50.0,  "defeated_fader",     8)   # Increased from 43.75
    
    def __init__(self, name: str, mult: float, key: str, tier: int):
        self.biome_name = name
        self.multiplier = mult
        self.boss_key = key
        self.tier = tier

BOSS_KEYS = {
    1: "", 2: "defeated_eikthyr", 3: "defeated_gdking", 4: "defeated_bonemass",
    5: "defeated_dragon", 6: "defeated_goblinking", 7: "defeated_queen", 8: "defeated_fader"
}

# ============================================================================
# DISPLAY NAME TO PREFAB MAPPING
# Recipe ingredients use display names, we need to map to prefabs
# ============================================================================

NAME_TO_PREFAB = {
    # Woods
    'Wood': 'Wood', 'Finewood': 'FineWood', 'Fine Wood': 'FineWood',
    'Corewood': 'CoreWood', 'Core Wood': 'CoreWood',
    'Ancient Bark': 'ElderBark', 'Elder Bark': 'ElderBark',
    'Yggdrasil Wood': 'YggdrasilWood', 'Ashwood': 'Blackwood',
    
    # Stone/Minerals
    'Stone': 'Stone', 'Flint': 'Flint', 'Obsidian': 'Obsidian',
    'Crystal': 'Crystal', 'Black Marble': 'BlackMarble',
    
    # Ores/Metals
    'Copper': 'Copper', 'Tin': 'Tin', 'Bronze': 'Bronze',
    'Iron': 'Iron', 'Silver': 'Silver',
    'Black Metal': 'BlackMetal', 'Flametal': 'Flametal',
    
    # Crafting components
    'Bronze Nails': 'BronzeNails', 'Iron Nails': 'IronNails',
    'Chain': 'Chain', 'Leather Scraps': 'LeatherScraps',
    'Deer Hide': 'DeerHide', 'Troll Hide': 'TrollHide',
    'Wolf Pelt': 'WolfPelt', 'Lox Pelt': 'LoxPelt',
    'Scale Hide': 'ScaleHide', 'Linen Thread': 'LinenThread',
    'Carapace': 'Carapace', 'Chitin': 'Chitin',
    'Bone Fragments': 'BoneFragments', 'Withered Bone': 'WitheredBone',
    'Charred Bone': 'CharredBone',
    'Sulfur': 'Sulfur',

    # Monster drops
    'Greydwarf Eye': 'GreydwarfEye', 'Surtling Core': 'SurtlingCore',
    'Freeze Gland': 'FreezeGland', 'Needle': 'Needle',
    'Guck': 'Guck', 'Ooze': 'Ooze', 'Entrails': 'Entrails',
    'Bloodbag': 'Bloodbag', 'Ectoplasm': 'Ectoplasm',
    'Wolf Fang': 'WolfFang', 'Fenris Hair': 'FenrisHair', 'Fenris Claw': 'FenrisClaw',
    'Dragon Tear': 'DragonTear', 'Hard Antler': 'HardAntler',
    'Ancient Seed': 'AncientSeed', 'Thistle': 'Thistle',
    'Root': 'Root', 'Resin': 'Resin', 'Coal': 'Coal',
    'Tar': 'Tar', 'Sap': 'Sap', 'Wisp': 'Wisp',
    'Black Core': 'BlackCore', 'Refined Eitr': 'Eitr',
    'Royal Jelly': 'RoyalJelly', 'Soft Tissue': 'SoftTissue',
    'Mandible': 'Mandible', 'Bilebag': 'Bilebag',
    
    # Ashlands materials
    'Morgen Heart': 'MorgenHeart', 'Morgen Sinew': 'MorgenSinew',
    'Asksvin Hide': 'AsksvinHide', 'Ask Hide': 'AskHide',
    'Asksvin Bladder': 'AskBladder', 'Celestial Feather': 'CelestialFeather',
    'Proustite Powder': 'ProustitePowder', 'Ceramic Plate': 'CeramicPlate',
    'Bell Fragment': 'BellFragment', 'Smoke Puff': 'SmokePuff',
    'Asksvin Tail': 'AsksvinMeat', 'Volture Meat': 'VoltureMeat',
    'Bonemaw Tooth': 'BonemawSerpentTooth',
    
    # Gemstones
    'Bloodstone': 'GemstoneRed', 'Iolite': 'GemstoneBlue', 'Jade': 'GemstoneGreen',
    
    # Food items
    'Honey': 'Honey', 'Raspberries': 'Raspberry', 'Blueberries': 'Blueberries',
    'Cloudberries': 'Cloudberry', 'Mushroom': 'Mushroom',
    'Yellow Mushroom': 'MushroomYellow', 'Jotun Puffs': 'MushroomJotunPuffs',
    'Magecap': 'MushroomMagecap', 'Carrot': 'Carrot', 'Turnip': 'Turnip',
    'Onion': 'Onion', 'Barley': 'Barley', 'Barley Flour': 'BarleyFlour',
    'Bread': 'Bread', 'Bread Dough': 'BreadDough', 'Fiddlehead': 'Fiddleheadfern',
    'Dandelion': 'Dandelion', 'Feathers': 'Feathers',
    
    # Meat
    'Boar Meat': 'RawMeat', 'Raw Meat': 'RawMeat',
    'Deer Meat': 'DeerMeat', 'Cooked Deer Meat': 'CookedDeerMeat',
    'Cooked Boar Meat': 'CookedMeat', 'Neck Tail': 'NeckTail',
    'Wolf Meat': 'WolfMeat', 'Lox Meat': 'LoxMeat',
    'Serpent Meat': 'SerpentMeat', 'Cooked Serpent Meat': 'SerpentMeatCooked',
    'Seeker Meat': 'SeekerMeat', 'Cooked Seeker Meat': 'CookedBugMeat',
    'Hare Meat': 'HareMeat', 'Chicken Meat': 'ChickenMeat',
    'Egg': 'ChickenEgg', 'Cooked Fish': 'FishCooked',
    'Bear Hide': 'BjornHide', 'Bear Paw': 'BjornPaw',
    
    # Cooked foods (for recipes)
    'Turnip Stew': 'TurnipStew', 'Sausages': 'Sausages',
    'Deer Stew': 'DeerStew', 'Onion Soup': 'OnionSoup',
    'Wolf Skewer': 'WolfSkewer', 'Lox Meat Pie': 'LoxPie',
    "Queen's Jam": 'QueensJam', 'Yggdrasil Porridge': 'YggdrasilPorridge',
    'Misthare Supreme': 'Mistharesupreme', 'Scorching Medley': 'ScorchingMedley',
    
    # Trophies (used in some recipes)
    'Deer Trophy': 'TrophyDeer', 'Troll Trophy': 'TrophyTroll',
    'Draugr Elite Trophy': 'TrophyDraugrElite', 'Serpent Trophy': 'TrophySerpent',
    'Abomination Trophy': 'TrophyAbomination', 'Ghost Trophy': 'TrophyGhost',
    'Hare Trophy': 'TrophyHare', 'Volture Trophy': 'TrophyVolture',
    
    # Weapons (for upgrade recipes)
    'Ash Fang': 'BowAshlands', 'Berserkir Axes': 'AxeBerzerkr',
    'Flametal Mace': 'MaceEldner', 'Splitnir': 'SpearSplitner',
    'Ripper': 'CrossbowRipper', 'Slayer': 'THSwordSlayer',
    
    # Food items
    'Deer Meat': 'DeerMeat', 'Blue Mushroom': 'MushroomBlue',
    'Smoke Puff': 'MushroomSmokePuff', 'Toadstool': 'MushroomBzerker',
    'Volture Egg': 'VoltureEgg',
    
    # Ruby is a material
    'Ruby': 'Ruby',
    
    # Special
    'Ymir Flesh': 'YmirRemains', 'Serpent Scale': 'SerpentScale',
    'Cured Squirrel Hamstring': 'CuredSquirrelHamstring',
    'Dyrnwyn Blade Fragment': 'DyrnwynBladeFragment',
    'Dyrnwyn Hilt Fragment': 'DyrnwynHiltFragment',
    'Dyrnwyn Tip Fragment': 'DyrnwynTipFragment',
}

# ============================================================================
# PREFAB TO RECIPE NAME MAPPING
# Some items have prefab names that differ from their recipe output names
# Recipe outputs are stored without "Recipe_" prefix
# ============================================================================

PREFAB_TO_RECIPE = {
    # THSword (Two-Handed Sword) prefabs map to SwordSlayer recipes
    'THSwordSlayer': 'SwordSlayer',
    'THSwordSlayerBlood': 'SwordSlayer_Blood',
    'THSwordSlayerLightning': 'SwordSlayer_Lightning',
    'THSwordSlayerNature': 'SwordSlayer_Nature',
}

# ============================================================================
# COMPLETE ITEM DATABASE
# Format: prefab -> (biome, base_price, stack_size, sell_only)
# base_price is BEFORE biome multiplier
# Every valid tradeable item MUST be in this database
# ============================================================================

ITEM_DATABASE: Dict[str, Tuple[Biome, int, int, bool]] = {
    # ========================================================================
    # MEADOWS - Tier 1 (multiplier 1.0)
    # ========================================================================
    
    # Raw Materials
    'Wood': (Biome.MEADOWS, 2, 20, False),
    'Stone': (Biome.MEADOWS, 2, 20, False),
    'Flint': (Biome.MEADOWS, 4, 20, False),
    'Resin': (Biome.MEADOWS, 3, 20, False),
    'LeatherScraps': (Biome.MEADOWS, 5, 20, False),
    'DeerHide': (Biome.MEADOWS, 8, 20, False),
    'BoarHide': (Biome.MEADOWS, 6, 20, False),
    'BoneFragments': (Biome.MEADOWS, 4, 20, False),
    'Feathers': (Biome.MEADOWS, 3, 20, False),
    'Coal': (Biome.MEADOWS, 5, 10, False),
    
    # Food Raw
    'Raspberry': (Biome.MEADOWS, 4, 1, False),
    'Blueberries': (Biome.MEADOWS, 3, 1, False),
    'Mushroom': (Biome.MEADOWS, 6, 1, False),
    'MushroomYellow': (Biome.MEADOWS, 12, 1, False),
    'Honey': (Biome.MEADOWS, 30, 1, False),
    'RawMeat': (Biome.MEADOWS, 10, 1, False),
    'DeerMeat': (Biome.MEADOWS, 12, 1, False),
    'NeckTail': (Biome.MEADOWS, 10, 1, False),
    'Dandelion': (Biome.MEADOWS, 2, 20, False),
    'Carrot': (Biome.MEADOWS, 10, 1, False),
    'Turnip': (Biome.MEADOWS, 16, 1, False),
    'Pukeberries': (Biome.MEADOWS, 6, 20, False),
    
    # Seeds
    'CarrotSeeds': (Biome.MEADOWS, 4, 20, False),
    'TurnipSeeds': (Biome.MEADOWS, 6, 20, False),
    'Acorn': (Biome.MEADOWS, 5, 20, False),
    'BeechSeeds': (Biome.MEADOWS, 2, 20, False),
    'BirchSeeds': (Biome.MEADOWS, 4, 20, False),
    'FirCone': (Biome.MEADOWS, 3, 20, False),
    'PineCone': (Biome.MEADOWS, 3, 20, False),
    
    # Misc
    'QueenBee': (Biome.MEADOWS, 50, 10, False),
    'HardAntler': (Biome.MEADOWS, 80, 1, False),  # Eikthyr drop
    'Coins': (Biome.MEADOWS, 1, 200, True),  # Sell-only (it's currency)
    'Sparkler': (Biome.MEADOWS, 8, 20, False),
    
    # Cosmetic helmets
    'HelmetStrawHat': (Biome.MEADOWS, 15, 1, False),
    'HelmetYule': (Biome.MEADOWS, 20, 1, False),
    
    # Food Cooked
    'CookedMeat': (Biome.MEADOWS, 24, 1, False),
    'NeckTailGrilled': (Biome.MEADOWS, 12, 1, False),
    'CookedDeerMeat': (Biome.MEADOWS, 24, 1, False),
    'QueensJam': (Biome.MEADOWS, 50, 1, False),
    'CarrotSoup': (Biome.MEADOWS, 44, 1, False),
    'BoarJerky': (Biome.MEADOWS, 40, 1, False),
    'DeerStew': (Biome.MEADOWS, 56, 1, False),
    'MincedMeatSauce': (Biome.MEADOWS, 60, 1, False),
    
    # Trophies (Meadows)
    'TrophyBoar': (Biome.MEADOWS, 25, 1, False),
    'TrophyDeer': (Biome.MEADOWS, 30, 1, False),
    'TrophyNeck': (Biome.MEADOWS, 20, 1, False),
    
    # Weapons - Meadows
    'Club': (Biome.MEADOWS, 15, 1, False),
    'AxeStone': (Biome.MEADOWS, 20, 1, False),
    'AxeFlint': (Biome.MEADOWS, 35, 1, False),
    'KnifeFlint': (Biome.MEADOWS, 30, 1, False),
    'SpearFlint': (Biome.MEADOWS, 35, 1, False),
    'Bow': (Biome.MEADOWS, 40, 1, False),
    'ShieldWood': (Biome.MEADOWS, 30, 1, False),
    'ShieldWoodTower': (Biome.MEADOWS, 45, 1, False),
    'Torch': (Biome.MEADOWS, 10, 1, False),
    'Hammer': (Biome.MEADOWS, 15, 1, False),
    'Hoe': (Biome.MEADOWS, 20, 1, False),
    'PickaxeAntler': (Biome.MEADOWS, 60, 1, False),
    
    # Armor - Meadows
    'ArmorRagsChest': (Biome.MEADOWS, 15, 1, False),
    'ArmorRagsLegs': (Biome.MEADOWS, 15, 1, False),
    'ArmorLeatherChest': (Biome.MEADOWS, 40, 1, False),
    'ArmorLeatherLegs': (Biome.MEADOWS, 40, 1, False),
    'HelmetLeather': (Biome.MEADOWS, 35, 1, False),
    'CapeDeerHide': (Biome.MEADOWS, 50, 1, False),
    
    # Ammo - Meadows (stack of 20 = 1 craft, price = ingredients * markup)
    'ArrowWood': (Biome.MEADOWS, 3, 20, False),
    'ArrowFlint': (Biome.MEADOWS, 5, 20, False),
    'ArrowFire': (Biome.MEADOWS, 8, 20, False),
    
    # ========================================================================
    # BLACK FOREST - Tier 2 (multiplier 1.8)
    # Target: materials ~25-50 final, weapons ~150-250 final
    # ========================================================================
    
    # Raw Materials (base × 1.8 = final)
    'FineWood': (Biome.BLACK_FOREST, 6, 20, False),      # → 11
    'CoreWood': (Biome.BLACK_FOREST, 8, 20, False),      # → 14
    'CopperOre': (Biome.BLACK_FOREST, 10, 1, False),
    'TinOre': (Biome.BLACK_FOREST, 10, 1, False),
    'Copper': (Biome.BLACK_FOREST, 14, 15, False),       # → 25
    'Tin': (Biome.BLACK_FOREST, 14, 15, False),          # → 25
    'Bronze': (Biome.BLACK_FOREST, 25, 15, False),       # → 45
    'BronzeNails': (Biome.BLACK_FOREST, 5, 50, False),  # → 9
    'CopperScrap': (Biome.BLACK_FOREST, 12, 15, False),  # → 22
    'BronzeScrap': (Biome.BLACK_FOREST, 20, 15, False),  # → 36
    'TrollHide': (Biome.BLACK_FOREST, 22, 20, False),    # → 40
    'GreydwarfEye': (Biome.BLACK_FOREST, 6, 20, False),  # → 11
    'AncientSeed': (Biome.BLACK_FOREST, 18, 20, False),  # → 32
    'SurtlingCore': (Biome.BLACK_FOREST, 50, 10, False), # Increased - essential for smelting
    'Thistle': (Biome.BLACK_FOREST, 6, 20, False),       # → 11
    'Guck': (Biome.BLACK_FOREST, 25, 20, False),         # → 45
    'MushroomBlue': (Biome.BLACK_FOREST, 16, 1, False),  # Blue Mushroom
    
    # Meads - Black Forest tier
    'MeadStrength': (Biome.BLACK_FOREST, 70, 10, False),
    'MeadTrollPheromones': (Biome.BLACK_FOREST, 80, 10, False),
    
    # Misc
    'CryptKey': (Biome.BLACK_FOREST, 110, 1, False),     # → 198
    
    # Treasure (sell only)
    'Amber': (Biome.BLACK_FOREST, 25, 20, True),         # → 45
    'AmberPearl': (Biome.BLACK_FOREST, 40, 20, True),    # → 72
    
    # Trophies
    'TrophyGreydwarf': (Biome.BLACK_FOREST, 20, 1, False),
    'TrophyGreydwarfBrute': (Biome.BLACK_FOREST, 45, 1, False),
    'TrophyGreydwarfShaman': (Biome.BLACK_FOREST, 40, 1, False),
    'TrophySkeleton': (Biome.BLACK_FOREST, 25, 1, False),
    'TrophySkeletonPoison': (Biome.BLACK_FOREST, 45, 1, False),
    'TrophyForestTroll': (Biome.BLACK_FOREST, 80, 1, False),
    
    # Weapons - Black Forest (base × 1.8 = final ~150-250)
    'AxeBronze': (Biome.BLACK_FOREST, 90, 1, False),       # → 162
    'AxeWood': (Biome.BLACK_FOREST, 50, 1, False),         # → 90
    'SwordBronze': (Biome.BLACK_FOREST, 110, 1, False),    # → 198
    'MaceBronze': (Biome.BLACK_FOREST, 100, 1, False),     # → 180
    'SpearBronze': (Biome.BLACK_FOREST, 95, 1, False),     # → 171
    'KnifeCopper': (Biome.BLACK_FOREST, 75, 1, False),     # → 135
    'AtgeirBronze': (Biome.BLACK_FOREST, 120, 1, False),   # → 216
    'AtgeirWood': (Biome.BLACK_FOREST, 55, 1, False),      # → 99
    'BowFineWood': (Biome.BLACK_FOREST, 100, 1, False),    # → 180
    'ShieldBronzeBuckler': (Biome.BLACK_FOREST, 90, 1, False),
    'ShieldBanded': (Biome.BLACK_FOREST, 100, 1, False),
    'PickaxeBronze': (Biome.BLACK_FOREST, 85, 1, False),
    'Cultivator': (Biome.BLACK_FOREST, 80, 1, False),
    'BattleaxeWood': (Biome.BLACK_FOREST, 60, 1, False),
    
    # Armor - Black Forest
    'ArmorBronzeChest': (Biome.BLACK_FOREST, 110, 1, False),
    'ArmorBronzeLegs': (Biome.BLACK_FOREST, 110, 1, False),
    'HelmetBronze': (Biome.BLACK_FOREST, 95, 1, False),
    'ArmorTrollLeatherChest': (Biome.BLACK_FOREST, 90, 1, False),
    'ArmorTrollLeatherLegs': (Biome.BLACK_FOREST, 90, 1, False),
    'HelmetTrollLeather': (Biome.BLACK_FOREST, 80, 1, False),
    'CapeTrollHide': (Biome.BLACK_FOREST, 95, 1, False),
    
    # Ammo - Black Forest (stack of 20 = 1 craft)
    'ArrowBronze': (Biome.BLACK_FOREST, 8, 20, False),
    
    # ========================================================================
    # SWAMP - Tier 3 (multiplier 3.0)
    # Target: Iron ~75 (must be > Bronze 45), weapons ~300-500
    # ========================================================================
    
    # Raw Materials (base × 3.0 = final)
    'ElderBark': (Biome.SWAMP, 10, 20, False),     # → 30
    'IronOre': (Biome.SWAMP, 18, 1, False),
    'IronScrap': (Biome.SWAMP, 18, 15, False),     # → 54
    'Iron': (Biome.SWAMP, 40, 15, False),          # Increased to be > Bronze
    'IronNails': (Biome.SWAMP, 6, 50, False),     # → 18
    'Chain': (Biome.SWAMP, 40, 20, False),         # Increased - rare dungeon drop
    'WitheredBone': (Biome.SWAMP, 16, 20, False),  # → 48
    'Entrails': (Biome.SWAMP, 20, 20, False),      # → 30
    'Bloodbag': (Biome.SWAMP, 24, 20, False),      # → 36
    'Ooze': (Biome.SWAMP, 10, 20, False),          # → 30
    'Ectoplasm': (Biome.SWAMP, 40, 20, False),     # Increased - rare ghost drop
    'Root': (Biome.SWAMP, 18, 20, False),          # → 54
    
    # Misc
    'Wishbone': (Biome.SWAMP, 150, 1, False),      # → 450
    
    # Treasure
    'Ruby': (Biome.SWAMP, 50, 20, True),           # → 150
    
    # Food
    'TurnipStew': (Biome.SWAMP, 56, 1, False),
    'Sausages': (Biome.SWAMP, 32, 1, False),
    'BlackSoup': (Biome.SWAMP, 60, 1, False),
    'ShockolateSmootie': (Biome.SWAMP, 36, 10, False),
    'SerpentStew': (Biome.SWAMP, 100, 1, False),
    
    # Trophies
    'TrophyBlob': (Biome.SWAMP, 30, 1, False),
    'TrophyDraugr': (Biome.SWAMP, 35, 1, False),
    'TrophyDraugrElite': (Biome.SWAMP, 70, 1, False),
    'TrophyDraugrFem': (Biome.SWAMP, 35, 1, False),
    'TrophyLeech': (Biome.SWAMP, 30, 1, False),
    'TrophySurtling': (Biome.SWAMP, 45, 1, False),
    'TrophyWraith': (Biome.SWAMP, 65, 1, False),
    'TrophyAbomination': (Biome.SWAMP, 110, 1, False),
    'TrophyGhost': (Biome.SWAMP, 55, 1, False),
    
    # Misc Swamp
    'HelmetFishingHat': (Biome.SWAMP, 45, 1, False),
    'MeadSwimmer': (Biome.SWAMP, 70, 10, False),
    
    # Weapons - Swamp (base × 3.0 = final ~300-550)
    'AxeIron': (Biome.SWAMP, 110, 1, False),       # → 330
    'SwordIron': (Biome.SWAMP, 130, 1, False),     # → 390
    'MaceIron': (Biome.SWAMP, 120, 1, False),      # → 360
    'SledgeIron': (Biome.SWAMP, 160, 1, False),    # → 480
    'SpearElderbark': (Biome.SWAMP, 115, 1, False),# → 345
    'KnifeChitin': (Biome.SWAMP, 90, 1, False),    # → 270
    'AtgeirIron': (Biome.SWAMP, 145, 1, False),    # → 435
    'Battleaxe': (Biome.SWAMP, 175, 1, False),     # → 525
    'BowHuntsman': (Biome.SWAMP, 125, 1, False),   # → 375
    'ShieldIronSquare': (Biome.SWAMP, 115, 1, False),
    'ShieldIronTower': (Biome.SWAMP, 140, 1, False),
    'ShieldIronBuckler': (Biome.SWAMP, 105, 1, False),
    'PickaxeIron': (Biome.SWAMP, 100, 1, False),
    'SpearChitin': (Biome.SWAMP, 100, 1, False),
    
    # Armor - Swamp
    'ArmorIronChest': (Biome.SWAMP, 145, 1, False),
    'ArmorIronLegs': (Biome.SWAMP, 145, 1, False),
    'HelmetIron': (Biome.SWAMP, 125, 1, False),
    'ArmorRootChest': (Biome.SWAMP, 120, 1, False),
    'ArmorRootLegs': (Biome.SWAMP, 120, 1, False),
    'HelmetRoot': (Biome.SWAMP, 105, 1, False),
    
    # Ammo - Swamp (stack of 20 = 1 craft)
    'ArrowIron': (Biome.SWAMP, 12, 20, False),
    'ArrowPoison': (Biome.SWAMP, 15, 20, False),
    
    # Bombs
    'BombOoze': (Biome.SWAMP, 40, 20, False),
    
    # Meads
    'MeadHealthMinor': (Biome.SWAMP, 50, 10, False),
    'MeadHealthMedium': (Biome.SWAMP, 90, 10, False),
    'MeadStaminaMinor': (Biome.SWAMP, 50, 10, False),
    'MeadStaminaMedium': (Biome.SWAMP, 90, 10, False),
    'MeadPoisonResist': (Biome.SWAMP, 80, 10, False),
    'MeadTasty': (Biome.SWAMP, 40, 10, False),
    
    # Mead Bases
    'MeadBaseHealthMinor': (Biome.SWAMP, 32, 10, False),
    'MeadBaseHealthMedium': (Biome.SWAMP, 64, 10, False),
    'MeadBaseStaminaMinor': (Biome.SWAMP, 32, 10, False),
    'MeadBaseStaminaMedium': (Biome.SWAMP, 64, 10, False),
    'MeadBasePoisonResist': (Biome.SWAMP, 56, 10, False),
    'MeadBaseTasty': (Biome.SWAMP, 24, 10, False),
    
    # Ocean items (tier 3)
    'Chitin': (Biome.SWAMP, 28, 20, False),
    'FishRaw': (Biome.SWAMP, 20, 20, False),
    'Fish1': (Biome.SWAMP, 20, 20, False),
    'Fish2': (Biome.SWAMP, 24, 20, False),
    'Fish3': (Biome.SWAMP, 20, 20, False),
    'Fish11': (Biome.SWAMP, 24, 20, False),
    'Fish12': (Biome.SWAMP, 20, 20, False),
    'FishCooked': (Biome.SWAMP, 36, 10, False),
    'SerpentMeat': (Biome.SWAMP, 70, 1, False),
    'SerpentMeatCooked': (Biome.SWAMP, 110, 1, False),
    'SerpentScale': (Biome.SWAMP, 90, 20, False),
    'FishingRod': (Biome.SWAMP, 200, 1, False),
    'FishingBait': (Biome.SWAMP, 4, 50, False),
    'FishingBaitForest': (Biome.SWAMP, 8, 50, False),
    'FishingBaitSwamp': (Biome.SWAMP, 16, 50, False),
    'FishingBaitOcean': (Biome.SWAMP, 16, 50, False),
    
    # ========================================================================
    # MOUNTAIN - Tier 4 (multiplier 5.0)
    # ========================================================================
    # Target: Silver ~150 (must be > Iron 75), weapons ~600-900
    
    # Raw Materials (base × 5.0 = final)
    'SilverOre': (Biome.MOUNTAIN, 22, 1, False),
    'Silver': (Biome.MOUNTAIN, 30, 15, False),     # → 150 (> Iron 75)
    'Obsidian': (Biome.MOUNTAIN, 14, 20, False),   # → 70
    'Crystal': (Biome.MOUNTAIN, 20, 20, False),    # → 100
    'WolfPelt': (Biome.MOUNTAIN, 18, 20, False),   # → 90
    'WolfFang': (Biome.MOUNTAIN, 14, 20, False),   # → 70
    'WolfClaw': (Biome.MOUNTAIN, 24, 20, False),   # → 120
    'WolfMeat': (Biome.MOUNTAIN, 20, 1, False),   # → 50
    'DragonTear': (Biome.MOUNTAIN, 120, 1, False),  # Moder drop - boss material
    'DragonEgg': (Biome.MOUNTAIN, 300, 1, False),  # Increased - boss summoning item
    'FreezeGland': (Biome.MOUNTAIN, 32, 20, False),# → 160
    'FenrisHair': (Biome.MOUNTAIN, 26, 20, False), # → 130
    'FenrisClaw': (Biome.MOUNTAIN, 38, 20, False), # → 190
    
    # Food
    'Onion': (Biome.MOUNTAIN, 20, 1, False),
    'OnionSeeds': (Biome.MOUNTAIN, 8, 20, False),
    'CookedWolfMeat': (Biome.MOUNTAIN, 56, 1, False),
    'WolfJerky': (Biome.MOUNTAIN, 72, 1, False),
    'WolfSkewer': (Biome.MOUNTAIN, 34, 10, False),
    'OnionSoup': (Biome.MOUNTAIN, 60, 1, False),
    'Eyescream': (Biome.MOUNTAIN, 48, 1, False),
    'WolfMeatSkewer': (Biome.MOUNTAIN, 40, 1, False),
    
    # Misc
    'Thunderstone': (Biome.MOUNTAIN, 55, 10, False),
    'YmirRemains': (Biome.MOUNTAIN, 120, 5, False),
    
    # Treasure
    'SilverNecklace': (Biome.MOUNTAIN, 65, 20, True),
    
    # Trophies
    'TrophyWolf': (Biome.MOUNTAIN, 45, 1, False),
    'TrophyFenring': (Biome.MOUNTAIN, 80, 1, False),
    'TrophyHatchling': (Biome.MOUNTAIN, 60, 1, False),
    'TrophySGolem': (Biome.MOUNTAIN, 130, 1, False),
    'TrophyUlv': (Biome.MOUNTAIN, 70, 1, False),
    'TrophyCultist': (Biome.MOUNTAIN, 65, 1, False),
    'TrophySerpent': (Biome.MOUNTAIN, 115, 1, False),
    'TrophyFrostTroll': (Biome.MOUNTAIN, 90, 1, False),
    'TrophyHare': (Biome.MOUNTAIN, 50, 1, False),
    
    # Meads - Mountain tier
    'MeadHasty': (Biome.MOUNTAIN, 90, 10, False),
    'MeadLightfoot': (Biome.MOUNTAIN, 90, 10, False),
    
    # Weapons - Mountain (base × 5.0 = final ~600-1000)
    'SwordSilver': (Biome.MOUNTAIN, 140, 1, False),    # → 700
    'MaceSilver': (Biome.MOUNTAIN, 135, 1, False),     # → 675
    'SpearWolfFang': (Biome.MOUNTAIN, 125, 1, False),  # → 625
    'KnifesilverKnife': (Biome.MOUNTAIN, 110, 1, False),
    'KnifeSilver': (Biome.MOUNTAIN, 110, 1, False),    # → 550
    'BowDraugrFang': (Biome.MOUNTAIN, 160, 1, False),  # → 800
    'ShieldSilver': (Biome.MOUNTAIN, 125, 1, False),
    'ShieldSerpentscale': (Biome.MOUNTAIN, 150, 1, False),
    'BattleaxeCrystal': (Biome.MOUNTAIN, 175, 1, False),
    'SledgeStagbreaker': (Biome.MOUNTAIN, 90, 1, False),
    
    # Armor - Mountain
    'ArmorWolfChest': (Biome.MOUNTAIN, 155, 1, False),
    'ArmorWolfLegs': (Biome.MOUNTAIN, 155, 1, False),
    'HelmetDrake': (Biome.MOUNTAIN, 140, 1, False),
    'ArmorFenringChest': (Biome.MOUNTAIN, 165, 1, False),
    'ArmorFenringLegs': (Biome.MOUNTAIN, 165, 1, False),
    'HelmetFenring': (Biome.MOUNTAIN, 150, 1, False),
    'CapeWolf': (Biome.MOUNTAIN, 120, 1, False),
    
    # Ammo - Mountain (stack of 20 = 1 craft)
    'ArrowSilver': (Biome.MOUNTAIN, 15, 20, False),
    'ArrowObsidian': (Biome.MOUNTAIN, 12, 20, False),
    'ArrowFrost': (Biome.MOUNTAIN, 18, 20, False),
    
    # Meads
    'MeadFrostResist': (Biome.MOUNTAIN, 80, 10, False),
    'MeadBaseFrostResist': (Biome.MOUNTAIN, 56, 10, False),
    
    # Fish
    'Fish6': (Biome.MOUNTAIN, 36, 20, False),
    'Fish7': (Biome.MOUNTAIN, 32, 20, False),
    'FishingBaitCave': (Biome.MOUNTAIN, 24, 50, False),
    
    # ========================================================================
    # PLAINS - Tier 5 (multiplier 8.0)
    # Target: BlackMetal ~280 (> Silver 150), weapons ~1200-1800
    # ========================================================================
    
    # Raw Materials (base × 8.0 = final)
    'BlackMetalScrap': (Biome.PLAINS, 25, 15, False),  # → 200
    'BlackMetal': (Biome.PLAINS, 35, 15, False),       # → 280 (> Silver 150)
    'Tar': (Biome.PLAINS, 18, 20, False),              # → 144
    'LoxPelt': (Biome.PLAINS, 56, 20, False),          # → 224
    'LoxMeat': (Biome.PLAINS, 24, 1, False),          # → 96
    'Needle': (Biome.PLAINS, 14, 20, False),           # → 112
    'Barley': (Biome.PLAINS, 20, 1, False),           # → 80
    'BarleyFlour': (Biome.PLAINS, 24, 1, False),
    'Flax': (Biome.PLAINS, 20, 1, False),
    'LinenThread': (Biome.PLAINS, 16, 20, False),
    'Cloudberry': (Biome.PLAINS, 10, 1, False),
    'FulingTotem': (Biome.PLAINS, 65, 10, False),
    'GoblinTotem': (Biome.PLAINS, 65, 10, False),
    'BjornHide': (Biome.PLAINS, 20, 20, False),
    'BjornMeat': (Biome.PLAINS, 20, 1, False),
    'BjornPaw': (Biome.PLAINS, 16, 20, False),
    'Jute': (Biome.PLAINS, 20, 20, False),
    'YagluthDrop': (Biome.PLAINS, 250, 1, False),   # Increased - Yagluth drop
    
    # Treasure
    'GoldRuby': (Biome.PLAINS, 150, 20, True),
    
    # Food
    'CookedLoxMeat': (Biome.PLAINS, 110, 1, False),
    'LoxPie': (Biome.PLAINS, 170, 1, False),
    'BloodPudding': (Biome.PLAINS, 160, 1, False),
    'Bread': (Biome.PLAINS, 90, 1, False),
    'FishAndBread': (Biome.PLAINS, 110, 1, False),
    'CookedBearMeat': (Biome.PLAINS, 110, 1, False),
    'BreadDough': (Biome.PLAINS, 30, 10, False),
    'BarleyWine': (Biome.PLAINS, 140, 1, False),
    'BarleyWineBase': (Biome.PLAINS, 100, 10, False),
    'FishWraps': (Biome.PLAINS, 130, 1, False),
    
    # Trophies
    'TrophyDeathsquito': (Biome.PLAINS, 60, 1, False),
    'TrophyGoblin': (Biome.PLAINS, 55, 1, False),
    'TrophyGoblinBrute': (Biome.PLAINS, 110, 1, False),
    'TrophyGoblinShaman': (Biome.PLAINS, 100, 1, False),
    'TrophyGrowth': (Biome.PLAINS, 140, 1, False),
    'TrophyLox': (Biome.PLAINS, 150, 1, False),
    'TrophyBjorn': (Biome.PLAINS, 130, 1, False),
    'TrophyBjornUndead': (Biome.PLAINS, 150, 1, False),
    
    # Misc Plains
    'SaddleLox': (Biome.PLAINS, 120, 1, False),
    'ScytheHandle': (Biome.PLAINS, 45, 1, False),
    'MushroomBzerker': (Biome.PLAINS, 50, 1, False),  # Toadstool
    'MeadBzerker': (Biome.PLAINS, 110, 10, False),
    'HelmetBerserkerHood': (Biome.PLAINS, 145, 1, False),
    
    # Weapons - Plains (base × 8.0 = final ~1100-1600)
    'SwordBlackmetal': (Biome.PLAINS, 175, 1, False),     # → 1400
    'AxeBlackMetal': (Biome.PLAINS, 165, 1, False),       # → 1320
    'KnifeBlackMetal': (Biome.PLAINS, 140, 1, False),     # → 1120
    'AtgeirBlackmetal': (Biome.PLAINS, 185, 1, False),    # → 1480
    'BattleaxeBlackmetal': (Biome.PLAINS, 200, 1, False), # → 1600
    'MaceNeedle': (Biome.PLAINS, 160, 1, False),          # → 1280
    'ShieldBlackmetal': (Biome.PLAINS, 155, 1, False),
    'ShieldBlackmetalTower': (Biome.PLAINS, 180, 1, False),
    
    # Armor - Plains
    'ArmorPaddedCuirass': (Biome.PLAINS, 185, 1, False),
    'ArmorPaddedGreaves': (Biome.PLAINS, 185, 1, False),
    'HelmetPadded': (Biome.PLAINS, 165, 1, False),
    'ArmorBerserkerChest': (Biome.PLAINS, 155, 1, False),
    'ArmorBerserkerLegs': (Biome.PLAINS, 155, 1, False),
    'HelmetBerserker': (Biome.PLAINS, 140, 1, False),
    'CapeFeather': (Biome.PLAINS, 140, 1, False),
    'CapeLinen': (Biome.PLAINS, 120, 1, False),
    'CapeLox': (Biome.PLAINS, 150, 1, False),
    
    # Ammo - Plains (stack of 20 = 1 craft)
    'ArrowNeedle': (Biome.PLAINS, 20, 20, False),
    
    # Fish
    'Fish5': (Biome.PLAINS, 100, 20, False),
    'Fish8': (Biome.PLAINS, 70, 20, False),
    'FishingBaitPlains': (Biome.PLAINS, 24, 50, False),
    
    # ========================================================================
    # MISTLANDS - Tier 6 (multiplier 14.0)
    # ========================================================================
    
    # Raw Materials
    'Carapace': (Biome.MISTLANDS, 14, 20, False),
    'ScaleHide': (Biome.MISTLANDS, 20, 20, False),
    'Eitr': (Biome.MISTLANDS, 65, 15, False),    # Increased - magic fuel is crucial
    'Sap': (Biome.MISTLANDS, 12, 20, False),
    'SoftTissue': (Biome.MISTLANDS, 24, 20, False),
    'YggdrasilWood': (Biome.MISTLANDS, 32, 20, False),
    'RoyalJelly': (Biome.MISTLANDS, 70, 20, False),
    'DvergrNeedle': (Biome.MISTLANDS, 22, 20, False),
    'BlackCore': (Biome.MISTLANDS, 65, 10, False),
    'DvergrKeyFragment': (Biome.MISTLANDS, 100, 5, False),
    'DvergrKey': (Biome.MISTLANDS, 160, 1, False),
    'Wisp': (Biome.MISTLANDS, 30, 20, False),
    'Mandible': (Biome.MISTLANDS, 26, 20, False),
    
    # Food
    'MushroomJotunPuffs': (Biome.MISTLANDS, 56, 1, False),
    'MushroomMagecap': (Biome.MISTLANDS, 80, 1, False),
    'BugMeat': (Biome.MISTLANDS, 64, 1, False),
    'SeekerMeat': (Biome.MISTLANDS, 80, 1, False),
    'ChickenEgg': (Biome.MISTLANDS, 24, 20, False),
    'ChickenMeat': (Biome.MISTLANDS, 56, 1, False),
    'HareMeat': (Biome.MISTLANDS, 52, 1, False),
    'JuteBlue': (Biome.MISTLANDS, 60, 20, False),
    'JuteRed': (Biome.MISTLANDS, 60, 20, False),
    'CookedBugMeat': (Biome.MISTLANDS, 110, 1, False),
    'CookedHareMeat': (Biome.MISTLANDS, 104, 1, False),
    'CookedChickenMeat': (Biome.MISTLANDS, 110, 1, False),
    'CookedEgg': (Biome.MISTLANDS, 96, 1, False),
    'MushroomOmelette': (Biome.MISTLANDS, 160, 1, False),
    'HoneyGlazedChicken': (Biome.MISTLANDS, 180, 1, False),
    'SeekerAspic': (Biome.MISTLANDS, 200, 1, False),
    'Mistharesupreme': (Biome.MISTLANDS, 190, 1, False),
    'MagicallyStuffedShroom': (Biome.MISTLANDS, 210, 1, False),
    'MeatPlatter': (Biome.MISTLANDS, 220, 1, False),
    'YggdrasilPorridge': (Biome.MISTLANDS, 176, 1, False),
    
    # Misc
    'QueenDrop': (Biome.MISTLANDS, 400, 1, False),  # Adjusted - Seeker Queen drop
    'BarberKit': (Biome.MISTLANDS, 200, 1, False),
    
    # Trophies
    'TrophySeeker': (Biome.MISTLANDS, 65, 1, False),
    'TrophySeekerBrute': (Biome.MISTLANDS, 110, 1, False),
    'TrophyTick': (Biome.MISTLANDS, 55, 1, False),
    'TrophyGjall': (Biome.MISTLANDS, 140, 1, False),
    'TrophyDvergr': (Biome.MISTLANDS, 100, 1, False),
    
    # Misc Mistlands
    'MushroomSmokePuff': (Biome.MISTLANDS, 40, 1, False),
    'MeadBugRepellent': (Biome.MISTLANDS, 110, 10, False),
    'HelmetDverger': (Biome.MISTLANDS, 120, 1, False),  # Dverger Circlet
    
    # Weapons - Mistlands (base × 14.0 = final ~1800-2800)
    'SwordMistwalker': (Biome.MISTLANDS, 165, 1, False),     # → 2310
    'AxeJotunBane': (Biome.MISTLANDS, 155, 1, False),        # → 2170
    'KnifeMistwalker': (Biome.MISTLANDS, 135, 1, False),     # → 1890
    'KnifeDvergr': (Biome.MISTLANDS, 130, 1, False),         # → 1820
    'AtgeirHimminAfl': (Biome.MISTLANDS, 175, 1, False),     # → 2450
    'SledgeDemolisher': (Biome.MISTLANDS, 185, 1, False),    # → 2590
    'BowSpineSnap': (Biome.MISTLANDS, 170, 1, False),        # → 2380
    'CrossbowArbalest': (Biome.MISTLANDS, 190, 1, False),    # → 2660
    'ShieldCarapace': (Biome.MISTLANDS, 150, 1, False),
    'ShieldCarapaceBuckler': (Biome.MISTLANDS, 135, 1, False),
    'StaffShield': (Biome.MISTLANDS, 155, 1, False),
    'StaffFireball': (Biome.MISTLANDS, 170, 1, False),
    'StaffIceShards': (Biome.MISTLANDS, 170, 1, False),
    'StaffClusterbomb': (Biome.MISTLANDS, 180, 1, False),
    'StaffGreenRoots': (Biome.MISTLANDS, 175, 1, False),
    'StaffSkeleton': (Biome.MISTLANDS, 165, 1, False),
    'StaffRedTroll': (Biome.MISTLANDS, 155, 1, False),
    'Krom': (Biome.MISTLANDS, 195, 1, False),
    'BattleaxeSkullSplittur': (Biome.MISTLANDS, 200, 1, False),
    'THSwordSkullSplitter': (Biome.MISTLANDS, 205, 1, False),
    'SkollAndHati': (Biome.MISTLANDS, 210, 1, False),
    
    # Armor - Mistlands
    'ArmorCarapaceChest': (Biome.MISTLANDS, 175, 1, False),
    'ArmorCarapaceLegs': (Biome.MISTLANDS, 175, 1, False),
    'HelmetCarapace': (Biome.MISTLANDS, 155, 1, False),
    'ArmorMageChest': (Biome.MISTLANDS, 180, 1, False),
    'ArmorMageLegs': (Biome.MISTLANDS, 180, 1, False),
    'HelmetMage': (Biome.MISTLANDS, 160, 1, False),
    
    # Ammo - Mistlands (stack of 20 = 1 craft)
    'ArrowCarapace': (Biome.MISTLANDS, 18, 20, False),
    'BoltBone': (Biome.MISTLANDS, 12, 20, False),
    'BoltIron': (Biome.MISTLANDS, 15, 20, False),
    'BoltBlackmetal': (Biome.MISTLANDS, 20, 20, False),
    'BoltCarapace': (Biome.MISTLANDS, 22, 20, False),
    
    # Bombs
    'BombBile': (Biome.MISTLANDS, 40, 20, False),
    
    # Fish
    'Fish4_cave': (Biome.MISTLANDS, 44, 20, False),
    'Fish9': (Biome.MISTLANDS, 80, 20, False),
    'FishAnglerRaw': (Biome.MISTLANDS, 44, 20, False),
    'FishingBaitMistlands': (Biome.MISTLANDS, 20, 50, False),
    
    # Meads
    'MeadHealthLingering': (Biome.MISTLANDS, 100, 10, False),
    'MeadHealthMajor': (Biome.MISTLANDS, 200, 10, False),
    'MeadStaminaLingering': (Biome.MISTLANDS, 180, 10, False),
    'MeadStaminaMajor': (Biome.MISTLANDS, 200, 10, False),
    'MeadEitrMinor': (Biome.MISTLANDS, 160, 10, False),
    'MeadBaseHealthLingering': (Biome.MISTLANDS, 120, 10, False),
    'MeadBaseHealthMajor': (Biome.MISTLANDS, 140, 10, False),
    'MeadBaseStaminaLingering': (Biome.MISTLANDS, 120, 10, False),
    'MeadBaseStaminaMajor': (Biome.MISTLANDS, 140, 10, False),
    'MeadBaseEitrMinor': (Biome.MISTLANDS, 110, 10, False),
    
    # ========================================================================
    # ASHLANDS - Tier 7 (multiplier 22.0)
    # ========================================================================
    
    # Raw Materials
    'FlametalOre': (Biome.ASHLANDS, 18, 1, False),
    'FlametalOreNew': (Biome.ASHLANDS, 18, 1, False),
    'Flametal': (Biome.ASHLANDS, 38, 15, False),
    'FlametalNew': (Biome.ASHLANDS, 38, 15, False),
    'CharredBone': (Biome.ASHLANDS, 12, 20, False),
    'Sulfur': (Biome.ASHLANDS, 18, 20, False),             # Ashlands material
    'CharredCogwheel': (Biome.ASHLANDS, 28, 20, False),
    'MorgenHeart': (Biome.ASHLANDS, 90, 10, False),
    'MorgenSinew': (Biome.ASHLANDS, 40, 20, False),
    'AsksvinHide': (Biome.ASHLANDS, 32, 20, False),
    'AskHide': (Biome.ASHLANDS, 32, 20, False),
    'AskBladder': (Biome.ASHLANDS, 24, 20, False),
    'CelestialFeather': (Biome.ASHLANDS, 65, 10, False),
    'ProustitePowder': (Biome.ASHLANDS, 28, 20, False),
    'GemstoneBlue': (Biome.ASHLANDS, 75, 10, False),
    'GemstoneGreen': (Biome.ASHLANDS, 75, 10, False),
    'GemstoneRed': (Biome.ASHLANDS, 75, 10, False),
    'BellFragment': (Biome.ASHLANDS, 150, 5, False),
    'Bilebag': (Biome.ASHLANDS, 26, 20, False),
    'Blackwood': (Biome.ASHLANDS, 18, 20, False),
    'CharcoalResin': (Biome.ASHLANDS, 10, 20, False),
    'Fiddleheadfern': (Biome.ASHLANDS, 10, 20, False),
    'CeramicPlate': (Biome.ASHLANDS, 16, 20, False),
    'CandleWick': (Biome.ASHLANDS, 12, 20, False),
    'GlowingMushroom': (Biome.ASHLANDS, 20, 1, False),
    'Charredskull': (Biome.ASHLANDS, 20, 20, False),
    'SmokePuff': (Biome.ASHLANDS, 12, 20, False),
    'BlackMarble': (Biome.ASHLANDS, 10, 20, False),
    'VoltureMeat': (Biome.ASHLANDS, 32, 1, False),
    'AsksvinMeat': (Biome.ASHLANDS, 30, 1, False),
    'AsksvinEgg': (Biome.ASHLANDS, 50, 10, False),
    'Bell': (Biome.ASHLANDS, 200, 1, False),
    'BarrelRings': (Biome.ASHLANDS, 22, 20, False),
    
    # Food - Ashlands
    'CookedVoltureMeat': (Biome.ASHLANDS, 144, 10, False),
    'CookedAsksvinMeat': (Biome.ASHLANDS, 150, 10, False),
    'SpicyMarmalade': (Biome.ASHLANDS, 110, 10, False),
    'FierySvinstew': (Biome.ASHLANDS, 130, 10, False),
    'ScorchingMedley': (Biome.ASHLANDS, 120, 10, False),
    'PiquantPie': (Biome.ASHLANDS, 320, 10, False),
    'SizzlingBerryBroth': (Biome.ASHLANDS, 210, 10, False),
    'RoastedCrustPie': (Biome.ASHLANDS, 280, 10, False),
    'SparklingShroomshake': (Biome.ASHLANDS, 250, 10, False),
    'CookedBjornMeat': (Biome.ASHLANDS, 150, 10, False),
    'CookedBoneMawSerpentMeat': (Biome.ASHLANDS, 170, 10, False),
    
    # Misc
    'FaderDrop': (Biome.ASHLANDS, 500, 1, False),   # Adjusted - Fader boss drop
    'VoltureEgg': (Biome.ASHLANDS, 45, 10, False),
    'CuredSquirrelHamstring': (Biome.ASHLANDS, 30, 20, False),
    'DyrnwynBladeFragment': (Biome.ASHLANDS, 200, 5, False),
    'DyrnwynHiltFragment': (Biome.ASHLANDS, 200, 5, False),
    'DyrnwynTipFragment': (Biome.ASHLANDS, 200, 5, False),
    
    # Trophies
    'TrophyAsksvin': (Biome.ASHLANDS, 170, 1, False),
    'TrophyCharredArcher': (Biome.ASHLANDS, 85, 1, False),
    'TrophyCharredMage': (Biome.ASHLANDS, 100, 1, False),
    'TrophyCharredMelee': (Biome.ASHLANDS, 80, 1, False),
    'TrophyBonemawSerpent': (Biome.ASHLANDS, 140, 1, False),
    'TrophyMorgen': (Biome.ASHLANDS, 165, 1, False),
    'TrophyFallenValkyrie': (Biome.ASHLANDS, 200, 1, False),
    'TrophyVolture': (Biome.ASHLANDS, 120, 1, False),
    
    # Bonemaw
    'BoneMawSerpentMeat': (Biome.ASHLANDS, 70, 1, False),
    'BonemawSerpentScale': (Biome.ASHLANDS, 45, 20, False),
    'BonemawSerpentTooth': (Biome.ASHLANDS, 55, 20, False),
    
    # Asksvin Parts
    'AsksvinCarrionNeck': (Biome.ASHLANDS, 22, 20, False),
    'AsksvinCarrionPelvic': (Biome.ASHLANDS, 25, 20, False),
    'AsksvinCarrionRibcage': (Biome.ASHLANDS, 28, 20, False),
    'AsksvinCarrionSkull': (Biome.ASHLANDS, 30, 20, False),
    
    # Weapons - Ashlands (base × 22.0 = final ~3500-5500)
    'SwordFire': (Biome.ASHLANDS, 185, 1, False),            # → 4070
    'SwordNiedhogg': (Biome.ASHLANDS, 200, 1, False),        # → 4400
    'SwordNiedhoggBlood': (Biome.ASHLANDS, 1400, 1, False),  # Variant - requires base weapon
    'SwordNiedhoggLightning': (Biome.ASHLANDS, 1400, 1, False),
    'SwordNiedhoggNature': (Biome.ASHLANDS, 1400, 1, False),
    'AxeBerzerkr': (Biome.ASHLANDS, 190, 1, False),          # → 4180
    'AxeBerzerkrBlood': (Biome.ASHLANDS, 1500, 1, False),    # Variant - requires base weapon
    'AxeBerzerkrLightning': (Biome.ASHLANDS, 1500, 1, False),
    'AxeBerzerkrNature': (Biome.ASHLANDS, 1500, 1, False),
    'KnifeSkelonbone': (Biome.ASHLANDS, 155, 1, False),      # → 3410
    'MaceEldner': (Biome.ASHLANDS, 180, 1, False),           # → 3960
    'MaceEldnerBlood': (Biome.ASHLANDS, 1400, 1, False),     # Variant - requires base weapon
    'MaceEldnerLightning': (Biome.ASHLANDS, 1400, 1, False),
    'MaceEldnerNature': (Biome.ASHLANDS, 1400, 1, False),
    'SpearSplitner': (Biome.ASHLANDS, 170, 1, False),        # → 3740
    'SpearSplitner_Blood': (Biome.ASHLANDS, 1300, 1, False),  # Variant - requires base weapon
    'SpearSplitner_Lightning': (Biome.ASHLANDS, 1300, 1, False),
    'SpearSplitner_Nature': (Biome.ASHLANDS, 1300, 1, False),
    'BowAshlands': (Biome.ASHLANDS, 188, 1, False),          # → 4136
    'BowAshlandsBlood': (Biome.ASHLANDS, 1400, 1, False),    # Variant - requires base weapon
    'BowAshlandsRoot': (Biome.ASHLANDS, 1400, 1, False),
    'BowAshlandsStorm': (Biome.ASHLANDS, 1400, 1, False),
    'CrossbowRipper': (Biome.ASHLANDS, 210, 1, False),       # → 4620
    'CrossbowRipperBlood': (Biome.ASHLANDS, 1500, 1, False), # Variant - requires base weapon
    'CrossbowRipperLightning': (Biome.ASHLANDS, 1500, 1, False),
    'CrossbowRipperNature': (Biome.ASHLANDS, 1500, 1, False),
    'ShieldFlametal': (Biome.ASHLANDS, 175, 1, False),
    'ShieldFlametalTower': (Biome.ASHLANDS, 195, 1, False),
    'StaffLightning': (Biome.ASHLANDS, 180, 1, False),
    'TheScarecrow': (Biome.ASHLANDS, 175, 1, False),
    'THSwordSlayer': (Biome.ASHLANDS, 220, 1, False),
    'THSwordSlayerBlood': (Biome.ASHLANDS, 2200, 1, False),   # Variant - requires base weapon
    'THSwordSlayerLightning': (Biome.ASHLANDS, 2200, 1, False),
    'THSwordSlayerNature': (Biome.ASHLANDS, 2200, 1, False),
    
    # Armor - Ashlands
    'ArmorFlametalChest': (Biome.ASHLANDS, 200, 1, False),
    'ArmorFlametalLegs': (Biome.ASHLANDS, 200, 1, False),
    'HelmetFlametal': (Biome.ASHLANDS, 180, 1, False),
    'ArmorAshlandsMediumChest': (Biome.ASHLANDS, 185, 1, False),
    'ArmorAshlandsMediumlegs': (Biome.ASHLANDS, 185, 1, False),
    'HelmetAshlandsMedium': (Biome.ASHLANDS, 165, 1, False),
    'HelmetAshlandsMediumHood': (Biome.ASHLANDS, 160, 1, False),
    'ArmorMageChest_Ashlands': (Biome.ASHLANDS, 195, 1, False),
    'ArmorMageLegs_Ashlands': (Biome.ASHLANDS, 195, 1, False),
    'HelmetMage_Ashlands': (Biome.ASHLANDS, 175, 1, False),
    'ArmorBerserkerUndeadChest': (Biome.ASHLANDS, 180, 1, False),
    'ArmorBerserkerUndeadLegs': (Biome.ASHLANDS, 180, 1, False),
    'HelmetBerserkerUndead': (Biome.ASHLANDS, 160, 1, False),
    'CapeAsh': (Biome.ASHLANDS, 145, 1, False),
    'CapeAsksvin': (Biome.ASHLANDS, 155, 1, False),
    
    # Ammo - Ashlands (stack of 20 = 1 craft)
    'ArrowCharred': (Biome.ASHLANDS, 25, 20, False),
    'BoltCharred': (Biome.ASHLANDS, 28, 20, False),
    
    # Bombs - Ashlands
    'BombLava': (Biome.ASHLANDS, 90, 20, False),
    'BombSmoke': (Biome.ASHLANDS, 35, 20, False),
    'BombSiege': (Biome.ASHLANDS, 160, 10, False),
    'Catapult_ammo': (Biome.ASHLANDS, 80, 10, False),
    
    # Fish - Ashlands
    'Fish10': (Biome.ASHLANDS, 180, 20, False),
    'FishingBaitAshlands': (Biome.ASHLANDS, 60, 50, False),
    
    # Meads - Ashlands
    'MeadEitrLingering': (Biome.ASHLANDS, 240, 10, False),
    'MeadBaseEitrLingering': (Biome.ASHLANDS, 170, 10, False),
    
    # ========================================================================
    # DEEP NORTH - Tier 8 (placeholder for future content)
    # ========================================================================
    'FishingBaitDeepNorth': (Biome.DEEP_NORTH, 80, 50, False),
    
    # ========================================================================
    # COSMETICS (Hildir items) - various tiers, sell only or restricted
    # ========================================================================
    'ArmorDress1': (Biome.MEADOWS, 60, 1, True),
    'ArmorDress2': (Biome.MEADOWS, 60, 1, True),
    'ArmorDress3': (Biome.MEADOWS, 70, 1, True),
    'ArmorDress4': (Biome.MEADOWS, 60, 1, True),
    'ArmorDress5': (Biome.MEADOWS, 60, 1, True),
    'ArmorDress6': (Biome.MEADOWS, 70, 1, True),
    'ArmorDress7': (Biome.MEADOWS, 60, 1, True),
    'ArmorDress8': (Biome.MEADOWS, 60, 1, True),
    'ArmorDress9': (Biome.MEADOWS, 70, 1, True),
    'ArmorDress10': (Biome.MEADOWS, 50, 1, True),
    'ArmorTunic1': (Biome.MEADOWS, 60, 1, True),
    'ArmorTunic2': (Biome.MEADOWS, 60, 1, True),
    'ArmorTunic3': (Biome.MEADOWS, 70, 1, True),
    'ArmorTunic4': (Biome.MEADOWS, 60, 1, True),
    'ArmorTunic5': (Biome.MEADOWS, 60, 1, True),
    'ArmorTunic6': (Biome.MEADOWS, 70, 1, True),
    'ArmorTunic7': (Biome.MEADOWS, 60, 1, True),
    'ArmorTunic8': (Biome.MEADOWS, 60, 1, True),
    'ArmorTunic9': (Biome.MEADOWS, 70, 1, True),
    'ArmorTunic10': (Biome.MEADOWS, 50, 1, True),
    'ArmorHarvester1': (Biome.MEADOWS, 50, 1, True),
    'ArmorHarvester2': (Biome.MEADOWS, 50, 1, True),
    
    # Fireworks
    'FireworksRocket_Blue': (Biome.PLAINS, 18, 20, False),
    'FireworksRocket_Cyan': (Biome.PLAINS, 18, 20, False),
    'FireworksRocket_Green': (Biome.PLAINS, 18, 20, False),
    'FireworksRocket_Purple': (Biome.PLAINS, 18, 20, False),
    'FireworksRocket_Red': (Biome.PLAINS, 18, 20, False),
    'FireworksRocket_White': (Biome.PLAINS, 18, 20, False),
    'FireworksRocket_Yellow': (Biome.PLAINS, 18, 20, False),
    
    # Blob Bombs (craftable)
    'BombBlob_Frost': (Biome.MOUNTAIN, 65, 20, False),
    'BombBlob_Poison': (Biome.SWAMP, 55, 20, False),
    'BombBlob_PoisonElite': (Biome.SWAMP, 75, 20, False),
    'BombBlob_Lava': (Biome.ASHLANDS, 95, 20, False),
    'BombBlob_Tar': (Biome.PLAINS, 60, 20, False),
    'BlobVial': (Biome.MISTLANDS, 45, 20, False),
    
    # Special items
    'BeltStrength': (Biome.MOUNTAIN, 400, 1, False),
    
    # ========================================================================
    # BOSS TROPHIES - Sell only, high value
    # ========================================================================
    'TrophyEikthyr': (Biome.MEADOWS, 500, 1, True),
    'TrophyTheElder': (Biome.BLACK_FOREST, 600, 1, True),
    'TrophyBonemass': (Biome.SWAMP, 700, 1, True),
    'TrophyDragonQueen': (Biome.MOUNTAIN, 800, 1, True),
    'TrophyGoblinKing': (Biome.PLAINS, 900, 1, True),
    'TrophySeekerQueen': (Biome.MISTLANDS, 1000, 1, True),
    'TrophyFader': (Biome.ASHLANDS, 1100, 1, True),
    
    # Special/Event items
    'CapeOdin': (Biome.MEADOWS, 100, 1, False),
    'HelmetOdin': (Biome.MEADOWS, 100, 1, False),
    'CapeTest': (Biome.MEADOWS, 10, 1, True),  # Test item, sell only
}

# ============================================================================
# MOD SUPPORT
# Items added by mods won't appear in Jotunn docs, so we define them here.
# Format: 'PrefabName': (Biome, base_price, stack_size, sell_only, item_type_hint)
# item_type_hint is passed to get_item_category() to ensure correct multiplier.
# ============================================================================

ENABLED_MODS = {
    'BowsBeforeHoes': True,  # https://thunderstore.io/c/valheim/p/Azumatt/BowsBeforeHoes/
}

MOD_DATABASES = {}

# Mod item recipes — used by process_mod_items() to compute ingredient-based
# prices the same way process_items() does for vanilla items.
# Format: 'PrefabName': {'ingredients': [('IngredientPrefab', qty), ...], 'amount': output}
# Ingredient prefabs must be in ITEM_DATABASE; unknowns fall back to 10/unit.
MOD_RECIPES = {}

# ─── BowsBeforeHoes by Azumatt ───────────────────────────────────────────────
# Biome matches the mod's Trade.RequiredGlobalKey boss key.
# base_price is a floor guarantee (same role as in ITEM_DATABASE for vanilla);
# the recipe-derived ingredient cost typically drives the final price higher.
# item_type_hint='Bow'  → get_item_category() returns 'bow'  (×1.1 multiplier)
# item_type_hint='Ammo' → recipe uses ammo pricing (no divide by craft output)
# RoundLog is a BBH-internal prefab with no DB entry; approximated as CoreWood.
if ENABLED_MODS.get('BowsBeforeHoes'):
    MOD_DATABASES['BowsBeforeHoes'] = {
        # Bows
        'BBH_BlackForest_Bow': (Biome.BLACK_FOREST, 80, 1, False, 'Bow'),
        'BBH_Surtling_Bow':    (Biome.SWAMP,         60, 1, False, 'Bow'),
        'BBH_Seeker_Bow':      (Biome.MISTLANDS,      80, 1, False, 'Bow'),
        # Quivers (equippable ammo-belt utility slots)
        'BBH_BlackForest_Quiver': (Biome.BLACK_FOREST, 80, 1, False, ''),
        'BBH_OdinPlus_Quiver':    (Biome.ASHLANDS,      40, 1, False, ''),
        'BBH_PlainsLox_Quiver':   (Biome.PLAINS,        60, 1, False, ''),
        'BBH_Seeker_Quiver':      (Biome.MISTLANDS,     80, 1, False, ''),
        # Arrows — stack 20 matches vanilla arrow bundle size
        'TorchArrow':     (Biome.SWAMP,     5, 20, False, 'Ammo'),
        'SeekerArrow':    (Biome.ASHLANDS,  5, 20, False, 'Ammo'),
        'MistTorchArrow': (Biome.ASHLANDS,  5, 20, False, 'Ammo'),
    }

    # Actual crafting recipes from the BowsBeforeHoes plugin source.
    # RoundLog (BBH internal prefab) approximated as CoreWood (same tier, similar value).
    MOD_RECIPES.update({
        'BBH_BlackForest_Bow':    {'ingredients': [('CoreWood', 2), ('DeerHide', 1), ('TrollHide', 1)],        'amount': 1},
        'BBH_Surtling_Bow':       {'ingredients': [('TrophyTheElder', 1), ('SurtlingCore', 10), ('Eitr', 25)], 'amount': 1},
        'BBH_Seeker_Bow':         {'ingredients': [('YggdrasilWood', 3), ('Carapace', 3), ('Wisp', 1)],        'amount': 1},
        'BBH_BlackForest_Quiver': {'ingredients': [('HardAntler', 1), ('DeerHide', 3)],                        'amount': 1},
        'BBH_OdinPlus_Quiver':    {'ingredients': [('Thunderstone', 10), ('YggdrasilWood', 3), ('FlametalNew', 2)], 'amount': 1},
        'BBH_PlainsLox_Quiver':   {'ingredients': [('FineWood', 15), ('SerpentScale', 5), ('LoxPelt', 3)],     'amount': 1},
        'BBH_Seeker_Quiver':      {'ingredients': [('YggdrasilWood', 1), ('Carapace', 1), ('Mandible', 4)],    'amount': 1},
        'TorchArrow':             {'ingredients': [('Wood', 1), ('DeerHide', 2)],                              'amount': 20},
        'SeekerArrow':            {'ingredients': [('Wood', 1), ('Mandible', 1)],                              'amount': 20},
        'MistTorchArrow':         {'ingredients': [('Wood', 1), ('Eitr', 2)],                                  'amount': 20},
    })

# Items that should never appear (not real items, attacks, spawners, etc.)
INVALID_PREFAB_PATTERNS = [
    # Attacks and abilities
    r'_attack', r'_Attack', r'_melee', r'_bite', r'_claw', r'_stomp', r'_slam',
    r'_charge', r'_swing', r'_swipe', r'_combo', r'_headbutt', r'_pounce',
    r'_projectile', r'_breath', r'_nova', r'_beam', r'_meteor', r'_wave', r'_aoe',
    r'_flee', r'_taunt', r'_scream', r'_screech', r'_spit', r'_spin', r'_sting',
    r'_summon', r'_spawn', r'_rage', r'_roar', r'_howl', r'_call', r'_fissure',
    r'_sweep', r'_jump', r'_swoop', r'_wing', r'_ground', r'_turn', r'_ram',
    r'_feint', r'_thrust', r'_volley',
    
    # VFX/SFX
    r'^sfx_', r'^vfx_', r'^fx_', r'_sfx$', r'_vfx$', r'_fx$', r'_particle', r'_effect',
    
    # Corpses/Ragdolls
    r'_ragdoll', r'Ragdoll', r'_corpse', r'_dead', r'_debris',
    
    # World objects
    r'Spawner', r'spawner', r'_spawn$', r'Location', r'^Pickable_', r'^MineRock',
    r'^Vegvisir', r'^Runestone', r'^piece_', r'^wood_', r'^stone_wall',
    
    # Test/Debug
    r'^_', r'_OLD$', r'^Test', r'^Debug', r'^Player$',
    
    # Creatures (not items)
    r'^Greydwarf$', r'^Troll$', r'^Skeleton$', r'^Draugr$', r'^Blob$', r'^Ghost$',
    r'^Wraith$', r'^Wolf$', r'^Boar$', r'^Deer$', r'^Neck$', r'^Serpent$', r'^Lox$',
    r'^Deathsquito$', r'^Goblin$', r'^Fuling', r'^Seeker$', r'^SeekerBrute$',
    r'^Tick$', r'^Gjall$', r'^Hare$', r'^Hen$', r'^Chicken$', r'^Abomination$',
    r'^Surtling$', r'^Leech$', r'^Ulv$', r'^Fenring$', r'^StoneGolem$', r'^Bat$',
    r'^Crow$', r'^Charred$', r'^Morgen$', r'^Asksvin$', r'^Volture$', r'^Fallen',
    r'^Dvergr$', r'^Dverger$', r'^BlobElite$', r'^Drake$', r'^Eikthyr$', r'^gd_king$',
    r'^GDKing', r'^Bonemass$', r'^Dragon$', r'^GoblinKing$', r'^SeekerQueen$',
    r'^Fader$', r'^TheElder$', r'^BogWitch',
    
    # Creature attacks (prefixed with creature name)
    r'^GoblinBrute_', r'^GoblinShaman_', r'^Draugr_', r'^Skeleton_', r'^Troll_',
    r'^Wolf_', r'^Greydwarf_', r'^Boar_', r'^Deer_', r'^Neck_', r'^charred_',
    r'^dvergr_', r'^seeker_', r'^gjall_', r'^lox_', r'^bat_', r'^blob_', r'^leech_',
    r'^wraith_', r'^surtling_', r'^imp_', r'^fenring_', r'^ghost_', r'^asksvin_',
    r'^Asksvin_', r'^morgen_', r'^volture_', r'^fallenvalkyrie_', r'^bonemaw_',
    r'^serpent_', r'^bjorn_', r'^unbjorn_', r'^babyseeker_', r'^bonemass_',
    r'^BonemawSerpent_', r'^blobelite_', r'^blobLava_', r'^blobtar_', r'^blob_frost_',
    
    # Equipment worn by enemies (not player items)
    r'^Charred_', r'^DvergerHair', r'^DvergerSuit', r'^StoneGolem_',
    
    # Misc invalid
    r'^chest_hildir', r'_Material$',
    
    # Customization (beards, hair)
    r'^Beard', r'^Hair', r'^BeardNone',
]

# Items exclusive to Hilda (should not be sold by Haldor)
HILDA_EXCLUSIVES = {
    # Dresses/Tunics
    'ArmorDress1', 'ArmorDress2', 'ArmorDress3', 'ArmorDress4',
    'ArmorDress5', 'ArmorDress6', 'ArmorDress7', 'ArmorDress8',
    'ArmorDress9', 'ArmorDress10',
    'ArmorTunic1', 'ArmorTunic2', 'ArmorTunic3', 'ArmorTunic4',
    'ArmorTunic5', 'ArmorTunic6', 'ArmorTunic7', 'ArmorTunic8',
    'ArmorTunic9', 'ArmorTunic10',
    # Caps/Hats
    'HelmetHat1', 'HelmetHat2', 'HelmetHat3', 'HelmetHat4', 'HelmetHat5',
    'HelmetHat6', 'HelmetHat7', 'HelmetHat8', 'HelmetHat9', 'HelmetHat10',
    'HelmetPointyHat', 'HelmetStrawHat', 'HelmetMidsummerCrown',
    # Odin cosmetics
    'CapeOdin', 'HelmetOdin',
    # Any item with Hildir cosmetic keywords in name (caught by pattern below)
}

# ============================================================================
# HTML PARSERS
# ============================================================================

class ItemParser(HTMLParser):
    """Parse item list HTML table."""
    def __init__(self):
        super().__init__()
        self.items = []
        self.in_tbody = False
        self.in_row = False
        self.in_cell = False
        self.current_row = []
        self.cell_content = ""
    
    def handle_starttag(self, tag, attrs):
        if tag == 'tbody':
            self.in_tbody = True
        elif tag == 'tr' and self.in_tbody:
            self.in_row = True
            self.current_row = []
        elif tag == 'td' and self.in_row:
            self.in_cell = True
            self.cell_content = ""
    
    def handle_endtag(self, tag):
        if tag == 'tbody':
            self.in_tbody = False
        elif tag == 'tr' and self.in_row:
            self.in_row = False
            if len(self.current_row) >= 5:
                prefab = re.sub(r'<br>.*$', '', self.current_row[0]).strip()
                self.items.append({
                    'prefab': prefab,
                    'name': self.current_row[3].strip(),
                    'type': self.current_row[4].strip()
                })
        elif tag == 'td' and self.in_cell:
            self.in_cell = False
            self.current_row.append(self.cell_content)
    
    def handle_data(self, data):
        if self.in_cell:
            self.cell_content += data


class RecipeParser(HTMLParser):
    """Parse recipe list HTML table."""
    def __init__(self):
        super().__init__()
        self.recipes = []
        self.in_tbody = False
        self.in_row = False
        self.in_cell = False
        self.in_li = False
        self.current_row = []
        self.cell_content = ""
        self.ingredients = []
        self.level1_done = False
    
    def handle_starttag(self, tag, attrs):
        if tag == 'tbody':
            self.in_tbody = True
        elif tag == 'tr' and self.in_tbody:
            self.in_row = True
            self.current_row = []
            self.ingredients = []
            self.level1_done = False
        elif tag == 'td' and self.in_row:
            self.in_cell = True
            self.cell_content = ""
        elif tag == 'li' and self.in_cell:
            self.in_li = True
            self.cell_content = ""
    
    def handle_endtag(self, tag):
        if tag == 'tbody':
            self.in_tbody = False
        elif tag == 'tr' and self.in_row:
            self.in_row = False
            if len(self.current_row) >= 4:
                output = re.sub(r'^Recipe_', '', self.current_row[0].strip())
                try:
                    amount = int(self.current_row[3]) if self.current_row[3].strip() else 1
                except ValueError:
                    amount = 1
                self.recipes.append({
                    'output': output,
                    'amount': max(amount, 1),
                    'ingredients': self.ingredients.copy()
                })
        elif tag == 'td' and self.in_cell:
            self.in_cell = False
            self.current_row.append(self.cell_content)
        elif tag == 'li' and self.in_li:
            self.in_li = False
            if not self.level1_done:
                m = re.match(r'^(\d+)\s+(.+)$', self.cell_content.strip())
                if m:
                    self.ingredients.append((m.group(2).strip(), int(m.group(1))))
        elif tag == 'ul':
            self.level1_done = True
    
    def handle_data(self, data):
        if self.in_li or self.in_cell:
            self.cell_content += data


# ============================================================================
# CORE FUNCTIONS
# ============================================================================

def fetch_url(url: str) -> str:
    """Fetch HTML content from URL."""
    print(f"      Fetching: {url}")
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    with urllib.request.urlopen(req, context=ctx, timeout=30) as response:
        return response.read().decode('utf-8')


def is_valid_prefab(prefab: str) -> bool:
    """Check if prefab passes pattern filtering (catches fake items not in DB)."""
    # Check invalid patterns
    for pattern in INVALID_PREFAB_PATTERNS:
        if re.search(pattern, prefab, re.IGNORECASE):
            return False
    # Check Hilda exclusives (dresses, tunics)
    if prefab in HILDA_EXCLUSIVES:
        return False
    # Exclude Hilda items by pattern
    if any(k in prefab for k in ['Dress', 'Scarf', 'Tunic', 'HelmetHat', 'PointyHat', 'StrawHat']):
        return False
    return True


def get_item_data(prefab: str) -> Optional[Tuple[Biome, int, int, bool]]:
    """Get item data from database. Returns None if item not found."""
    return ITEM_DATABASE.get(prefab)


def get_item_category(prefab: str, item_type: str) -> str:
    """Determine item category for pricing multiplier."""
    prefab_lower = prefab.lower()
    type_lower = item_type.lower() if item_type else ''

    # Boss trophies
    if prefab.startswith('Trophy') and any(b in prefab for b in ['Eikthyr', 'TheElder', 'Bonemass', 'DragonQueen', 'GoblinKing', 'SeekerQueen', 'Fader']):
        return 'trophy_boss'

    # Keys
    if 'key' in prefab_lower or prefab in ['CryptKey', 'DvergrKey']:
        return 'key'

    # Boss drops
    if prefab in ['HardAntler', 'CryptKey', 'Wishbone', 'DragonTear', 'YagluthDrop', 'QueenDrop', 'FaderDrop']:
        return 'material_boss'

    # Eggs (summoning/taming)
    if 'egg' in prefab_lower:
        return 'egg'

    # Staves
    if prefab.startswith('Staff'):
        return 'staff'

    # Crossbows
    if 'crossbow' in prefab_lower:
        return 'crossbow'

    # Bows
    if prefab.startswith('Bow') or 'bow' in type_lower:
        return 'bow'

    # Two-handed weapons
    if prefab.startswith('Battleaxe') or prefab.startswith('Sledge') or prefab.startswith('THSword') or 'atgeir' in prefab_lower:
        return 'weapon_2h'

    # Shields
    if prefab.startswith('Shield'):
        return 'shield'

    # Tools
    if any(t in prefab for t in ['Pickaxe', 'Cultivator', 'Hammer', 'Hoe', 'FishingRod']):
        return 'tool'

    # Capes
    if prefab.startswith('Cape'):
        return 'cape'

    # Armor detection
    if prefab.startswith('Armor') or prefab.startswith('Helmet'):
        if any(h in prefab for h in ['Padded', 'Iron', 'Bronze', 'Carapace', 'Flametal']):
            return 'armor_heavy'
        return 'armor_light'

    # Ammo
    if prefab.startswith('Arrow') or prefab.startswith('Bolt'):
        return 'ammo'

    # Meads
    if prefab.startswith('Mead'):
        return 'food_mead'

    # Cooked food
    if prefab.startswith('Cooked') or any(f in prefab for f in ['Stew', 'Soup', 'Pie', 'Jam', 'Jerky', 'Sausages', 'Pudding', 'Wrap', 'Omelette', 'Porridge']):
        return 'food_cooked'

    # Raw food
    if any(f in prefab for f in ['Meat', 'Mushroom', 'Berry', 'Carrot', 'Turnip', 'Onion', 'Barley', 'Honey', 'Fish']):
        return 'food_raw'

    # Trophies
    if prefab.startswith('Trophy'):
        if any(r in prefab for r in ['Brute', 'Elite', 'Shaman', 'Troll', 'Serpent', 'Golem', 'Abomination', 'Gjall', 'Lox', 'Growth', 'Morgen', 'Valkyrie']):
            return 'trophy_rare'
        return 'trophy_common'

    # Rare materials
    if prefab in ['Chain', 'SurtlingCore', 'Ectoplasm', 'Wisp', 'BlackCore', 'Eitr', 'RoyalJelly', 'Thunderstone']:
        return 'material_rare'

    # Cosmetic
    if any(c in prefab for c in ['Dress', 'Tunic', 'Harvester', 'StrawHat', 'Yule']):
        return 'cosmetic'

    # Treasure (sell only items)
    if prefab in ['Amber', 'AmberPearl', 'Ruby', 'Coins', 'SilverNecklace', 'GoldRuby']:
        return 'treasure'

    # One-handed weapons (default for weapon-like items)
    if any(w in prefab for w in ['Sword', 'Mace', 'Axe', 'Knife', 'Spear', 'Club']):
        return 'weapon_1h'

    # Default to common material
    return 'material_common'


def calculate_price(base_price: int, biome: Biome, prefab: str = '', item_type: str = '') -> int:
    """Calculate final price with biome, category, and rarity multipliers."""
    # Start with biome multiplier
    price = base_price * biome.multiplier

    # Apply category multiplier
    category = get_item_category(prefab, item_type)
    category_mult = CATEGORY_MULTIPLIERS.get(category, 1.0)
    price *= category_mult

    # Apply rarity override if exists
    rarity_mult = RARITY_OVERRIDES.get(prefab, 1.0)
    price *= rarity_mult

    # Clamp to valid range
    return min(MAX_PRICE, max(MIN_PRICE, int(price)))


def get_biome_from_recipe(recipe: dict, name_to_prefab: dict) -> Optional[Biome]:
    """
    Determine biome tier from recipe ingredients.
    Returns the highest-tier biome among all ingredients.
    """
    if not recipe or not recipe.get('ingredients'):
        return None
    
    highest_biome = None
    highest_order = -1
    
    # Biome order for comparison
    biome_order = {
        Biome.MEADOWS: 0,
        Biome.BLACK_FOREST: 1,
        Biome.SWAMP: 2,
        Biome.MOUNTAIN: 3,
        Biome.PLAINS: 4,
        Biome.MISTLANDS: 5,
        Biome.ASHLANDS: 6,
        Biome.DEEP_NORTH: 7,
    }
    
    for ing_name, ing_qty in recipe['ingredients']:
        # Convert display name to prefab
        ing_prefab = name_to_prefab.get(ing_name)
        if not ing_prefab:
            # Try case-insensitive match
            for name, pf in name_to_prefab.items():
                if name.lower() == ing_name.lower():
                    ing_prefab = pf
                    break
        
        if ing_prefab and ing_prefab in ITEM_DATABASE:
            ing_biome, _, _, _ = ITEM_DATABASE[ing_prefab]
            order = biome_order.get(ing_biome, 0)
            if order > highest_order:
                highest_order = order
                highest_biome = ing_biome
    
    return highest_biome


@dataclass
class ProcessedItem:
    prefab: str
    name: str
    item_type: str
    biome: Biome
    buy_price: int
    sell_price: int
    stack: int
    boss_key: str
    buyable: bool
    sellable: bool


def process_items(items_html: str, recipes_html: str) -> Tuple[List[ProcessedItem], List[ProcessedItem]]:
    """Process items and recipes to generate trader configs."""
    
    print("\n[1/5] Parsing items...")
    item_parser = ItemParser()
    item_parser.feed(items_html)
    raw_items = item_parser.items
    print(f"      Found {len(raw_items)} raw entries")
    
    print("\n[2/5] Parsing recipes...")
    recipe_parser = RecipeParser()
    recipe_parser.feed(recipes_html)
    recipes = {r['output']: r for r in recipe_parser.recipes}
    print(f"      Found {len(recipes)} recipes")
    
    print("\n[3/5] Filtering and validating...")
    valid_items = []
    rejected_count = 0
    not_in_db_count = 0
    auto_categorized = 0
    
    for item in raw_items:
        prefab = item['prefab']
        
        # First check pattern filter
        if not is_valid_prefab(prefab):
            rejected_count += 1
            continue
        
        # Check if in our database (whitelist approach)
        data = get_item_data(prefab)
        if data is None:
            # Try to auto-categorize from recipe
            recipe = recipes.get(prefab)
            if recipe:
                auto_biome = get_biome_from_recipe(recipe, NAME_TO_PREFAB)
                if auto_biome:
                    # Calculate base price from recipe ingredients
                    ingredient_cost = 0
                    for ing_name, ing_qty in recipe['ingredients']:
                        ing_prefab = NAME_TO_PREFAB.get(ing_name)
                        if not ing_prefab:
                            for name, pf in NAME_TO_PREFAB.items():
                                if name.lower() == ing_name.lower():
                                    ing_prefab = pf
                                    break
                        if ing_prefab and ing_prefab in ITEM_DATABASE:
                            _, ing_base, _, _ = ITEM_DATABASE[ing_prefab]
                            ingredient_cost += ing_base * ing_qty
                        else:
                            ingredient_cost += 10 * ing_qty  # Fallback
                    
                    # Set base price from ingredients
                    base_price = max(10, int(ingredient_cost * CRAFTING_MARKUP / max(1, recipe.get('amount', 1))))
                    
                    # Add to database dynamically
                    ITEM_DATABASE[prefab] = (auto_biome, base_price, 1, False)
                    auto_categorized += 1
                    valid_items.append(item)
                    continue
            
            not_in_db_count += 1
            # Uncomment below to see what's being skipped:
            # print(f"      Not in DB: {prefab} (type: {item['type']})")
            continue
        
        valid_items.append(item)
    
    print(f"      Valid: {len(valid_items)} | Auto-categorized: {auto_categorized} | Rejected: {rejected_count} | Skipped: {not_in_db_count}")
    
    print("\n[4/5] Building price index...")
    # Build prices from database
    prices = {}
    for prefab, (biome, base_price, stack, sell_only) in ITEM_DATABASE.items():
        prices[prefab] = calculate_price(base_price, biome, prefab, '')
    print(f"      Indexed {len(prices)} prices")
    
    print("\n[5/5] Processing items...")
    buy_items = []
    sell_items = []
    
    for item in valid_items:
        prefab = item['prefab']
        data = get_item_data(prefab)
        if data is None:
            continue
        
        biome, base_price, stack, sell_only = data
        item_type = item.get('type', '')

        # Calculate base price with all multipliers (biome, category, rarity)
        base_buy_price = calculate_price(base_price, biome, prefab, item_type)

        # Check if item has recipe - calculate from BASE ingredient prices
        # Use PREFAB_TO_RECIPE mapping if prefab differs from recipe output name
        recipe_name = PREFAB_TO_RECIPE.get(prefab, prefab)
        recipe = recipes.get(recipe_name)

        # Only skip recipe pricing for RAW materials (no recipe exists)
        # Everything craftable uses recipe-based pricing!
        NO_RECIPE_PRICING = {
            # Raw ores (mined, no recipe)
            'CopperOre', 'TinOre', 'IronOre', 'IronScrap', 'SilverOre',
            'BlackMetalScrap', 'FlametalOre', 'FlametalOreNew',
            # Raw wood (chopped, no recipe)
            'Wood', 'FineWood', 'CoreWood', 'ElderBark', 'YggdrasilWood', 'Blackwood',
            # Raw drops (monster/world drops, no recipe)
            'Stone', 'Flint', 'LeatherScraps', 'DeerHide', 'TrollHide', 'WolfPelt',
            'LoxPelt', 'ScaleHide', 'Carapace', 'Chitin', 'Feathers', 'Resin',
            'Coal', 'Chain', 'Ectoplasm', 'Guck', 'Honey', 'Thistle', 'Obsidian',
            'Crystal', 'Needle', 'BoneFragments', 'WitheredBone', 'SurtlingCore',
            'Entrails', 'Bloodbag', 'FreezeGland', 'DragonTear', 'Wisp', 'Sap',
            'SoftTissue', 'RoyalJelly', 'BlackCore', 'Mandible', 'Thunderstone',
            # Elemental weapon variants (require base weapon - use high base prices)
            'AxeBerzerkrBlood', 'AxeBerzerkrLightning', 'AxeBerzerkrNature',
            'MaceEldnerBlood', 'MaceEldnerLightning', 'MaceEldnerNature',
            'SpearSplitner_Blood', 'SpearSplitner_Lightning', 'SpearSplitner_Nature',
            'BowAshlandsBlood', 'BowAshlandsRoot', 'BowAshlandsStorm',
            'CrossbowRipperBlood', 'CrossbowRipperLightning', 'CrossbowRipperNature',
            'THSwordSlayerBlood', 'THSwordSlayerLightning', 'THSwordSlayerNature',
            'SwordNiedhoggBlood', 'SwordNiedhoggLightning', 'SwordNiedhoggNature',
        }

        if prefab in NO_RECIPE_PRICING:
            buy_price = base_buy_price
        elif recipe and recipe['ingredients']:
            ingredient_base_cost = 0
            found_all = True

            for ing_display_name, ing_qty in recipe['ingredients']:
                # Convert display name to prefab
                ing_prefab = NAME_TO_PREFAB.get(ing_display_name)
                if not ing_prefab:
                    # Try case-insensitive
                    for name, pf in NAME_TO_PREFAB.items():
                        if name.lower() == ing_display_name.lower():
                            ing_prefab = pf
                            break

                if ing_prefab and ing_prefab in ITEM_DATABASE:
                    _, ing_base_price, _, _ = ITEM_DATABASE[ing_prefab]
                    # Use RAW BASE price - no biome multiplier yet!
                    ingredient_base_cost += ing_base_price * ing_qty
                else:
                    # Unknown ingredient - use small fallback
                    found_all = False
                    ingredient_base_cost += 10 * ing_qty

            # Recipe price = base ingredient cost * markup / output amount
            # Then apply THIS ITEM's biome multiplier and category multiplier
            output_amount = recipe.get('amount', 1)
            category = get_item_category(prefab, item_type)

            # For ammo: stack size = recipe output, so don't divide by output
            # This makes stack price = total ingredient cost * markup
            if category == 'ammo':
                recipe_base = int(ingredient_base_cost * CRAFTING_MARKUP)
            else:
                recipe_base = int(ingredient_base_cost * CRAFTING_MARKUP / max(1, output_amount))

            # Apply biome and category multipliers to recipe price
            category_mult = CATEGORY_MULTIPLIERS.get(category, 1.0)
            rarity_mult = RARITY_OVERRIDES.get(prefab, 1.0)
            recipe_price = int(recipe_base * biome.multiplier * category_mult * rarity_mult)

            # Use recipe price if we found all ingredients and it's higher
            if found_all:
                buy_price = max(base_buy_price, recipe_price)
            else:
                buy_price = base_buy_price
        else:
            buy_price = base_buy_price

        # Final price cap
        buy_price = min(MAX_PRICE, max(MIN_PRICE, buy_price))
        
        sell_price = max(1, int(buy_price * SELL_MULTIPLIER))
        boss_key = biome.boss_key

        # Treasure items can be sold regardless of boss progression
        if sell_only and prefab in ['Amber', 'AmberPearl', 'Ruby', 'SilverNecklace', 'GoldRuby']:
            boss_key = ""
        
        processed = ProcessedItem(
            prefab=prefab,
            name=item['name'],
            item_type=item['type'],
            biome=biome,
            buy_price=buy_price,
            sell_price=sell_price,
            stack=stack,
            boss_key=boss_key,
            buyable=not sell_only,
            sellable=True
        )
        
        if processed.buyable:
            buy_items.append(processed)
        if processed.sellable:
            sell_items.append(processed)
    
    print(f"      Buy: {len(buy_items)}, Sell: {len(sell_items)}")
    return buy_items, sell_items


def process_mod_items(mod_databases: dict) -> Tuple[List[ProcessedItem], List[ProcessedItem]]:
    """
    Convert mod item databases into ProcessedItem lists using the same
    ingredient-cost pricing logic as process_items() for vanilla items.

    For each item:
      1. Compute base_buy_price from its ITEM_DATABASE-style base_price (floor).
      2. If a recipe exists in MOD_RECIPES, sum ingredient base costs, apply
         CRAFTING_MARKUP, then apply biome + category multipliers (recipe_price).
         Ammo items skip dividing by output amount, matching vanilla ammo logic.
      3. Final buy_price = max(base_buy_price, recipe_price), capped to MAX_PRICE.
    """
    buy_items: List[ProcessedItem] = []
    sell_items: List[ProcessedItem] = []

    for mod_name, mod_db in mod_databases.items():
        mod_buy_count = 0
        mod_sell_count = 0

        for prefab, data in mod_db.items():
            biome, base_price, stack, sell_only, item_type = data

            category = get_item_category(prefab, item_type)
            category_mult = CATEGORY_MULTIPLIERS.get(category, 1.0)
            rarity_mult = RARITY_OVERRIDES.get(prefab, 1.0)

            # Floor price from the manually specified base_price
            base_buy_price = calculate_price(base_price, biome, prefab, item_type)

            # Recipe-based price (mirrors vanilla process_items logic)
            recipe = MOD_RECIPES.get(prefab)
            if recipe:
                ingredient_base_cost = 0
                found_all = True
                for ing_prefab, ing_qty in recipe['ingredients']:
                    if ing_prefab in ITEM_DATABASE:
                        _, ing_base, _, _ = ITEM_DATABASE[ing_prefab]
                        ingredient_base_cost += ing_base * ing_qty
                    else:
                        found_all = False
                        ingredient_base_cost += 10 * ing_qty  # fallback for unknowns

                output_amount = recipe.get('amount', 1)
                # Ammo: price covers the full craft output batch (no per-item division).
                # Check item_type hint since ammo prefabs here don't start with 'Arrow'.
                is_ammo = (category == 'ammo' or item_type.lower() == 'ammo')
                if is_ammo:
                    recipe_base = int(ingredient_base_cost * CRAFTING_MARKUP)
                else:
                    recipe_base = int(ingredient_base_cost * CRAFTING_MARKUP / max(1, output_amount))

                recipe_price = int(recipe_base * biome.multiplier * category_mult * rarity_mult)

                if found_all:
                    buy_price = max(base_buy_price, recipe_price)
                else:
                    buy_price = base_buy_price
            else:
                buy_price = base_buy_price

            buy_price = min(MAX_PRICE, max(MIN_PRICE, buy_price))
            sell_price = max(1, int(buy_price * SELL_MULTIPLIER))

            processed = ProcessedItem(
                prefab=prefab,
                name=prefab,
                item_type=item_type,
                biome=biome,
                buy_price=buy_price,
                sell_price=sell_price,
                stack=stack,
                boss_key=biome.boss_key,
                buyable=not sell_only,
                sellable=True,
            )

            if processed.buyable:
                buy_items.append(processed)
                mod_buy_count += 1
            if processed.sellable:
                sell_items.append(processed)
                mod_sell_count += 1

        print(f"      [{mod_name}] {mod_buy_count} buy, {mod_sell_count} sell")

    return buy_items, sell_items


def generate_configs(buy_items: List[ProcessedItem], sell_items: List[ProcessedItem], output_dir: Path) -> Tuple[Path, Path]:
    """Generate JSON config files."""
    print("\n[6/6] Generating configs...")
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Sort alphabetically
    buy_items.sort(key=lambda x: x.prefab.lower())
    sell_items.sort(key=lambda x: x.prefab.lower())
    
    # Generate buy config
    buy_cfg = [{
        "item_prefab": i.prefab,
        "item_quantity": i.stack,
        "item_price": i.buy_price,
        "must_defeated_boss": i.boss_key
    } for i in buy_items]
    
    buy_path = output_dir / "TraderOverhaul.buy.json"
    with open(buy_path, 'w') as f:
        json.dump(buy_cfg, f, indent=2)
    print(f"      Buy: {buy_path} ({len(buy_cfg)} items)")
    
    # Generate sell config
    sell_cfg = [{
        "item_prefab": i.prefab,
        "item_quantity": i.stack,
        "item_price": i.sell_price,
        "must_defeated_boss": i.boss_key
    } for i in sell_items]
    
    sell_path = output_dir / "TraderOverhaul.sell.json"
    with open(sell_path, 'w') as f:
        json.dump(sell_cfg, f, indent=2)
    print(f"      Sell: {sell_path} ({len(sell_cfg)} items)")
    
    return buy_path, sell_path


def validate_prices(buy_items: List[ProcessedItem]) -> List[str]:
    """Validate price progression and identify potential balance issues."""
    warnings = []

    # Check material tier progression
    materials = {
        'Wood': None, 'Bronze': None, 'Iron': None, 'Silver': None,
        'BlackMetal': None, 'Flametal': None, 'Eitr': None
    }
    for item in buy_items:
        if item.prefab in materials:
            materials[item.prefab] = item.buy_price

    # Verify progression: each tier should cost more than previous
    progression = ['Wood', 'Bronze', 'Iron', 'Silver', 'BlackMetal', 'Flametal']
    for i in range(1, len(progression)):
        prev, curr = progression[i-1], progression[i]
        if materials.get(prev) and materials.get(curr):
            if materials[curr] <= materials[prev]:
                warnings.append(f"Price inversion: {curr} ({materials[curr]}) <= {prev} ({materials[prev]})")

    # Check for extremely cheap or expensive items
    for item in buy_items:
        if item.buy_price < 10 and item.biome.tier > 3:
            warnings.append(f"Suspiciously cheap high-tier item: {item.prefab} ({item.buy_price})")
        if item.buy_price > 50000:
            warnings.append(f"Very expensive item: {item.prefab} ({item.buy_price})")

    return warnings


def print_summary(buy_items: List[ProcessedItem], sell_items: List[ProcessedItem]):
    """Print summary statistics."""
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)

    # Count by biome
    biome_counts = defaultdict(lambda: {'buy': 0, 'sell': 0})
    for i in buy_items:
        biome_counts[i.biome.biome_name]['buy'] += 1
    for i in sell_items:
        biome_counts[i.biome.biome_name]['sell'] += 1

    print("\n| Biome          | Buy  | Sell | Boss Key              |")
    print("|----------------|------|------|-----------------------|")
    for b in Biome:
        c = biome_counts[b.biome_name]
        print(f"| {b.biome_name:14} | {c['buy']:4} | {c['sell']:4} | {b.boss_key or '(none)':21} |")

    # Material progression
    materials = ['Wood', 'Bronze', 'Iron', 'Silver', 'BlackMetal', 'Flametal', 'Eitr']
    print("\n| Material      | Buy Price | Sell Price | Biome       |")
    print("|---------------|-----------|------------|-------------|")
    for p in materials:
        buy = next((i for i in buy_items if i.prefab == p), None)
        if buy:
            print(f"| {p:13} | {buy.buy_price:9} | {buy.sell_price:10} | {buy.biome.biome_name:11} |")

    # Rare materials (to show rarity multiplier effect)
    rare_mats = ['SurtlingCore', 'Chain', 'DragonTear', 'Wisp', 'BlackCore', 'RoyalJelly']
    print("\n| Rare Material  | Buy Price | Category        |")
    print("|----------------|-----------|-----------------|")
    for p in rare_mats:
        buy = next((i for i in buy_items if i.prefab == p), None)
        if buy:
            cat = get_item_category(buy.prefab, buy.item_type)
            print(f"| {p:14} | {buy.buy_price:9} | {cat:15} |")

    # Weapon progression
    weapon_samples = ['AxeFlint', 'SwordBronze', 'SwordIron', 'SwordSilver', 'SwordBlackmetal', 'SwordFire']
    print("\n| Weapon              | Buy Price | Biome       |")
    print("|---------------------|-----------|-------------|")
    for p in weapon_samples:
        buy = next((i for i in buy_items if i.prefab == p), None)
        if buy:
            print(f"| {p:19} | {buy.buy_price:9} | {buy.biome.biome_name:11} |")

    # Two-handed weapons (to show category multiplier)
    twoh_samples = ['AtgeirBronze', 'Battleaxe', 'SledgeDemolisher', 'THSwordSlayer']
    print("\n| 2H Weapon           | Buy Price | Biome       |")
    print("|---------------------|-----------|-------------|")
    for p in twoh_samples:
        buy = next((i for i in buy_items if i.prefab == p), None)
        if buy:
            print(f"| {p:19} | {buy.buy_price:9} | {buy.biome.biome_name:11} |")

    # Validate and show warnings
    warnings = validate_prices(buy_items)
    if warnings:
        print("\n[!] BALANCE WARNINGS:")
        for w in warnings:
            print(f"   - {w}")

    print("=" * 70)


def main():
    print("=" * 70)
    print("VALHEIM TRADER CONFIG GENERATOR v1.0")
    print("=" * 70)
    
    items_html = None
    recipes_html = None
    
    # Try fetching from URLs first
    print("\n[0/6] Fetching data from Jotunn documentation...")
    try:
        items_html = fetch_url(ITEM_LIST_URL)
        print("      [OK] Items fetched from URL")
    except Exception as e:
        print(f"      [FAIL] Could not fetch items: {e}")

    try:
        recipes_html = fetch_url(RECIPE_LIST_URL)
        print("      [OK] Recipes fetched from URL")
    except Exception as e:
        print(f"      [FAIL] Could not fetch recipes: {e}")
    
    # Fallback to local files if needed
    if not items_html or not recipes_html:
        print("      Checking local files...")
        local_paths = [
            Path('/mnt/user-data/uploads'),
            Path('.'),
            Path(__file__).parent if '__file__' in dir() else Path('.'),
        ]
        
        for sp in local_paths:
            if not items_html:
                items_file = sp / "Item_list.html"
                if items_file.exists():
                    with open(items_file, 'r', encoding='utf-8') as f:
                        items_html = f.read()
                    print(f"      [OK] Items from: {items_file}")
            if not recipes_html:
                recipes_file = sp / "Recipe_list.html"
                if recipes_file.exists():
                    with open(recipes_file, 'r', encoding='utf-8') as f:
                        recipes_html = f.read()
                    print(f"      [OK] Recipes from: {recipes_file}")
    
    if not items_html or not recipes_html:
        print("\nERROR: Could not obtain HTML data from URLs or local files.")
        print("Please ensure you have internet access, or place these files locally:")
        print("  - Item_list.html")
        print("  - Recipe_list.html")
        print("\nYou can download them from:")
        print(f"  {ITEM_LIST_URL}")
        print(f"  {RECIPE_LIST_URL}")
        return 1
    
    # Process vanilla items
    buy_items, sell_items = process_items(items_html, recipes_html)

    # Process mod items
    if MOD_DATABASES:
        print("\n[Mods] Processing mod items...")
        mod_buy, mod_sell = process_mod_items(MOD_DATABASES)
        buy_items.extend(mod_buy)
        sell_items.extend(mod_sell)
        print(f"      Total added: {len(mod_buy)} buy, {len(mod_sell)} sell")

    # Determine output directory - priority:
    # 1. Command-line argument
    # 2. HALDOR_CONFIG_PATH environment variable (set by the mod)
    # 3. Default Steam path
    # 4. Local ./output folder
    import os
    
    output_dir = None
    
    # Check command-line argument
    if len(sys.argv) > 1:
        output_dir = Path(sys.argv[1])
        print(f"\nUsing output from argument: {output_dir}")
    
    # Check environment variable (set by TraderOverhaul mod)
    if not output_dir:
        env_path = os.environ.get('HALDOR_CONFIG_PATH')
        if env_path:
            output_dir = Path(env_path)
            print(f"\nUsing output from HALDOR_CONFIG_PATH: {output_dir}")
    
    # Check default Steam path
    if not output_dir and CONFIG_OUTPUT_DIR.parent.exists():
        output_dir = CONFIG_OUTPUT_DIR
        print(f"\nUsing Steam config path: {output_dir}")
    
    # Fallback to local output
    if not output_dir:
        output_dir = Path(__file__).parent / "output" if '__file__' in dir() else Path('./output')
        print(f"\nUsing local output: {output_dir}")
    
    # Generate configs
    generate_configs(buy_items, sell_items, output_dir)
    
    # Print summary
    print_summary(buy_items, sell_items)
    
    print("\n[DONE] Complete!")
    return 0


if __name__ == "__main__":
    sys.exit(main())
