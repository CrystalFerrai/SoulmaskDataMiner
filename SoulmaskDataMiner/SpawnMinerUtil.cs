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
		/// <param name="scgClass">The spawner class (derived from HShuaGuaiQi), usually referenced from a property named "SCGClass" (ShengChengGuai Class)</param>
		/// <param name="logger">For logging warnings if data failed to load</param>
		/// <param name="spawnerNameForLogging">The name of the spawner instance to use when logging warnings</param>
		/// <param name="defaultScgObj">If the passed in <see cref="scgClass" /> has no defaults object, fallback on this defaults object.</param>
		/// <returns>The spawn data if successfully loaded, else null</returns>
		public static SpawnData? LoadSpawnData(UBlueprintGeneratedClass scgClass, Logger logger, string? spawnerNameForLogging, UObject? defaultScgObj = null)
		{
			MultiSpawnData? multiData = LoadSpawnData(scgClass.AsEnumerable(), logger, spawnerNameForLogging, defaultScgObj);
			if (multiData is null) return null;

			return new(multiData.NpcNames.First(), multiData.NpcClasses.First(), multiData.MinLevel, multiData.MaxLevel);
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
			List<FStructFallback> scgDataList = new();
			HashSet<string> humanNames = new();
			foreach (UBlueprintGeneratedClass scgClass in scgClasses)
			{
				FStructFallback? scgData = null;
				string? humanName = null;

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
								if (scgData is null)
								{
									scgData = property.Tag?.GetValue<UScriptArray>()?.Properties[0].GetValue<FStructFallback>();
									if (scgData is not null)
									{
										scgDataList.Add(scgData);
									}
								}
								break;
							case "ManRenMingZi":
								if (humanName is null)
								{
									humanName = GameUtil.ReadTextProperty(property);
									if (humanName is not null)
									{
										humanNames.Add(humanName);
									}
								}
								break;
						}
					}

					return scgData is not null && humanName is not null;
				});
			}

			if (scgDataList.Count == 0)
			{
				// Not all spawners have a spawn list baked in. Some are scripted at runtime.
				return null;
			}

			List<UScriptArray> sgbLists = new();
			foreach (FStructFallback scgData in scgDataList)
			{
				foreach (FPropertyTag property in scgData.Properties)
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
					}
				}
			}

			if (sgbLists.Count == 0)
			{
				logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] Failed to load spawn point data");
				return null;
			}

			List<UBlueprintGeneratedClass> npcClasses = new();
			int minLevel = int.MaxValue, maxLevel = int.MinValue;
			foreach (UScriptArray sgbList in sgbLists)
			{
				foreach (FPropertyTagType item in sgbList.Properties)
				{
					FStructFallback itemStruct = item.GetValue<FStructFallback>()!;
					foreach (FPropertyTag property in itemStruct.Properties)
					{
						switch (property.Name.Text)
						{
							case "GuaiWuClass":
								{
									UBlueprintGeneratedClass? npcClass = property.Tag?.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>();
									if (npcClass is not null)
									{
										npcClasses.Add(npcClass);
									}
								}
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

			bool isHumanSpawner = humanNames.Count > 0;

			HashSet<String> outNames;
			if (isHumanSpawner)
			{
				outNames = humanNames;
			}
			else
			{
				HashSet<string> npcNames = new(npcClasses.Count);
				foreach (UBlueprintGeneratedClass npcClass in npcClasses)
				{
					BlueprintHeirarchy.SearchInheritance(npcClass, (current =>
					{
						UObject? npcObj = current?.ClassDefaultObject.Load();
						if (npcObj is null)
						{
							return false;
						}

						string? npcName = null;
						foreach (FPropertyTag property in npcObj.Properties)
						{
							if (property.Name.Text.Equals("MoRenMingZi"))
							{
								npcName = GameUtil.ReadTextProperty(property);
								break;
							}
						}

						if (npcName is null)
						{
							return false;
						}

						npcNames.Add(npcName);
						return true;
					}));
				}
				if (npcNames.Count == 0)
				{
					logger.Log(LogLevel.Warning, $"[{spawnerNameForLogging}] Failed to locate NPC name for spawn point");
					return null;
				}
				outNames = npcNames;
			}

			return new(outNames, npcClasses, minLevel, maxLevel);
		}
	}

	/// <summary>
	/// Data loaded via <see cref="SpawnMinerUtil.LoadSpawnData(UBlueprintGeneratedClass,Logger,string,UObject)" />
	/// </summary>
	internal class SpawnData
	{
		/// <summary>
		/// The name of the NPC that the spawner spawns
		/// </summary>
		public string NpcName { get; }

		/// <summary>
		/// The class for the NPC that the spawner spawns
		/// </summary>
		public UBlueprintGeneratedClass NpcClass { get; }

		/// <summary>
		/// The minimum NPC level the spawner will spawn
		/// </summary>
		public int MinLevel { get; }

		/// <summary>
		/// The maximum NPC level the spawner will spawn
		/// </summary>
		public int MaxLevel { get; }

		public SpawnData(string npcName, UBlueprintGeneratedClass npcClass, int minLevel, int maxLevel)
		{
			NpcName = npcName;
			NpcClass = npcClass;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
		}

		public override string ToString()
		{
			return $"[{MinLevel}-{MaxLevel}] {NpcName}";
		}
	}

	/// <summary>
	/// Data loaded via <see cref="SpawnMinerUtil.LoadSpawnData(IEnumerable{UBlueprintGeneratedClass},Logger,string,UObject)" />
	/// </summary>
	internal class MultiSpawnData
	{
		/// <summary>
		/// The names of the NPCs the spawner spawns
		/// </summary>
		public IReadOnlySet<string> NpcNames { get; }

		/// <summary>
		/// The classes for the NPCs that the spawner spawns
		/// </summary>
		public IEnumerable<UBlueprintGeneratedClass> NpcClasses { get; }

		/// <summary>
		/// The minimum NPC level the spawner will spawn
		/// </summary>
		public int MinLevel { get; }

		/// <summary>
		/// The maximum NPC level the spawner will spawn
		/// </summary>
		public int MaxLevel { get; }

		public MultiSpawnData(IReadOnlySet<string> npcNames, IEnumerable<UBlueprintGeneratedClass> npcClasses, int minLevel, int maxLevel)
		{
			NpcNames = npcNames;
			NpcClasses = npcClasses;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
		}

		public override string ToString()
		{
			return $"[{MinLevel}-{MaxLevel}] {string.Join(", ", NpcNames)}";
		}
	}
}
