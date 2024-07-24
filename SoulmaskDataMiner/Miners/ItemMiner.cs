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

using System.Text;

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Gathers information about item classes
	/// </summary>
	internal class ItemMiner : SubclassMinerBase
	{
		public override string Name => "Item";

		protected override string NameProperty => "Name";

		public override bool Run(IProviderManager providerManager, Config config, Logger logger, TextWriter sqlWriter)
		{
			string[] baseClassNames = new string[]
			{
				"HDaoJuBase",
					"HDaoJuZhuangBei",
						"HDaoJuWuQi",
							"HDaoJu_SheJiWuQi",
							"HDaoJu_TouZhi_WuQi",
							"HDaoJuShuiTong",
							"HDaoJu_SheJiWuQi",
							"HDaoJu_TouZhi_WuQi",
							"HDaoJuShuiTong",
					"HDaoJu_ZiDan",
					"HDaoJuXiaoHao",
						"HDaoJuChuCaoJi",
						"HDaoJuFeiLiao",
						"HDaoJuFunction",
						"HDaoJuMianJu",
						"HDaoJuShaChongJi",
						"HDaoJuShiWu",
					"HDaoJuDianChi",
					"HDaoJuHongJingShi",
					"HDaoJuJianZhu",
					"HDaoJuJianZhuPingTai",
					"HDaoJuShuiPing",
					"HDaoJuZhaoMingMoKuai"
			};

			var items = FindObjects(baseClassNames);

			WriteCsv(items, config, logger);
			WriteSql(items, sqlWriter, logger);

			return true;
		}

		private void WriteCsv(IEnumerable<ObjectInfo> items, Config config, Logger logger)
		{
			string outPath = Path.Combine(config.OutputDirectory, Name, $"{Name}.csv");
			using (FileStream outFile = IOUtil.CreateFile(outPath, logger))
			using (StreamWriter writer = new(outFile))
			{
				writer.WriteLine("name,class");
				foreach (ObjectInfo info in items)
				{
					writer.WriteLine($"\"{info.Name}\",\"{info.ClassName}\"");
				}
			}
		}

		private void WriteSql(IEnumerable<ObjectInfo> items, TextWriter sqlWriter, Logger logger)
		{
			// Schema
			// create table `item` (`name` varchar(255) not null, `class` varchar(255) not null)

			sqlWriter.WriteLine("truncate table `item`;");

			string dbStr(string? value)
			{
				if (value is null) return "''";
				return $"'{value.Replace("\'", "\'\'")}'";
			}

			foreach (ObjectInfo objectInfo in items)
			{
				sqlWriter.WriteLine($"insert into `item` values ({dbStr(objectInfo.Name)}, {dbStr(objectInfo.ClassName)});");
			}
		}
	}
}
