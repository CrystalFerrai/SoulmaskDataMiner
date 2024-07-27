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

using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
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
	}
}
