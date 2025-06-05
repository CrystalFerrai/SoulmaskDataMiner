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
using CUE4Parse.UE4.Assets.Exports.Engine;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Provides access to data from the common game text asset DT_YiWenText
	/// </summary>
	internal class GameTextTable : IReadOnlyDictionary<string, string>
	{
		private readonly IReadOnlyDictionary<string, string> mData;

		public string this[string key] => mData[key];

		public IEnumerable<string> Keys => mData.Keys;

		public IEnumerable<string> Values => mData.Values;

		public int Count => mData.Count;

		private GameTextTable(IReadOnlyDictionary<string, string> data)
		{
			mData = data;
		}

		public static GameTextTable? Load(IFileProvider provider, Logger logger)
		{
			if (!provider.TryFindGameFile("WS/Content/Blueprints/ZiYuanGuanLi/DT_YiWenText.uasset", out GameFile file))
			{
				logger.Error("Unable to locate asset DT_YiWenText.");
				return null;
			}

			Package package = (Package)provider.LoadPackage(file);

			UDataTable? table = package.ExportMap[0].ExportObject.Value as UDataTable;
			if (table is null)
			{
				logger.Error("Unable to read data from asset DT_YiWenText");
				return null;
			}

			Dictionary<string, string> data = new(StringComparer.OrdinalIgnoreCase);
			foreach (var pair in table.RowMap)
			{
				data.Add(pair.Key.Text, GameUtil.ReadTextProperty(pair.Value.Properties[0])!);
			}

			return new(data);
		}

		public bool ContainsKey(string key)
		{
			return mData.ContainsKey(key);
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			return mData.GetEnumerator();
		}

		public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
		{
			return mData.TryGetValue(key, out value);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)mData).GetEnumerator();
		}
	}
}
