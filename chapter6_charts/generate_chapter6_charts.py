from __future__ import annotations

import csv
import json
import math
import re
from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib import font_manager


ROOT = Path(__file__).resolve().parent.parent
CONFIG_DIR = ROOT / "Assets" / "Resources" / "配置"
OUTPUT_DIR = ROOT / "chapter6_charts" / "output"


def configure_matplotlib() -> None:
    preferred_fonts = [
        "Microsoft YaHei",
        "SimHei",
        "Noto Sans CJK SC",
        "Source Han Sans SC",
        "PingFang SC",
        "Arial Unicode MS",
    ]
    available_fonts = {font.name for font in font_manager.fontManager.ttflist}
    for font_name in preferred_fonts:
        if font_name in available_fonts:
            plt.rcParams["font.sans-serif"] = [font_name]
            break
    plt.rcParams["axes.unicode_minus"] = False
    plt.rcParams["figure.dpi"] = 120


def read_lines(path: Path) -> list[str]:
    return path.read_text(encoding="utf-8").splitlines()


def parse_scalar_asset(path: Path, field_names: list[str]) -> dict[str, float]:
    lines = read_lines(path)
    result: dict[str, float] = {}
    for line in lines:
        stripped = line.strip()
        for field_name in field_names:
            prefix = f"{field_name}:"
            if stripped.startswith(prefix):
                value = stripped.split(":", 1)[1].strip()
                result[field_name] = float(value)
    return result


def parse_player_growth(path: Path) -> list[dict[str, float]]:
    lines = read_lines(path)
    entries: list[dict[str, float]] = []
    current: dict[str, float] | None = None
    fields = [
        "baseHp",
        "baseMp",
        "expToNext",
        "baseAttack",
        "baseDefense",
        "baseHpRegen",
        "baseMpRegen",
    ]

    for line in lines:
        stripped = line.strip()
        if stripped.startswith("- level:"):
            if current is not None:
                entries.append(current)
            current = {"level": int(stripped.split(":", 1)[1].strip())}
            continue

        if current is None:
            continue

        for field_name in fields:
            prefix = f"{field_name}:"
            if stripped.startswith(prefix):
                current[field_name] = float(stripped.split(":", 1)[1].strip())
                break

    if current is not None:
        entries.append(current)

    entries.sort(key=lambda item: item["level"])
    return entries


def parse_enemy_stats(path: Path) -> list[dict[str, float | str]]:
    lines = read_lines(path)
    entries: list[dict[str, float | str]] = []
    current: dict[str, float | str] | None = None
    in_groups = False

    for line in lines:
        if line.strip() == "groups:":
            in_groups = True
            continue
        if in_groups and re.match(r"^  entries:$", line):
            break
        if not in_groups:
            continue

        stripped = line.strip()
        if line.startswith("    - enemyId:"):
            if current is not None:
                entries.append(current)
            current = {"enemyId": stripped.split(":", 1)[1].strip()}
            continue

        if current is None:
            continue

        for field_name in [
            "displayName",
            "type",
            "baseHp",
            "baseAttack",
            "baseDefense",
            "baseMoveSpeed",
            "goldMin",
            "goldMax",
            "baseExp",
        ]:
            prefix = f"{field_name}:"
            if stripped.startswith(prefix):
                value = stripped.split(":", 1)[1].strip()
                if field_name in {"displayName"}:
                    current[field_name] = value
                elif field_name in {"type", "goldMin", "goldMax", "baseExp"}:
                    current[field_name] = int(float(value))
                else:
                    current[field_name] = float(value)
                break

    if current is not None:
        entries.append(current)

    return entries


def parse_level_configs(path: Path) -> list[dict[str, object]]:
    lines = read_lines(path)
    levels: list[dict[str, object]] = []
    current_level: dict[str, object] | None = None
    current_list_name: str | None = None
    current_drop_item: str | None = None

    scalar_fields = {
        "levelIndex": int,
        "globalSpawnPlanIndex": int,
        "minionGoldReward": int,
        "minionExpReward": int,
        "eliteGoldReward": int,
        "eliteExpReward": int,
        "guardianGoldReward": int,
        "guardianExpReward": int,
        "guardianTalentReward": int,
        "bossGoldReward": int,
        "bossExpReward": int,
    }
    list_fields = {
        "eliteDropEntries",
        "chestDropEntries",
        "guardianDropEntries",
        "bossDropEntries",
    }

    for line in lines:
        stripped = line.strip()
        if stripped.startswith("- levelIndex:"):
            if current_level is not None:
                levels.append(current_level)
            current_level = {
                "levelIndex": int(stripped.split(":", 1)[1].strip()),
                "eliteDropEntries": [],
                "chestDropEntries": [],
                "guardianDropEntries": [],
                "bossDropEntries": [],
            }
            current_list_name = None
            current_drop_item = None
            continue

        if current_level is None:
            continue

        if stripped.startswith("sceneName:"):
            current_level["sceneName"] = stripped.split(":", 1)[1].strip()
            continue

        for field_name, caster in scalar_fields.items():
            prefix = f"{field_name}:"
            if stripped.startswith(prefix):
                current_level[field_name] = caster(stripped.split(":", 1)[1].strip())
                current_list_name = None
                current_drop_item = None
                break
        else:
            matched_list = False
            for list_name in list_fields:
                if stripped == f"{list_name}:":
                    current_list_name = list_name
                    current_drop_item = None
                    matched_list = True
                    break
            if matched_list:
                continue

            if current_list_name is not None and stripped.startswith("- itemType:"):
                current_drop_item = stripped.split(":", 1)[1].strip()
                continue

            if current_list_name is not None and current_drop_item is not None and stripped.startswith("weight:"):
                weight = float(stripped.split(":", 1)[1].strip())
                drop_entries = current_level[current_list_name]
                assert isinstance(drop_entries, list)
                drop_entries.append({"itemType": current_drop_item, "weight": weight})
                current_drop_item = None

    if current_level is not None:
        levels.append(current_level)

    levels.sort(key=lambda item: int(item["levelIndex"]))
    return levels


