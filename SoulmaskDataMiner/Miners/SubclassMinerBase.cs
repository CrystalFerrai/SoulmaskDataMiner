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

using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Base class for miners that gather information about specific class heirarchies
	/// </summary>
	internal abstract class SubclassMinerBase : IDataMiner
	{
		public abstract string Name { get; }

		public abstract bool Run(IProviderManager providerManager, Config config, Logger logger);

		/// <summary>
		/// The name of the property that stores the name to associate with the class.
		/// </summary>
		protected abstract string NameProperty { get; }

		/// <summary>
		/// Gathers and outputs a list of classes which derive from a specific class
		/// </summary>
		protected void ProcessClasses(BlueprintHeirarchy blueprintHeirarchy, IEnumerable<string> baseClassNames, string outFileNamePrefix, Config config, Logger logger)
		{
			List<ObjectInfo> infos = new();
			foreach (string className in baseClassNames)
			{
				foreach (BlueprintClassInfo classInfo in blueprintHeirarchy.GetDerivedClasses(className))
				{
					if (classInfo.Export?.ExportObject.Value is UClass classObj)
					{
						string? name = FindObjectName(classObj);
						infos.Add(new() { ClassName = classInfo.Name, Name = name });
					}
				}
			}

			infos.Sort();

			string outPath = Path.Combine(config.OutputDirectory, Name, $"{outFileNamePrefix}_classes.csv");
			using (FileStream outFile = IOUtil.CreateFile(outPath, logger))
			using (StreamWriter writer = new(outFile))
			{
				writer.WriteLine("Name,Class");
				foreach (ObjectInfo info in infos)
				{
					writer.WriteLine($"{info.Name},{info.ClassName}");
				}
			}
		}

		private string? FindObjectName(UClass classObj)
		{
			if (classObj.ClassDefaultObject.TryLoad(out UObject? defaults))
			{
				string? name = GetObjectName(defaults!);
				if (name is not null) return name;

				if (classObj.Super is not null &&
					classObj.Super.TryLoad(out UObject? super) &&
					super is UBlueprintGeneratedClass superBlueprint)
				{
					return FindObjectName(superBlueprint);
				}
			}

			return null;
		}

		private string? GetObjectName(UObject obj)
		{
			foreach (FPropertyTag property in obj.Properties)
			{
				if (!property.Name.Text.Equals(NameProperty)) continue;

				if (property.Tag is TextProperty textProperty)
				{
					return textProperty.Value?.Text;
				}

				break;
			}

			return null;
		}

		private struct ObjectInfo : IComparable<ObjectInfo>
		{
			public string ClassName;
			public string? Name;

			public int CompareTo(ObjectInfo other)
			{
				if (Name is null)
				{
					if (other.Name is not null) return -1;
					return ClassName.CompareTo(other.ClassName);
				}
				int result = Name.CompareTo(other.Name);
				if (result == 0)
				{
					return ClassName.CompareTo(other.ClassName);
				}
				return result;
			}
		}
	}
}
