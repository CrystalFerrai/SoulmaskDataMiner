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

using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Objects.UObject;
using System.Diagnostics;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Utility for gathering information about blueprint inheritance
	/// </summary>
	internal class BlueprintHeirarchy
	{
		private static BlueprintHeirarchy? sInstance;

		private readonly Dictionary<string, InternalClassInfo> mSuperMap;

		public IReadOnlySet<string> FoliageComponentClasses { get; private set; }

		/// <summary>
		/// Gets the loaded blueprint heiarchy.
		/// </summary>
		/// <exception cref="InvalidOperationException">The hierarchy has not been loaded</exception>
		public static BlueprintHeirarchy Instance
		{
			get
			{
				if (sInstance is null) throw new InvalidOperationException("BluePrintHierarchy has not been loaded. Any miner needing this resource should declare so by adding the RequireHierarchy attribute to the class.");
				return sInstance;
			}
		}

		private BlueprintHeirarchy(Dictionary<string, InternalClassInfo> superMap)
		{
			mSuperMap = superMap;
			FoliageComponentClasses = null!;
		}

		/// <summary>
		/// Loads the blueprint heirarchy, making it ready for miners to make use of
		/// </summary>
		public static void Load(IProviderManager providerManager, Logger logger)
		{
			logger.Information("Loading blueprint heirarchy...");

			Stopwatch timer = new Stopwatch();
			timer.Start();

			Dictionary<string, InternalClassInfo> superMap = new();

			foreach (var pair in providerManager.Provider.Files)
			{
				if (!pair.Value.Extension.Equals("uasset", StringComparison.OrdinalIgnoreCase)) continue;

				Package? package;
				try
				{
					package = providerManager.Provider.LoadPackage(pair.Value) as Package;
				}
				catch
				{
					// If an asset gets corrupted, we end up here. This may occur due to a corrupt file patch from Steam, for example.
					logger.Warning($"Failed to load asset. It may be corrupted. Path: {pair.Value.Path}");
					continue;
				}
				if (package is null) continue;

				foreach (FObjectExport export in package.ExportMap)
				{
					if (!export.ClassName.Equals("BlueprintGeneratedClass")) continue;

					if (superMap.TryGetValue(export.ObjectName.Text, out InternalClassInfo classInfo))
					{
						if (!classInfo.SuperName!.Equals(export.SuperIndex.Name))
						{
							logger.Warning($"Class {export.ObjectName.Text} found multiple times with different super classes");
						}
					}
					else
					{
						superMap.Add(export.ObjectName.Text, new(export, export.SuperIndex.Name));
					}

					break;
				}
			}

			Dictionary<string, InternalClassInfo> addToSuperMap = new();
			foreach (var pair in superMap)
			{
				InternalClassInfo superClassInfo;
				if (superMap.TryGetValue(pair.Value.SuperName!, out superClassInfo))
				{
					superClassInfo.DerivedNames.Add(pair.Key);
					superMap[pair.Value.SuperName!] = superClassInfo;
				}
				else if (addToSuperMap.TryGetValue(pair.Value.SuperName!, out superClassInfo))
				{
					superClassInfo.DerivedNames.Add(pair.Key);
					addToSuperMap[pair.Value.SuperName!] = superClassInfo;
				}
				else
				{
					superClassInfo = new();
					superClassInfo.DerivedNames.Add(pair.Key);
					addToSuperMap.Add(pair.Value.SuperName!, superClassInfo);
				}
			}

			foreach (var pair in addToSuperMap)
			{
				superMap.Add(pair.Key, pair.Value);
			}

			sInstance = new(superMap);

			HashSet<string> foliageComponents = new();
			foreach (BlueprintClassInfo zhiBeiComponent in sInstance.GetDerivedClasses("HZhiBeiComponent"))
			{
				foliageComponents.Add(zhiBeiComponent.Name);
			}
			foreach (BlueprintClassInfo zhiBeiComponent in sInstance.GetDerivedClasses("HBuLuoZhiBeiComponent"))
			{
				foliageComponents.Add(zhiBeiComponent.Name);
			}

			sInstance.FoliageComponentClasses = foliageComponents;
			foreach (string component in foliageComponents)
			{
				ObjectTypeRegistry.RegisterClass(component, typeof(UInstancedStaticMeshComponent));
			}

			timer.Stop();
			logger.Information($"Blueprint hierarchy load completed in {((double)timer.ElapsedTicks / (double)Stopwatch.Frequency):0.###}s");
		}

		/// <summary>
		/// Returns all blueprint classes derived from the specified class. Only able to see
		/// inheritance within the blueprint heirarchy, not between classes defined in code.
		/// </summary>
		public IEnumerable<BlueprintClassInfo> GetDerivedClasses(string className)
		{
			if (mSuperMap.TryGetValue(className, out InternalClassInfo classInfo))
			{
				return InternalGetDerivedClasses(classInfo);
			}
			return Enumerable.Empty<BlueprintClassInfo>();
		}

		/// <summary>
		/// Returns whether the passed in class is or derives from a specific super class
		/// </summary>
		/// <param name="className">The class to check</param>
		/// <param name="superName">The class to search for</param>
		/// <returns>True if the passed in class is or derives from the specified super class, else false</returns>
		public bool IsDerivedFrom(string className, string superName)
		{
			if (className == superName) return true;

			string? current = className;
			while (current != null && mSuperMap.TryGetValue(current, out InternalClassInfo classInfo))
			{
				if (classInfo.SuperName == superName) return true;
				current = classInfo.SuperName;
			}
			return false;
		}

		/// <summary>
		/// Search a blueprint and all super classes
		/// </summary>
		/// <param name="start">The blueprint to search first</param>
		/// <param name="searchFunc">A function to call for the start class and each class in the super chain. Return true to stop the search or false to conitnue.</param>
		public static void SearchInheritance(UClass start, Predicate<UClass> searchFunc)
		{
			UClass? current = start;
			while (current != null)
			{
				if (searchFunc(current))
				{
					break;
				}

				current = current.Super?.Load() as UClass;
			}
		}

		private IEnumerable<BlueprintClassInfo> InternalGetDerivedClasses(InternalClassInfo classInfo)
		{
			foreach (string name in classInfo.DerivedNames)
			{
				if (mSuperMap.TryGetValue(name, out InternalClassInfo derivedInfo))
				{
					yield return new() { Name = name, Export = derivedInfo.Export, Super = classInfo.Export };
					foreach (BlueprintClassInfo derived in InternalGetDerivedClasses(derivedInfo))
					{
						yield return derived;
					}
				}
			}
		}

		private struct InternalClassInfo
		{
			public FObjectExport? Export;

			public string? SuperName;

			public List<string> DerivedNames;

			public InternalClassInfo()
			{
				Export = null;
				SuperName = null;
				DerivedNames = new();
			}

			public InternalClassInfo(FObjectExport export, string? superName)
			{
				Export = export;
				SuperName = superName;
				DerivedNames = new();
			}
		}
	}

	internal struct BlueprintClassInfo
	{
		public string Name;
		public FObjectExport? Export;
		public FObjectExport? Super;
	}

	internal struct ObjectWithDefaults
	{
		public FObjectExport Export;
		public UObject? DefaultsObject;

		public override string ToString()
		{
			return Export.ObjectName.Text;
		}
	}
}
