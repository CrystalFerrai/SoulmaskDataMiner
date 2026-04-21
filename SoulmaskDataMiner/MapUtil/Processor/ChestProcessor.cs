// Copyright 2026 Crystal Ferrai
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
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using SoulmaskDataMiner.Data;
using SoulmaskDataMiner.GameData;

namespace SoulmaskDataMiner.MapUtil.Processor
{
	/// <summary>
	/// Map point of interest processor for chest locations
	/// </summary>
	internal class ChestProcessor : ProcessorBase
	{
		public ChestProcessor(MapData mapData)
			: base(mapData)
		{
		}

		public void Process(MapPoiDatabase poiDatabase, IReadOnlyList<ObjectWithDefaults> chestObjects, IReadOnlyList<ChestData> distributedChests, Logger logger)
		{
			logger.Information($"Processing {chestObjects.Count} chests...");

			HashSet<ChestCompareData> seenChests = new();

			foreach (ObjectWithDefaults chestObject in chestObjects)
			{
				HashSet<ChestCompareData> currentChests = new();

				FObjectExport export = chestObject.Export;
				UObject obj = export.ExportObject.Value;
				AddPoisForChest(poiDatabase, export.ObjectName.Text, obj, null, currentChests, logger);

				seenChests.UnionWith(currentChests);
			}

			// These chests are either duplicates of existing chests or chests that are not currently in the game.
			// They may be added in the future, so this can be uncommented for testing.
			//foreach (ChestData chestData in distributedChests)
			//{
			//	AddPoisForChest(poiDatabase, chestData.ChestObject.Name, chestData.ChestObject, chestData.SpawnLocations, seenChests, logger);
			//}
		}

		private void AddPoisForChest(MapPoiDatabase poiDatabase, string objectName, UObject chestObject, List<FVector>? locations, HashSet<ChestCompareData> seenChests, Logger logger)
		{
			HashSet<ChestCompareData> currentChests = new();

			int respawnTime = -1;
			float respawnExclusionRadius = -1.0f;
			string? lootId = null;
			string? poiName = null;
			string? openTip = null;
			FPackageIndex? lootItem = null;
			USceneComponent? rootComponent = null;
			HashSet<ECustomGameMode> availableGameModes = new();
			Dictionary<ECustomGameMode, string> gameModeLootIds = new();
			void searchProperties(UObject searchObj)
			{
				UScriptArray? openCheckList = null;

				foreach (FPropertyTag property in searchObj.Properties)
				{
					switch (property.Name.Text)
					{
						case "ShuaXinTime":
							if (respawnTime < 0)
							{
								respawnTime = property.Tag!.GetValue<int>();
							}
							break;
						case "CheckNearlyPlayerFanWei":
							if (respawnExclusionRadius < 0.0f)
							{
								respawnExclusionRadius = property.Tag!.GetValue<float>();
							}
							break;
						case "AliveCustomeGameMode":
							{
								UScriptArray? gameModeArray = property.Tag?.GetValue<UScriptArray>();
								if (gameModeArray is not null)
								{
									foreach (FPropertyTagType item in gameModeArray.Properties)
									{
										if (DataUtil.TryParseEnum(item, out ECustomGameMode gameMode))
										{
											availableGameModes.Add(gameMode);
										}
										else
										{
											logger.Warning($"[{objectName}] Chest specifies unrecognized game mode: {item.GetValue<FName>().Text}");
										}
									}
								}
							}
							break;
						case "DifferentGameModeDropID":
							{
								UScriptMap? gameModeLootMap = property.Tag?.GetValue<UScriptMap>();
								if (gameModeLootMap is not null)
								{
									foreach (var lootPair in gameModeLootMap.Properties)
									{
										ECustomGameMode mode;
										string loot;
										if (DataUtil.TryParseEnum(lootPair.Key, out mode))
										{
											loot = lootPair.Value!.GetValue<FName>().Text;
											gameModeLootIds.TryAdd(mode, loot);
										}
										else
										{
											logger.Warning($"[{objectName}] Chest specifies unrecognized game mode: {lootPair.Key.GetValue<FName>().Text}");
										}
									}
								}
							}
							break;
						case "BaoXiangDiaoLuoID":
							if (lootId is null)
							{
								lootId = property.Tag!.GetValue<FName>().Text;
							}
							break;
						case "JianZhuDisplayName":
							if (poiName is null)
							{
								poiName = DataUtil.ReadTextProperty(property);
							}
							break;
						case "OpenCheckDaoJuData":
							if (openTip is null)
							{
								openCheckList = property.Tag?.GetValue<UScriptArray>();
							}
							break;
						case "KaiQiJiaoHuDaoJuClass":
							if (lootItem is null)
							{
								lootItem = property.Tag?.GetValue<FPackageIndex>();
							}
							break;
						case "RootComponent":
							if (rootComponent is null)
							{
								rootComponent = property.Tag?.GetValue<FPackageIndex>()?.Load<USceneComponent>();
							}
							break;
					}
				}

				if (openCheckList is not null)
				{
					List<string> openTips = new();
					foreach (FPropertyTagType property in openCheckList.Properties)
					{
						FStructFallback? openCheckObj = property.GetValue<FStructFallback>();
						if (openCheckObj is null) continue;

						FPropertyTag? openTipProperty = openCheckObj.Properties.FirstOrDefault(p => p.Name.Text.Equals("NotOpenTips"));
						if (openTipProperty is null) continue;

						openTips.Add(DataUtil.ReadTextProperty(openTipProperty)!);
					}
					openTip = string.Join("<br />", openTips);
				}
			}

			searchProperties(chestObject);
			if ((respawnTime < 0 || respawnExclusionRadius < 0.0f || lootId is null || poiName is null || openTip is null || rootComponent is null) && chestObject.Class?.Load() is UBlueprintGeneratedClass objClass)
			{
				BlueprintHeirarchy.SearchInheritance(objClass, (current) =>
				{
					UObject? currentObj = current.ClassDefaultObject.Load();
					if (currentObj is null) return true;

					searchProperties(currentObj);
					return respawnTime >= 0 && respawnExclusionRadius >= 0.0f && lootId is not null && poiName is not null && openTip is not null && rootComponent is not null;
				});
			}

			if (respawnTime < 0)
			{
				respawnTime = 0; // Default from HJianZhuBaoXiang
			}
			if (respawnExclusionRadius < 0.0f)
			{
				respawnExclusionRadius = 2000.0f; // Default from HJianZhuBaoXiang
			}

			if (lootId is null && lootItem is null || poiName is null)
			{
				logger.Warning($"[{objectName}] Unable to load data for chest");
				return;
			}

			if (locations is null)
			{
				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Warning($"[{objectName}] Failed to find location for chest");
					return;
				}

				locations = new() { locationProperty.Tag!.GetValue<FVector>() };
			}

