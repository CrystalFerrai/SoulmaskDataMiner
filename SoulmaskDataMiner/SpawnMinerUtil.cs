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

			return new(multiData.NpcNames.First(), multiData.NpcClasses, multiData.Sexes, multiData.Statuses, multiData.Occupations, multiData.MinLevel, multiData.MaxLevel, multiData.SpawnCount);
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
									scgData.ScgInfo = property.Tag?.GetValue<UScriptArray>()?.Properties[0].GetValue<FStructFallback>()!;
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
			List<WeightedValue<EClanDiWei>> tribeStatusList = new();
			List<WeightedValue<EClanZhiYe>> occupationList = new();
			int spawnCount = 0;
			foreach (ScgData scgData in scgDataList)
			{
				foreach (FPropertyTag property in scgData.ScgInfo.Properties)
				{
					switch (property.Name.Text)
					{
						case "SGBList":
							{
								UScriptArray? sgbList = property.Tag?.GetValue<UScriptArray>();
								if (sgbList is not null)
								{
									sgbLists.Add(sgbList);
								}
							}
							break;
						case "GuaiSXCount":
							spawnCount += property.Tag!.GetValue<int>();
							break;
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
			}

			tribeStatusList = WeightedValue<EClanDiWei>.Reduce(tribeStatusList).ToList();
			occupationList = WeightedValue<EClanZhiYe>.Reduce(occupationList).ToList();

			if (sgbLists.Count == 0)
			{
				logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] Failed to load spawn point data");
				return null;
			}

			List<WeightedValue<UBlueprintGeneratedClass>> npcClasses = new();
			int minLevel = int.MaxValue, maxLevel = int.MinValue;
			foreach (UScriptArray sgbList in sgbLists)
			{
				foreach (FPropertyTagType item in sgbList.Properties)
				{
					float weight = 0.0f;
					UBlueprintGeneratedClass? @class = null;

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
							case "SCGZuiXiaoDengJi":
								{
									int value = property.Tag!.GetValue<int>();
									if (minLevel > value) minLevel = value;
								}
								break;
							case "SCGZuiDaDengJi":
								{
									int value = property.Tag!.GetValue<int>();
									if (maxLevel < value) maxLevel = value;
								}
								break;
						}
					}

					if (@class is null)
					{
						continue;
					}

					npcClasses.Add(new(@class, weight));
				}
				if (npcClasses.Count == 0)
				{
					logger.Log(LogLevel.Warning, "No spawn class found in spawn data.");
				}
			}

			if (npcClasses.Count == 0)
			{
				logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] No NPC classes found for spawn point");
				return null;
			}

			if (minLevel == int.MaxValue)
			{
				minLevel = (maxLevel == int.MinValue) ? 0 : maxLevel;
			}
			if (maxLevel == int.MinValue)
			{
				maxLevel = (minLevel == int.MaxValue) ? 0 : minLevel;
			}

			HashSet<string> humanNames = new(scgDataList.Where(d => d.HumanName is not null).Select(d => d.HumanName!));
			bool isHumanSpawner = humanNames.Count > 0;

			HashSet<string> npcNames = new(npcClasses.Count);
			List<WeightedValue<EXingBieType>> sexes = new();
			EXingBieType defaultSex = isHumanSpawner ? EXingBieType.CHARACTER_XINGBIE_NAN : EXingBieType.CHARACTER_XINGBIE_WEIZHI;
			foreach (WeightedValue<UBlueprintGeneratedClass> npcClass in npcClasses)
			{
				string? npcName = null;
				EXingBieType? sex = null;
				BlueprintHeirarchy.SearchInheritance(npcClass.Value, (current =>
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
								npcName = GameUtil.ReadTextProperty(property);
								break;
							case "XingBie":
								if (GameUtil.TryParseEnum(property, out EXingBieType xingBie))
								{
									sex = xingBie;
								}
								break;
						}
					}

					return npcName is not null && sex.HasValue;
				}));

				if (npcName is not null)
				{
					npcNames.Add(npcName);
				}
				
				sexes.Add(new(sex.HasValue ? sex.Value : defaultSex, npcClass.Weight));
			}

			HashSet<String> outNames = isHumanSpawner ? humanNames : npcNames;
			if (outNames.Count == 0)
			{
				logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] Failed to locate NPC name for spawn point");
				return null;
			}

			return new(outNames, npcClasses, sexes, tribeStatusList, occupationList, minLevel, maxLevel, spawnCount);
		}

		/// <summary>
		/// Get the category of an NPC based on its class
		/// </summary>
		/// <param name="npcClass"></param>
		/// <returns></returns>
		public static NpcCategory GetNpcCategory(UBlueprintGeneratedClass npcClass)
		{
			string npcClassName = npcClass.Name;

			if (BlueprintHeirarchy.Get().IsDerivedFrom(npcClassName, "BP_JiXie_Base_C"))
			{
				return NpcCategory.Mechanical;
			}

			if (BlueprintHeirarchy.Get().IsDerivedFrom(npcClassName, "HCharacterDongWu"))
			{
				return NpcCategory.Animal;
			}

			if (BlueprintHeirarchy.Get().IsDerivedFrom(npcClassName, "HCharacterRen"))
			{
				return NpcCategory.Human;
			}

			return NpcCategory.Unknown;
		}

		private struct ScgData
		{
			public bool IsRandomBarbarian;
			public FStructFallback ScgInfo;
			public string? HumanName;
			public UScriptMap? TribeStatusMap;
			public UScriptMap? OccupationMap;

			public bool IsValid()
			{
				return ScgInfo is not null;
			}

			public bool IsComplete()
			{
				return ScgInfo is not null && HumanName is not null && TribeStatusMap is not null && OccupationMap is not null;
			}
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
		public IEnumerable<WeightedValue<UBlueprintGeneratedClass>> NpcClasses { get; }

		/// <summary>
		/// Possible sex of spawned NPC
		/// </summary>
		public IEnumerable<WeightedValue<EXingBieType>> Sexes { get; }

		/// <summary>
		/// Possible tribal status of spawned NPC
		/// </summary>
		public IEnumerable<WeightedValue<EClanDiWei>> Statuses { get; }

		/// <summary>
		/// Possible occupation of spawned NPC
		/// </summary>
		public IEnumerable<WeightedValue<EClanZhiYe>> Occupations { get; }

		/// <summary>
		/// The minimum NPC level the spawner will spawn
		/// </summary>
		public int MinLevel { get; }

		/// <summary>
		/// The maximum NPC level the spawner will spawn
		/// </summary>
		public int MaxLevel { get; }

		/// <summary>
		/// The maximum that can be spawned by this spawner at one time
		/// </summary>
		public int SpawnCount { get; }

		protected SpawnData(
			IEnumerable<WeightedValue<UBlueprintGeneratedClass>> npcClasses,
			IEnumerable<WeightedValue<EXingBieType>> sexes,
			IEnumerable<WeightedValue<EClanDiWei>> statuses,
			IEnumerable<WeightedValue<EClanZhiYe>> occupations,
			int minLevel,
			int maxLevel,
			int spawnCount)
		{
			NpcClasses = npcClasses;
			Sexes = sexes;
			Statuses = statuses;
			Occupations = occupations;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
			SpawnCount = spawnCount;
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
			IEnumerable<WeightedValue<UBlueprintGeneratedClass>> npcClasses,
			IEnumerable<WeightedValue<EXingBieType>> sexes,
			IEnumerable<WeightedValue<EClanDiWei>> statuses,
			IEnumerable<WeightedValue<EClanZhiYe>> occupations,
			int minLevel,
			int maxLevel,
			int spawnCount)
			: base(npcClasses, sexes, statuses, occupations, minLevel, maxLevel, spawnCount)
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
			IEnumerable<WeightedValue<UBlueprintGeneratedClass>> npcClasses,
			IEnumerable<WeightedValue<EXingBieType>> sexes,
			IEnumerable<WeightedValue<EClanDiWei>> statuses,
			IEnumerable<WeightedValue<EClanZhiYe>> occupations,
			int minLevel,
			int maxLevel,
			int spawnCount)
			: base(npcClasses, sexes, statuses, occupations, minLevel, maxLevel, spawnCount)
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
		CLAN_DIWEI_MAX,
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
		ZHIYE_TYPE_MAX,
	};

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

		private static string Default(Enum value)
		{
			string valueStr = value.ToString();
			return valueStr.Substring(valueStr.LastIndexOf('_') + 1).ToLowerInvariant();
		}
	}
}
