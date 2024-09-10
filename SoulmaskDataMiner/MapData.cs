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

using CUE4Parse.UE4.Objects.Core.Math;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Data about the dimensions of the world map
	/// </summary>
	internal class MapData
	{
		/// <summary>
		/// The northwest boundary of the world map
		/// </summary>
		public FVector2D BoundaryMin { get; }

		/// <summary>
		/// The southeast boundary of the world map
		/// </summary>
		public FVector2D BoundaryMax { get; }

		/// <summary>
		/// The number of cells the world is divided into along the east-west axis
		/// </summary>
		public int CellCountX { get; }

		/// <summary>
		/// The number of cells the world is divided into along the north-south axis
		/// </summary>
		public int CellCountY { get; }

		/// <summary>
		/// The width and height of the world map boundaries
		/// </summary>
		public FVector2D TotalSize { get; }

		/// <summary>
		/// The width and height of a single world cell
		/// </summary>
		public FVector2D CellSize { get; }

		/// <summary>
		/// The width and height of the image of the map
		/// </summary>
		public FVector2D ImageSize { get; }

		/// <summary>
		/// Creates an instance with default values based on the game's primary map
		/// </summary>
		public MapData()
			: this(new(-408000, -408000), new(408000, 408000), 8, 8, new(4096, 4096))
		{
		}

		/// <summary>
		/// Creates an instance with custom values
		/// </summary>
		/// <param name="boundaryMin">The northwest boundary of the world map</param>
		/// <param name="boundaryMax">The southeast boundary of the world map</param>
		/// <param name="cellCountX">The number of cells the world is divided into along the east-west axis</param>
		/// <param name="cellCountY">The number of cells the world is divided into along the north-south axis</param>
		/// <param name="imageSize">The width and height of the image of the map</param>
		public MapData(FVector2D boundaryMin, FVector2D boundaryMax, int cellCountX, int cellCountY, FVector2D imageSize)
		{
			BoundaryMin = boundaryMin;
			BoundaryMax = boundaryMax;
			CellCountX = cellCountX;
			CellCountY = cellCountY;
			TotalSize = new(boundaryMax.X - boundaryMin.X, boundaryMax.Y - boundaryMin.Y);
			CellSize = new(TotalSize.X / cellCountX, TotalSize.Y / cellCountY);
			ImageSize = imageSize;
		}

		/// <summary>
		/// Converts a location from world space to map image space
		/// </summary>
		/// <param name="world">The coordinate to convert</param>
		public FVector2D WorldToImage(FVector world)
		{
			return new(WorldToImageX(world.X), WorldToImageY(world.Y));
		}

		/// <summary>
		/// Converts the X component of a location from world space to map image space
		/// </summary>
		/// <param name="world">The coordinate to convert</param>
		public float WorldToImageX(float world)
		{
			return (float)Math.Round((world - BoundaryMin.X) / TotalSize.X * ImageSize.X);
		}

		/// <summary>
		/// Converts the Y component of a location from world space to map image space
		/// </summary>
		/// <param name="world">The coordinate to convert</param>
		public float WorldToImageY(float world)
		{
			return (float)Math.Round((world - BoundaryMin.Y) / TotalSize.Y * ImageSize.Y);
		}

		/// <summary>
		/// Converts a distance from world space to map image space
		/// </summary>
		/// <param name="world">The distance to convert</param>
		public float WorldToImage(float world)
		{
			return (float)Math.Round(world / TotalSize.Y * ImageSize.Y);
		}
	}
}
