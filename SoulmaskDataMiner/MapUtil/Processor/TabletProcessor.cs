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
	/// Map point of interest processor for tablet locations
	/// </summary>
	internal class TabletProcessor : ProcessorBase
	{
		public TabletProcessor(MapData mapData)
			: base(mapData)
		{
		}

		public void Process(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> tabletObjects, Logger logger)
		{
			logger.Information($"Processing {tabletObjects.Count} tablets...");
			foreach (FObjectExport tabletObject in tabletObjects)
			{
				if (!poiDatabase.Tablets.TryGetValue(tabletObject.ClassName, out MapPoi? poi))
				{
					logger.Warning("Tablet object data missing. This should not happen.");
					continue;
				}

				UObject obj = tabletObject.ExportObject.Value;
				FPropertyTag? rootComponentProperty = obj.Properties.FirstOrDefault(p => p.Name.Text.Equals("RootComponent"));
				UObject? rootComponent = rootComponentProperty?.Tag?.GetValue<FPackageIndex>()?.Load();
				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Warning("Failed to locate tablet POI");
					continue;
				}

				poi.Location = locationProperty.Tag!.GetValue<FVector>();
				poi.MapLocation = WorldToMap(poi.Location.Value);
			}
		}
	}
}
