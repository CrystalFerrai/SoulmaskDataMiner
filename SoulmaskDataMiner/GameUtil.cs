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
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Utility functions for working with game objects
	/// </summary>
	internal static class GameUtil
	{
		/// <summary>
		/// Parse an enum value from a game enum
		/// </summary>
		/// <typeparam name="T">The type of the enum</typeparam>
		/// <param name="property">An enum property containing the value to parse</param>
		/// <param name="result">IF the parse was successful, this will contain the result</param>
		/// <returns>Whether the parse was successful</returns>
		public static bool TryParseEnum<T>(FPropertyTag property, out T result) where T : struct
		{
			if (property.Tag is null)
			{
				result = default;
				return false;
			}
			return TryParseEnum(property.Tag.GetValue<FName>(), out result);
		}

		/// <summary>
		/// Parse an enum value from a game enum
		/// </summary>
		/// <typeparam name="T">The type of the enum</typeparam>
		/// <param name="value">The value to parse</param>
		/// <param name="result">IF the parse was successful, this will contain the result</param>
		/// <returns>Whether the parse was successful</returns>
		public static bool TryParseEnum<T>(FName value, out T result) where T : struct
		{
			string name = value.Text.Substring(value.Text.LastIndexOf(':') + 1);
			return Enum.TryParse<T>(name, out result);
		}

		/// Attempts to read a property value as text
		/// </summary>
		/// <param name="property">The property to read</param>
		/// <returns>The text, or null if failure</returns>
		public static string? ReadTextProperty(FPropertyTag property)
		{
			return property.Tag?.GetValue<FText>()?.Text;
		}

		/// <summary>
		/// Attempts to read a property value as a texture
		/// </summary>
		/// <param name="property">The property to read</param>
		/// <returns>The texture, or null if failure</returns>
		public static UTexture2D? ReadTextureProperty(FPropertyTag property)
		{
			return property.Tag?.GetValue<FPackageIndex>()?.ResolvedObject?.Object?.Value as UTexture2D;
		}

		/// <summary>
		/// Attempts to read a property value as a texture
		/// </summary>
		/// <param name="property">The property to read</param>
		/// <returns>The texture, or null if failure</returns>
		public static UTexture2D? ReadTextureProperty(FPropertyTagType? property)
		{
			return property?.GetValue<FPackageIndex>()?.ResolvedObject?.Object?.Value as UTexture2D;
		}

		/// <summary>
		/// Locates the default properties object for a blueprint
		/// </summary>
		/// <param name="package">The package containing the blueprint</param>
		/// <returns>The object, or null if failure</returns>
		public static UObject? FindBlueprintDefaultsObject(Package package)
		{
			foreach (FObjectExport export in package.ExportMap)
			{
				if (!export.ClassName.Equals("BlueprintGeneratedClass")) continue;

				UBlueprintGeneratedClass? exportObj = export.ExportObject.Value as UBlueprintGeneratedClass;
				if (exportObj is null) continue;

				return exportObj.ClassDefaultObject.ResolvedObject?.Load();
			}

			return null;
		}

		/// <summary>
		/// Load the first texture found within an asset's exports
		/// </summary>
		/// <param name="provider">The provider to load the asset from</param>
		/// <param name="assetPath">The path to the asset</param>
		/// <param name="logger">For logging warnings and errors</param>
		/// <returns>The loaded texture, or null if no texture could be oaded</returns>
		public static UTexture2D? LoadFirstTexture(IFileProvider provider, string assetPath, Logger logger)
		{
			if (!provider.TryFindGameFile(assetPath, out GameFile file))
			{
				logger.LogError($"Unable to locate asset {assetPath}.");
				return null;
			}

			Package package = (Package)provider.LoadPackage(file);

			foreach (FObjectExport export in package.ExportMap)
			{
				if (!export.ClassName.Equals("Texture2D")) continue;

				UTexture2D? texture = export.ExportObject.Value as UTexture2D;
				if (texture is null) continue;

				return texture;
			}

			return null;
		}

		/// <summary>
		/// Converts a location from world space to map space
		/// </summary>
		/// <param name="world">The coordinate to convert</param>
		/// <param name="worldSize">The full width/height of the world</param>
		/// <param name="mapSize">The full width/height of the map texture</param>
		public static FVector2D WorldToMap(FVector world, float worldSize = 816000.0f, float mapSize = 4096.0f)
		{
			return new(WorldToMap(world.X, worldSize, mapSize), WorldToMap(world.Y, worldSize, mapSize));
		}

		/// <summary>
		/// Converts a coordinate value from world space to map space
		/// </summary>
		/// <param name="world">The value to convert</param>
		/// <param name="worldSize">The full width/height of the world</param>
		/// <param name="mapSize">The full width/height of the map texture</param>
		public static float WorldToMap(float world, float worldSize = 816000.0f, float mapSize = 4096.0f)
		{
			return (float)Math.Round((world + worldSize * 0.5f) / worldSize * mapSize);
		}
	}
}
