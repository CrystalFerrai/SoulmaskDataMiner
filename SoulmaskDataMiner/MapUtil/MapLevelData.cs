// Copyright 2026 Crystal Ferrai
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
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.CriWare.Readers;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner.MapUtil
{
	/// <summary>
	/// Data related to the levels associated with a game map
	/// </summary>
	internal class MapLevelData
	{
		public string MapName { get; }

		public string MapMainDirectory { get; }

		public Package MainLevel { get; }

		public Package GameplayLevel1 { get; }

		public Package GameplayLevel2 { get; }

		public Package GameplayLevel3 { get; }

		public IReadOnlyList<Package> CrowdNpcLevels { get; }

		public IReadOnlyList<Package> Sublevels { get; }

		public UObject WorldSettings { get; }

		public UObject ConfigData { get; }

		public IEnumerable<Package> AllLevels
		{
			get
			{
				yield return MainLevel;
				yield return GameplayLevel1;
				yield return GameplayLevel2;
				yield return GameplayLevel3;
				foreach (Package crowdNpcLevel in CrowdNpcLevels)
				{
					yield return crowdNpcLevel;
				}
				foreach (Package subLevel in Sublevels)
				{
					yield return subLevel;
				}
			}
		}

		private MapLevelData(
			string mapName,
			string mapMainDirectory,
			Package mainLevel,
			Package gameplayLevel1,
			Package gameplayLevel2,
			Package gameplayLevel3,
			IReadOnlyList<Package> crowdNpcLevels,
			IReadOnlyList<Package> subLevels,
			UObject worldSettings,
			UObject configData)
		{
			MapName = mapName;
			MapMainDirectory = mapMainDirectory;
			MainLevel = mainLevel;
			GameplayLevel1 = gameplayLevel1;
			GameplayLevel2 = gameplayLevel2;
			GameplayLevel3 = gameplayLevel3;
			CrowdNpcLevels = crowdNpcLevels;
			Sublevels = subLevels;
			WorldSettings = worldSettings;
			ConfigData = configData;
		}

		public static MapLevelData? Load(string mapName, string mainLevelPath, IProviderManager providerManager, Logger logger)
		{
			Package? mainLevel = LoadLevel(mainLevelPath, providerManager, logger);

			string mapDir = mainLevelPath.Substring(0, mainLevelPath.LastIndexOf('/'));
			if (mapDir.StartsWith("/Game/")) mapDir = $"WS/Content{mapDir.Substring(5)}";
			string mapBaseName = mapDir.Substring(mapDir.LastIndexOf('/') + 1);
			string hubDir = $"{mapDir}/{mapBaseName}_Hub";
			string crowdNpcDir = $"{mapDir}/CrowdNPC";

			Package? gameplayLevel1 = LoadLevel($"{hubDir}/{mapBaseName}_GamePlay.umap", providerManager, logger);
			Package? gameplayLevel2 = LoadLevel($"{hubDir}/{mapBaseName}_GamePlay2.umap", providerManager, logger);
			Package? gameplayLevel3 = LoadLevel($"{hubDir}/{mapBaseName}_GamePlay3.umap", providerManager, logger);

			if (mainLevel is null || gameplayLevel1 is null || gameplayLevel2 is null || gameplayLevel3 is null)
			{
				return null;
			}

			List<Package> crowdNpcLevels = new();
			foreach (var pair in providerManager.Provider.Files)
			{
				if (!pair.Key.StartsWith(crowdNpcDir) || !pair.Key.EndsWith(".umap"))
				{
					continue;
				}

				crowdNpcLevels.Add((Package)providerManager.Provider.LoadPackage(pair.Value));
			}

			UObject mainExport = mainLevel.ExportMap[mainLevel.GetExportIndex("PersistentLevel")].ExportObject.Value;
			FPackageIndex? worldSettingIndex = mainExport.Properties.FirstOrDefault(p => p.Name.Text.Equals("WorldSettings"))?.Tag?.GetValue<FPackageIndex>();
			UObject? worldSettings = worldSettingIndex?.Load();
			if (worldSettings is null)
			{
				logger.Warning($"Failed to read world settings from {mainLevelPath}");
				return null;
			}

			UBlueprintGeneratedClass? configClass = worldSettings.Properties.FirstOrDefault(p => p.Name.Text.Equals("ConfigDataClass"))?.Tag?.GetValue<FPackageIndex>()?.Load() as UBlueprintGeneratedClass;
			UObject? configData = configClass?.ClassDefaultObject.Load();
			if (configData is null)
			{
				logger.Warning($"Failed to read config data from world settings in {mainLevelPath}");
				return null;
			}

			List<Package> subLevels = new();

			UScriptArray? subLevelArray = worldSettings.Properties.FirstOrDefault(p => p.Name.Text.Equals("SubLevelNameList"))?.Tag?.GetValue<UScriptArray>();
			if (subLevelArray is null)
			{
				logger.Warning($"Unable to read SubLevelNameList from world settings in {mainLevelPath}.");
				return null;
			}
			foreach (FPropertyTagType subLevelItem in subLevelArray.Properties)
			{
				string? levelName = subLevelItem.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("SubLevelName"))?.Tag?.GetValue<FName>().Text; ;
				if (levelName is null)
				{
					logger.Warning("Unable to read level name from SubLevelNameList");
					continue;
				}

				if (providerManager.Provider.TryLoadPackage($"{levelName}.umap", out IPackage? level))
				{
					subLevels.Add((Package)level);
				}
				else
				{
					logger.Warning($"Unable to load sublevel {levelName} from SubLevelNameList");
				}
			}

			return new(mapName, mapDir, mainLevel, gameplayLevel1, gameplayLevel2, gameplayLevel3, crowdNpcLevels, subLevels, worldSettings, configData);
		}

		private static Package? LoadLevel(string path, IProviderManager providerManager, Logger logger)
		{
			providerManager.Provider.TryGetGameFile(path, out GameFile? file);
			if (file is null)
			{
				logger.Warning($"Failed to find level asset {path}");
				return null;
			}
			return (Package)providerManager.Provider.LoadPackage(file);
		}
	}
}
