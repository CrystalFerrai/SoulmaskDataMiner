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

namespace SoulmaskDataMiner.MapUtil.Processor
{
	/// <summary>
	/// Base class for map point of interest processors
	/// </summary>
	internal abstract class ProcessorBase
	{
		protected readonly MapData mMapData;

		protected ProcessorBase(MapData mapData)
		{
			mMapData = mapData;
		}

		protected FVector2D WorldToMap(FVector world)
		{
			return mMapData.WorldToImage(world);
		}
	}
}
