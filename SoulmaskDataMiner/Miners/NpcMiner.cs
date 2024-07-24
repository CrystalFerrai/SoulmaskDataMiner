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
	/// Gathers information about NPC classes
	/// </summary>
	internal class NpcMiner : SubclassMinerBase
	{
		public override string Name => "Npc";

		protected override string NameProperty => "MoRenMingZi";

		private const string BaseClassName_NonHuman = "HCharacterDongWu";
		private const string BaseClassName_Human = "HCharacterRen";
		//private const string BaseClassName_Other = "HCharacterBase";

		public override bool Run(IProviderManager providerManager, Config config, Logger logger, TextWriter sqlWriter)
		{
			IEnumerable<ObjectInfo> nonHumanClasses = FindObjects(BaseClassName_NonHuman.AsEnumerable());
			IEnumerable<ObjectInfo> humanClasses = FindObjects(BaseClassName_Human.AsEnumerable());

			WriteCsv(nonHumanClasses, "NonHuman.csv", config, logger);
			WriteCsv(humanClasses, "Human.csv", config, logger);

			WriteSql(nonHumanClasses, humanClasses, sqlWriter, logger);

			return true;
		}

		private void WriteCsv(IEnumerable<ObjectInfo> items, string filename, Config config, Logger logger)
		{
			string outPath = Path.Combine(config.OutputDirectory, Name, filename);
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

		private void WriteSql(IEnumerable<ObjectInfo> nonhumans, IEnumerable<ObjectInfo> humans, TextWriter sqlWriter, Logger logger)
		{
			// Schema
			// create table `npc` (`human` bool not null, `name` varchar(255) not null, `class` varchar(255) not null)

			sqlWriter.WriteLine("truncate table `npc`;");

			string dbStr(string? value)
			{
				if (value is null) return "''";
				return $"'{value.Replace("\'", "\'\'")}'";
			}

			foreach (ObjectInfo objectInfo in nonhumans)
			{
				sqlWriter.WriteLine($"insert into `npc` values (false, {dbStr(objectInfo.Name)}, {dbStr(objectInfo.ClassName)});");
			}
			foreach (ObjectInfo objectInfo in humans)
			{
				sqlWriter.WriteLine($"insert into `npc` values (true, {dbStr(objectInfo.Name)} ,  {dbStr(objectInfo.ClassName)});");
			}
		}
	}
}
