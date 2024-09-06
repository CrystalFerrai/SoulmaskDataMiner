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
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Utility for gather loot table data
	/// </summary>
	internal class LootDatabase
	{
		private bool mIsLoaded;

		private readonly Dictionary<string, LootTable> mLootMap;
		private readonly Dictionary<string, CollectionData> mCollectionMap;

		/// <summary>
		/// Map of loot table keys to loot table data
		/// </summary>
		public IReadOnlyDictionary<string, LootTable> LootMap => mLootMap;

		/// <summary>
		/// Map of class names to collection data
		/// </summary>
		public IReadOnlyDictionary<string, CollectionData> CollectionMap => mCollectionMap;

		public LootDatabase()
		{
			mIsLoaded = false;
			mLootMap = new();
			mCollectionMap = new();
		}

		/// <summary>
		/// Loads data for this instance
		/// </summary>
		public bool Load(IFileProvider provider, Logger logger)
		{
			if (mIsLoaded) return true;

			if (!LoadLootData(provider, logger)) return false;
			if (!LoadCollectionData(provider, logger)) return false;

			mIsLoaded = true;
			return true;
		}

		private bool LoadLootData(IFileProvider provider, Logger logger)
		{
			foreach (var filePair in provider.Files)
			{
				if (!filePair.Key.StartsWith("WS/Content/Blueprints/DataTable/CaiJiBao")) continue;
				if (!filePair.Key.EndsWith(".uasset")) continue;

				if (!provider.TryLoadPackage(filePair.Value, out IPackage iPackage)) continue;

				Package? package = iPackage as Package;
				if (package is null) continue;

				UDataTable? table = package.ExportMap[0].ExportObject.Value as UDataTable;
				if (table is null) continue;

				FPropertyTag? rowStructProperty = table.Properties.FirstOrDefault(p => p.Name.Text.Equals("RowStruct"));
				if (rowStructProperty is null) continue;

				FPackageIndex pi = rowStructProperty.Tag!.GetValue<FPackageIndex>()!;
				if (!pi.Name.Equals("CaiJiDaoJuBaoDataTable")) continue;

				foreach (var pair in table.RowMap)
				{
					string? name = null;
					UScriptArray? contentArray = null;

					foreach (FPropertyTag property in pair.Value.Properties)
					{
						switch (property.Name.Text)
						{
							case "DaoJuBaoName":
								name = property.Tag!.GetValue<FName>().Text;
								break;
							case "DaoJuBaoContent":
								contentArray = property.Tag!.GetValue<UScriptArray>();
								break;
						}
					}

					if (name is null || contentArray is null)
					{
						logger.Log(LogLevel.Warning, $"Could not read chest data from {Path.GetFileNameWithoutExtension(filePair.Key)} row \"{pair.Key.Text}\"");
						continue;
					}

					LootTable content = new();
					foreach (FPropertyTagType contentItem in contentArray.Properties)
					{
						int probability = 0;
						UScriptArray? itemArray = null;

						FStructFallback entryData = contentItem.GetValue<FStructFallback>()!;
						foreach (FPropertyTag property in entryData.Properties)
						{
							switch (property.Name.Text)
							{
								case "SelectedRandomProbability":
									probability = property.Tag!.GetValue<int>();
									break;
								case "BaoNeiDaoJuInfos":
									itemArray = property.Tag!.GetValue<UScriptArray>();
									break;
							}
						}

						if (probability == 0 || itemArray is null)
						{
							continue;
						}

						LootEntry entry = new() { Probability = probability };

						int totalWeight = 0;
						foreach (FPropertyTagType entryItem in itemArray.Properties)
						{
							LootItem item = new();
							FPackageIndex? daoJuIndex = null;

							FStructFallback itemData = entryItem.GetValue<FStructFallback>()!;
							foreach (FPropertyTag property in itemData.Properties)
							{
								switch (property.Name.Text)
								{
									case "DaoJuQuanZhong":
										item.Weight = property.Tag!.GetValue<int>();
										totalWeight += item.Weight;
										break;
									case "DaoJuMagnitude":
										{
											TRange<float>? value = GameUtil.ReadRangeProperty<float>(property);
											if (value.HasValue) item.Amount = value.Value;
										}
										break;
									case "DaoJuPinZhi":
										if (GameUtil.TryParseEnum(property, out EDaoJuPinZhi pinZhi))
										{
											item.Quality = pinZhi;
										}
										break;
									case "DaoJuClass":
										daoJuIndex = property.Tag!.GetValue<FPackageIndex>();
										break;
								}
							}

							if (daoJuIndex is null)
							{
								totalWeight -= item.Weight;
								continue;
							}

							item.Asset = daoJuIndex.Name;

							entry.Items.Add(item);
						}

						for (int i = 0; i < entry.Items.Count; ++i)
						{
							LootItem item = entry.Items[i];
							item.Weight = (int)((float)item.Weight / totalWeight * 100.0f);
							entry.Items[i] = item;
						}

						content.Entries.Add(entry);
					}

					mLootMap.Add(name, content);
				}
			}

			return true;
		}

		private bool LoadCollectionData(IFileProvider provider, Logger logger)
		{
			if (!provider.TryFindGameFile("WS/Content/Blueprints/ZiYuanGuanLi/BP_ShengWuCollectData.uasset", out GameFile file))
			{
				logger.LogError("Unable to load BP_ShengWuCollectData");
				return false;
			}

			Package package = (Package)provider.LoadPackage(file);
			UScriptMap? configMap = GameUtil.FindBlueprintDefaultsObject(package)?.Properties.FirstOrDefault(p => p.Name.Text.Equals("ShengWuPropConfigMap"))?.Tag?.GetValue<UScriptMap>();
			if (configMap == null)
			{
				logger.LogError("Unable to load ShengWuPropConfigMap from BP_ShengWuCollectData");
				return false;
			}

			foreach (var configPair in configMap.Properties)
			{
				string key = configPair.Key.GetValue<FPackageIndex>()!.Name;
				FStructFallback config = configPair.Value!.GetValue<FStructFallback>()!;

				UScriptMap? collectMap = null;
				int amount = 0;
				float totalDamage = 0, damagePerReward = 0;
				foreach (FPropertyTag property in config.Properties)
				{
					switch (property.Name.Text)
					{
						case "ShengWuCollectDaoJuMap":
							collectMap = property.Tag?.GetValue<UScriptMap>();
							break;
						case "ShengWuCollectAmount":
							amount = property.Tag!.GetValue<int>();
							break;
						case "ShengWuCollectableTotalAmount":
							totalDamage = property.Tag!.GetValue<float>();
							break;
						case "ShengWuCollectGainDaojuDamage":
							damagePerReward = property.Tag!.GetValue<float>();
							break;
					}
				}

				if (collectMap is null || amount == 0 || totalDamage == 0 || damagePerReward == 0)
				{
					logger.Log(LogLevel.Warning, $"Unable to locate collection data for {key}");
					continue;
				}

				string? hit = null, finalHit = null, baby = null;
				foreach (var collectPair in collectMap.Properties)
				{
					FStructFallback collectObj = collectPair.Value!.GetValue<FStructFallback>()!;
					foreach (FPropertyTag property in collectObj.Properties)
					{
						switch (property.Name.Text)
						{
							case "CaiJiDaoJuBaoName":
								{
									string? item = property.Tag?.GetValue<FName>().Text;
									if (hit is null)
									{
										hit = item;
									}
									else if (!hit.Equals(item))
									{
										logger.Log(LogLevel.Warning, $"Found differing loot data values in {key}");
									}
								}
								break;
							case "CaiJiFinalDaoJuBaoName":
								{
									string? item = property.Tag?.GetValue<FName>().Text;
									if (finalHit is null)
									{
										finalHit = item;
									}
									else if (!finalHit.Equals(item))
									{
										logger.Log(LogLevel.Warning, $"Found differing loot data values in {key}");
									}
								}
								break;
							case "FaYuingCaiJiDaoJuBaoName":
								{
									string? item = property.Tag?.GetValue<FName>().Text;
									if (baby is null)
									{
										baby = item;
									}
									else if (!baby.Equals(item))
									{
										logger.Log(LogLevel.Warning, $"Found differing loot data values in {key}");
									}
								}
								break;
						}
					}
				}

				if (string.Equals(hit, "None")) hit = null;
				if (string.Equals(finalHit, "None")) finalHit = null;
				if (string.Equals(baby, "None")) baby = null;

				mCollectionMap.Add(key, new()
				{
					Hit = hit,
					FinalHit = finalHit,
					Baby = baby,
					Amount = amount
				});
			}

			return true;
		}
	}

	/// <summary>
	/// Data for generating loot when collecting from a dead animal
	/// </summary>
	internal struct CollectionData
	{
		public string? Hit;
		public string? FinalHit;
		public string? Baby;
		public int Amount;
	}

	/// <summary>
	/// The data for a named loot table
	/// </summary>
	internal class LootTable
	{
		public List<LootEntry> Entries { get; }

		public LootTable()
		{
			Entries = new();
		}

		public override string? ToString()
		{
			return $"{Entries.Count} entries";
		}
	}

	/// <summary>
	/// An entry in a loot table
	/// </summary>
	internal struct LootEntry
	{
		public int Probability;
		public List<LootItem> Items;

		public LootEntry()
		{
			Probability = 0;
			Items = new();
		}

		public override string ToString()
		{
			return $"{Probability}, {Items.Count} items";
		}
	}

	/// <summary>
	/// An item in a loot entry
	/// </summary>
	internal struct LootItem
	{
		public int Weight;
		public TRange<float> Amount;
		public EDaoJuPinZhi Quality;
		public string Asset;

		public LootItem()
		{
			Weight = 0;
			Amount = new TRange<float>();
			Quality = EDaoJuPinZhi.EDJPZ_Level1;
			Asset = null!;
		}

		public override string ToString()
		{
			return $"{Weight}, {Amount.LowerBound.Value}-{Amount.UpperBound.Value} {Asset} (Quality {(int)Quality})";
		}
	}

	/// <summary>
	/// Item quality
	/// </summary>
	internal enum EDaoJuPinZhi
	{
		EDJPZ_Level1,
		EDJPZ_Level2,
		EDJPZ_Level3,
		EDJPZ_Level4,
		EDJPZ_Level5,
		EDJPZ_Level6,
		EDJPZ_Max
	}
}