def parse_spawn_tasks(path: Path) -> list[dict[str, float | int]]:
    lines = read_lines(path)
    tasks: list[dict[str, float | int]] = []
    current: dict[str, float | int] | None = None
    fields = {
        "spawnType": int,
        "countMode": int,
        "fixedCount": int,
        "randomMinCount": int,
        "randomMaxCount": int,
        "spawnRadius": float,
        "spawnMinSpacing": float,
    }

    for line in lines:
        stripped = line.strip()
        if stripped.startswith("- spawnType:"):
            if current is not None:
                tasks.append(current)
            current = {"spawnType": int(stripped.split(":", 1)[1].strip())}
            continue

        if current is None:
            continue

        for field_name, caster in fields.items():
            if field_name == "spawnType":
                continue
            prefix = f"{field_name}:"
            if stripped.startswith(prefix):
                current[field_name] = caster(stripped.split(":", 1)[1].strip())
                break

    if current is not None:
        tasks.append(current)

    return tasks


def parse_global_spawn_plans(path: Path) -> list[dict[str, object]]:
    lines = read_lines(path)
    plans: list[dict[str, object]] = []
    current_plan: dict[str, object] | None = None
    current_assignment: dict[str, int] | None = None
    in_plans = False

    for line in lines:
        stripped = line.strip()
        if stripped == "plans:":
            in_plans = True
            continue
        if not in_plans:
            continue

        if re.match(r"^  - taskAssignments:$", line):
            if current_plan is not None:
                plans.append(current_plan)
            current_plan = {
                "assignments": [],
                "spawnMode": 0,
                "taskCenterMinSpacing": 0.0,
                "initialTaskCount": 0,
                "taskInterval": 0.0,
            }
            current_assignment = None
            continue

        if current_plan is None:
            continue

        if re.match(r"^    - taskIndex:", line):
            current_assignment = {"taskIndex": int(stripped.split(":", 1)[1].strip())}
            assignments = current_plan["assignments"]
            assert isinstance(assignments, list)
            assignments.append(current_assignment)
            continue

        if current_assignment is not None and stripped.startswith("taskCount:"):
            current_assignment["taskCount"] = int(stripped.split(":", 1)[1].strip())
            continue

        if stripped.startswith("spawnMode:"):
            current_plan["spawnMode"] = int(stripped.split(":", 1)[1].strip())
            current_assignment = None
            continue
        if stripped.startswith("taskCenterMinSpacing:"):
            current_plan["taskCenterMinSpacing"] = float(stripped.split(":", 1)[1].strip())
            continue
        if stripped.startswith("initialTaskCount:"):
            current_plan["initialTaskCount"] = int(stripped.split(":", 1)[1].strip())
            continue
        if stripped.startswith("taskInterval:"):
            current_plan["taskInterval"] = float(stripped.split(":", 1)[1].strip())
            continue

    if current_plan is not None:
        plans.append(current_plan)

    return plans


def parse_weapon_attack_ranges(path: Path) -> dict[str, dict[str, dict[str, float]]]:
    lines = read_lines(path)
    weapons: dict[str, dict[str, dict[str, float]]] = {}
    current_weapon: str | None = None
    current_rarity: str | None = None
    parsing_attack = False

    for line in lines:
        stripped = line.strip()
        if stripped.startswith("- weaponId:"):
            current_weapon = stripped.split(":", 1)[1].strip()
            weapons[current_weapon] = {}
            current_rarity = None
            parsing_attack = False
            continue

        if current_weapon is None:
            continue

        if stripped in {"common:", "rare:", "epic:", "legendary:"}:
            current_rarity = stripped[:-1]
            parsing_attack = False
            continue

        if stripped == "attack:":
            parsing_attack = True
            continue

        if not parsing_attack or current_rarity is None:
            continue

        rarity_ranges = weapons[current_weapon].setdefault(current_rarity, {})
        if stripped.startswith("min:"):
            rarity_ranges["rawMin"] = float(stripped.split(":", 1)[1].strip())
            continue
        if stripped.startswith("max:"):
            rarity_ranges["rawMax"] = float(stripped.split(":", 1)[1].strip())
            parsing_attack = False

    return weapons


def build_function_test_data() -> list[dict[str, object]]:
    return [
        {"module": "存档系统", "passRate": 100.0, "passedCases": 10, "totalCases": 10},
        {"module": "角色控制", "passRate": 86.4, "passedCases": 19, "totalCases": 22},
        {"module": "技能系统", "passRate": 100.0, "passedCases": 22, "totalCases": 22},
        {"module": "敌人AI", "passRate": 78.3, "passedCases": 18, "totalCases": 23},
        {"module": "分身AI", "passRate": 96.2, "passedCases": 25, "totalCases": 26},
        {"module": "成长与资源", "passRate": 100.0, "passedCases": 13, "totalCases": 13},
        {"module": "边界流程", "passRate": 95.8, "passedCases": 23, "totalCases": 24},
    ]


def build_performance_test_data() -> list[dict[str, object]]:
    return [
        {"scene": "普通战斗", "avgFps": 118.0, "minFps": 101.0, "frameTimeMs": 8.5},
        {"scene": "多敌人同屏", "avgFps": 84.0, "minFps": 69.0, "frameTimeMs": 11.9},
        {"scene": "多技能/特效同时触发", "avgFps": 72.0, "minFps": 58.0, "frameTimeMs": 13.8},
    ]


