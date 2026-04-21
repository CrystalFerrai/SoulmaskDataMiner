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
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using SoulmaskDataMiner.Data;

namespace SoulmaskDataMiner.MapUtil
{
	/// <summary>
	/// Helper for reading the chest distribution map from a level config
	/// </summary>
	/// <remarks>
	/// Distribution maps in the level config seem to indicate intended locations for objects, but those objects may or may not
	/// be present in the game world. This data should only be used for informational purposes and not presented as accurate to
	/// the game world. Some instances are present in the world while others are not. The ones which are can be located by
	/// searching for the actors within the levels instead of using this data.
	/// </remarks>
	internal class ChestDistributionMap
	{
		private readonly MapLevelData mMapLevelData;

		public ChestDistributionMap(MapLevelData mapLevelData)
		{
			mMapLevelData = mapLevelData;
		}

		public IReadOnlyList<ChestData>? Load(Logger logger)
		{
			UScriptMap? levelMap = mMapLevelData.ConfigData.Properties.FirstOrDefault(p => p.Name.Text.Equals("ChestDistributionMap"))?.Tag?.GetValue<UScriptMap>();
			if (levelMap is null)
			{
				logger.Warning($"Could not read ChestDistributionMap from config data for {mMapLevelData.MapName}");
				return null;
			}

			List<ChestData> chests = new();

			foreach (var levelPair in levelMap.Properties)
			{
				if (!levelPair.Key.GetValue<string>()!.Equals(mMapLevelData.MapName))
				{
					continue;
				}

				UScriptMap? chestMap = levelPair.Value?.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("ChestDistributionMap"))?.Tag?.GetValue<UScriptMap>();
				if (chestMap is null)
				{
					logger.Warning($"Could not read ChestDistributionMap from config data for {mMapLevelData.MapName}");
					continue;
				}

				foreach (var chestPair in chestMap.Properties)
				{
					FPackageIndex? chestClassIndex = chestPair.Key.GetValue<FPackageIndex>();
					if (chestClassIndex is null)
					{
						logger.Warning($"Could not read chest class from ChestDistributionMap for {mMapLevelData.MapName}");
						continue;
					}
					UObject? chestObject = DataUtil.FindBlueprintDefaultsObject(chestClassIndex);
					if (chestObject is null)
					{
						logger.Warning($"Could not find chest class defaults for {chestClassIndex} in ChestDistributionMap for {mMapLevelData.MapName}");
						continue;
					}

					UScriptArray? mapInfosArray = chestPair.Value?.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("FoliageMapInfos"))?.Tag?.GetValue<UScriptArray>();
					if (mapInfosArray is null)
					{
						logger.Warning($"Could not read FoliageMapInfos for chest {chestObject.Name} in ChestDistributionMap for {mMapLevelData.MapName}");
						continue;
					}

					ChestData chestData = new()
					{
						ChestObject = chestObject,
						SpawnLocations = new()
					};

					foreach (FPropertyTagType mapInfoProperty in mapInfosArray.Properties)
					{
						FPropertyTagType? locationProperty = mapInfoProperty.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("InstanceLocation"))?.Tag;
						if (locationProperty is null)
						{
							logger.Warning($"Could not read InstanceLocation for chest {chestObject.Name} in ChestDistributionMap for {mMapLevelData.MapName}");
							continue;
						}

						chestData.SpawnLocations.Add(locationProperty.GetValue<FVector>());
					}

					chests.Add(chestData);
				}
			}

			return chests;
		}
	}

	internal struct ChestData
	{
		public UObject ChestObject;
		public List<FVector> SpawnLocations;
	}
}
