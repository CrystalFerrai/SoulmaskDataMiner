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

		public void Process(MapPoiDatabase poiDatabase, IReadOnlyList<ObjectWithDefaults> chestObjects, IReadOnlyList<DistributionChestData> distributedChests, Logger logger)
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
			//foreach (DistributionChestData chestData in distributedChests)
			//{
			//	AddPoisForChest(poiDatabase, chestData.ChestObject.Name, chestData.ChestObject, chestData.SpawnLocations, seenChests, logger);
			//}
		}

		private void AddPoisForChest(MapPoiDatabase poiDatabase, string objectName, UObject chestObject, List<FVector>? locations, HashSet<ChestCompareData> seenChests, Logger logger)
		{
			HashSet<ChestCompareData> currentChests = new();

			ChestData? chestData = ChestDataUtil.LoadChestData(objectName, chestObject, logger);
			if (chestData is null)
			{
				logger.Warning($"[{objectName}] Unable to load data for chest");
				return;
			}

			if (locations is null)
			{
				FPropertyTag? locationProperty = chestData.RootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Warning($"[{objectName}] Failed to find location for chest");
					return;
				}

				locations = new() { locationProperty.Tag!.GetValue<FVector>() };
			}

			foreach (FVector location in locations)
			{
				ChestCompareData currentChestData = new(chestData, location);
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
					Type = chestData.Name,
					Title = chestData.Name,
					Name = "Lootable Object",
					Extra = chestData.OpenTip,
					Icon = poiDatabase.StaticData.LootIcon,
					Location = location,
					MapLocation = WorldToMap(location),
					LootId = chestData.LootId,
					LootItem = chestData.LootItem?.Name,
					SpawnInterval = chestData.RespawnTime,
					PlayerExclusionRadius = chestData.RespawnExclusionRadius
				};

				foreach (MapPoi modePoi in GetPoisForAllGameModes(poi, chestData))
				{
					poiDatabase.Lootables.Add(modePoi);
				}
			}

			seenChests.UnionWith(currentChests);
		}

		private static IEnumerable<MapPoi> GetPoisForAllGameModes(MapPoi basePoi, ChestData chestData)
		{
			if (chestData.AvailableGameModes.Count > 0)
			{
				foreach (ECustomGameMode mode in chestData.AvailableGameModes)
				{
					MapPoi modePoi = new(basePoi)
					{
						GameModeMask = mode.CreateMask()
					};
					if (chestData.GameModeLootIds.TryGetValue(mode, out string? value))
					{
						modePoi.LootId = value;
					}
					yield return modePoi;
				}
			}
			else
			{
				byte remainingModes = GameEnumExtensions.AllGameModesMask;
				foreach (var pair in chestData.GameModeLootIds)
				{
					byte modeMask = pair.Key.CreateMask();
					remainingModes &= (byte)~modeMask;
					MapPoi modePoi = new(basePoi)
					{
						GameModeMask = modeMask,
						LootId = pair.Value
					};
					yield return modePoi;
				}

				MapPoi remainingModesPoi = new(basePoi)
				{
					GameModeMask = remainingModes == GameEnumExtensions.AllGameModesMask ? null : remainingModes
				};
				yield return remainingModesPoi;
			}
		}

		private class ChestCompareData
		{
			private readonly string mChestName;
			private readonly string? mLootId;
			private readonly string? mLootItem;
			private readonly FVector mLocation;

			public ChestCompareData(ChestData chestData, FVector location)
			{
				mChestName = chestData.Name;
				mLootId = chestData.LootId;
				mLootItem = chestData.LootItem?.Name;
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
