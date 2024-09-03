// Copyright 2024 Crystal Ferrai
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
		public static SingleSpawnData? LoadSpawnData(FPropertyTag scgClassProperty, Logger logger, string? spawnerNameForLogging, UObject? defaultScgObj = null)
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
		public static SingleSpawnData? LoadSpawnData(UBlueprintGeneratedClass scgClass, Logger logger, string? spawnerNameForLogging, UObject? defaultScgObj = null)
		{
			MultiSpawnData? multiData = LoadSpawnData(scgClass.AsEnumerable(), logger, spawnerNameForLogging, defaultScgObj);
			if (multiData is null) return null;

			return new(
				multiData.NpcNames.First(),
				multiData.NpcData,
				multiData.Statuses,
				multiData.Occupations,
				multiData.ClanType,
				multiData.MinLevel,
				multiData.MaxLevel,
				multiData.SpawnCount,
				multiData.IsMixedAge);
		}

		/// <summary>
		/// Load spawn data from an set of npc spawner classes
		/// </summary>
		/// <param name="scgClasses">The spawner classes (derived from HShuaGuaiQi), usually referenced from a properties named "SCGClass" (ShengChengGuai Class)</param>
		/// <param name="logger">For logging warnings if data failed to load</param>
		/// <param name="spawnerNameForLogging">The name of the spawner instance to use when logging warnings</param>
		/// <param name="defaultScgObj">If the passed in <see cref="scgClass" /> has no defaults object, fallback on this defaults object.</param>
		/// <returns>The spawn data if successfully loaded, else null</returns>
		public static MultiSpawnData? LoadSpawnData(IEnumerable<UBlueprintGeneratedClass> scgClasses, Logger logger, string? spawnerNameForLogging, UObject? defaultScgObj = null)
		{
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
							logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] No data found for spawner class.");
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

			List<UScriptArray> sgbLists = new();
			List<int> spawnCounts = new();
			List<WeightedValue<EClanDiWei>> tribeStatusList = new();
			List<WeightedValue<EClanZhiYe>> occupationList = new();
			EClanType clanType = EClanType.CLAN_TYPE_NONE;
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
							logger.Log(LogLevel.Warning, "Spawn data contains multiple clan types. Only the first type will be recorded.");
						}
					}
				}
			}

			tribeStatusList = WeightedValue<EClanDiWei>.Reduce(tribeStatusList).ToList();
			occupationList = WeightedValue<EClanZhiYe>.Reduce(occupationList).ToList();

			if (sgbLists.Count == 0)
			{
				logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] Failed to load spawn point data");
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
					bool isBaby = false;
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
								isBaby = property.Tag!.GetValue<bool>();
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

					npcData.Add(new(new(@class, isBaby, levelMin, levelMax, spawnCounts[i], loot), weight));
				}
			}

			if (npcData.Count == 0)
			{
				logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] No NPC classes found for spawn point");
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
			foreach (WeightedValue<NpcData> npcClass in npcData)
			{
				string? npcName = null;
				EXingBieType? sex = null;
				string? extraLoot = null;
				BlueprintHeirarchy.SearchInheritance(npcClass.Value.CharacterClass, (current =>
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
					npcNames.Add(npcName);
				}

				npcClass.Value.Sex = sex.HasValue ? sex.Value : defaultSex;
				npcClass.Value.ExtraLoot = extraLoot;
			}

			HashSet<String> outNames = isHumanSpawner ? humanNames : npcNames;
			if (outNames.Count == 0)
			{
				logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] Failed to locate NPC name for spawn point");
				return null;
			}

			int totalSpawnCount = isMixedAge ? npcData.Select(wv => wv.Value).Where(n => !n.IsBaby).Sum(n => n.SpawnCount) : spawnCounts.Sum();

			return new(outNames, npcData, tribeStatusList, occupationList, clanType, minLevel, maxLevel, totalSpawnCount, isMixedAge);
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

			BlueprintHeirarchy bph = BlueprintHeirarchy.Get();

			if (bph.IsDerivedFrom(fistNpcClass, "BP_JiXie_Base_C"))
			{
				return NpcCategory.Mechanical;
			}

			if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_TuoNiao_Egg_C"))
			{
				return NpcCategory.Ostrich;
			}

			if (bph.IsDerivedFrom(fistNpcClass, "HCharacterDongWu"))
			{
				if (npcData.IsBaby)
				{
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_BaoZi_C") || bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_XueBao_C"))
					{
						return NpcCategory.Cats;
					}
					if (bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_YangTuo_C") || bph.IsDerivedFrom(fistNpcClass, "BP_DongWu_DaYangTuo_C"))
					{
						return NpcCategory.Lamas;
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
			public bool? IsRandomBarbarian;
			public List<FStructFallback>? ScgInfo;
			public string? HumanName;
			public UScriptMap? TribeStatusMap;
			public UScriptMap? OccupationMap;
			public EClanType? ClanType;

			public bool IsValid()
			{
				return ScgInfo is not null;
			}

			public bool IsComplete()
			{
				return ScgInfo is not null && IsRandomBarbarian.HasValue && HumanName is not null && TribeStatusMap is not null && OccupationMap is not null && ClanType.HasValue;
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
		/// Loot dropped by the NPC
		/// </summary>
		public string? Loot { get; }

		/// <summary>
		/// Additional loot dropped by the NPC
		/// </summary>
		public string? ExtraLoot { get; set; }

		public NpcData(UBlueprintGeneratedClass characterClass, bool isBaby, int minLevel, int maxLevel, int spawnCount, string? loot)
		{
			CharacterClass = characterClass;
			IsBaby = isBaby;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
			SpawnCount = spawnCount;
			Loot = loot;
		}

		public override string ToString()
		{
			return $"{CharacterClass.Name}: {(IsBaby ? "Baby" : "Adult")} {Sex.ToEn()} [{MinLevel}-{MaxLevel}]";
		}
	}

	/// <summary>
	/// Base class for spawn data generated by <see cref="SpawnMinerUtil" />
	/// </summary>
	internal abstract class SpawnData
	{
		/// <summary>
		/// The classes for the NPCs that the spawner spawns
		/// </summary>
		public IEnumerable<WeightedValue<NpcData>> NpcData { get; }

		/// <summary>
		/// Possible tribal status of spawned NPC
		/// </summary>
		public IEnumerable<WeightedValue<EClanDiWei>> Statuses { get; }

		/// <summary>
		/// Possible occupation of spawned NPC
		/// </summary>
		public IEnumerable<WeightedValue<EClanZhiYe>> Occupations { get; }

		/// <summary>
		/// Clan type of spawned NPC
		/// </summary>
		public EClanType ClanType { get; }

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

		protected SpawnData(
			IEnumerable<WeightedValue<NpcData>> npcClasses,
			IEnumerable<WeightedValue<EClanDiWei>> statuses,
			IEnumerable<WeightedValue<EClanZhiYe>> occupations,
			EClanType clanType,
			int minLevel,
			int maxLevel,
			int spawnCount,
			bool isMixedAge)
		{
			NpcData = npcClasses;
			Statuses = statuses;
			Occupations = occupations;
			ClanType = clanType;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
			SpawnCount = spawnCount;
			IsMixedAge = isMixedAge;
		}
	}

	/// <summary>
	/// Data loaded via <see cref="SpawnMinerUtil.LoadSpawnData(UBlueprintGeneratedClass,Logger,string,UObject)" />
	/// </summary>
	internal class SingleSpawnData : SpawnData
	{
		/// <summary>
		/// The name of the NPC that the spawner spawns
		/// </summary>
		public string NpcName { get; }

		public SingleSpawnData(
			string npcName,
			IEnumerable<WeightedValue<NpcData>> npcClasses,
			IEnumerable<WeightedValue<EClanDiWei>> statuses,
			IEnumerable<WeightedValue<EClanZhiYe>> occupations,
			EClanType clanType,
			int minLevel,
			int maxLevel,
			int spawnCount,
			bool isMixedAge)
			: base(npcClasses, statuses, occupations, clanType, minLevel, maxLevel, spawnCount, isMixedAge)
		{
			NpcName = npcName;
		}

		public override string ToString()
		{
			return $"[{MinLevel}-{MaxLevel}] {NpcName}";
		}
	}

	/// <summary>
	/// Data loaded via <see cref="SpawnMinerUtil.LoadSpawnData(IEnumerable{UBlueprintGeneratedClass},Logger,string,UObject)" />
	/// </summary>
	internal class MultiSpawnData : SpawnData
	{
		/// <summary>
		/// The names of the NPCs the spawner spawns
		/// </summary>
		public IReadOnlySet<string> NpcNames { get; }

		public MultiSpawnData(
			IReadOnlySet<string> npcNames,
			IEnumerable<WeightedValue<NpcData>> npcClasses,
			IEnumerable<WeightedValue<EClanDiWei>> statuses,
			IEnumerable<WeightedValue<EClanZhiYe>> occupations,
			EClanType clanType,
			int minLevel,
			int maxLevel,
			int spawnCount,
			bool isMixedAge)
			: base(npcClasses, statuses, occupations, clanType, minLevel, maxLevel, spawnCount, isMixedAge)
		{
			NpcNames = npcNames;
		}

		public override string ToString()
		{
			return $"[{MinLevel}-{MaxLevel}] {string.Join(", ", NpcNames)}";
		}
	}

	/// <summary>
	/// Represents a value and an associated weight
	/// </summary>
	/// <typeparam name="T">The type of the value</typeparam>
	internal class WeightedValue<T> where T : notnull
	{
		/// <summary>
		/// The value
		/// </summary>
		public T Value { get; }

		/// <summary>
		/// The weight of the value proportional to other weighted values
		/// </summary>
		public double Weight { get; private set; }

		public WeightedValue(T value, double weight)
		{
			Value = value;
			Weight = weight;
		}

		public override string ToString()
		{
			return $"{Value}: {Weight}";
		}

		/// <summary>
		/// Combines weights with matching values and calculates relative weight values.
		/// </summary>
		/// <param name="collection">The collection to reduce</param>
		/// <returns>
		/// A new collection where each value occurs only once, and the weight is a percentage of
		/// the total weight of all values.
		/// </returns>
		/// <remarks>
		/// Values will only be combined if their GetHashCode and Equals functions both indicate
		/// that they are the same value. This will work by defualt for primitive types. Complex
		/// types will need to implement these functions to ensure desired results.
		/// </remarks>
		public static IEnumerable<WeightedValue<T>> Reduce(IEnumerable<WeightedValue<T>> collection)
		{
			Dictionary<T, WeightedValue<T>> map = new();
			double totalWeight = 0.0;
			foreach (WeightedValue<T> item in collection)
			{
				if (item.Weight == 0.0) continue;

				totalWeight += item.Weight;

				WeightedValue<T>? current;
				if (!map.TryGetValue(item.Value, out current))
				{
					current = new(item.Value, 0.0);
					map.Add(item.Value, current);
				}
				current.Weight += item.Weight;
			}

			foreach (WeightedValue<T> current in map.Values)
			{
				current.Weight = current.Weight / totalWeight;
			}

			return map.Values;
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
		Lamas,
		Cats,
		Ostrich,
		Count
	}

	/// <summary>
	/// Sex of character
	/// </summary>
	internal enum EXingBieType
	{
		CHARACTER_XINGBIE_NAN,
		CHARACTER_XINGBIE_NV,
		CHARACTER_XINGBIE_MAX,
		CHARACTER_XINGBIE_WEIZHI
	};

	/// <summary>
	/// Tirbal status of human NPC
	/// </summary>
	internal enum EClanDiWei
	{
		CLAN_DIWEI_LOW,
		CLAN_DIWEI_MIDDLE,
		CLAN_DIWEI_HIGH,
		CLAN_DIWEI_MAX
	};

	/// <summary>
	/// Occupation of human NPC
	/// </summary>
	internal enum EClanZhiYe
	{
		ZHIYE_TYPE_NONE,
		ZHIYE_TYPE_WUWEI,
		ZHIYE_TYPE_SHOULIE,
		ZHIYE_TYPE_SHOUHU,
		ZHIYE_TYPE_KULI,
		ZHIYE_TYPE_ZAGONG,
		ZHIYE_TYPE_ZONGJIANG,
		ZHIYE_TYPE_ZHIZHE,
		ZHIYE_TYPE_XIULIAN,
		ZHIYE_TYPE_JISHI,
		ZHIYE_TYPE_MAX
	};

	/// <summary>
	/// Clan membership of human NPC
	/// </summary>
	internal enum EClanType
	{
		CLAN_TYPE_NONE,
		CLAN_TYPE_A,
		CLAN_TYPE_B,
		CLAN_TYPE_C,
		CLAN_TYPE_D,
		CLAN_TYPE_E,
		CLAN_TYPE_F,
		CLAN_TYPE_MAX,
		CLAN_TYPE_INVADER // Not part of original enum
	}

	/// <summary>
	/// Exntension methods for NPC related enums
	/// </summary>
	internal static class NpcEnumExtensions
	{
		public static string ToEn(this EXingBieType value)
		{
			return value switch
			{
				EXingBieType.CHARACTER_XINGBIE_NAN => "Male",
				EXingBieType.CHARACTER_XINGBIE_NV => "Female",
				EXingBieType.CHARACTER_XINGBIE_WEIZHI => "Random", // Technically "Unknown", but means "Random" for spawners
				_ => Default(value)
			};
		}

		/// <summary>
		/// Return an English representation of the value
		/// </summary>
		public static string ToEn(this EClanDiWei value)
		{
			// Values from DT_YiWenText ClanDiWei_#
			return value switch
			{
				EClanDiWei.CLAN_DIWEI_LOW => "Novice",
				EClanDiWei.CLAN_DIWEI_MIDDLE => "Skilled",
				EClanDiWei.CLAN_DIWEI_HIGH => "Master",
				_ => Default(value)
			};
		}

		/// <summary>
		/// Return an English representation of the value
		/// </summary>
		public static string ToEn(this EClanZhiYe value)
		{
			// Values from DT_YiWenText ZhiYe_#
			return value switch
			{
				EClanZhiYe.ZHIYE_TYPE_NONE => "Vagrant",
				EClanZhiYe.ZHIYE_TYPE_WUWEI => "Warrior",
				EClanZhiYe.ZHIYE_TYPE_SHOULIE => "Hunter",
				EClanZhiYe.ZHIYE_TYPE_SHOUHU => "Guard",
				EClanZhiYe.ZHIYE_TYPE_KULI => "Laborer",
				EClanZhiYe.ZHIYE_TYPE_ZAGONG => "Porter",
				EClanZhiYe.ZHIYE_TYPE_ZONGJIANG => "Craftsman",
				_ => Default(value)
			};
		}

		/// <summary>
		/// Return an English representation of the value
		/// </summary>
		public static string ToEn(this EClanType value)
		{
			return value switch
			{
				EClanType.CLAN_TYPE_NONE => "Unaffiliated",
				EClanType.CLAN_TYPE_A => "Claw Tribe",
				EClanType.CLAN_TYPE_B => "Flint Tribe",
				EClanType.CLAN_TYPE_C => "Fang Tribe",
				EClanType.CLAN_TYPE_D => "Plunderer",
				EClanType.CLAN_TYPE_E => "Unknown",
				EClanType.CLAN_TYPE_F => "Unknown",
				EClanType.CLAN_TYPE_INVADER => "Invader",
				_ => Default(value)
			};
		}

		private static string Default(Enum value)
		{
			string valueStr = value.ToString();
			return valueStr.Substring(valueStr.LastIndexOf('_') + 1).ToLowerInvariant();
		}
	}
}