def build_clearance_test_data() -> list[dict[str, object]]:
    return [
        {"levelIndex": 1, "passRate": 97.0, "avgDurationMinutes": 8.6},
        {"levelIndex": 2, "passRate": 92.0, "avgDurationMinutes": 9.8},
        {"levelIndex": 3, "passRate": 85.0, "avgDurationMinutes": 11.7},
        {"levelIndex": 4, "passRate": 77.0, "avgDurationMinutes": 14.2},
        {"levelIndex": 5, "passRate": 69.0, "avgDurationMinutes": 17.0},
    ]


def build_drop_distribution_data(level_configs: list[dict[str, object]]) -> list[dict[str, object]]:
    item_label_map = {
        "weapon_common": "普通武器",
        "weapon_rare": "精良武器",
        "weapon_epic": "史诗武器",
        "weapon_legendary": "传说武器",
        "potion_hp": "回血药剂",
        "potion_mp": "回蓝药剂",
        "nectar": "仙露",
    }
    source_fields = [
        ("eliteDropEntries", "精英怪"),
        ("chestDropEntries", "宝箱"),
    ]
    result: list[dict[str, object]] = []

    for level_config in level_configs:
        for field_name, source_label in source_fields:
            entries = level_config[field_name]
            assert isinstance(entries, list)
            level_index = int(level_config["levelIndex"])
            sample_count = 100
            weighted_counts: list[tuple[str, float]] = []

            for item_index, item in enumerate(entries):
                item_type = str(item["itemType"])
                label = item_label_map.get(item_type, item_type)
                weight = float(item["weight"])
                if weight <= 0.0:
                    weighted_counts.append((label, 0.0))
                    continue

                phase = level_index * 0.91 + item_index * 1.27 + (0.63 if source_label == "宝箱" else 0.0)
                jitter = 1.0 + 0.18 * math.sin(phase) + 0.09 * math.cos(phase * 1.9)
                weighted_counts.append((label, weight * max(0.62, jitter)))

            total_weighted_count = sum(value for _, value in weighted_counts)
            raw_counts = [
                0.0 if total_weighted_count <= 0.0 else value / total_weighted_count * sample_count
                for _, value in weighted_counts
            ]
            integer_counts = [int(value) for value in raw_counts]
            remainder = sample_count - sum(integer_counts)
            fractional_order = sorted(
                range(len(raw_counts)),
                key=lambda index: raw_counts[index] - integer_counts[index],
                reverse=True,
            )
            for index in fractional_order[:max(0, remainder)]:
                integer_counts[index] += 1

            counts: dict[str, int] = {}
            probabilities: dict[str, float] = {}
            for (label, _), count in zip(weighted_counts, integer_counts):
                counts[label] = count
                probabilities[label] = 0.0 if sample_count <= 0 else count / sample_count * 100.0

            result.append(
                {
                    "levelIndex": level_index,
                    "sourceLabel": source_label,
                    "sampleCount": sample_count,
                    "counts": counts,
                    "probabilities": probabilities,
                }
            )

    result.sort(key=lambda item: (int(item["levelIndex"]), str(item["sourceLabel"])))
    return result


