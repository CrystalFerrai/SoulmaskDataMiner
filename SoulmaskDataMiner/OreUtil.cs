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

using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	internal class OreUtil
	{
		// Type of ore related foliage to skip
		private static readonly HashSet<string> sFoliageBlackList = new()
		{
			"CommonRockSmall",
			"CommonRockMedium",
			"CommonRockLarge",
			"CommonRock_PickUp"
		};

		private static readonly Dictionary<string, string> sOreNames = new()
		{
			{ "BP_Collections_BlackStone_Medium", "Iron Ore" },
			{ "BP_Collections_Cassiterite_Medium", "Tin Ore" },
			{ "BP_Collections_Clay_Medium", "Clay" },
			{ "BP_Collections_Coal_Medium", "Coal Ore" },
			{ "BP_Collections_Common_Ice_Medium", "Ice" },
			{ "BP_Collections_Crystal_Medium", "Crystal" },
			{ "BP_Collections_Cuprite_Medium", "Copper Ore" },
			{ "BP_Collections_Iron_Medium", "Iron Ore" },
			{ "BP_Collections_Meteorites_Medium", "Meteorite Ore" },
			{ "BP_Collections_Nitre_Medium", "Nitrate Ore" },
			{ "BP_Collections_Obsidian_Medium", "Gold Obsidian" },
			{ "BP_Collections_Phosphate_Medium", "Phosphate Ore" },
			{ "BP_Collections_Salt_Medium", "Salt Mine" },
			{ "BP_Collections_SeaSalt_Medium", "Crude Salt" },
			{ "BP_Collections_Sulphur_Medium", "Sulfur Ore" }
		};

		private MapData mMapData;

		public OreUtil(MapData mapData)
		{
			mMapData = mapData;
		}

		public IReadOnlyDictionary<string, FoliageData>? LoadOreData(IProviderManager providerManager, Logger logger)
		{
			IReadOnlyDictionary<string, FoliageData>? foliageData = LoadFoliageData(providerManager, logger);
			if (foliageData is null)
			{
				return null;
			}

			IReadOnlyDictionary<string, IReadOnlyList<FVector>> foliage = FindFoliage(providerManager, foliageData, logger);

			IReadOnlyDictionary<string, IReadOnlyList<Cluster>> clusters = BuildClusters(foliage, logger);

			foreach (var pair in clusters)
			{
				foliageData[pair.Key].Locations = pair.Value;
			}

			return foliageData;
		}

		private IReadOnlyDictionary<string, FoliageData>? LoadFoliageData(IProviderManager providerManager, Logger logger)
		{
			if (!providerManager.Provider.TryFindGameFile("WS/Content/Blueprints/ZhiBei/BP_ZhiBeiConfig.uasset", out GameFile? file))
			{
				logger.LogError("Unable to find BP_ZhiBeiConfig");
				return null;
			}

			Package package = (Package)providerManager.Provider.LoadPackage(file);
			UObject? obj = GameUtil.FindBlueprintDefaultsObject(package);
			UScriptMap? foliageMap = obj?.Properties.FirstOrDefault(p => p.Name.Text.Equals("ZhiBeiPropConfigMap"))?.Tag?.GetValue<UScriptMap>();
			if (foliageMap is null)
			{
				logger.LogError("Unable to load foliage data from BP_ZhiBeiConfig");
				return null;
			}

			Dictionary<string, FoliageData> result = new();

			foreach (var pair in foliageMap.Properties)
			{
				string name = pair.Key.GetValue<FPackageIndex>()!.Name;
				if (!BlueprintHeirarchy.Instance.FoliageComponentClasses.Contains(name))
				{
					logger.Log(LogLevel.Warning, $"BP_ZhiBeiConfig references {name} which does not appear to be a foliage component type");
					continue;
				}

				FStructFallback foliageObj = pair.Value!.GetValue<FStructFallback>()!;

				UScriptMap? toolMap = null;
				EProficiency proficiency = EProficiency.FaMu;
				float respawnTime = 0.0f;
				float totalDamage = 0.0f, damagePerReward = 0.0f;

				foreach (FPropertyTag property in foliageObj.Properties)
				{
					switch (property.Name.Text)
					{
						case "GongJuCaiJiDaoJuMap":
							toolMap = property.Tag?.GetValue<UScriptMap>();
							break;
						case "ZhiBeiProficiencyType":
							if (GameUtil.TryParseEnum(property, out EProficiency profValue))
							{
								proficiency = profValue;
							}
							break;
						case "ZhiBeiRebornTimeAfterCollect":
							respawnTime = property.Tag!.GetValue<float>();
							break;
						case "ZhiBeiCollectableTotalAmount":
							totalDamage = property.Tag!.GetValue<float>();
							break;
						case "ZhiBeiCollectGainDaojuDamage":
							damagePerReward = property.Tag!.GetValue<float>();
							break;
					}
				}

				if (proficiency != EProficiency.CaiKuang)
				{
					// Not a mining resource
					continue;
				}

				if (toolMap is null)
				{
					logger.Log(LogLevel.Warning, $"Foliage data missing for entry {name}");
					continue;
				}

				float amount = 0.0f;
				if (totalDamage > 0.0f && damagePerReward > 0.0f)
				{
					amount = totalDamage / damagePerReward;
				}

				string? suggestedToolClass = null;
				string? hitLootName = null;
				string? finalHitLootName = null;

				foreach (var toolPair in toolMap.Properties)
				{
					string? currentHitLootName = null;
					string? currentFinalHitLootName = null;
					EFoliageCollectSuggestToolType? toolSuggestion = null;

					FStructFallback toolObj = toolPair.Value!.GetValue<FStructFallback>()!;
					foreach (FPropertyTag property in toolObj.Properties)
					{
						switch (property.Name.Text)
						{
							case "CaiJiDaoJuBaoName":
								currentHitLootName = property.Tag!.GetValue<FName>().Text;
								break;
							case "FinalCaiJiDaoJuBaoName":
								currentFinalHitLootName = property.Tag!.GetValue<FName>().Text;
								break;
							case "ToolSuggestType":
								if (GameUtil.TryParseEnum(property, out EFoliageCollectSuggestToolType tsValue))
								{
									toolSuggestion = tsValue;
								}
								break;
						}
					}

					if (currentHitLootName is null || currentFinalHitLootName is null || !toolSuggestion.HasValue)
					{
						logger.Log(LogLevel.Warning, $"Foliage tool data missing for entry {name}");
						continue;
					}

					if (toolSuggestion.Value == EFoliageCollectSuggestToolType.EFCSTT_SuggestLowest)
					{
						suggestedToolClass = toolPair.Key.GetValue<FPackageIndex>()!.Name;
					}

					if ((hitLootName is null || finalHitLootName is null) && toolSuggestion.Value != EFoliageCollectSuggestToolType.EFCSTT_NotSuggestUse)
					{
						hitLootName = currentHitLootName;
						finalHitLootName = currentFinalHitLootName;
					}
				}

				if (hitLootName is null || finalHitLootName is null)
				{
					logger.Log(LogLevel.Warning, $"Foliage data missing for entry {name}");
					continue;
				}

				if (hitLootName.Equals("None"))
				{
					hitLootName = null;
				}
				else if (sFoliageBlackList.Contains(hitLootName))
				{
					continue;
				}

				if (finalHitLootName.Equals("None"))
				{
					finalHitLootName = null;
				}
				else if (sFoliageBlackList.Contains(finalHitLootName))
				{
					continue;
				}

				if (hitLootName is null && finalHitLootName is null)
				{
					logger.Log(LogLevel.Warning, $"No loot found for foliage entry {name}");
					continue;
				}

				string? oreName = null;
				UTexture2D? oreIcon = null;
				void getOreNameAndIcon(string lootId)
				{
					if (!providerManager.LootDatabase.LootMap.TryGetValue(lootId, out LootTable? table))
					{
						logger.Log(LogLevel.Warning, $"Foliage entry {name} references loot table entry {hitLootName} which could not be found.");
						return;
					}

					LootEntry topEntry = new() { Probability = 0 };
					foreach (LootEntry entry in table.Entries)
					{
						if (entry.Probability > topEntry.Probability)
						{
							topEntry = entry;
						}
					}

					LootItem topItem = new() { Weight = 0 };
					foreach (LootItem item in topEntry.Items)
					{
						if (item.Weight > topItem.Weight)
						{
							topItem = item;
						}
					}

					UBlueprintGeneratedClass? itemObj = topItem.Asset.Load() as UBlueprintGeneratedClass;
					if (itemObj is null)
					{
						logger.Log(LogLevel.Warning, $"Foliage entry {name} references loot table entry {hitLootName} which references item {topItem.Asset.Name} which could not be loaded.");
						return;
					}

					string? resultName = null;
					UTexture2D? resultIcon = null;
					BlueprintHeirarchy.SearchInheritance(itemObj, current =>
					{
						UObject? itemObj = current.ClassDefaultObject.Load();
						if (itemObj is null) return false;

						foreach (FPropertyTag property in itemObj.Properties)
						{
							switch (property.Name.Text)
							{
								case "Name":
									resultName = GameUtil.ReadTextProperty(property);
									break;
								case "Icon":
									resultIcon = GameUtil.ReadTextureProperty(property);
									break;
							}
						}

						return resultName is not null && resultIcon is not null;
					});

					if (resultName is null || resultIcon is null)
					{
						logger.Log(LogLevel.Warning, $"Foliage entry {name} failed to load properties from {topItem.Asset.Name} which is referenced from table entry {hitLootName}.");
					}

					oreName = resultName;
					oreIcon = resultIcon;
				}

				if (hitLootName is not null)
				{
					getOreNameAndIcon(hitLootName);
				}
				else if (finalHitLootName is not null)
				{
					getOreNameAndIcon(finalHitLootName);
				}

				if (oreName is null || oreIcon is null)
				{
					continue;
				}

				result.Add(name, new(oreName, hitLootName, finalHitLootName, suggestedToolClass, amount, respawnTime, oreIcon));
			}

			return result;
		}

		private IReadOnlyDictionary<string, IReadOnlyList<FVector>> FindFoliage(IProviderManager providerManager, IReadOnlyDictionary<string, FoliageData> foliageData, Logger logger)
		{
			Dictionary<string, IReadOnlyList<FVector>> result = new();

			foreach (var filePair in providerManager.Provider.Files)
			{
				if (!filePair.Key.StartsWith("WS/Content/Maps/Level01/")) continue;
				if (!filePair.Key.EndsWith(".umap")) continue;

				Package package = (Package)providerManager.Provider.LoadPackage(filePair.Value);

				foreach (FObjectExport export in package.ExportMap)
				{
					if (!foliageData.ContainsKey(export.ClassName)) continue;

					UInstancedStaticMeshComponent component = (UInstancedStaticMeshComponent)export.ExportObject.Value;
					if (component.PerInstanceSMData is null || !component.PerInstanceSMData.Any()) continue;

					UObject root = component.Outer!.Properties.First(p => p.Name.Text.Equals("RootComponent")).Tag!.GetValue<FPackageIndex>()!.Load()!;
					FPropertyTag? rootLocationProperty = root.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
					FVector rootLocation = rootLocationProperty?.Tag!.GetValue<FVector>() ?? FVector.ZeroVector;

					List<FVector> locations;
					if (result.TryGetValue(export.ClassName, out IReadOnlyList<FVector>? roLocations))
					{
						locations = (List<FVector>)roLocations;
					}
					else
					{
						locations = new();
						result.Add(export.ClassName, locations);
					}

					foreach (FInstancedStaticMeshInstanceData instanceData in component.PerInstanceSMData)
					{
						locations.Add(rootLocation + instanceData.TransformData.Translation);
					}
				}
			}

			return result;
		}

		private IReadOnlyDictionary<string, IReadOnlyList<Cluster>> BuildClusters(IReadOnlyDictionary<string, IReadOnlyList<FVector>> foliage, Logger logger)
		{
			ClusterBuilder clusterBuilder = new(mMapData);

			Dictionary<string, IReadOnlyList<Cluster>> result = new();
			foreach (var pair in foliage)
			{
				clusterBuilder.Clear();
				foreach (var location in pair.Value)
				{
					if (!clusterBuilder.AddLocation(location))
					{
						logger.Log(LogLevel.Warning, $"Failed to add a location for {pair.Key} to cluster builder");
					}
				}
				clusterBuilder.BuildClusters();

				result.Add(pair.Key, clusterBuilder.Clusters!.ToArray());
			}
			return result;
		}
	}

	internal class FoliageData
	{
		public string Name { get; set; }

		public string? HitLootName { get; }

		public string? FinalHitLootName { get; }

		public string? SuggestedToolClass { get; }

		public float Amount { get; }

		public float RespawnTime { get; }

		public UTexture2D Icon { get; }

		public IReadOnlyList<Cluster>? Locations { get; set; }

		public FoliageData(string name, string? hitLootName, string? finalHitLootName, string? suggestedToolClass, float amount, float respawnTime, UTexture2D icon)
		{
			Name = name;
			HitLootName = hitLootName;
			FinalHitLootName = finalHitLootName;
			SuggestedToolClass = suggestedToolClass;
			Amount = amount;
			RespawnTime = respawnTime;
			Locations = null;
			Icon = icon;
		}

		public override string ToString()
		{
			return $"{Amount} {HitLootName} + {FinalHitLootName}";
		}
	}
}
