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

using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using System.Diagnostics.CodeAnalysis;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Utility for accessing the game's resource manager asset
	/// </summary>
	internal class GameResourceManager
	{
		private readonly Package mPackage;

		public IReadOnlyList<FPropertyTag> Properties { get; }

		private GameResourceManager(Package package, IReadOnlyList<FPropertyTag> properties)
		{
			mPackage = package;
			Properties = properties;
		}

		/// <summary>
		/// Load the resource manager
		/// </summary>
		/// <param name="provider">The provider to load from</param>
		/// <param name="logger">For logging errors</param>
		/// <returns>The loaded manager, or null if there was an error</returns>
		public static GameResourceManager? Load(IFileProvider provider, Logger logger)
		{
			if (!provider.TryFindGameFile("WS/Content/Blueprints/ZiYuanGuanLi/BP_ZiYuanGuanLiQi.uasset", out GameFile file))
			{
				logger.LogError("Unable to locate resource manager asset (BP_ZiYuanGuanLiQi).");
				return null;
			}
			Package package = (Package)provider.LoadPackage(file);
			UObject? defaultsObj = GameUtil.FindBlueprintDefaultsObject(package);
			if (defaultsObj is null)
			{
				logger.LogError("Unable to load resource manager asset (BP_ZiYuanGuanLiQi).");
				return null;
			}

			return new(package, defaultsObj.Properties);
		}

		/// <summary>
		/// Returns the value of the specified resource manager property
		/// </summary>
		/// <typeparam name="T">The type of the property value</typeparam>
		/// <param name="propertyName">The name of the property</param>
		/// <param name="value">The value, if successful</param>
		/// <param name="stringComparison">Comparison to use for the property name</param>
		/// <returns>True if the value was located and matches the expected type, else false</returns>
		public bool TryGetPropertyValue<T>(string propertyName, [NotNullWhen(true)] out T value, StringComparison stringComparison = StringComparison.Ordinal)
		{
			FPropertyTag? property = Properties.FirstOrDefault(p => p.Name.Text.Equals(propertyName, stringComparison));
			if (property is null)
			{
				value = default!;
				return false;
			}

			object? outVal = property.Tag?.GetValue(typeof(T));
			if (outVal is not T typedVal)
			{
				value = default!;
				return false;
			}

			value = typedVal;
			return true;
		}
	}
}
