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
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	internal class MapLevelData
	{
		public string MapName { get; }

		public string MapMainDirectory { get; }

		public Package MainLevel { get; }

		public Package GameplayLevel1 { get; }

		public Package GameplayLevel2 { get; }

		public Package GameplayLevel3 { get; }

		public IReadOnlyList<Package> CrowdNpcLevels { get; }

		public UObject WorldSettings { get; }

		private MapLevelData(string mapName, string mapMainDirectory, Package mainLevel, Package gameplayLevel1, Package gameplayLevel2, Package gameplayLevel3, IReadOnlyList<Package> crowdNpcLevels, UObject worldSettings)
		{
			MapName = mapName;
			MapMainDirectory = mapMainDirectory;
			MainLevel = mainLevel;
			GameplayLevel1 = gameplayLevel1;
			GameplayLevel2 = gameplayLevel2;
			GameplayLevel3 = gameplayLevel3;
			CrowdNpcLevels = crowdNpcLevels;
			WorldSettings = worldSettings;
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

			return new(mapName, mapDir, mainLevel, gameplayLevel1, gameplayLevel2, gameplayLevel3, crowdNpcLevels, worldSettings);
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
