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

using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Utility for loading NPC equipment tables
	/// </summary>
	internal class NpcEquipmentUtil
	{
		private readonly Dictionary<string, EquipmentTable> mCachedEquipmentTables;

		public NpcEquipmentUtil()
		{
			mCachedEquipmentTables = new();
		}

		/// <summary>
		/// Loads an equipment table or retrieves it from a cache if it has already been loaded
		/// </summary>
		/// <param name="sourceTable">The data table to read from</param>
		/// <param name="logger">For logging problems</param>
		/// <returns>The loaded table, or null if loading failed</returns>
		public EquipmentTable? LoadEquipmentTable(UDataTable sourceTable, Logger logger)
		{
			if (!mCachedEquipmentTables.TryGetValue(sourceTable.Name, out var equipmentTable))
			{
				FPropertyTag? rowStructProperty = sourceTable.Properties.FirstOrDefault(p => p.Name.Text.Equals("RowStruct"));
				if (rowStructProperty is null)
				{
					logger.Warning("Unable to read equipment data table type");
					return null;
				}

				string rowStruct = rowStructProperty.Tag!.GetValue<FPackageIndex>()!.Name;
				int tableType = rowStruct.Equals("DiWeiHeZhuangBeiLvTableData") ? 1 : rowStruct.Equals("DiWeiWuQiTableData") ? 2 : 0;
				if (tableType == 0)
				{
					logger.Warning($"Data table type {rowStruct} cannot be read as an equipment data table");
					return null;
				}

				equipmentTable = new();
				if (tableType == 1)
				{
					equipmentTable.LoadEquipmentTable(sourceTable, logger);
				}
				else
				{
					equipmentTable.LoadWeaponTable(sourceTable, logger);
				}

				if (equipmentTable is not null)
				{
					mCachedEquipmentTables.Add(sourceTable.Name, equipmentTable);
				}
			}

			return equipmentTable;
		}
	}

	/// <summary>
	/// An NPC equipment table
	/// </summary>
	internal class EquipmentTable
	{
		private readonly Dictionary<EClanDiWei, IReadOnlyList<EquipmentTableRow>> mRows;

		/// <summary>
		/// The rows of data in the table
		/// </summary>
		public IReadOnlyDictionary<EClanDiWei, IReadOnlyList<EquipmentTableRow>> Rows => mRows;

		public EquipmentTable()
		{
			mRows = new();
		}

		/// <summary>
		/// Retrieves all possible equipment for the given list of NPC occupations
		/// </summary>
		/// <param name="occupations">The opccupations to get equipment for</param>
		/// <returns>The class name and amount of each equipment type</returns>
		public IReadOnlyDictionary<string, Range<int>> GetItemsForOccupations(IEnumerable<EClanZhiYe> occupations)
		{
			Dictionary<string, Range<int>> result = new();

			foreach (var pair in mRows)
			{
				foreach (EquipmentTableRow row in pair.Value)
				{
					foreach (EClanZhiYe occupation in occupations)
					{
						if (row.Occupations.Contains(occupation))
						{
							foreach (var item in row.Items)
							{
								if (result.TryGetValue(item.Key, out Range<int> value))
								{
									result[item.Key] = value.Combine(item.Value);
								}
								else
								{
									result.Add(item.Key, item.Value);
								}
							}
							break;
						}
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Internally used by NpcEquipmentUtil
		/// </summary>
		internal bool LoadEquipmentTable(UDataTable table, Logger logger)
		{
			foreach (var pair in table.RowMap)
			{
				EClanDiWei? clanStatus = null;
				UScriptArray? rowData = null;

				foreach (FPropertyTag property in pair.Value.Properties)
				{
					switch (property.Name.Text)
					{
						case "DiWei":
							if (GameUtil.TryParseEnum(property, out EClanDiWei value))
							{
								clanStatus = value;
							}
							break;
						case "DwAndZBLv":
							rowData = property.Tag?.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("DWZhuangBei"))?.Tag?.GetValue<UScriptArray>();
							break;
					}
				}

				if (!clanStatus.HasValue || rowData is null)
				{
					logger.Warning("Equipment table could not be parsed");
					return false;
				}

				List<EquipmentTableRow> rows = new();
				foreach (FPropertyTagType item in rowData.Properties)
				{
					FStructFallback? rowObj = item.GetValue<FStructFallback>();
					if (rowObj is null)
					{
						logger.Warning("Equipment table row could not be parsed");
						continue;
					}

					EquipmentTableRow row = new();
					row.LoadEquipmentRow(rowObj, logger);
					rows.Add(row);
				}

				mRows.Add(clanStatus.Value, rows);
			}

			return true;
		}

		/// <summary>
		/// Internally used by NpcEquipmentUtil
		/// </summary>
		internal bool LoadWeaponTable(UDataTable table, Logger logger)
		{
			foreach (var pair in table.RowMap)
			{
				EClanDiWei? clanStatus = null;
				UScriptArray? rowData = null;

				foreach (FPropertyTag property in pair.Value.Properties)
				{
					switch (property.Name.Text)
					{
						case "DiWei":
							if (GameUtil.TryParseEnum(property, out EClanDiWei value))
							{
								clanStatus = value;
							}
							break;
						case "ZhiYeWuQi":
							rowData = property.Tag?.GetValue<UScriptArray>();
							break;
					}
				}

				if (!clanStatus.HasValue || rowData is null)
				{
					logger.Warning("Weapon table could not be parsed");
					return false;
				}

				List<EquipmentTableRow> rows = new();
				foreach (FPropertyTagType item in rowData.Properties)
				{
					FStructFallback? rowObj = item.GetValue<FStructFallback>();
					if (rowObj is null)
					{
						logger.Warning("Weapon table row could not be parsed");
						continue;
					}

					EquipmentTableRow row = new();
					row.LoadWeaponRow(rowObj, logger);
					rows.Add(row);
				}

				mRows.Add(clanStatus.Value, rows);
			}

			return true;
		}
	}

	/// <summary>
	/// A row of data from an EquipmentTable
	/// </summary>
	internal class EquipmentTableRow
	{
		private readonly HashSet<EClanZhiYe> mOccupations;
		private readonly Dictionary<string, Range<int>> mItems;

		/// <summary>
		/// The NPC occupations associated with this row
		/// </summary>
		public IReadOnlySet<EClanZhiYe> Occupations => mOccupations;

		/// <summary>
		/// Items and amounts
		/// </summary>
		public IReadOnlyDictionary<string, Range<int>> Items => mItems;

		public EquipmentTableRow()
		{
			mOccupations = new();
			mItems = new();
		}

		/// <summary>
		/// Internally used by EquipmentTable
		/// </summary>
		public bool LoadEquipmentRow(FStructFallback data, Logger logger)
		{
			UScriptArray? occupationArray = null;
			UScriptMap? itemClassMap = null;

			foreach (FPropertyTag property in data.Properties)
			{
				switch (property.Name.Text)
				{
					case "ZhiYeList":
						occupationArray = property.Tag?.GetValue<UScriptArray>();
						break;
					case "ZhiYeZhuangBeiMap":
						itemClassMap = property.Tag?.GetValue<UScriptMap>();
						break;
				}
			}

			if (occupationArray is null || itemClassMap is null)
			{
				logger.Warning("Equipment table row could not be parsed");
				return false;
			}

			foreach (FPropertyTagType item in occupationArray.Properties)
			{
				if (GameUtil.TryParseEnum(item, out EClanZhiYe value))
				{
					mOccupations.Add(value);
				}
				else
				{
					logger.Warning("Unable to parse occupation value in equipment table row");
				}
			}

			foreach (var pair in itemClassMap.Properties)
			{
				FPackageIndex? value = pair.Value?.GetValue<FPackageIndex>();
				if (value is null)
				{
					logger.Warning("Unable to parse item class value in equipment table row");
					continue;
				}
				if (value.Name.Equals("None"))
				{
					continue;
				}
				mItems.Add(value.Name, new(0, 1));
			}

			return true;
		}

		/// <summary>
		/// Internally used by EquipmentTable
		/// </summary>
		public bool LoadWeaponRow(FStructFallback data, Logger logger)
		{
			UScriptArray? occupationArray = null;
			FStructFallback? essentialWeaponObj = null;
			UScriptArray? weaponDataArray = null;

			foreach (FPropertyTag property in data.Properties)
			{
				switch (property.Name.Text)
				{
					case "ZhiYeList":
						occupationArray = property.Tag?.GetValue<UScriptArray>();
						break;
					case "BiBeiWuQi":
						essentialWeaponObj = property.Tag?.GetValue<FStructFallback>();
						break;
					case "ZhuanJingWuQi":
						weaponDataArray = property.Tag?.GetValue<UScriptArray>();
						break;
				}
			}

			if (occupationArray is null || essentialWeaponObj is null || weaponDataArray is null)
			{
				logger.Warning("Equipment table row could not be parsed");
				return false;
			}

			foreach (FPropertyTagType item in occupationArray.Properties)
			{
				if (GameUtil.TryParseEnum(item, out EClanZhiYe value))
				{
					mOccupations.Add(value);
				}
				else
				{
					logger.Warning("Unable to parse occupation value in equipment table row");
				}
			}

			UScriptArray? weaponArray = null;
			UScriptArray? ammoArray = null;
			int ammoMin = 0;
			int ammoMax = 0;
			foreach (FPropertyTag property in essentialWeaponObj.Properties)
			{
				switch (property.Name.Text)
				{
					case "WuQiTypeList":
						weaponArray = property.Tag?.GetValue<UScriptArray>();
						break;
					case "ZiDanClassList":
						ammoArray = property.Tag?.GetValue<UScriptArray>();
						break;
					case "ZiDanMin":
						ammoMin = property.Tag!.GetValue<int>();
						break;
					case "ZiDanMax":
						ammoMax = property.Tag!.GetValue<int>();
						break;
				}
			}

			if (weaponArray is not null)
			{
				foreach (FPropertyTagType item in weaponArray.Properties)
				{
					UScriptArray? innerWeaponArray = item.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("BiBeiWuQiList"))?.Tag?.GetValue<UScriptArray>();
					if (innerWeaponArray is null) continue;

					foreach (FPropertyTagType innerItem in innerWeaponArray.Properties)
					{
						FPackageIndex? value = innerItem?.GetValue<FPackageIndex>();
						if (value is null)
						{
							logger.Warning("Unable to parse item class value in weapon table row");
							continue;
						}
						if (value.Name.Equals("None"))
						{
							continue;
						}
						mItems.Add(value.Name, new(0, 1));
					}
				}
			}

			if (ammoArray is not null)
			{
				foreach (FPropertyTagType item in ammoArray.Properties)
				{
					FPackageIndex? value = item?.GetValue<FPackageIndex>();
					if (value is null)
					{
						logger.Warning("Unable to parse item class value in weapon table row");
						continue;
					}
					if (value.Name.Equals("None"))
					{
						continue;
					}
					mItems.Add(value.Name, new(0, ammoMax));
				}
			}

			foreach (FPropertyTagType weaponDataItem in weaponDataArray.Properties)
			{
				FStructFallback? weaponDataItemObj = weaponDataItem.GetValue<FStructFallback>();
				if (weaponDataItemObj is null)
				{
					logger.Warning("Unable to parse weapon table row");
					continue;
				}

				weaponArray = null;
				ammoArray = null;
				ammoMin = 0;
				ammoMax = 0;

				foreach (FPropertyTag property in weaponDataItemObj.Properties)
				{
					switch (property.Name.Text)
					{
						case "WuQiClassList":
							weaponArray = property.Tag?.GetValue<UScriptArray>();
							break;
						case "ZiDanClassList":
							ammoArray = property.Tag?.GetValue<UScriptArray>();
							break;
						case "ZiDanMin":
							ammoMin = property.Tag!.GetValue<int>();
							break;
						case "ZiDanMax":
							ammoMax = property.Tag!.GetValue<int>();
							break;
					}
				}

				if (weaponArray is not null)
				{
					foreach (FPropertyTagType item in weaponArray.Properties)
					{
						FPackageIndex? value = item?.GetValue<FPackageIndex>();
						if (value is null)
						{
							logger.Warning("Unable to parse item class value in weapon table row");
							continue;
						}
						if (value.Name.Equals("None"))
						{
							continue;
						}
						mItems.Add(value.Name, new(0, 1));
					}
				}

				if (ammoArray is not null)
				{
					foreach (FPropertyTagType item in ammoArray.Properties)
					{
						FPackageIndex? value = item?.GetValue<FPackageIndex>();
						if (value is null)
						{
							logger.Warning("Unable to parse item class value in weapon table row");
							continue;
						}
						if (value.Name.Equals("None"))
						{
							continue;
						}
						mItems.Add(value.Name, new(0, ammoMax));
					}
				}
			}

			return true;
		}
	}
}