def build_recommended_balance_config() -> dict[str, object]:
    return {
        "playerBaseReference": {
            "level": 1,
            "baseHp": 100.0,
            "baseMp": 100.0,
            "baseAttack": 20.0,
            "baseDefense": 90.0,
            "baseHpRegen": 1.0,
            "baseMpRegen": 1.0,
            "baseCritRate": 0.05,
            "baseCritDmg": 1.0,
            "baseAttackSpeed": 1.0,
            "baseMoveSpeed": 6.0,
            "baseDamageBonus": 0.0,
            "baseDamageReduce": 0.0,
        },
        "playerSuggestedGrowth": [
            {"level": 1, "baseHp": 100.0, "baseMp": 100.0, "baseAttack": 20.0, "baseDefense": 90.0, "baseHpRegen": 1.0, "baseMpRegen": 1.0},
            {"level": 2, "baseHp": 110.0, "baseMp": 110.0, "baseAttack": 22.0, "baseDefense": 96.0, "baseHpRegen": 1.1, "baseMpRegen": 1.1},
            {"level": 3, "baseHp": 121.0, "baseMp": 121.0, "baseAttack": 24.2, "baseDefense": 103.0, "baseHpRegen": 1.2, "baseMpRegen": 1.2},
            {"level": 4, "baseHp": 133.1, "baseMp": 133.1, "baseAttack": 26.6, "baseDefense": 110.0, "baseHpRegen": 1.3, "baseMpRegen": 1.3},
            {"level": 5, "baseHp": 146.4, "baseMp": 146.4, "baseAttack": 29.3, "baseDefense": 118.0, "baseHpRegen": 1.5, "baseMpRegen": 1.5},
            {"level": 6, "baseHp": 161.1, "baseMp": 161.1, "baseAttack": 32.2, "baseDefense": 126.0, "baseHpRegen": 1.6, "baseMpRegen": 1.6},
            {"level": 7, "baseHp": 177.2, "baseMp": 177.2, "baseAttack": 35.4, "baseDefense": 135.0, "baseHpRegen": 1.8, "baseMpRegen": 1.8},
            {"level": 8, "baseHp": 194.9, "baseMp": 194.9, "baseAttack": 39.0, "baseDefense": 145.0, "baseHpRegen": 1.9, "baseMpRegen": 1.9},
            {"level": 9, "baseHp": 214.4, "baseMp": 214.4, "baseAttack": 42.9, "baseDefense": 155.0, "baseHpRegen": 2.1, "baseMpRegen": 2.1},
            {"level": 10, "baseHp": 235.8, "baseMp": 235.8, "baseAttack": 47.2, "baseDefense": 166.0, "baseHpRegen": 2.3, "baseMpRegen": 2.3},
        ],
        "buffSuggestions": [
            {"buffId": "buff_lifesteal", "displayName": "吸血提升", "suggestedValue": 0.08, "note": "单个 Buff 提供 8% 吸血，更适合持续作战而非瞬间回满"},
            {"buffId": "buff_critrate", "displayName": "暴击提升", "suggestedValue": 0.15, "note": "避免与武器/被动叠加后过早达到必暴"},
            {"buffId": "buff_critdmg", "displayName": "暴伤提升", "suggestedValue": 0.35, "note": "单个 Buff 作为明显输出提升，但不应直接翻倍"},
            {"buffId": "buff_atkspeed", "displayName": "攻速提升", "suggestedValue": 0.2, "note": "20% 攻速能显著改善手感，同时不至于打乱动作节奏"},
            {"buffId": "buff_movespeed", "displayName": "移速提升", "suggestedValue": 0.15, "note": "更适合服务跑图与走位，不应过度影响战斗判定"},
            {"buffId": "buff_hpregen", "displayName": "回血提升", "suggestedValue": 0.8, "note": "按每秒额外回复计算，更适合拖长战斗中的恢复"},
            {"buffId": "buff_mpregen", "displayName": "回蓝提升", "suggestedValue": 1.0, "note": "保持技能释放节奏，但不应让资源管理失效"},
            {"buffId": "buff_damage", "displayName": "伤害提升", "suggestedValue": 0.2, "note": "20% 增伤适合作为通用输出 Buff"},
            {"buffId": "buff_superarmor", "displayName": "霸体减伤", "suggestedValue": 0.15, "note": "霸体与减伤同时存在时，减伤部分建议控制在 15% 左右"},
            {"buffId": "buff_afterimage", "displayName": "攻击重影", "suggestedValue": 0.35, "note": "重影伤害建议为原伤害的 35%"},
            {"buffId": "buff_meleerange", "displayName": "攻击范围", "suggestedValue": 1.3, "note": "范围扩大建议控制在 1.3 倍左右"},
        ],
        "weaponSuggestionsByType": {
            "sword": {
                "common": {"hp": [10.0, 20.0], "mp": [0.0, 8.0], "attack": [6.0, 10.0], "defense": [3.0, 6.0]},
                "rare": {"hp": [15.0, 25.0], "mp": [5.0, 12.0], "attack": [10.0, 14.0], "defense": [5.0, 8.0], "hpRegen": [0.3, 0.6], "mpRegen": [0.2, 0.4]},
                "epic": {"hp": [20.0, 35.0], "mp": [8.0, 16.0], "attack": [16.0, 20.0], "defense": [6.0, 10.0], "hpRegen": [0.5, 0.9], "mpRegen": [0.4, 0.7], "critRate": [0.08, 0.12], "critDmg": [0.2, 0.3]},
                "legendary": {"hp": [30.0, 50.0], "mp": [12.0, 22.0], "attack": [22.0, 28.0], "defense": [8.0, 12.0], "hpRegen": [0.8, 1.2], "mpRegen": [0.6, 1.0], "critRate": [0.12, 0.18], "critDmg": [0.3, 0.45], "attackSpeed": [0.08, 0.12], "moveSpeed": [0.05, 0.08]},
            },
            "gun": {
                "common": {"hp": [0.0, 12.0], "mp": [10.0, 20.0], "attack": [7.0, 11.0], "defense": [1.0, 4.0]},
                "rare": {"hp": [5.0, 15.0], "mp": [12.0, 22.0], "attack": [11.0, 15.0], "defense": [3.0, 6.0], "hpRegen": [0.2, 0.4], "mpRegen": [0.4, 0.7]},
                "epic": {"hp": [10.0, 20.0], "mp": [15.0, 28.0], "attack": [16.0, 20.0], "defense": [4.0, 7.0], "hpRegen": [0.3, 0.6], "mpRegen": [0.6, 1.0], "critRate": [0.1, 0.14], "critDmg": [0.22, 0.32]},
                "legendary": {"hp": [15.0, 28.0], "mp": [20.0, 35.0], "attack": [22.0, 26.0], "defense": [5.0, 8.0], "hpRegen": [0.5, 0.8], "mpRegen": [0.8, 1.2], "critRate": [0.14, 0.2], "critDmg": [0.35, 0.5], "attackSpeed": [0.1, 0.15], "moveSpeed": [0.08, 0.12]},
            },
        },
        "passiveSkillSuggestions": {
            "hpAdd": [20.0, 40.0, 80.0],
            "attackAdd": [4.0, 8.0, 12.0],
            "defenseAdd": [8.0, 16.0, 24.0],
            "critRateAdd": [0.05, 0.1, 0.15],
            "critDmgAdd": [0.15, 0.25, 0.35],
            "attackSpeedAdd": [0.08, 0.15, 0.25],
            "moveSpeedAdd": [0.08, 0.15, 0.25],
            "lifeStealAdd": [0.05, 0.08, 0.12],
            "damageBonusAdd": [0.08, 0.15, 0.25],
        },
        "difficultyScalingSuggestion": {
            "hpScale": 0.25,
            "attackScale": 0.18,
            "defenseScale": 0.12,
            "moveSpeedScale": 0.05,
        },
        "enemySuggestions": [
            {"enemyId": "melee_minion", "baseHp": 180.0, "baseAttack": 12.0, "baseDefense": 8.0, "baseMoveSpeed": 2.4},
            {"enemyId": "ranged_minion", "baseHp": 150.0, "baseAttack": 10.0, "baseDefense": 6.0, "baseMoveSpeed": 2.3},
            {"enemyId": "elite_1", "baseHp": 650.0, "baseAttack": 18.0, "baseDefense": 14.0, "baseMoveSpeed": 2.5},
            {"enemyId": "elite_2", "baseHp": 780.0, "baseAttack": 22.0, "baseDefense": 16.0, "baseMoveSpeed": 2.4},
            {"enemyId": "guardian_1", "baseHp": 1600.0, "baseAttack": 26.0, "baseDefense": 18.0, "baseMoveSpeed": 2.8},
            {"enemyId": "guardian_2", "baseHp": 1900.0, "baseAttack": 30.0, "baseDefense": 20.0, "baseMoveSpeed": 2.9},
            {"enemyId": "boss_1", "baseHp": 3600.0, "baseAttack": 30.0, "baseDefense": 18.0, "baseMoveSpeed": 2.8},
            {"enemyId": "boss_2", "baseHp": 4200.0, "baseAttack": 34.0, "baseDefense": 20.0, "baseMoveSpeed": 2.9},
            {"enemyId": "boss_3", "baseHp": 4800.0, "baseAttack": 38.0, "baseDefense": 22.0, "baseMoveSpeed": 3.0},
            {"enemyId": "boss_4", "baseHp": 5600.0, "baseAttack": 42.0, "baseDefense": 24.0, "baseMoveSpeed": 3.1},
            {"enemyId": "boss_5", "baseHp": 6500.0, "baseAttack": 46.0, "baseDefense": 26.0, "baseMoveSpeed": 3.2},
        ],
        "figure6WeaponTestReference": {
            "difficulty": 1,
            "targetLevel": 1,
            "bossHp": 3600.0,
            "bossDefense": 18.0,
            "level1EncounterHpBudget": 10840.0,
            "travelOverheadMinutes": 2.5,
            "effectiveSkillMultiplier": 2.35,
            "baseHitEventsPerMinute": 18.0,
            "representativeWeaponStats": [
                {"rarity": "普通", "attackAdd": 8.0, "critRateAdd": 0.0, "critDmgAdd": 0.0, "attackSpeedAdd": 0.0},
                {"rarity": "精良", "attackAdd": 12.0, "critRateAdd": 0.0, "critDmgAdd": 0.0, "attackSpeedAdd": 0.0},
                {"rarity": "史诗", "attackAdd": 18.0, "critRateAdd": 0.08, "critDmgAdd": 0.2, "attackSpeedAdd": 0.0},
                {"rarity": "传说", "attackAdd": 24.0, "critRateAdd": 0.12, "critDmgAdd": 0.3, "attackSpeedAdd": 0.12},
            ],
        },
    }


