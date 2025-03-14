﻿// Copyright 2024 Crystal Ferrai
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Helper for extracting data from NPC spawner classes
	/// </summary>
	internal static class SpawnMinerUtil
	{
		/// <summary>
		/// Load spawn data from an npc spawner class
		/// </summary>
		/// <param name="scgClassProperty">An object property pointing to a spawner class (derived from HShuaGuaiQi), usually referenced from a property named "SCGClass" (ShengChengGuai Class)</param>
		/// <param name="logger">For logging warnings if data failed to load</param>
		/// <param name="spawnerNameForLogging">The name of the spawner instance to use when logging warnings</param>
		/// <param name="defaultScgObj">If the passed in <see cref="scgClass" /> has no defaults object, fallback on this defaults object.</param>
		/// <returns>The spawn data if successfully loaded, else null</returns>
		public static SpawnData? LoadSpawnData(FPropertyTag scgClassProperty, Logger logger, string? spawnerNameForLogging, UObject? defaultScgObj = null)
		{
			UBlueprintGeneratedClass? scgClass = scgClassProperty.Tag?.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>();
			if (scgClass is null) return null;

			return LoadSpawnData(scgClass, logger, spawnerNameForLogging, defaultScgObj);
		}

		/// <summary>
		/// Load spawn data from an npc spawner class
		/// </summary>
		/// <param name="scgClass">The spawner class (derived from HShuaGuaiQi), usually referenced from a property named "SCGClass" (ShengChengGuai Class)</param>
		/// <param name="logger">For logging warnings if data failed to load</param>
		/// <param name="spawnerNameForLogging">The name of the spawner instance to use when logging warnings</param>
		/// <param name="defaultScgObj">If the passed in <see cref="scgClass" /> has no defaults object, fallback on this defaults object.</param>
		/// <returns>The spawn data if successfully loaded, else null</returns>
		public static SpawnData? LoadSpawnData(UBlueprintGeneratedClass scgClass, Logger logger, string? spawnerNameForLogging, UObject? defaultScgObj = null)
		{
			return LoadSpawnData(scgClass.AsEnumerable(), logger, spawnerNameForLogging, defaultScgObj);
		}

		/// <summary>
		/// Load spawn data from an set of npc spawner classes
		/// </summary>
		/// <param name="scgClasses">The spawner classes (derived from HShuaGuaiQi), usually referenced from a properties named "SCGClass" (ShengChengGuai Class)</param>
		/// <param name="logger">For logging warnings if data failed to load</param>
		/// <param name="spawnerNameForLogging">The name of the spawner instance to use when logging warnings</param>
		/// <param name="defaultScgObj">If the passed in <see cref="scgClass" /> has no defaults object, fallback on this defaults object.</param>
		/// <returns>The spawn data if successfully loaded, else null</returns>
		public static SpawnData? LoadSpawnData(IEnumerable<UBlueprintGeneratedClass> scgClasses, Logger logger, string? spawnerNameForLogging, UObject? defaultScgObj = null)
		{
			NpcEquipmentUtil equipmentUtil = new();

			List<ScgData> scgDataList = new();
			foreach (UBlueprintGeneratedClass scgClass in scgClasses)
			{
				ScgData scgData = new();

				BlueprintHeirarchy.SearchInheritance(scgClass, (current) =>
				{
					UObject? scgObj = current.ClassDefaultObject.Load();
					if (scgObj is null)
					{
						scgObj = defaultScgObj;
						if (scgObj is null)
						{
							logger.Warning($"[{spawnerNameForLogging}] No data found for spawner class.");
							return false;
						}
					}

					foreach (FPropertyTag property in scgObj.Properties)
					{
						switch (property.Name.Text)
						{
							case "SCGInfoList":
								if (scgData.ScgInfo is null)
								{
									scgData.ScgInfo = property.Tag?.GetValue<UScriptArray>()?.Properties.Select(p => p.GetValue<FStructFallback>()!).ToList();
								}
								break;
							case "bManRen":
								if (property.Tag!.GetValue<bool>())
								{
									scgData.IsRandomBarbarian = true;
								}
								break;
							case "ManRenMingZi":
								if (scgData.HumanName is null)
								{
									scgData.HumanName = GameUtil.ReadTextProperty(property);
								}
								break;
							case "DiWeiQuanZhong":
								if (scgData.TribeStatusMap is null)
								{
									scgData.TribeStatusMap = property.Tag?.GetValue<UScriptMap>();
								}
								break;
							case "ZhiYeQuanZhong":
								if (scgData.OccupationMap is null)
								{
									scgData.OccupationMap = property.Tag?.GetValue<UScriptMap>();
								}
								break;
							case "ClanType":
								if (!scgData.ClanType.HasValue)
								{
									if (GameUtil.TryParseEnum(property, out EClanType value))
									{
										scgData.ClanType = value;
									}
								}
								break;
							case "ClanArea":
								if (!scgData.ClanArea.HasValue)
								{
									scgData.ClanArea = property.Tag!.GetValue<int>();
								}
								break;
							case "DiWeiAndZhuangBeiDataTable":
								if (scgData.EquipmentTable is null)
								{
									UDataTable? table = property.Tag?.GetValue<FPackageIndex>()?.Load() as UDataTable;
									if (table is not null)
									{
										scgData.EquipmentTable = equipmentUtil.LoadEquipmentTable(table, logger);
									}
								}
								break;
							case "DiWeiAndWuQiDataTable":
								if (scgData.WeaponTable is null)
								{
									UDataTable? table = property.Tag?.GetValue<FPackageIndex>()?.Load() as UDataTable;
									if (table is not null)
									{
										scgData.WeaponTable = equipmentUtil.LoadEquipmentTable(table, logger);
									}
								}
								break;
						}
					}

					return scgData.IsComplete();
				});

				if (scgData.IsValid())
				{
					scgDataList.Add(scgData);
				}
			}

			if (scgDataList.Count == 0)
			{
				// Not all spawners have a spawn list baked in. Some are scripted at runtime.
				return null;
			}

			Dictionary<int, int> sgbToScgIndexMap = new();
			int scgIndex = 0, sgbIndex = 0;

			List<UScriptArray> sgbLists = new();
			List<int> spawnCounts = new();
			List<WeightedValue<EClanDiWei>> tribeStatusList = new();
			List<WeightedValue<EClanZhiYe>> occupationList = new();
			IReadOnlyDictionary<string, Range<int>>? equipmentList = null;
			IReadOnlyDictionary<string, Range<int>>? weaponList = null;
			EClanType clanType = EClanType.CLAN_TYPE_NONE;
			int clanArea = -1;
			foreach (ScgData scgData in scgDataList)
			{
				foreach (FStructFallback scgInfo in scgData.ScgInfo!)
				{
					UScriptArray? sgbList = null;
					int spawnCount = 0;
					foreach (FPropertyTag property in scgInfo.Properties)
					{
						switch (property.Name.Text)
						{
							case "SGBList":
								sgbList = property.Tag?.GetValue<UScriptArray>();
								break;
							case "GuaiSXCount":
								spawnCount = property.Tag!.GetValue<int>();
								break;
						}
					}
					if (sgbList is not null)
					{
						sgbLists.Add(sgbList);
						spawnCounts.Add(spawnCount);

						sgbToScgIndexMap[sgbIndex] = scgIndex;
						++sgbIndex;
					}
				}

				if (scgData.TribeStatusMap is not null)
				{
					List<WeightedValue<EClanDiWei>> tribeStatuses = new();
					foreach (var pair in scgData.TribeStatusMap.Properties)
					{
						EClanDiWei status;
						if (!GameUtil.TryParseEnum(pair.Key, out status)) continue;

						int weight = pair.Value!.GetValue<int>();
						tribeStatuses.Add(new(status, weight));
					}
					tribeStatusList.AddRange(WeightedValue<EClanDiWei>.Reduce(tribeStatuses));
				}

				if (scgData.OccupationMap is not null)
				{
					List<WeightedValue<EClanZhiYe>> occupations = new();
					foreach (var pair in scgData.OccupationMap.Properties)
					{
						EClanZhiYe status;
						if (!GameUtil.TryParseEnum(pair.Key, out status)) continue;

						int weight = 0;
						FStructFallback? value = pair.Value?.GetValue<FStructFallback>();
						if (value is not null)
						{
							weight = value.Properties.First().Tag!.GetValue<int>();
						}
						occupations.Add(new(status, weight));
					}
					occupationList.AddRange(WeightedValue<EClanZhiYe>.Reduce(occupations));

					if (scgData.EquipmentTable is not null)
					{
						equipmentList = scgData.EquipmentTable.GetItemsForOccupations(occupationList.Select(wv => wv.Value));
					}
					if (scgData.WeaponTable is not null)
					{
						weaponList = scgData.WeaponTable.GetItemsForOccupations(occupationList.Select(wv => wv.Value));
					}
				}

				if (scgData.ClanType is not null)
				{
					if (clanType == EClanType.CLAN_TYPE_NONE)
					{
						clanType = scgData.ClanType.Value;
					}
					else if (clanType != scgData.ClanType.Value)
					{
						if (scgData.HumanName?.Contains("Invader", StringComparison.OrdinalIgnoreCase) ?? false)
						{
							clanType = EClanType.CLAN_TYPE_INVADER;
						}
						else
						{
							logger.Warning("Spawn data contains multiple clan types. Only the first type will be recorded.");
						}
					}
				}

				if (scgData.ClanArea is not null)
				{
					if (clanArea < 0)
					{
						clanArea = scgData.ClanArea.Value;
					}
					else if (scgData.ClanArea != clanArea)
					{
						logger.Warning("Spawn data contains multiple clan areas. Only the first type will be recorded.");
					}
				}

				++scgIndex;
			}

			tribeStatusList = WeightedValue<EClanDiWei>.Reduce(tribeStatusList).ToList();
			occupationList = WeightedValue<EClanZhiYe>.Reduce(occupationList).ToList();

			if (sgbLists.Count == 0)
			{
				logger.Warning($"[{spawnerNameForLogging}] Failed to load spawn point data");
				return null;
			}

			List<WeightedValue<NpcData>> npcData = new();
			for (int i = 0; i < sgbLists.Count; ++i)
			{
				UScriptArray sgbList = sgbLists[i];
				foreach (FPropertyTagType item in sgbList.Properties)
				{
					float weight = 0.0f;
					UBlueprintGeneratedClass? @class = null;
					bool canGrow = false;
					bool directCapture = false;
					int levelMin = -1, levelMax = -1;
					string? loot = null;

					FStructFallback itemStruct = item.GetValue<FStructFallback>()!;
					foreach (FPropertyTag property in itemStruct.Properties)
					{
						switch (property.Name.Text)
						{
							case "QuanZhongBiLi":
								weight = property.Tag!.GetValue<float>();
								break;
							case "GuaiWuClass":
								@class = property.Tag?.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>();
								break;
							case "ShiFouFaYu":
								canGrow = property.Tag!.GetValue<bool>();
								break;
							case "CanDirectBuZhuo":
								directCapture = property.Tag!.GetValue<bool>();
								break;
							case "SCGZuiXiaoDengJi":
								levelMin = property.Tag!.GetValue<int>();
								break;
							case "SCGZuiDaDengJi":
								levelMax = property.Tag!.GetValue<int>();
								break;
							case "DiaoLuoBaoID":
								loot = property.Tag!.GetValue<FName>().Text;
								break;
						}
					}

					if (@class is null)
					{
						continue;
					}

					ScgData scgData = scgDataList[sgbToScgIndexMap[i]];

					npcData.Add(new(new(@class, canGrow || directCapture, levelMin, levelMax, spawnCounts[i], loot) { Name = scgData.HumanName! }, weight));
				}
			}

			if (npcData.Count == 0)
			{
				logger.Warning($"[{spawnerNameForLogging}] No NPC classes found for spawn point");
				return null;
			}

			bool isMixedAge = false;
			if (npcData.Count > 1)
			{
				bool firstIsBaby = npcData.First().Value.IsBaby;
				if (npcData.Skip(1).Any(n => n.Value.IsBaby != firstIsBaby))
				{
					isMixedAge = true;
				}
			}

			int minLevel, maxLevel;
			CalculateLevels(npcData, isMixedAge, out minLevel, out maxLevel);

			HashSet<string> humanNames = new(scgDataList.Where(d => d.HumanName is not null).Select(d => d.HumanName!));
			bool isHumanSpawner = humanNames.Count > 0;

			HashSet<string> npcNames = new(npcData.Count);
			EXingBieType defaultSex = isHumanSpawner ? EXingBieType.CHARACTER_XINGBIE_NAN : EXingBieType.CHARACTER_XINGBIE_WEIZHI;
			foreach (WeightedValue<NpcData> npc in npcData)
			{
				string? npcName = null;
				EXingBieType? sex = null;
				string? extraLoot = null;
				BlueprintHeirarchy.SearchInheritance(npc.Value.CharacterClass, (current =>
				{
					UObject? npcObj = current?.ClassDefaultObject.Load();
					if (npcObj is null)
					{
						return false;
					}

					foreach (FPropertyTag property in npcObj.Properties)
					{
						switch (property.Name.Text)
						{
							case "MoRenMingZi":
								if (npcName is null)
								{
									npcName = GameUtil.ReadTextProperty(property);
								}
								break;
							case "XingBie":
								if (!sex.HasValue && GameUtil.TryParseEnum(property, out EXingBieType xingBie))
								{
									sex = xingBie;
								}
								break;
							case "CharMorenDaoJuInitData":
								if (extraLoot is null)
								{
									FStructFallback? initData = property.Tag?.GetValue<FStructFallback>();
									if (initData is null) break;

									FPropertyTag? prop = initData.Properties.FirstOrDefault(p => p.Name.Text.Equals("ExtraDiaoLuoBaoAfterSiWang"));
									if (prop is null) break;

									extraLoot = prop.Tag?.GetValue<FName>().Text;
								}
								break;
						}
					}

					return npcName is not null && sex.HasValue && extraLoot is not null;
				}));

				if (npcName is not null)
				{
					if (npc.Value.Name is null)
					{
						npc.Value.Name = npcName;
					}
					npcNames.Add(npcName);
				}

				npc.Value.Sex = sex.HasValue ? sex.Value : defaultSex;
				npc.Value.CharacterLoot = extraLoot;
			}

			HashSet<String> outNames = isHumanSpawner ? humanNames : npcNames;
			if (outNames.Count == 0)
			{
				logger.Warning($"[{spawnerNameForLogging}] Failed to locate NPC name for spawn point");
				return null;
			}

			int totalSpawnCount = isMixedAge ? npcData.Select(wv => wv.Value).Where(n => !n.IsBaby).Sum(n => n.SpawnCount) : spawnCounts.Sum();

			return new(outNames, npcData, tribeStatusList, occupationList, equipmentList, weaponList, clanType, clanArea, minLevel, maxLevel, totalSpawnCount, isMixedAge);
		}

		public static void CalculateLevels(IEnumerable<WeightedValue<NpcData>> npcData, bool isMixedAge, out int minLevel, out int maxLevel)
		{
			minLevel = int.MaxValue;
			maxLevel = int.MinValue;

			foreach (NpcData npc in npcData.Select(n => n.Value))
			{
				// Only include adult levels if there is mix of adults and babies
				if (isMixedAge && npc.IsBaby) continue;

				if (npc.MinLevel < minLevel)
				{
					minLevel = npc.MinLevel;
				}
				if (npc.MaxLevel > maxLevel)
				{
					maxLevel = npc.MaxLevel;
				}
			}

			if (minLevel == int.MaxValue)
			{
				minLevel = (maxLevel == int.MinValue) ? 0 : maxLevel;
			}
			if (maxLevel == int.MinValue)
			{
				maxLevel = (minLevel == int.MaxValue) ? 0 : minLevel;
			}
		}

		/// <summary>
		/// Get the category of an NPC
		/// </summary>
		/// <param name="npcData">The NPC data</param>
		public static NpcCategory GetNpcCategory(NpcData npcData)
		{
			string fistNpcClass = npcData.CharacterClass.Name;

			BlueprintHeirarchy bph = BlueprintHeirarchy.Instance;

			if (bph.IsDerivedFrom(fistNpcClass, "BP_JiXie_Base_C"))
			{
				return NpcCategory.Mechanical;
			}

			if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_TuoNiao_Egg_C"))
			{
				return NpcCategory.Ostrich;
			}

			if (bph.IsDerivedFrom(fistNpcClass, "HCharacterDongWu") || bph.IsDerivedFrom(fistNpcClass, "HCharacterKurma"))
			{
				if (npcData.IsBaby)
				{
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_DaYangTuo_C"))
					{
						return NpcCategory.Llama;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_YangTuo_C"))
					{
						return NpcCategory.Alpaca;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_XueBao_C"))
					{
						return NpcCategory.Jaguar;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_BaoZi_C"))
					{
						return NpcCategory.Leopard;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_TuoNiao_Egg"))
					{
						return NpcCategory.Ostrich;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_HuoJi_C"))
					{
						return NpcCategory.Turkey;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_ShuiTun_C"))
					{
						return NpcCategory.Capybara;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_YeZhu_C"))
					{
						return NpcCategory.Boar;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_Kurma_DaXiang_C"))
					{
						return NpcCategory.Elephant;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_QiuYuXi_C"))
					{
						return NpcCategory.Lizard;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_YeNiu_C"))
					{
						return NpcCategory.Bison;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_JiaoDiao_Egg_C"))
					{
						return NpcCategory.Eagle;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_XiangGui_Egg_C"))
					{
						return NpcCategory.Tortoise;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_Kurma_TuoLu_C"))
					{
						return NpcCategory.Moose;
					}
				}
				return NpcCategory.Animal;
			}

			if (bph.IsDerivedFrom(fistNpcClass, "HCharacterRen"))
			{
				return NpcCategory.Human;
			}

			return NpcCategory.Unknown;
		}

		private struct ScgData
		{
			public List<FStructFallback>? ScgInfo;
			public bool? IsRandomBarbarian;
			public string? HumanName;
			public UScriptMap? TribeStatusMap;
			public UScriptMap? OccupationMap;
			public EClanType? ClanType;
			public int? ClanArea;
			public EquipmentTable? EquipmentTable;
			public EquipmentTable? WeaponTable;

			public bool IsValid()
			{
				return ScgInfo is not null;
			}

			public bool IsComplete()
			{
				return
					ScgInfo is not null &&
					IsRandomBarbarian.HasValue &&
					HumanName is not null &&
					TribeStatusMap is not null &&
					OccupationMap is not null &&
					ClanType.HasValue &&
					ClanArea.HasValue &&
					EquipmentTable is not null &&
					WeaponTable is not null;
			}
		}
	}

	/// <summary>
	/// An NPC class and associated data
	/// </summary>
	internal class NpcData
	{
		/// <summary>
		/// The NPC character class
		/// </summary>
		public UBlueprintGeneratedClass CharacterClass { get; }

		/// <summary>
		/// The NPC display name
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The sex of the NPC
		/// </summary>
		public EXingBieType Sex { get; set; }

		/// <summary>
		/// Whether the NPC is a baby
		/// </summary>
		public bool IsBaby { get; }

		/// <summary>
		/// The minimum spawn level
		/// </summary>
		public int MinLevel { get; }

		/// <summary>
		/// The maximum spawn level
		/// </summary>
		public int MaxLevel { get; }

		/// <summary>
		/// The spawn count
		/// </summary>
		public int SpawnCount { get; }

		/// <summary>
		/// Loot referenced from the spawner class (overrides character loot if present)
		/// </summary>
		public string? SpawnerLoot { get; }

		/// <summary>
		/// Loot referenced from the character class
		/// </summary>
		public string? CharacterLoot { get; set; }

		public NpcData(UBlueprintGeneratedClass characterClass, bool isBaby, int minLevel, int maxLevel, int spawnCount, string? loot)
		{
			CharacterClass = characterClass;
			Name = null!;
			IsBaby = isBaby;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
			SpawnCount = spawnCount;
			SpawnerLoot = loot;
		}

		public override string ToString()
		{
			return $"{CharacterClass.Name}: {(IsBaby ? "Baby" : "Adult")} {Sex.ToEn()} [{MinLevel}-{MaxLevel}]";
		}
	}

	/// <summary>
	/// Data loaded via SpawnMinerUtil.LoadSpawnData
	/// </summary>
	internal class SpawnData
	{
		/// <summary>
		/// The names of the NPCs the spawner spawns
		/// </summary>
		public IReadOnlySet<string> NpcNames { get; }

		/// <summary>
		/// The classes for the NPCs that the spawner spawns
		/// </summary>
		public IEnumerable<WeightedValue<NpcData>> NpcData { get; }

		/// <summary>
		/// Possible tribal status of spawned human NPC
		/// </summary>
		public IEnumerable<WeightedValue<EClanDiWei>> Statuses { get; }

		/// <summary>
		/// Possible occupation of spawned human NPC
		/// </summary>
		public IEnumerable<WeightedValue<EClanZhiYe>> Occupations { get; }

		/// <summary>
		/// Clan type of spawned human NPC
		/// </summary>
		public EClanType ClanType { get; }

		/// <summary>
		/// Map region of spawned human NPC
		/// </summary>
		public int ClanArea { get; }

		/// <summary>
		/// The minimum NPC level the spawner will spawn
		/// </summary>
		/// <remarks>
		/// If this is a mixed age spawner, this value only includes adult levels
		/// </remarks>
		public int MinLevel { get; }

		/// <summary>
		/// The maximum NPC level the spawner will spawn
		/// </summary>
		/// <remarks>
		/// If this is a mixed age spawner, this value only includes adult levels
		/// </remarks>
		public int MaxLevel { get; }

		/// <summary>
		/// The maximum that can be spawned by this spawner at one time
		/// </summary>
		public int SpawnCount { get; }

		/// <summary>
		/// Whether the spawner spawns a mix of adults and babies
		/// </summary>
		public bool IsMixedAge { get; }

		/// <summary>
		/// Possible equipment NPCs can spawn with
		/// </summary>
		public IReadOnlyDictionary<string, Range<int>>? EquipmentClasses { get; }

		/// <summary>
		/// Possible weapons NPCs can spawn with
		/// </summary>
		public IReadOnlyDictionary<string, Range<int>>? WeaponClasses { get; }

		public SpawnData(
			IReadOnlySet<string> npcNames,
			IEnumerable<WeightedValue<NpcData>> npcClasses,
			IEnumerable<WeightedValue<EClanDiWei>> statuses,
			IEnumerable<WeightedValue<EClanZhiYe>> occupations,
			IReadOnlyDictionary<string, Range<int>>? equipmentClasses,
			IReadOnlyDictionary<string, Range<int>>? weaponClasses,
			EClanType clanType,
			int clanArea,
			int minLevel,
			int maxLevel,
			int spawnCount,
			bool isMixedAge)
		{
			NpcNames = npcNames;
			NpcData = npcClasses;
			Statuses = statuses;
			Occupations = occupations;
			EquipmentClasses = equipmentClasses;
			WeaponClasses = weaponClasses;
			ClanType = clanType;
			ClanArea = clanArea;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
			SpawnCount = spawnCount;
			IsMixedAge = isMixedAge;
		}
	}

	/// <summary>
	/// Broad categorization of NPC type
	/// </summary>
	internal enum NpcCategory
	{
		Unknown,
		Animal,
		Mechanical,
		Human,
		Llama,
		Alpaca,
		Jaguar,
		Leopard,
		Ostrich,
		Turkey,
		Capybara,
		Boar,
		Elephant,
		Lizard,
		Bison,
		Eagle,
		Tortoise,
		Moose,
		Count
	}
}
