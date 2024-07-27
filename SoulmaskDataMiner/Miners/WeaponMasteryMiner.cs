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


using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Mines data related to weapon masteries (ZhuanJing)
	/// </summary>
	internal class WeaponMasteryMiner : IDataMiner
	{
		public string Name => "Mastery";

		public bool Run(IProviderManager providerManager, Config config, Logger logger, TextWriter sqlWriter)
		{
			IReadOnlyDictionary<EWuQiLeiXing, List<MasteryData>>? masteries;
			if (!LoadMasteryData(providerManager, logger, out masteries))
			{
				return false;
			}

			WriteCsv(masteries, config, logger);
			WriteSql(masteries, sqlWriter, logger);
			WriteTextures(masteries, config, logger);

			return true;
		}

		private bool LoadMasteryData(IProviderManager providerManager, Logger logger, [NotNullWhen(true)] out IReadOnlyDictionary<EWuQiLeiXing, List<MasteryData>>? masteries)
		{
			if (!providerManager.Provider.TryFindGameFile("WS/Content/Blueprints/ZiYuanGuanLi/BP_ZiYuanGuanLiQi.uasset", out GameFile file))
			{
				logger.LogError("Unable to locate asset BP_ZiYuanGuanLiQi.");
				masteries = null;
				return false;
			}

			Package package = (Package)providerManager.Provider.LoadPackage(file);

			List<FPropertyTagType>? masteryArray = null;
			foreach (FObjectExport export in package.ExportMap)
			{
				if (export.ClassName.Equals("BP_ZiYuanGuanLiQi_C"))
				{
					UObject classDefaultObject = export.ExportObject.Value;
					foreach (FPropertyTag prop in classDefaultObject.Properties)
					{
						if (prop.Name.Text.Equals("ZhuanJingArray"))
						{
							if (prop.Tag is ArrayProperty arr)
							{
								masteryArray = arr.Value?.Properties;
							}
							break;
						}
					}
					break;
				}
			}

			// TODO: Ivestigate the "fixed mastery map" - GuDingZJAbilityMap -  which seems to specify a mastery
			// per weapon type for each mastery level (30, 60 and 90). What does this get used for?

			if (masteryArray is null)
			{
				logger.LogError("Unable to locate ZhuanJingArray in BP_ZiYuanGuanLiQi");
				masteries = null;
				return false;
			}

			Dictionary<EWuQiLeiXing, List<MasteryData>> masteryMap = new();
			foreach (StructProperty masteryProperty in masteryArray)
			{
				EWuQiLeiXing weaponType = EWuQiLeiXing.WUQI_LEIXING_NONE;
				MasteryData data = new();

				List<FPropertyTag> masteryProperties = ((FStructFallback)masteryProperty.Value!.StructType).Properties;
				foreach (FPropertyTag property in masteryProperties)
				{
					switch (property.Name.Text)
					{
						case "JiNengIndex":
							data.ID = property.Tag!.GetValue<int>();
							break;
						case "UseWuQiLeiXing":
							if (GameUtil.TryParseEnum<EWuQiLeiXing>(property, out EWuQiLeiXing wt))
							{
								weaponType = wt;
							}
							break;
						case "ZJJN":
							{
								List<FPropertyTag> zjjnProperties = ((FStructFallback)property.Tag!.GetValue<UScriptStruct>()!.StructType).Properties;
								foreach (FPropertyTag zjjnProperty in zjjnProperties)
								{
									if (zjjnProperty.Name.Text.Equals("Ability"))
									{
										UBlueprintGeneratedClass? abilityClass = zjjnProperty.Tag?.GetValue<FPackageIndex>()?.ResolvedObject?.Object?.Value as UBlueprintGeneratedClass;
										UObject? ability = abilityClass?.ClassDefaultObject.ResolvedObject?.Object?.Value;
										if (ability is null)
										{
											logger.Log(LogLevel.Warning, "Failed to load ability blueprint for mastery.");
											break;
										}
										ParseAbility(ability, ref data);
									}
								}
							}
							break;
					}
				}

				if (weaponType == EWuQiLeiXing.WUQI_LEIXING_NONE || data.Name is null)
				{
					logger.Log(LogLevel.Warning, "Missing data for mastery. It will be skipped.");
					continue;
				}

				List<MasteryData>? list;
				if (!masteryMap.TryGetValue(weaponType, out list))
				{
					list = new();
					masteryMap.Add(weaponType, list);
				}

				list.Add(data);
			}

			masteries = masteryMap;
			return true;
		}

		private void WriteCsv(IReadOnlyDictionary<EWuQiLeiXing, List<MasteryData>> masteries, Config config, Logger logger)
		{
			string outPath = Path.Combine(config.OutputDirectory, Name, $"{Name}.csv");
			using FileStream stream = IOUtil.CreateFile(outPath, logger);
			using StreamWriter writer = new(stream, Encoding.UTF8);

			writer.WriteLine("type,idx,id,name,desc,icon");

			foreach (var pair in masteries)
			{
				for (int i = 0; i < pair.Value.Count; ++i)
				{
					MasteryData data = pair.Value[i];
					writer.WriteLine($"{(int)pair.Key},{i},{data.ID},\"{data.Name}\",\"{data.Description}\",{data.Icon?.Name}");
				}
			}
		}

		private void WriteSql(IReadOnlyDictionary<EWuQiLeiXing, List<MasteryData>> masteries, TextWriter sqlWriter, Logger logger)
		{
			// Schema
			// create table `zj` (
			//     `type` int not null,
			//     `idx` int not null,
			//     `id` int not null,
			//     `name` varchar(127) not null,
			//     `desc` varchar(511),
			//     `icon` varchar(127),
			//     primary key (`type`, `idx`)
			// );

			string dbStr(string? value)
			{
				if (value is null) return "null";
				return $"'{value.Replace("\'", "\'\'")}'";
			}

			sqlWriter.WriteLine("truncate table `zj`;");
			foreach (var pair in masteries)
			{
				for (int i = 0; i < pair.Value.Count; ++i)
				{
					MasteryData data = pair.Value[i];
					sqlWriter.WriteLine($"insert into `zj` values ({(int)pair.Key}, {i}, {data.ID}, {dbStr(data.Name)}, {dbStr(data.Description)}, {dbStr(data.Icon?.Name)});");
				}
			}
		}

		private void WriteTextures(IReadOnlyDictionary<EWuQiLeiXing, List<MasteryData>> masteries, Config config, Logger logger)
		{
			string outDir = Path.Combine(config.OutputDirectory, Name, "icons");
			foreach (var pair in masteries)
			{
				for (int i = 0; i < pair.Value.Count; ++i)
				{
					if (pair.Value[i].Icon is null) continue;
					TextureExporter.ExportTexture(pair.Value[i].Icon!, false, logger, outDir);
				}
			}
		}

		private static void ParseAbility(UObject ability, ref MasteryData mastery)
		{
			List<FPropertyTag> properties = ability.Properties;
			foreach (FPropertyTag property in properties)
			{
				switch (property.Name.Text)
				{
					case "AbilityIcon":
						mastery.Icon = GameUtil.ReadTextureProperty(property);
						break;
					case "AbilityName":
						mastery.Name = GameUtil.ReadTextProperty(property);
						break;
					case "JinengMiaoshu":
						mastery.Description = GameUtil.ReadTextProperty(property);
						break;
				}
			}
		}

		private struct MasteryData
		{
			public int ID;
			public string? Name;
			public string? Description;
			public UTexture2D? Icon;

			public override string ToString()
			{
				return $"[{ID}] {Name}";
			}
		}

		private enum EWuQiLeiXing
		{
			WUQI_LEIXING_NONE,
			WUQI_LEIXING_DAO,
			WUQI_LEIXING_MAO,
			WUQI_LEIXING_GONG,
			WUQI_LEIXING_CHUI,
			WUQI_LEIXING_DUN,
			WUQI_LEIXING_QUANTAO,
			WUQI_LEIXING_SHUANGDAO,
			WUQI_LEIXING_JIAN,
			WUQI_LEIXING_TOUZHIWU,
			WUQI_LEIXING_GONGCHENGCHUI,
			WUQI_LEIXING_MAX
		};
	}
}
