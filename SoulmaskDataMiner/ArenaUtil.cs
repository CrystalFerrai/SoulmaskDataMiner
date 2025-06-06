// Copyright 2025 Crystal Ferrai
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

using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Utility for gathering data about the game's battle arenas
	/// </summary>
	internal static class ArenaUtil
	{
		/// <summary>
		/// Loads the arena reward data table
		/// </summary>
		public static IReadOnlyDictionary<int, ArenaRewardData>? LoadRewardData(IProviderManager providerManager, Logger logger)
		{
			if (!providerManager.Provider.TryFindGameFile("WS/Content/Blueprints/DataTable/DT_JJC_Reward.uasset", out GameFile file))
			{
				logger.Error("Unable to load asset DT_JJC_Reward.");
				return null;
			}
			Package package = (Package)providerManager.Provider.LoadPackage(file);

			UDataTable? table = package.ExportMap[0].ExportObject.Value as UDataTable;
			if (table is null)
			{
				logger.Error("Unable to load asset DT_JJC_Reward.");
				return null;
			}

			Dictionary<int, ArenaRewardData> outData = new();
			foreach (var pair in table.RowMap)
			{
				string? lootId = null;
				string? description = null;
				List<ArenaRewardItem>? items = null;

				foreach (FPropertyTag property in pair.Value.Properties)
				{
					switch (property.Name.Text)
					{
						case "JiangLiDaoJuBaoName":
							lootId = property.Tag?.GetValue<FName>().Text;
							break;
						case "DescText":
							description = GameUtil.ReadTextProperty(property);
							break;
						case "RewardPreviewList":
							{
								UScriptArray? itemArray = property.Tag?.GetValue<UScriptArray>();
								if (itemArray is null) break;

								items = new();
								foreach (FPropertyTagType itemProperty in itemArray.Properties)
								{
									FStructFallback? itemStruct = itemProperty.GetValue<FStructFallback>();
									if (itemStruct is null) continue;

									FPackageIndex? itemAsset = null;
									int? itemCount = null;
									foreach (FPropertyTag itemValue in itemStruct.Properties)
									{
										switch (itemValue.Name.Text)
										{
											case "DaoJuClass":
												itemAsset = itemValue.Tag?.GetValue<FPackageIndex>();
												break;
											case "Count":
												itemCount = itemValue.Tag?.GetValue<int>();
												break;
										}
									}

									if (itemAsset is null || !itemCount.HasValue)
									{
										logger.Warning($"DT_JJC_Reward row '{pair.Key.Text}' is missing data");
										continue;
									}

									items.Add(new() { Asset = itemAsset, Count = itemCount.Value });
								}
							}
							break;
					}
				}

				if (lootId is null || description is null || items is null)
				{
					logger.Warning($"Failed to read DT_JJC_Reward row '{pair.Key.Text}'");
					continue;
				}

				int rewardId;
				if (!int.TryParse(pair.Key.Text, out rewardId))
				{
					logger.Warning($"DT_JJC_Reward row name '{pair.Key.Text}' is not an integer");
					continue;
				}

				if (!outData.TryAdd(rewardId, new ArenaRewardData(lootId, description, items)))
				{
					logger.Warning($"DT_JJC_Reward row name '{pair.Key.Text}' found more than once");
				}
			}
			return outData;
		}
	}

	/// <summary>
	/// Reward data for a battle arena
	/// </summary>
	internal class ArenaRewardData
	{
		public string LootId { get; }

		public string Description { get; }

		public IReadOnlyList<ArenaRewardItem> PreviewItems { get; }

		public ArenaRewardData(string lootId, string description, IReadOnlyList<ArenaRewardItem> previewItems)
		{
			LootId = lootId;
			Description = description;
			PreviewItems = previewItems;
		}
	}

	/// <summary>
	/// A game item within a battle arena reward list
	/// </summary>
	internal struct ArenaRewardItem
	{
		public FPackageIndex Asset;
		public int Count;
	}
}
