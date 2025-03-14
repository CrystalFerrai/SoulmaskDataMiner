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

using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using System.Diagnostics;
using System.Text;

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

			logger.Information("Loading loot database..");

			Stopwatch timer = new Stopwatch();
			timer.Start();

			if (!LoadLootData(provider, logger)) return false;
			if (!LoadCollectionData(provider, logger)) return false;

			timer.Stop();

			logger.Information($"Loot database load completed in {((double)timer.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0):0.##}ms");

			mIsLoaded = true;
			return true;
		}

		/// <summary>
		/// Saves loot data to output directory
		/// </summary>
		/// <param name="sqlWriter">For writing sql data</param>
		/// <param name="config">For obtaining an directory for csv output</param>
		/// <param name="logger">For logging messages</param>
		public void SaveData(ISqlWriter sqlWriter, Config config, Logger logger)
		{
			logger.Information("Saving loot data...");
			WriteCsvLoot(config, logger);
			WriteSqlLoot(sqlWriter, logger);
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
						logger.Warning($"Could not read chest data from {Path.GetFileNameWithoutExtension(filePair.Key)} row \"{pair.Key.Text}\"");
						continue;
					}

					LootTable content = new();
					foreach (FPropertyTagType contentItem in contentArray.Properties)
					{
						int probability = 0;
						UScriptArray? conditionArray = null;
						UScriptArray? itemArray = null;

						FStructFallback entryData = contentItem.GetValue<FStructFallback>()!;
						foreach (FPropertyTag property in entryData.Properties)
						{
							switch (property.Name.Text)
							{
								case "SelectedRandomProbability":
									probability = property.Tag!.GetValue<int>();
									break;
								case "ConditionAndCheckData":
									conditionArray = property.Tag!.GetValue<UScriptArray>();
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

						if (conditionArray is not null)
						{
							foreach (FPropertyTagType conditionObj in conditionArray.Properties)
							{
								UScriptArray? innerArray = conditionObj.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("ConditionOrCheck"))?.Tag?.GetValue<UScriptArray>();
								if (innerArray is null) continue;

								foreach (FPropertyTagType conditionProp in innerArray.Properties)
								{
									string conditionName = conditionProp.GetValue<FPackageIndex>()!.Name;
									switch (conditionName)
									{
										case "BP_Condition_Area_IsDaCaoYuan_C":
											entry.AreaCondition = 8;
											break;
										case "BP_Condition_Area_IsHuangYuan_C":
											entry.AreaCondition = 3;
											break;
										case "BP_Condition_Area_IsHuAnSenLin_C":
											entry.AreaCondition = 9;
											break;
										case "BP_Condition_Area_IsHuoShan_C":
											entry.AreaCondition = 4;
											break;
										case "BP_Condition_Area_IsJuMuLin_C":
											entry.AreaCondition = 7;
											break;
										case "BP_Condition_Area_IsQiuLing_C":
											entry.AreaCondition = 6;
											break;
										case "BP_Condition_Area_IsXueShan_C":
											entry.AreaCondition = 5;
											break;
										case "BP_Condition_Area_IsYuLin_C":
											entry.AreaCondition = 1;
											break;
										case "BP_Condition_Area_IsZhaoze_C":
										case "BP_Condition_Area_IsZhaoZe_C":
											entry.AreaCondition = 2;
											break;
										case "BP_Condition_GongJiangDrop_C":
											entry.CraftsmanOnly = true;
											break;
										case "BP_Condition_IsShenMi_C":
											entry.ClanCondition = (int)EClanType.CLAN_TYPE_C;
											break;
										case "BP_Condition_IsYeMan_C":
											entry.ClanCondition = (int)EClanType.CLAN_TYPE_A;
											break;
										case "BP_Condition_IsZhiHui_C":
											entry.ClanCondition = (int)EClanType.CLAN_TYPE_B;
											break;
										case "TJ_DL_IsPVP_C":
											entry.PvpOnly = true;
											break;
										case "TJ_DL_PVP_1Day+_C":
											entry.PvpDayCondition = 1;
											break;
										case "TJ_DL_PVP_3Day+_C":
											entry.PvpDayCondition = 3;
											break;
										case "TJ_DL_PVP_7Day+_C":
											entry.PvpDayCondition = 7;
											break;
										default:
											break;
									}
								}
							}
						}

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

							if (daoJuIndex is null || daoJuIndex.Name.Equals("None"))
							{
								totalWeight -= item.Weight;
								continue;
							}

							item.Asset = daoJuIndex;

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

			if (mLootMap.Count == 0)
			{
				logger.Error("Failed to load any loot tables");
				return false;
			}

			return true;
		}

		private bool LoadCollectionData(IFileProvider provider, Logger logger)
		{
			if (!provider.TryFindGameFile("WS/Content/Blueprints/ZiYuanGuanLi/BP_ShengWuCollectData.uasset", out GameFile file))
			{
				logger.Error("Unable to load BP_ShengWuCollectData");
				return false;
			}

			Package package = (Package)provider.LoadPackage(file);
			UScriptMap? configMap = GameUtil.FindBlueprintDefaultsObject(package)?.Properties.FirstOrDefault(p => p.Name.Text.Equals("ShengWuPropConfigMap"))?.Tag?.GetValue<UScriptMap>();
			if (configMap == null)
			{
				logger.Error("Unable to load ShengWuPropConfigMap from BP_ShengWuCollectData");
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
					logger.Warning($"Unable to locate collection data for {key}");
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
										logger.Warning($"Found differing loot data values in {key}");
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
										logger.Warning($"Found differing loot data values in {key}");
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
										logger.Warning($"Found differing loot data values in {key}");
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

		private void WriteCsvLoot(Config config, Logger logger)
		{
			string outPath = Path.Combine(config.OutputDirectory, "loot.csv");
			using FileStream outFile = IOUtil.CreateFile(outPath, logger);
			using StreamWriter writer = new(outFile, Encoding.UTF8);

			writer.WriteLine("id,entry,item,chance,cond,weight,min,max,quality,asset");

			foreach (var pair in LootMap)
			{
				for (int e = 0; e < pair.Value.Entries.Count; ++e)
				{
					LootEntry entry = pair.Value.Entries[e];
					string? conditions = entry.GetConditionsJson();
					for (int i = 0; i < entry.Items.Count; ++i)
					{
						LootItem item = entry.Items[i];
						writer.WriteLine($"{SqlUtil.CsvStr(pair.Key)},{e},{i},{entry.Probability},{SqlUtil.CsvStr(conditions)},{item.Weight},{item.Amount.LowerBound.Value},{item.Amount.UpperBound.Value},{(int)item.Quality},{SqlUtil.CsvStr(item.Asset.Name)}");
					}
				}
			}
		}

		private void WriteSqlLoot(ISqlWriter sqlWriter, Logger logger)
		{
			// create table `loot` (
			//   `id` varchar(127) not null,
			//   `entry` int not null,
			//   `item` int not null,
			//   `chance` int not null,
			//   `cond` varchar(255),
			//   `weight` int not null,
			//   `min` int not null,
			//   `max` int not null,
			//   `quality` int not null,
			//   `asset` varchar(127) not null,
			//   primary key (`id`, `entry`, `item`)
			// )

			sqlWriter.WriteStartTable("loot");

			foreach (var pair in LootMap)
			{
				for (int e = 0; e < pair.Value.Entries.Count; ++e)
				{
					LootEntry entry = pair.Value.Entries[e];
					string? conditions = entry.GetConditionsJson();
					for (int i = 0; i < entry.Items.Count; ++i)
					{
						LootItem item = entry.Items[i];
						sqlWriter.WriteRow($"{SqlUtil.DbStr(pair.Key)}, {e}, {i}, {entry.Probability}, {SqlUtil.DbStr(conditions)}, {item.Weight}, {item.Amount.LowerBound.Value}, {item.Amount.UpperBound.Value}, {(int)item.Quality}, {SqlUtil.DbStr(item.Asset.Name)}");
					}
				}
			}

			sqlWriter.WriteEndTable();
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

		public int? AreaCondition;
		public int? ClanCondition;
		public bool CraftsmanOnly;
		public bool PvpOnly;
		public int? PvpDayCondition;
		public bool UnknownCondition;

		public LootEntry()
		{
			Probability = 0;
			Items = new();
			AreaCondition = null;
			ClanCondition = null;
			CraftsmanOnly = false;
			PvpOnly = false;
			PvpDayCondition = null;
			UnknownCondition = false;
		}

		public string? GetConditionsJson()
		{
			if (AreaCondition.HasValue || ClanCondition.HasValue || CraftsmanOnly || PvpOnly || PvpDayCondition.HasValue || UnknownCondition)
			{
				StringBuilder builder = new("{");
				if (AreaCondition.HasValue) builder.Append($"\"area\":{AreaCondition.Value},");
				if (ClanCondition.HasValue) builder.Append($"\"clan\":{ClanCondition.Value},");
				if (CraftsmanOnly) builder.Append($"\"craft\":1,");
				if (PvpOnly) builder.Append($"\"pvp\":1,");
				if (PvpDayCondition.HasValue) builder.Append($"\"pvpday\":{PvpDayCondition.Value},");
				if (UnknownCondition) builder.Append($"\"unknown\":1,");
				--builder.Length; // Remove trailing comma
				builder.Append("}");
				return builder.ToString();
			}
			return null;
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
		public FPackageIndex Asset;

		public LootItem()
		{
			Weight = 0;
			Amount = new TRange<float>();
			Quality = EDaoJuPinZhi.EDJPZ_Level1;
			Asset = null!;
		}

		public override string ToString()
		{
			return $"{Weight}, {Amount.LowerBound.Value}-{Amount.UpperBound.Value} {Asset.Name} (Quality {(int)Quality})";
		}
	}
}
