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

using CUE4Parse.UE4.Objects.Core.Math;
using Org.BouncyCastle.Asn1.Pkcs;
using SoulmaskDataMiner.GameData;
using System.Text;

namespace SoulmaskDataMiner.MapUtil.Processor
{
	/// <summary>
	/// Map point of interest processor for foliage locations
	/// </summary>
	internal class FoliageProcessor : ProcessorBase
	{
		public FoliageProcessor(MapData mapData)
			: base(mapData)
		{
		}

		public void Process(MapPoiDatabase poiDatabase, IReadOnlyDictionary<EProficiency, IReadOnlyDictionary<string, FoliageData>> foliageData, Logger logger)
		{
			logger.Information($"Processing {foliageData.Count} ore clusters...");

			foreach (var map in foliageData)
			{
				// Max = hand, CaiKuang = mining
				if (map.Key != EProficiency.Max && map.Key != EProficiency.CaiKuang) continue;

				bool isOre = map.Key == EProficiency.CaiKuang;

				foreach (var pair in map.Value)
				{
					FoliageData foliage = pair.Value;
					if (foliage.Locations is null) continue;

					string collectMap;
					{
						StringBuilder collectMapBuilder = new("[{");
						if (foliage.HitLootName is not null) collectMapBuilder.Append($"\"base\":\"{foliage.HitLootName}\",");
						if (foliage.FinalHitLootName is not null) collectMapBuilder.Append($"\"bonus\":\"{foliage.FinalHitLootName}\",");
						collectMapBuilder.Append($"\"amount\":{foliage.Amount}");
						collectMapBuilder.Append("}]");
						collectMap = collectMapBuilder.ToString();
					}

					string? toolClass = foliage.SuggestedToolClass;
					float spawnInterval = foliage.RespawnTime;

					foreach (Cluster location in foliage.Locations)
					{
						string nameText = isOre
							? (location.Count == 1 ? $"{location.Count} deposit" : $"{location.Count} deposits")
							: (location.Count == 1 ? $"Collectible object" : $"{location.Count} objects");

						MapPoi poi = new()
						{
							GroupIndex = isOre ? SpawnLayerGroup.Ore : SpawnLayerGroup.Pickup,
							Type = foliage.Name,
							Title = foliage.Name,
							Name = nameText,
							Description = isOre ? toolClass : null,
							SpawnCountMax = location.Count,
							SpawnInterval = spawnInterval,
							CollectMap = collectMap,
							MapLocation = WorldToMap(new(location.CenterX, location.CenterY, 0.0f)),
							MapRadius = mMapData.WorldToImage(location.CalculateRadius()),
							Icon = foliage.Icon
						};

						if (!isOre)
						{
							poi.Location = new FVector(location.CenterX, location.CenterY, location.CenterZ);
						}

						poiDatabase.Ores.Add(poi);
					}
				}
			}
		}
	}
}
