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

using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Mines data about character proficiencies
	/// </summary>
	internal class ProficiencyMiner : IDataMiner
	{
		public string Name => "Proficiency";

		public bool Run(IProviderManager providerManager, Config config, Logger logger, TextWriter sqlWriter)
		{
			IEnumerable<ProficiencyData>? proficiencies;
			if (!LoadProficiencyData(providerManager, logger, out proficiencies))
			{
				return false;
			}

			WriteCsv(proficiencies, config, logger);
			WriteSql(proficiencies, sqlWriter, logger);
			WriteTextures(proficiencies, config, logger);

			return true;
		}

		private bool LoadProficiencyData(IProviderManager providerManager, Logger logger, [NotNullWhen(true)] out IEnumerable<ProficiencyData>? proficiencies)
		{
			if (!providerManager.Provider.TryFindGameFile("WS/Content/Blueprints/UI/ShuLianDu/WBP_ShuLianDu.uasset", out GameFile file))
			{
				logger.LogError("Unable to locate asset WBP_ShuLianDu.");
				proficiencies = null;
				return false;
			}

			Package package = (Package)providerManager.Provider.LoadPackage(file);

			Dictionary<EProficiency, ProficiencyData> proficiencyMap = new();
			foreach (FObjectExport export in package.ExportMap)
			{
				if (!export.ClassName.Equals("WBP_ShuLianDuSingle_C"))
				{
					continue;
				}

				EProficiency? proficiency = null;
				string? name = null;
				UTexture2D? icon = null;

				UObject exportObject = export.ExportObject.Value;
				foreach (FPropertyTag property in exportObject.Properties)
				{
					switch (property.Name.Text)
					{
						case "ShuLianDuType":
							if (GameUtil.TryParseEnum(property, out EProficiency p))
							{
								proficiency = p;
							}
							break;
						case "SLDText":
							name = GameUtil.ReadTextProperty(property);
							break;
						case "SLDImage":
							icon = GameUtil.ReadTextureProperty(property);
							break;
					}
				}

				if (!proficiency.HasValue || name is null)
				{
					logger.Log(LogLevel.Warning, "Could not find necessary data from an instance of WBP_ShuLianDuSingle_C to build proficiency information. Skipping this instance.");
					continue;
				}

				if (proficiencyMap.ContainsKey(proficiency.Value))
				{
					logger.Log(LogLevel.Warning, $"Found an additional instance of WBP_ShuLianDuSingle_C for the {proficiency.Value} proficiency. Skipping this instance.");
					continue;
				}

				ProficiencyData data = new()
				{
					ID = proficiency.Value,
					Name = name,
					Icon = icon
				};

				proficiencyMap.Add(proficiency.Value, data);
			}

			EProficiency[] allProfIds = Enum.GetValues<EProficiency>();
			ProficiencyData[] allProfs = new ProficiencyData[allProfIds.Length];
			for (int i = 0; i < allProfs.Length; ++i)
			{
				if (proficiencyMap.TryGetValue(allProfIds[i], out ProficiencyData data))
				{
					allProfs[i] = data;
				}
				else
				{
					allProfs[i] = new() { ID = allProfIds[i] };
				}
			}

			proficiencies = allProfs;
			return true;
		}
		private void WriteCsv(IEnumerable<ProficiencyData> proficiencies, Config config, Logger logger)
		{
			string outPath = Path.Combine(config.OutputDirectory, Name, $"{Name}.csv");
			using FileStream stream = IOUtil.CreateFile(outPath, logger);
			using StreamWriter writer = new(stream, Encoding.UTF8);

			writer.WriteLine("idx,id,name,icon");

			foreach (ProficiencyData proficiency in proficiencies)
			{
				writer.WriteLine($"{(int)proficiency.ID},{proficiency.ID},\"{proficiency.Name}\",\"{proficiency.Icon?.Name}\"");
			}
		}

		private void WriteSql(IEnumerable<ProficiencyData> proficiencies, TextWriter sqlWriter, Logger logger)
		{
			// Schema
			// create table `sld` (
			//     `id` int not null,
			//     `type` varchar(127) not null,
			//     `name` varchar(127),
			//     `icon` varchar(127),
			//     primary key (`id`)
			// );

			string dbStr(string? value)
			{
				if (value is null) return "null";
				return $"'{value.Replace("\'", "\'\'")}'";
			}

			sqlWriter.WriteLine("truncate table `sld`;");
			foreach (ProficiencyData proficiency in proficiencies)
			{
				sqlWriter.WriteLine($"insert into `sld` values ({(int)proficiency.ID},{dbStr(proficiency.ID.ToString())},{dbStr(proficiency.Name)},{dbStr(proficiency.Icon?.Name)});");
			}
		}

		private void WriteTextures(IEnumerable<ProficiencyData> proficiencies, Config config, Logger logger)
		{
			string outDir = Path.Combine(config.OutputDirectory, Name, "icons");
			foreach (ProficiencyData proficiency in proficiencies)
			{
				if (proficiency.Icon is null) continue;
				TextureExporter.ExportTexture(proficiency.Icon, false, logger, outDir);
			}
		}

		private struct ProficiencyData
		{
			public EProficiency ID;
			public string? Name;
			public UTexture2D? Icon;

			public override string ToString()
			{
				return $"[{ID}] {Name}";
			}
		}

		private enum EProficiency
		{
			FaMu,
			CaiKuang,
			ZhongZhi,
			BuZhuo,
			CaiShou,
			YangZhi,
			TuZai,
			PaoMu,
			QieShi,
			RongLian,
			RouPi,
			FangZhi,
			ZhiTao,
			YanMo,
			QiJu,
			WuQi,
			JiaZhou,
			ZhuBao,
			JianZhu,
			LianJin,
			PengRen,
			Dao,
			ShuangDao,
			Mao,
			Chui,
			QuanTao,
			Gong,
			DaJian,
			PouJie,
			DunPai
		};
	}
}