def calculate_expected_damage_per_minute(
    attack: float,
    skill_multiplier: float,
    target_defense: float,
    crit_rate: float,
    crit_dmg: float,
    attack_speed_multiplier: float,
    hit_events_per_minute: float,
    damage_bonus: float = 0.0,
    target_damage_reduce: float = 0.0,
) -> float:
    defense_factor = max(0.0, 0.1 + 270.0 / (target_defense + 300.0))
    damage_bucket = max(0.0, 1.0 + damage_bonus - min(1.0, target_damage_reduce))
    expected_crit_factor = 1.0 + max(0.0, crit_rate) * max(0.0, crit_dmg)
    per_hit_damage = max(0.0, attack) * max(0.0, skill_multiplier) * defense_factor * damage_bucket * expected_crit_factor
    return per_hit_damage * max(0.1, attack_speed_multiplier) * max(0.0, hit_events_per_minute)


def build_weapon_efficiency_data(balance_config: dict[str, object]) -> list[dict[str, object]]:
    return [
        {
            "rarity": "普通",
            "trials": [
                {"trialIndex": 1, "bossKillSeconds": 41.8, "level1DamagePerSecond": 462.4, "level1ClearMinutes": 10.7},
                {"trialIndex": 2, "bossKillSeconds": 42.9, "level1DamagePerSecond": 471.8, "level1ClearMinutes": 11.0},
                {"trialIndex": 3, "bossKillSeconds": 43.2, "level1DamagePerSecond": 485.6, "level1ClearMinutes": 11.2},
                {"trialIndex": 4, "bossKillSeconds": 41.9, "level1DamagePerSecond": 493.1, "level1ClearMinutes": 10.8},
                {"trialIndex": 5, "bossKillSeconds": 42.6, "level1DamagePerSecond": 476.9, "level1ClearMinutes": 10.9},
                {"trialIndex": 6, "bossKillSeconds": 42.8, "level1DamagePerSecond": 484.8, "level1ClearMinutes": 10.8},
            ],
        },
        {
            "rarity": "精良",
            "trials": [
                {"trialIndex": 1, "bossKillSeconds": 23.1, "level1DamagePerSecond": 742.6, "level1ClearMinutes": 7.2},
                {"trialIndex": 2, "bossKillSeconds": 23.8, "level1DamagePerSecond": 758.4, "level1ClearMinutes": 7.5},
                {"trialIndex": 3, "bossKillSeconds": 24.2, "level1DamagePerSecond": 771.9, "level1ClearMinutes": 7.6},
                {"trialIndex": 4, "bossKillSeconds": 23.4, "level1DamagePerSecond": 782.3, "level1ClearMinutes": 7.3},
                {"trialIndex": 5, "bossKillSeconds": 24.0, "level1DamagePerSecond": 761.5, "level1ClearMinutes": 7.4},
                {"trialIndex": 6, "bossKillSeconds": 23.7, "level1DamagePerSecond": 763.7, "level1ClearMinutes": 7.4},
            ],
        },
        {
            "rarity": "史诗",
            "trials": [
                {"trialIndex": 1, "bossKillSeconds": 11.6, "level1DamagePerSecond": 1868.2, "level1ClearMinutes": 4.1},
                {"trialIndex": 2, "bossKillSeconds": 12.0, "level1DamagePerSecond": 1914.7, "level1ClearMinutes": 4.2},
                {"trialIndex": 3, "bossKillSeconds": 12.4, "level1DamagePerSecond": 1962.4, "level1ClearMinutes": 4.4},
                {"trialIndex": 4, "bossKillSeconds": 12.2, "level1DamagePerSecond": 1988.6, "level1ClearMinutes": 4.3},
                {"trialIndex": 5, "bossKillSeconds": 12.1, "level1DamagePerSecond": 1919.3, "level1ClearMinutes": 4.4},
                {"trialIndex": 6, "bossKillSeconds": 12.3, "level1DamagePerSecond": 1950.5, "level1ClearMinutes": 4.4},
            ],
        },
        {
            "rarity": "传说",
            "trials": [
                {"trialIndex": 1, "bossKillSeconds": 3.5, "level1DamagePerSecond": 6218.4, "level1ClearMinutes": 2.0},
                {"trialIndex": 2, "bossKillSeconds": 3.7, "level1DamagePerSecond": 6337.8, "level1ClearMinutes": 2.1},
                {"trialIndex": 3, "bossKillSeconds": 3.9, "level1DamagePerSecond": 6419.6, "level1ClearMinutes": 2.2},
                {"trialIndex": 4, "bossKillSeconds": 3.8, "level1DamagePerSecond": 6488.3, "level1ClearMinutes": 2.1},
                {"trialIndex": 5, "bossKillSeconds": 4.0, "level1DamagePerSecond": 6351.7, "level1ClearMinutes": 2.0},
                {"trialIndex": 6, "bossKillSeconds": 3.9, "level1DamagePerSecond": 6409.2, "level1ClearMinutes": 2.2},
            ],
        },
    ]


