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
	internal class FoliageUtil
	{
		// Type of ore related foliage to skip
		private static readonly HashSet<string> sFoliageBlackList = new()
		{
			"CommonRockSmall",
			"CommonRockMedium",
			"CommonRockLarge",
			"CommonRock_PickUp"
		};

		private MapData mMapData;

		public FoliageUtil(MapData mapData)
		{
			mMapData = mapData;
		}

		public IReadOnlyDictionary<EProficiency, IReadOnlyDictionary<string, FoliageData>>? LoadFoliage(IProviderManager providerManager, Logger logger)
		{
			IReadOnlyDictionary<EProficiency, IReadOnlyDictionary<string, FoliageData>>? foliageData = LoadFoliageData(providerManager, logger);
			if (foliageData is null)
			{
				return null;
			}
			Dictionary<string, FoliageData> allFoliage = new(foliageData.SelectMany(d => d.Value));

			IReadOnlyDictionary<string, IReadOnlyList<FVector>> foliage = FindFoliage(providerManager, allFoliage, logger);

			IReadOnlyDictionary<string, IReadOnlyList<Cluster>> clusters = BuildClusters(foliage, logger);

			foreach (var pair in clusters)
			{
				allFoliage[pair.Key].Locations = pair.Value;
			}

			return foliageData;
		}

		private IReadOnlyDictionary<EProficiency, IReadOnlyDictionary<string, FoliageData>>? LoadFoliageData(IProviderManager providerManager, Logger logger)
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

			Dictionary<EProficiency, IReadOnlyDictionary<string, FoliageData>> result = new();

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
					logger.Log(LogLevel.Debug, $"No loot found for foliage entry {name}");
				}

				string? foliageName = null;
				UTexture2D? foliageIcon = null;
				void getFoliageNameAndIcon(string lootId)
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

					foliageName = resultName;
					foliageIcon = resultIcon;
				}

				if (hitLootName is not null)
				{
					getFoliageNameAndIcon(hitLootName);
				}
				else if (finalHitLootName is not null)
				{
					getFoliageNameAndIcon(finalHitLootName);
				}

				if (foliageName is null || foliageIcon is null)
				{
					continue;
				}

				if (!result.TryGetValue(proficiency, out IReadOnlyDictionary<string, FoliageData>? entry))
				{
					entry = new Dictionary<string, FoliageData>();
					result.Add(proficiency, entry);
				}

				((Dictionary<string, FoliageData>)entry).Add(name, new(foliageName, hitLootName, finalHitLootName, suggestedToolClass, amount, respawnTime, foliageIcon));
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
						logger.Log(LogLevel.Debug, $"Failed to add a location for {pair.Key} to cluster builder because it is outside of the map bounds. ({location})");
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
