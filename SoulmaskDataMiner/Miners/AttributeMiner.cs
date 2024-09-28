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

using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Mines data about character attributes
	/// </summary>
	[RequireClassData(true)]
	internal class AttributeMiner : MinerBase
	{
		public override string Name => "Attribute";

		public override bool Run(IProviderManager providerManager, Config config, Logger logger, ISqlWriter sqlWriter)
		{
			IEnumerable<string>? attributes;
			if (!FindAttributeData(providerManager, logger, out attributes))
			{
				return false;
			}

			WriteCsv(attributes, config, logger);
			WriteSql(attributes, sqlWriter, logger);

			return true;
		}

		private bool FindAttributeData(IProviderManager providerManager, Logger logger, [NotNullWhen(true)] out IEnumerable<string>? attributes)
		{
			string[] classNames = new string[]
			{
				"UHSuperCommonSet",
				"UHSuperStateSet",
				"UHBuWeiShangHaiAttriSet"
			};

			List<string> attrList = new();

			foreach (string className in classNames)
			{
				MetaClass mclass;
				if (!providerManager.ClassMetadata!.TryGetValue(className, out mclass))
				{
					logger.Error($"Failed to locate class {className} in class metadata.");
					attributes = null;
					return false;
				}

				foreach (MetaClassProperty property in mclass.Properties)
				{
					if (!property.Type.Equals("FGameplayAttributeData")) continue;

					attrList.Add(property.Name);
				}
			}

			attributes = attrList;
			return true;
		}

		private void WriteCsv(IEnumerable<string> attributes, Config config, Logger logger)
		{
			string outPath = Path.Combine(config.OutputDirectory, Name, $"{Name}.csv");
			using FileStream stream = IOUtil.CreateFile(outPath, logger);
			using StreamWriter writer = new(stream, Encoding.UTF8);

			writer.WriteLine("name,desc");

			foreach (string attribute in attributes)
			{
				writer.WriteLine($"{CsvStr(attribute)},");
			}
		}

		private void WriteSql(IEnumerable<string> attributes, ISqlWriter sqlWriter, Logger logger)
		{
			// Schema
			// create table `attr` (
			//   `idx` int not null,
			//   `name` varchar(127) not null,
			//   primary key (`idx`)
			// )

			sqlWriter.WriteStartTable("attr");
			int i = 0;
			foreach (string attribute in attributes)
			{
				sqlWriter.WriteRow($"{i++}, {DbStr(attribute)}");
			}
			sqlWriter.WriteEndTable();
		}
	}
}