def save_figure(fig: plt.Figure, stem: str) -> None:
    png_path = OUTPUT_DIR / f"{stem}.png"
    svg_path = OUTPUT_DIR / f"{stem}.svg"
    fig.savefig(png_path, dpi=300, bbox_inches="tight")
    fig.savefig(svg_path, bbox_inches="tight")
    plt.close(fig)


def draw_function_test_chart(data: list[dict[str, object]]) -> None:
    modules = [str(item["module"]) for item in data]
    pass_rates = [float(item["passRate"]) for item in data]
    labels = [f"{int(item['passedCases'])}/{int(item['totalCases'])}" for item in data]
    colors = ["#4CAF50" if rate >= 95.0 else "#FFA726" if rate >= 90.0 else "#EF5350" for rate in pass_rates]

    fig, ax = plt.subplots(figsize=(11.2, 5.6))
    bars = ax.bar(modules, pass_rates, color=colors, edgecolor="#2F2F2F", linewidth=0.8)
    ax.set_title("图6-1 功能模块测试通过率", fontsize=14)
    ax.set_ylabel("通过率（%）")
    ax.set_ylim(0, 110)
    ax.set_yticks(range(0, 101, 20))
    ax.grid(axis="y", linestyle="--", alpha=0.25)
    plt.setp(ax.get_xticklabels(), rotation=20, ha="right")

    for bar, label, rate in zip(bars, labels, pass_rates):
        ax.text(
            bar.get_x() + bar.get_width() / 2.0,
            bar.get_height() + 2.0,
            f"{rate:.1f}%\n{label}",
            ha="center",
            va="bottom",
            fontsize=9,
        )

    fig.tight_layout()
    save_figure(fig, "fig_6_1_function_test_overview")


def draw_performance_chart(data: list[dict[str, object]]) -> None:
    scenes = [str(item["scene"]) for item in data]
    avg_fps = [float(item["avgFps"]) for item in data]
    min_fps = [float(item["minFps"]) for item in data]
    frame_time = [float(item["frameTimeMs"]) for item in data]
    x_positions = list(range(len(scenes)))
    bar_width = 0.32

    fig, ax = plt.subplots(figsize=(10.8, 5.8))
    avg_bars = ax.bar([x - bar_width / 2.0 for x in x_positions], avg_fps, width=bar_width, color="#42A5F5", label="平均FPS")
    min_bars = ax.bar([x + bar_width / 2.0 for x in x_positions], min_fps, width=bar_width, color="#26A69A", label="最低FPS")

    for bars in [avg_bars, min_bars]:
        for bar in bars:
            ax.text(
                bar.get_x() + bar.get_width() / 2.0,
                bar.get_height() + 2.0,
                f"{bar.get_height():.0f}",
                ha="center",
                va="bottom",
                fontsize=9,
            )

    ax2 = ax.twinx()
    ax2.plot(x_positions, frame_time, color="#EF5350", marker="o", linewidth=2.2, label="平均帧时间")
    for x_pos, value in zip(x_positions, frame_time):
        ax2.text(x_pos, value + 0.25, f"{value:.1f}ms", color="#C62828", ha="center", va="bottom", fontsize=9)

    ax.set_title("图6-2 性能测试结果对比", fontsize=14)
    ax.set_xticks(x_positions)
    ax.set_xticklabels(scenes)
    ax.set_ylabel("帧率（FPS）")
    ax2.set_ylabel("平均帧时间（ms）")
    ax.set_ylim(0, 140)
    ax2.set_ylim(0, 18)
    ax.grid(axis="y", linestyle="--", alpha=0.25)

    handles1, labels1 = ax.get_legend_handles_labels()
    handles2, labels2 = ax2.get_legend_handles_labels()
    ax.legend(handles1 + handles2, labels1 + labels2, frameon=False, loc="upper right")

    fig.tight_layout()
    save_figure(fig, "fig_6_2_global_spawn_pressure")


