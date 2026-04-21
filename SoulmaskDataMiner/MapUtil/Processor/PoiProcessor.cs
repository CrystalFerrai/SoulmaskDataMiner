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
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner.MapUtil.Processor
{
	/// <summary>
	/// General map point of interest processor
	/// </summary>
	internal class PoiProcessor : ProcessorBase
	{
		public PoiProcessor(MapData mapData)
			: base(mapData)
		{
		}

		public void Process(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> poiObjects, Logger logger)
		{
			logger.Information($"Processing {poiObjects.Count} POIs...");
			foreach (FObjectExport poiObject in poiObjects)
			{
				int? index = null;
				UObject? rootComponent = null;

				UObject obj = poiObject.ExportObject.Value;
				foreach (FPropertyTag property in obj.Properties)
				{
					switch (property.Name.Text)
					{
						case "ParamInt":
							index = property.Tag?.GetValue<int>();
							break;
						case "RootComponent":
							rootComponent = property.Tag?.GetValue<FPackageIndex>()?.ResolvedObject?.Object?.Value;
							break;
					}
				}

				if (!index.HasValue) continue;

				if (!poiDatabase.IndexLookup.TryGetValue(index.Value, out MapPoi? poi))
				{
					continue;
				}

				if (rootComponent is null)
				{
					logger.Warning($"Failed to locate POI {index}");
					continue;
				}

				FPropertyTag? locationProperty = rootComponent.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Warning($"Failed to locate POI {index}");
					continue;
				}
				poi.Location = locationProperty.Tag!.GetValue<FVector>();
				poi.MapLocation = WorldToMap(poi.Location.Value);
			}
		}

		public void FindPoiTextures(MapPoiDatabase poiDatabase, Logger logger)
		{
			foreach (var pair in poiDatabase.TypeLookup)
			{
				if (!poiDatabase.StaticData.MapIcons.TryGetValue(pair.Key, out var texture))
				{
					continue;
				}
				foreach (MapPoi poi in pair.Value)
				{
					poi.Icon = texture;
				}
			}
		}
	}
}