			foreach (FVector location in locations)
			{
				ChestCompareData currentChestData = new(poiName, lootId, lootItem?.Name, location);
				if (seenChests.Count > 0)
				{
					if (seenChests.Contains(currentChestData))
					{
						continue;
					}
					else
					{
						logger.Debug($"Chest not found in levels: {currentChestData}");
					}
				}

				currentChests.Add(currentChestData);

				MapPoi poi = new()
				{
					GroupIndex = SpawnLayerGroup.Chest,
					Type = poiName,
					Title = poiName,
					Name = "Lootable Object",
					Extra = openTip,
					Icon = poiDatabase.StaticData.LootIcon,
					Location = location,
					MapLocation = WorldToMap(location),
					LootId = lootId,
					LootItem = lootItem?.Name,
					SpawnInterval = respawnTime,
					PlayerExclusionRadius = respawnExclusionRadius
				};

				if (availableGameModes.Count > 0)
				{
					foreach (ECustomGameMode mode in availableGameModes)
					{
						MapPoi modePoi = new(poi)
						{
							GameModeMask = mode.CreateMask()
						};
						if (gameModeLootIds.TryGetValue(mode, out string? value))
						{
							modePoi.LootId = value;
						}
						poiDatabase.Lootables.Add(modePoi);
					}
				}
				else
				{
					byte remainingModes = 0xff;
					foreach (var pair in gameModeLootIds)
					{
						byte modeMask = pair.Key.CreateMask();
						remainingModes &= (byte)~modeMask;
						MapPoi modePoi = new(poi)
						{
							GameModeMask = modeMask,
							LootId = pair.Value
						};
						poiDatabase.Lootables.Add(modePoi);
					}

					MapPoi remainingModesPoi = new(poi)
					{
						GameModeMask = remainingModes == 0xff ? null : remainingModes
					};
					poiDatabase.Lootables.Add(remainingModesPoi);
				}
			}

			seenChests.UnionWith(currentChests);
		}

		private class ChestCompareData
		{
			private readonly string mChestName;
			private readonly string? mLootId;
			private readonly string? mLootItem;
			private readonly FVector mLocation;

			public ChestCompareData(string chestName, string? lootId, string? lootItem, FVector location)
			{
				mChestName = chestName;
				mLootId = lootId;
				mLootItem = lootItem;
				mLocation = location;
			}

			public override int GetHashCode()
			{
				// Intentionally not including location here to prevent false mismatches
				return HashCode.Combine(mChestName, mLootId, mLootItem);
			}

			public override bool Equals(object? obj)
			{
				return obj is ChestCompareData other
					&& mChestName.Equals(other.mChestName)
					&& mLootId == other.mLootId
					&& mLootItem == other.mLootItem
					&& mLocation.Equals(other.mLocation, 1.0f);
			}

			public override string ToString()
			{
				return $"{mChestName} at {mLocation}";
			}
		}
	}
}