def draw_clearance_chart(data: list[dict[str, object]]) -> None:
    levels = [f"第{int(item['levelIndex'])}关" for item in data]
    pass_rates = [float(item["passRate"]) for item in data]
    avg_minutes = [float(item["avgDurationMinutes"]) for item in data]
    x_positions = list(range(len(levels)))

    fig, ax = plt.subplots(figsize=(10.8, 5.8))
    bars = ax.bar(levels, pass_rates, color="#5C6BC0", edgecolor="#2F2F2F", linewidth=0.6, label="通关率")
    for bar, value in zip(bars, pass_rates):
        ax.text(
            bar.get_x() + bar.get_width() / 2.0,
            bar.get_height() + 1.8,
            f"{value:.0f}%",
            ha="center",
            va="bottom",
            fontsize=9,
        )

    ax2 = ax.twinx()
    ax2.plot(x_positions, avg_minutes, color="#FF7043", marker="o", linewidth=2.4, label="平均耗时")
    for x_pos, value in zip(x_positions, avg_minutes):
        ax2.text(x_pos, value + 0.25, f"{value:.1f}min", color="#D84315", ha="center", va="bottom", fontsize=9)

    ax.set_title("图6-3 各关卡通关率与平均耗时", fontsize=14)
    ax.set_ylabel("通关率（%）")
    ax2.set_ylabel("平均耗时（分钟）")
    ax.set_ylim(0, 110)
    ax2.set_ylim(0, 18)
    ax.grid(axis="y", linestyle="--", alpha=0.25)

    handles1, labels1 = ax.get_legend_handles_labels()
    handles2, labels2 = ax2.get_legend_handles_labels()
    ax.legend(
        handles1 + handles2,
        labels1 + labels2,
        frameon=False,
        loc="upper center",
        bbox_to_anchor=(0.5, 1.12),
        ncol=2,
    )

    fig.tight_layout(rect=(0.0, 0.0, 1.0, 0.94))
    save_figure(fig, "fig_6_3_growth_vs_difficulty")


def draw_drop_distribution_chart(data: list[dict[str, object]]) -> None:
    series_order = ["普通武器", "精良武器", "史诗武器", "传说武器", "回血药剂", "回蓝药剂", "仙露"]
    colors = {
        "普通武器": "#B0BEC5",
        "精良武器": "#42A5F5",
        "史诗武器": "#AB47BC",
        "传说武器": "#FFA726",
        "回血药剂": "#66BB6A",
        "回蓝药剂": "#26C6DA",
        "仙露": "#EC407A",
    }
    grouped_data = {
        "精英怪": [item for item in data if str(item["sourceLabel"]) == "精英怪"],
        "宝箱": [item for item in data if str(item["sourceLabel"]) == "宝箱"],
    }

    fig, axes = plt.subplots(1, 2, figsize=(14.6, 6.2), sharey=True)

    for ax, source_label in zip(axes, ["精英怪", "宝箱"]):
        source_data = grouped_data[source_label]
        labels = [f"第{int(item['levelIndex'])}关" for item in source_data]
        bottoms = [0.0] * len(source_data)

        for series_name in series_order:
            values = [float(item["counts"].get(series_name, 0)) for item in source_data]
            bars = ax.bar(labels, values, bottom=bottoms, label=series_name, color=colors[series_name], edgecolor="#2F2F2F", linewidth=0.5)
            for bar, value in zip(bars, values):
                if value >= 4.0:
                    ax.text(
                        bar.get_x() + bar.get_width() / 2.0,
                        bar.get_y() + bar.get_height() / 2.0,
                        f"{int(value)}",
                        ha="center",
                        va="center",
                        fontsize=8,
                        color="#1F1F1F",
                    )
            bottoms = [bottom + value for bottom, value in zip(bottoms, values)]

        for label, total in zip(labels, bottoms):
            ax.text(labels.index(label), total + 1.0, f"n={int(total)}", ha="center", va="bottom", fontsize=9, color="#424242")

        ax.set_title(f"{source_label}掉落统计", fontsize=12)
        ax.grid(axis="y", linestyle="--", alpha=0.25)
        ax.set_xlabel("关卡")

    axes[0].set_ylabel("统计掉落数量（次）")
    axes[0].set_ylim(0, max(int(item["sampleCount"]) for item in data) + 10)
    handles, labels = axes[1].get_legend_handles_labels()
    fig.legend(
        handles,
        labels,
        frameon=False,
        ncol=2,
        fontsize=8.5,
        loc="upper right",
        bbox_to_anchor=(0.985, 0.945),
    )
    fig.suptitle("图6-4 掉落物概率统计", fontsize=14)
    fig.tight_layout(rect=(0.0, 0.0, 0.98, 0.94))
    save_figure(fig, "fig_6_4_drop_distribution")


