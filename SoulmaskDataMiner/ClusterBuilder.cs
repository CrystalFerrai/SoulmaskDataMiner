﻿// Copyright 2024 Crystal Ferrai
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
	/// Utility for grouping two dimension points into clusters based on their proximity to each other
	/// </summary>
	internal class ClusterBuilder
	{
		private readonly MapData mMapData;

		private readonly float mClusterDistanceThreshold;
		private readonly float mPartitionSize;

		private readonly int mCellCountX;
		private readonly int mCellCountY;

		private readonly List<Cluster>[,] mCells;

		public IReadOnlyList<Cluster>? Clusters { get; private set; }

		/// <summary>
		/// Create an isntance with default parameters
		/// </summary>
		/// <param name="mapData">Map data</param>
		public ClusterBuilder(MapData mapData)
			: this(mapData, mapData.CellSize.X * 0.1f, mapData.CellSize.X * 0.5f)
		{
		}

		/// <summary>
		/// Create an instance with customized parameters
		/// </summary>
		/// <param name="mapData">Map data</param>
		/// <param name="clusterDistanceThreshold">
		/// Foliage instances less than this distance apart will be grouped. Distance calculation based on Manhattan distance.
		/// This affects how output data is clustered.
		/// </param>
		/// <param name="partitionSize">
		/// Size to break world into for collision detection algorithm. This affects running time and and memory usage of the
		/// algorithm. Must be at least double the value of GroupDistanceThreshold to not break the algorithm.
		/// </param>
		public ClusterBuilder(MapData mapData, float clusterDistanceThreshold, float partitionSize)
		{
			if (partitionSize < clusterDistanceThreshold * 2)
			{
				throw new ArgumentException("Partition size must be at least double the value of group distance threshold at minimum");
			}

			mMapData = mapData;

			mClusterDistanceThreshold = clusterDistanceThreshold;
			mPartitionSize = partitionSize;

			mCellCountX = (int)Math.Ceiling(mapData.TotalSize.X / partitionSize);
			mCellCountY = (int)Math.Ceiling(mapData.TotalSize.Y / partitionSize);

			mCells = new List<Cluster>[mCellCountX, mCellCountY];
			for (int y = 0; y < mCellCountY; ++y)
			{
				for (int x = 0; x < mCellCountX; ++x)
				{
					mCells[x, y] = new List<Cluster>();
				}
			}
		}

		/// <summary>
		/// Adds a location to the builder
		/// </summary>
		/// <param name="location">The location to add</param>
		/// <returns>Whether the location was able to be added</returns>
		public bool AddLocation(FVector location)
		{
			int x = (int)Math.Floor((location.X - mMapData.BoundaryMin.X) / mPartitionSize);
			int y = (int)Math.Floor((location.Y - mMapData.BoundaryMin.Y) / mPartitionSize);

			if (x < 0 || x >= mCells.GetLength(0) ||
				y < 0 || y >= mCells.GetLength(1))
			{
				return false; // Discard if outside the map bounds
			}

			List<Cluster> clusters = mCells[x, y];

			bool added = false;
			for (int i = 0; i < clusters.Count; ++i)
			{
				Cluster cluster = clusters[i];
				if (cluster.AddLocation(location))
				{
					clusters[i] = cluster;
					added = true;
					break;
				}
			}
			if (!added)
			{
				clusters.Add(new Cluster(location, mClusterDistanceThreshold));
			}

			return true;
		}

		/// <summary>
		/// Builds clusters using all previously added locations. The result will
		/// be stored in the Clusters property.
		/// </summary>
		public void BuildClusters()
		{
			for (int y = 0; y < mCellCountY - 1; ++y)
			{
				for (int x = 0; x < mCellCountX - 1; ++x)
				{
					List<Cluster> targets = mCells[x, y];
					for (int y2 = 0; y2 <= 1; ++y2)
					{
						for (int x2 = 0; x2 <= 1; ++x2)
						{
							if (x2 == 0 && y2 == 0) continue;

							List<Cluster> sources = mCells[x + x2, y + y2];

							for (int t = 0; t < targets.Count; ++t)
							{
								Cluster target = targets[t];
								for (int s = 0; s < sources.Count; ++s)
								{
									if (target.CombineWith(sources[s]))
									{
										targets[t] = target;
										sources.RemoveAt(s--);
									}
								}
							}
						}
					}
				}
			}

			List<Cluster> clusters = new();
			for (int y = 0; y < mCellCountY; ++y)
			{
				for (int x = 0; x < mCellCountX; ++x)
				{
					clusters.AddRange(mCells[x, y]);
				}
			}
			Clusters = clusters;
		}

		/// <summary>
		/// Clears all locations and clusters from this instance
		/// </summary>
		public void Clear()
		{
			for (int y = 0; y < mCellCountY; ++y)
			{
				for (int x = 0; x < mCellCountX; ++x)
				{
					mCells[x, y].Clear();
				}
			}

			Clusters = null;
		}

		public override string ToString()
		{
			var cast = mCells.Cast<List<Cluster>>();
			return $"{cast.Count(v => v.Count > 0)} cells | {cast.Sum(v => v.Count)} instances";
		}
	}

	/// <summary>
	/// Represents a cluster of data points built by a ClsuterBuilder
	/// </summary>
	internal struct Cluster
	{
		private readonly float mClusterDistanceThreshold;

		/// <summary>
		/// The position of the west side of a bounding box encompassing all points in the cluster
		/// </summary>
		public float MinX;

		/// <summary>
		/// The position of the east side of a bounding box encompassing all points in the cluster
		/// </summary>
		public float MaxX;

		/// <summary>
		/// The position of the north side of a bounding box encompassing all points in the cluster
		/// </summary>
		public float MinY;

		/// <summary>
		/// The position of the south side of a bounding box encompassing all points in the cluster
		/// </summary>
		public float MaxY;

		/// <summary>
		/// The nubmer of points in the cluster
		/// </summary>
		public int Count;

		/// <summary>
		/// The X compoent of the center point of the bouding box
		/// </summary>
		public float CenterX => (MinX + MaxX) * 0.5f;

		/// <summary>
		/// The Y compoent of the center point of the bounding box
		/// </summary>
		public float CenterY => (MinY + MaxY) * 0.5f;

		/// <summary>
		/// Creates an instance
		/// </summary>
		/// <param name="initialLocation">The first point in the cluster</param>
		/// <param name="clusterDistanceThreshold">The maximum distance any two points within the cluster can be apart</param>
		public Cluster(FVector initialLocation, float clusterDistanceThreshold)
		{
			mClusterDistanceThreshold = clusterDistanceThreshold;
			MinX = MaxX = initialLocation.X;
			MinY = MaxY = initialLocation.Y;
			Count = 1;
		}

		/// <summary>
		/// Attempts to add a point to the cluster
		/// </summary>
		/// <param name="location">The location of the point to add</param>
		/// <returns>Whether the point was successfully added</returns>
		public bool AddLocation(FVector location)
		{
			if (location.X < MinX + mClusterDistanceThreshold &&
				location.X > MaxX - mClusterDistanceThreshold &&
				location.Y < MinY + mClusterDistanceThreshold &&
				location.Y > MaxY - mClusterDistanceThreshold)
			{
				if (location.X < MinX) MinX = location.X;
				else if (location.X > MaxX) MaxX = location.X;

				if (location.Y < MinY) MinY = location.Y;
				else if (location.Y > MaxY) MaxY = location.Y;

				++Count;

				return true;
			}

			return false;
		}

		/// <summary>
		/// Attempts to combine another clsuter into this one
		/// </summary>
		/// <param name="other">The cluster to add</param>
		/// <returns>Whether the clsuter was successfully combined</returns>
		public bool CombineWith(Cluster other)
		{
			if (Math.Abs(MaxX - other.MinX) < mClusterDistanceThreshold &&
				Math.Abs(MinX - other.MaxX) < mClusterDistanceThreshold &&
				Math.Abs(MaxY - other.MinY) < mClusterDistanceThreshold &&
				Math.Abs(MinY - other.MaxY) < mClusterDistanceThreshold)
			{
				if (other.MinX < MinX) MinX = other.MinX;
				else if (other.MaxX > MaxX) MaxX = other.MaxX;

				if (other.MinY < MinY) MinY = other.MinY;
				else if (other.MaxY > MaxY) MaxY = other.MaxY;

				Count += other.Count;

				return true;
			}

			return false;
		}

		public override string ToString()
		{
			return $"({CenterX},{CenterY}) Count={Count}";
		}
	}
}
