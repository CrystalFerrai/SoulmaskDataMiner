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
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using SoulmaskDataMiner.Data;

namespace SoulmaskDataMiner.MapUtil.Processor
{
	/// <summary>
	/// Map point of interest processor for respawn point locations
	/// </summary>
	internal class RespawnProcessor : ProcessorBase
	{
		public RespawnProcessor(MapData mapData)
			: base(mapData)
		{
		}

		public void Process(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> respawnObjects, Logger logger)
		{
			logger.Information($"Processing {respawnObjects.Count} respawn points...");

			foreach (FObjectExport respawnObject in respawnObjects)
			{
				string? name = null;
				float radius = 0.0f;
				USceneComponent? rootComponent = null;

				UObject obj = respawnObject.ExportObject.Value;
				foreach (FPropertyTag property in obj.Properties)
				{
					switch (property.Name.Text)
					{
						case "Name":
							name = DataUtil.ReadTextProperty(property);
							break;
						case "Radius":
							radius = property.Tag!.GetValue<float>();
							break;
						case "RootComponent":
							rootComponent = property.Tag?.GetValue<FPackageIndex>()?.Load<USceneComponent>();
							break;
					}
				}

				if (name is null || rootComponent is null)
				{
					logger.Warning("Respawn point properties not found");
					continue;
				}

				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Warning("Failed to locate respawn point");
					continue;
				}

				FVector location = locationProperty.Tag!.GetValue<FVector>();

				MapPoi poi = new()
				{
					GroupIndex = SpawnLayerGroup.PointOfInterest,
					Type = "Respawn Point",
					Title = "Respawn Point",
					Name = name,
					Description = radius > 0.0f ? $"Radius: {radius}" : null,
					Location = location,
					MapLocation = WorldToMap(location),
					Icon = poiDatabase.StaticData.RespawnIcon
				};

				poiDatabase.RespawnPoints.Add(poi);
			}
		}
	}
}