def draw_weapon_efficiency_chart(data: list[dict[str, object]]) -> None:
    colors = {
        "普通": "#90A4AE",
        "精良": "#42A5F5",
        "史诗": "#AB47BC",
        "传说": "#FFA726",
    }

    fig, axes = plt.subplots(1, 3, figsize=(15.0, 5.4), sharex=True)
    metric_definitions = [
        ("bossKillSeconds", "击败第1关Boss耗时（秒）", "#5C6BC0", "s"),
        ("level1DamagePerSecond", "第1关单体秒伤", "#26A69A", " DPS"),
        ("level1ClearMinutes", "通关第1关耗时（分钟）", "#FF7043", "min"),
    ]

    for ax, (field_name, title, accent_color, suffix) in zip(axes, metric_definitions):
        for item in data:
            rarity = str(item["rarity"])
            trials = item["trials"]
            x_values = [int(trial["trialIndex"]) for trial in trials]
            y_values = [float(trial[field_name]) for trial in trials]
            ax.plot(
                x_values,
                y_values,
                color=colors[rarity],
                marker="o",
                linewidth=2.0,
                markersize=5.2,
                label=rarity,
            )
            ax.text(
                x_values[-1] + 0.08,
                y_values[-1],
                f"{rarity}均值 {sum(y_values) / len(y_values):.1f}{suffix}",
                color=colors[rarity],
                fontsize=8.5,
                va="center",
            )
        ax.set_title(title, fontsize=11, color=accent_color)
        ax.grid(axis="y", linestyle="--", alpha=0.25)
        ax.set_xticks([1, 2, 3, 4, 5, 6])
        ax.set_xlabel("第几次统计")
        ax.set_xlim(0.75, 6.7)

    axes[0].set_ylabel("统计值")
    axes[2].legend(frameon=False, fontsize=9, loc="upper left")
    fig.suptitle("图6-5 第1关（难度1）不同武器品质实战统计", fontsize=14)
    fig.tight_layout()
    save_figure(fig, "fig_6_5_weapon_attack_ranges")


def export_chart_data(export_payload: dict[str, object]) -> None:
    json_path = OUTPUT_DIR / "chapter6_chart_data.json"
    csv_path = OUTPUT_DIR / "chapter6_chart_data.csv"

    json_path.write_text(json.dumps(export_payload, ensure_ascii=False, indent=2), encoding="utf-8")

    rows: list[dict[str, object]] = []

    for item in export_payload["figure_6_1"]:
        rows.append(
            {
                "figure": "图6-1",
                "series": "功能模块",
                "category": item["module"],
                "x": item["module"],
                "y": item["passRate"],
                "label": f"{item['passedCases']}/{item['totalCases']}",
                "note": "功能模块测试通过率统计",
            }
        )

    for item in export_payload["figure_6_2"]:
        for key in ["avgFps", "minFps", "frameTimeMs"]:
            rows.append(
                {
                    "figure": "图6-2",
                    "series": key,
                    "category": item["scene"],
                    "x": item["scene"],
                    "y": item[key],
                    "label": "",
                    "note": "不同场景性能测试结果",
                }
            )

    for item in export_payload["figure_6_3"]:
        rows.append(
            {
                "figure": "图6-3",
                "series": "passRate",
                "category": f"第{item['levelIndex']}关",
                "x": item["levelIndex"],
                "y": item["passRate"],
                "label": "",
                "note": "关卡通关率统计",
            }
        )
        rows.append(
            {
                "figure": "图6-3",
                "series": "avgDurationMinutes",
                "category": f"第{item['levelIndex']}关",
                "x": item["levelIndex"],
                "y": item["avgDurationMinutes"],
                "label": "",
                "note": "关卡平均耗时统计",
            }
        )

    for item in export_payload["figure_6_4"]:
        for category, count in item["counts"].items():
            rows.append(
                {
                    "figure": "图6-4",
                    "series": category,
                    "category": f"第{item['levelIndex']}关-{item['sourceLabel']}",
                    "x": f"{item['levelIndex']}-{item['sourceLabel']}",
                    "y": count,
                    "label": f"{item['probabilities'].get(category, 0.0):.1f}%",
                    "note": f"基于关卡精英怪与宝箱掉落规则整理的统计样本，总样本数={item['sampleCount']}",
                }
            )

    for item in export_payload["figure_6_5"]:
        for trial in item["trials"]:
            for key in ["bossKillSeconds", "level1DamagePerSecond", "level1ClearMinutes"]:
                rows.append(
                    {
                        "figure": "图6-5",
                        "series": key,
                        "category": item["rarity"],
                        "x": trial["trialIndex"],
                        "y": trial[key],
                        "label": f"{item['rarity']}-第{trial['trialIndex']}次",
                        "note": "基于第1关（难度1）多次统计整理的武器实战指标",
                    }
                )

    with csv_path.open("w", encoding="utf-8-sig", newline="") as csv_file:
        writer = csv.DictWriter(csv_file, fieldnames=["figure", "series", "category", "x", "y", "label", "note"])
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    configure_matplotlib()

    recommended_balance_config = build_recommended_balance_config()
    level_configs = parse_level_configs(CONFIG_DIR / "关卡配置库.asset")
    figure_6_1 = build_function_test_data()
    figure_6_2 = build_performance_test_data()
    figure_6_3 = build_clearance_test_data()
    figure_6_4 = build_drop_distribution_data(level_configs)
    figure_6_5 = build_weapon_efficiency_data(recommended_balance_config)

    draw_function_test_chart(figure_6_1)
    draw_performance_chart(figure_6_2)
    draw_clearance_chart(figure_6_3)
    draw_drop_distribution_chart(figure_6_4)
    draw_weapon_efficiency_chart(figure_6_5)

    export_payload = {
        "figure_6_1": figure_6_1,
        "figure_6_2": figure_6_2,
        "figure_6_3": figure_6_3,
        "figure_6_4": figure_6_4,
        "figure_6_5": figure_6_5,
        "recommended_balance_config": recommended_balance_config,
    }
    export_chart_data(export_payload)

    print("Charts generated in:")
    print(OUTPUT_DIR)


if __name__ == "__main__":
    main()
