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
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Mines map images and information about points of interest
	/// </summary>
	[RequireHeirarchy(true)]
	internal class MapMiner : IDataMiner
	{
		public string Name => "Map";

		public bool Run(IProviderManager providerManager, Config config, Logger logger, TextWriter sqlWriter)
		{
			logger.Log(LogLevel.Information, "Exporting map images...");
			if (!ExportMapImages(providerManager, config, logger))
			{
				return false;
			}

			logger.Log(LogLevel.Information, "Begin processing map.");
			MapData? mapData = ProcessMap(providerManager, logger);
			if (mapData is null)
			{
				return false;
			}
			logger.Log(LogLevel.Information, "Finished processing map.");

			logger.Log(LogLevel.Information, "Exporting data...");
			WriteIcons(mapData, config, logger);
			WriteCsv(mapData, config, logger);
			WriteSql(mapData, sqlWriter, logger);

			return true;
		}

		private bool ExportMapImages(IProviderManager providerManager, Config config, Logger logger)
		{
			string outDir = Path.Combine(config.OutputDirectory, Name);

			bool success = TextureExporter.ExportFirstTexture(providerManager.Provider, "WS/Content/UI/Map/Level01_Map.uasset", false, logger, outDir);
			success &= TextureExporter.ExportFirstTexture(providerManager.Provider, "WS/Content/UI/Map/T_MapMask.uasset", false, logger, outDir);

			return success;
		}

		private MapData? ProcessMap(IProviderManager providerManager, Logger logger)
		{
			logger.Log(LogLevel.Information, "Loading dependencies...");

			UObject? mapIntel = LoadMapIntel(providerManager, logger);
			if (mapIntel is null) return null;

			IReadOnlyDictionary<ETanSuoDianType, UTexture2D>? mapIcons = GetMapIcons(mapIntel, logger);
			if (mapIcons is null) return null;

			MapPoiLookups? lookups = GetPois(mapIntel, providerManager.Achievements, logger);
			if (lookups is null) return null;

			lookups.LootIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/ChuShenTianFu/ChengHao/ChengHao_poxiangren.uasset", logger)!;
			if (lookups.LootIcon is null) return null;

			if (!FindTabletData(providerManager, lookups, providerManager.Achievements, logger))
			{
				return null;
			}

			if (!LoadLootData(providerManager, lookups, logger))
			{
				return null;
			}

			if (!LoadSpawnLayers(providerManager, lookups, logger))
			{
				return null;
			}

			if (!FindMapObjects(providerManager, logger, lookups,
				out IReadOnlyList<FObjectExport>? poiObjects,
				out IReadOnlyList<FObjectExport>? tabletObjects,
				out IReadOnlyList<ObjectWithDefaults>? spawnerObjects,
				out IReadOnlyList<ObjectWithDefaults>? chestObjects))
			{
				return null;
			}

			ProcessPois(lookups, poiObjects, logger);
			ProcessTablets(lookups, tabletObjects, logger);
			ProcessSpawners(lookups, spawnerObjects, logger);
			ProcessChests(lookups, chestObjects, logger);

			FindPoiTextures(lookups, mapIcons, logger);

			return new(lookups.GetAllPois(), (IReadOnlyDictionary<string, LootData>)lookups.Loot);
		}

		private UObject? LoadMapIntel(IProviderManager providerManager, Logger logger)
		{
			if (!providerManager.Provider.TryFindGameFile("WS/Content/Blueprints/ZiYuanGuanLi/BP_MapQingBaoConfig.uasset", out GameFile file))
			{
				logger.LogError("Unable to load asset BP_MapQingBaoConfig.");
				return null;
			}

			Package package = (Package)providerManager.Provider.LoadPackage(file);
			return GameUtil.FindBlueprintDefaultsObject(package);
		}

		private IReadOnlyDictionary<ETanSuoDianType, UTexture2D>? GetMapIcons(UObject mapIntel, Logger logger)
		{
			foreach (FPropertyTag property in mapIntel.Properties)
			{
				if (!property.Name.Text.Equals("AllTanSuoDianIconMap")) continue;

				Dictionary<ETanSuoDianType, UTexture2D> mapIcons = new();

				UScriptMap iconMap = property.Tag!.GetValue<FStructFallback>()!.Properties[0].Tag!.GetValue<UScriptMap>()!;
				foreach (var pair in iconMap.Properties)
				{
					ETanSuoDianType iconType;
					if (!GameUtil.TryParseEnum<ETanSuoDianType>(pair.Key.GetValue<FName>(), out iconType))
					{
						logger.Log(LogLevel.Warning, $"Unable to parse icon type {pair.Key.GetValue<FName>().Text}");
						continue;
					}
					UTexture2D? icon = GameUtil.ReadTextureProperty(pair.Value);
					if (icon is null)
					{
						logger.Log(LogLevel.Warning, $"Unable to load icon for type {iconType}");
						continue;
					}

					mapIcons.Add(iconType, icon);
				}

				return mapIcons;
			}

			return null;
		}

		private MapPoiLookups? GetPois(UObject mapIntel, Achievements achievements, Logger logger)
		{
			foreach (FPropertyTag property in mapIntel.Properties)
			{
				if (!property.Name.Text.Equals("AllTanSuoDianInfoMap")) continue;

				MapPoiLookups lookups = new();

				UScriptMap poiMap = property.Tag!.GetValue<FStructFallback>()!.Properties[0].Tag!.GetValue<UScriptMap>()!;
				foreach (var pair in poiMap.Properties)
				{
					int index = pair.Key.GetValue<int>();

					FStructFallback? poiProperties = pair.Value?.GetValue<FStructFallback>();
					if (poiProperties is null)
					{
						logger.Log(LogLevel.Warning, $"Failed to load data for POI {index}");
						continue;
					}

					ETanSuoDianType? poiType = null;
					MapPoi poi = new()
					{
						GroupIndex = SpawnLayerGroup.PointOfInterest
					};
					foreach (FPropertyTag poiProperty in poiProperties.Properties)
					{
						switch (poiProperty.Name.Text)
						{
							case "TSDType":
								if (GameUtil.TryParseEnum(poiProperty, out ETanSuoDianType value))
								{
									poiType = value;
								}
								break;
							case "TSDName":
								poi.Title = GameUtil.ReadTextProperty(poiProperty);
								break;
							case "TSDBossName":
								poi.Name = GameUtil.ReadTextProperty(poiProperty);
								break;
							case "TSDDesc":
								poi.Description = GameUtil.ReadTextProperty(poiProperty);
								break;
							case "TSDDesc1":
								poi.Extra = GameUtil.ReadTextProperty(poiProperty);
								break;
								// The game displays either Desc1 or Desc2 depending on a game setting regarding barracks respawns.
								// I am choosing to use the Desc1 version in my data, but leaving this here as a note in case
								// something changes in the future.
								//case "TSDDesc2":
								//	break;
						}
					}

					if (!poiType.HasValue)
					{
						logger.Log(LogLevel.Warning, $"Failed to locate type for POI {index}");
						continue;
					}

					poi.Type = GetType(poiType.Value);
					poi.Title = GetTitle(poiType.Value);

					if (achievements.CollectMap.TryGetValue(index, out AchievementData? achievement))
					{
						poi.Achievement = achievement;
					}

					lookups.IndexLookup.Add(index, poi);
					if (!lookups.TypeLookup.TryGetValue(poiType.Value, out List<MapPoi>? list))
					{
						list = new();
						lookups.TypeLookup.Add(poiType.Value, list);
					}
					list.Add(poi);
				}

				return lookups;
			}

			return null;
		}

		private bool FindTabletData(IProviderManager providerManager, MapPoiLookups lookups, Achievements achievements, Logger logger)
		{
			if (!achievements.AllAchievements.TryGetValue("BP_ChengJiu_ShiBan_001_C", out AchievementData? ancientAchievement))
			{
				ancientAchievement = null;
			}
			if (!achievements.AllAchievements.TryGetValue("BP_ChengJiu_ShiBan_002_C", out AchievementData? divineAchievement))
			{
				divineAchievement = null;
			}

			foreach (var pair in providerManager.Provider.Files)
			{
				if (!pair.Key.StartsWith("WS/Content/Blueprints/JianZhu/GameFunction/Shibei/")) continue;
				if (!pair.Key.EndsWith(".uasset")) continue;

				Package package = (Package)providerManager.Provider.LoadPackage(pair.Value);

				string className = $"{Path.GetFileNameWithoutExtension(pair.Key)}_C";

				UObject? classDefaults = GameUtil.FindBlueprintDefaultsObject(package);
				if (classDefaults is null)
				{
					logger.Log(LogLevel.Warning, $"Could not find data for tablet POI {className}");
					continue;
				}

				UBlueprintGeneratedClass? tabletDataClass = null;
				foreach (FPropertyTag property in classDefaults.Properties)
				{
					if (!property.Name.Text.Equals("GameFunctionExecutionMap")) continue;

					UScriptMap? executionMap = property.Tag?.GetValue<UScriptMap>();
					if (executionMap is null)
					{
						logger.Log(LogLevel.Warning, $"Unable to read data for tablet POI {className}");
						break;
					}

					if (executionMap.Properties.Count < 1)
					{
						logger.Log(LogLevel.Warning, $"Unable to read data for tablet POI {className}");
						break;
					}

					FStructFallback? executionMapValue = executionMap.Properties.First().Value?.GetValue<FStructFallback>();
					UScriptArray? executionList = executionMapValue?.Properties[0].Tag?.GetValue<UScriptArray>();
					FStructFallback? executionStruct = executionList?.Properties[0].GetValue<FStructFallback>();
					if (executionStruct is null)
					{
						logger.Log(LogLevel.Warning, $"Unable to read data for tablet POI {className}");
						break;
					}

					foreach (FPropertyTag executionProperty in executionStruct.Properties)
					{
						if (!executionProperty.Name.Text.Equals("ExecuteObjPara")) continue;

						tabletDataClass = executionProperty.Tag?.GetValue<FPackageIndex>()?.ResolvedObject?.Load() as UBlueprintGeneratedClass;

						break;
					}

					break;
				}

				UObject? tabletDataObj = tabletDataClass?.ClassDefaultObject.Load();
				if (tabletDataObj is null)
				{
					logger.Log(LogLevel.Warning, $"Unable to read data for tablet POI {className}");
					continue;
				}

				MapPoi tabletData = new()
				{
					GroupIndex = SpawnLayerGroup.PointOfInterest,
					Type = "Tablet (Ancient)",
					Title = "Ancient Tablet",
					Achievement = ancientAchievement
				};
				foreach (FPropertyTag property in tabletDataObj.Properties)
				{
					switch (property.Name.Text)
					{
						case "ChengJiuKeJiPoint":
							tabletData.Name = $"Points: {property.Tag!.GetValue<int>()}";
							break;
						case "ChengJiuName":
							tabletData.Description = GameUtil.ReadTextProperty(property)!;
							break;
						case "ChengJiuTiaoJian":
							tabletData.Extra = GameUtil.ReadTextProperty(property)!;
							break;
						case "TextureIcon":
							tabletData.Icon = GameUtil.ReadTextureProperty(property)!;
							break;
						case "NumberParam1":
							if (property.Tag!.GetValue<int>() == 1)
							{
								tabletData.Type = "Tablet (Divine)";
								tabletData.Title = "Divine Tablet";
								tabletData.Achievement = divineAchievement;
							}
							break;
					}
				}
				if (tabletData.Icon is null)
				{
					logger.Log(LogLevel.Warning, $"Unable to find all data for tablet POI {className}");
					continue;
				}

				lookups.Tablets.Add(className, tabletData);
			}

			return lookups.Tablets.Any();
		}

		private bool LoadLootData(IProviderManager providerManager, MapPoiLookups lookups, Logger logger)
		{
			foreach (var filePair in providerManager.Provider.Files)
			{
				if (!filePair.Key.StartsWith("WS/Content/Blueprints/DataTable/CaiJiBao")) continue;
				if (!filePair.Key.EndsWith(".uasset")) continue;

				if (!providerManager.Provider.TryLoadPackage(filePair.Value, out IPackage iPackage)) continue;

				Package? package = iPackage as Package;
				if (package is null) continue;

				UDataTable? table = package.ExportMap[0].ExportObject.Value as UDataTable;
				if (table is null) continue;

				FPropertyTag? rowStructProperty = table.Properties.FirstOrDefault(p => p.Name.Text.Equals("RowStruct"));
				if (rowStructProperty is null) continue;

				FPackageIndex pi = rowStructProperty.Tag!.GetValue<FPackageIndex>()!;
				if (!pi.Name.Equals("CaiJiDaoJuBaoDataTable")) continue;

				foreach (var pair in table.RowMap)
				{
					string? name = null;
					UScriptArray? contentArray = null;

					foreach (FPropertyTag property in pair.Value.Properties)
					{
						switch (property.Name.Text)
						{
							case "DaoJuBaoName":
								name = property.Tag!.GetValue<FName>().Text;
								break;
							case "DaoJuBaoContent":
								contentArray = property.Tag!.GetValue<UScriptArray>();
								break;
						}
					}

					if (name is null || contentArray is null)
					{
						logger.Log(LogLevel.Warning, $"Could not read chest data from {Path.GetFileNameWithoutExtension(filePair.Key)} row \"{pair.Key.Text}\"");
						continue;
					}

					LootData content = new();
					foreach (FPropertyTagType contentItem in contentArray.Properties)
					{
						int probability = 0;
						UScriptArray? itemArray = null;

						FStructFallback entryData = contentItem.GetValue<FStructFallback>()!;
						foreach (FPropertyTag property in entryData.Properties)
						{
							switch (property.Name.Text)
							{
								case "SelectedRandomProbability":
									probability = property.Tag!.GetValue<int>();
									break;
								case "BaoNeiDaoJuInfos":
									itemArray = property.Tag!.GetValue<UScriptArray>();
									break;
							}
						}

						if (probability == 0 || itemArray is null)
						{
							continue;
						}

						LootEntry entry = new() { Probability = probability };

						int totalWeight = 0;
						foreach (FPropertyTagType entryItem in itemArray.Properties)
						{
							LootItem item = new();
							FPackageIndex? daoJuIndex = null;

							FStructFallback itemData = entryItem.GetValue<FStructFallback>()!;
							foreach (FPropertyTag property in itemData.Properties)
							{
								switch (property.Name.Text)
								{
									case "DaoJuQuanZhong":
										item.Weight = property.Tag!.GetValue<int>();
										totalWeight += item.Weight;
										break;
									case "DaoJuMagnitude":
										{
											TRange<float>? value = GameUtil.ReadRangeProperty<float>(property);
											if (value.HasValue) item.Amount = value.Value;
										}
										break;
									case "DaoJuPinZhi":
										if (GameUtil.TryParseEnum(property, out EDaoJuPinZhi pinZhi))
										{
											item.Quality = pinZhi;
										}
										break;
									case "DaoJuClass":
										daoJuIndex = property.Tag!.GetValue<FPackageIndex>();
										break;
								}
							}

							if (daoJuIndex is null)
							{
								totalWeight -= item.Weight;
								continue;
							}

							item.Asset = daoJuIndex.Name;

							entry.Items.Add(item);
						}

						for (int i = 0; i < entry.Items.Count; ++i)
						{
							LootItem item = entry.Items[i];
							item.Weight = (int)((float)item.Weight / totalWeight * 100.0f);
							entry.Items[i] = item;
						}

						content.Entries.Add(entry);
					}

					lookups.Loot.Add(name, content);
				}
			}

			return true;
		}

		private bool LoadSpawnLayers(IProviderManager providerManager, MapPoiLookups lookups, Logger logger)
		{
			string[] texturePaths = new string[]
			{
					"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji1.uasset",
					"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji.uasset",
					"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji2.uasset",
					"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji3.uasset"
			};

			for (int i = 0; i < texturePaths.Length; ++i)
			{
				UTexture2D? icon = GameUtil.LoadFirstTexture(providerManager.Provider, texturePaths[i], logger);
				if (icon is null)
				{
					logger.LogError("Failed to load spawner icon texture.");
					return false;
				}

				lookups.SpawnLayerMap[(NpcCategory)i] = new SpawnLayerInfo() { Name = ((NpcCategory)i).ToString(), Icon = icon };
			}

			UTexture2D? lamasIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/dongwutubiao/ditubiaoji_yangtuo.uasset", logger);
			UTexture2D? catsIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/dongwutubiao/ditubiaoji_baozi.uasset", logger);
			UTexture2D? ostrichIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/dongwutubiao/ditubiaoji_tuoniao.uasset", logger);
			if (lamasIcon is null || catsIcon is null || ostrichIcon is null)
			{
				logger.LogError("Failed to load spawner icon texture.");
				return false;
			}

			const string babyAnimalSpawnName = "Baby Animal Spawn";
			lookups.SpawnLayerMap[NpcCategory.Lamas] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = lamasIcon };
			lookups.SpawnLayerMap[NpcCategory.Cats] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = catsIcon };
			lookups.SpawnLayerMap[NpcCategory.Ostrich] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = ostrichIcon };

			return true;
		}

		private bool FindMapObjects(IProviderManager providerManager, Logger logger, MapPoiLookups lookups,
			[NotNullWhen(true)] out IReadOnlyList<FObjectExport>? poiObjects,
			[NotNullWhen(true)] out IReadOnlyList<FObjectExport>? tabletObjects,
			[NotNullWhen(true)] out IReadOnlyList<ObjectWithDefaults>? spawnerObjects,
			[NotNullWhen(true)] out IReadOnlyList<ObjectWithDefaults>? chestObjects)
		{
			poiObjects = null;
			tabletObjects = null;
			spawnerObjects = null;
			chestObjects = null;

			Package[] packages = new Package[2];
			{
				if (!providerManager.Provider.TryFindGameFile("WS/Content/Maps/Level01/Level01_Hub/Level01_GamePlay.umap", out GameFile file))
				{
					logger.LogError("Unable to load asset Level01_GamePlay.");
					return false;
				}
				packages[0] = (Package)providerManager.Provider.LoadPackage(file);
			}
			{
				if (!providerManager.Provider.TryFindGameFile("WS/Content/Maps/Level01/Level01_Hub/Level01_GamePlay2.umap", out GameFile file))
				{
					logger.LogError("Unable to load asset Level01_GamePlay2.");
					return false;
				}
				packages[1] = (Package)providerManager.Provider.LoadPackage(file);
			}

			const string poiClass = "HVolumeChuFaQi";

			string[] spawnerBaseClasses = new string[]
			{
				"HShuaGuaiQiBase",
					"HShuaGuaiQiRandNPC",
						"HShuaGuaiQiShouLong",
							"HJianZhuBuLuoQiuLong",
						"ShuaGuaiQi_RuQingNPC",
						"HShuaGuaiQiDiXiaCheng",
				"HTanChaActor"
			};

			List<BlueprintClassInfo> spawnerBpClasses = new();
			foreach (String searchClass in spawnerBaseClasses)
			{
				spawnerBpClasses.AddRange(BlueprintHeirarchy.Get().GetDerivedClasses(searchClass));
			}

			Dictionary<string, UObject?> spawnerClasses = spawnerBaseClasses.ToDictionary(c => c, c => (UObject?)null);
			foreach (BlueprintClassInfo bpClass in spawnerBpClasses)
			{
				UBlueprintGeneratedClass? exportObj = (UBlueprintGeneratedClass?)bpClass.Export?.ExportObject.Value;
				FPropertyTag? scgClassProperty = exportObj?.ClassDefaultObject.Load()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("SCGClass"));
				UObject? defaultScgObj = scgClassProperty?.Tag?.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>()?.ClassDefaultObject.Load();
				spawnerClasses.Add(bpClass.Name, defaultScgObj);
			}

			const string chestBaseClass = "HJianZhuBaoXiang";

			List<BlueprintClassInfo> chestBpClasses = new(BlueprintHeirarchy.Get().GetDerivedClasses(chestBaseClass));

			Dictionary<string, UObject?> chestClasses = spawnerBaseClasses.ToDictionary(c => c, c => (UObject?)null);
			foreach (BlueprintClassInfo bpClass in chestBpClasses)
			{
				UBlueprintGeneratedClass? exportObj = (UBlueprintGeneratedClass?)bpClass.Export?.ExportObject.Value;
				UObject? defaultObj = exportObj?.ClassDefaultObject.Load();
				chestClasses.Add(bpClass.Name, defaultObj);
			}

			List<FObjectExport> poiObjectList = new();
			List<FObjectExport> tabletObjectList = new();
			List<ObjectWithDefaults> spawnerObjectList = new();
			List<ObjectWithDefaults> chestObjectList = new();

			logger.Log(LogLevel.Information, "Scanning for objects...");
			foreach (Package package in packages)
			{
				logger.Log(LogLevel.Debug, package.Name);
				foreach (FObjectExport export in package.ExportMap)
				{
					if (export.ClassName.Equals(poiClass))
					{
						poiObjectList.Add(export);
					}
					else if (spawnerClasses.TryGetValue(export.ClassName, out UObject? defaultScgObj))
					{
						spawnerObjectList.Add(new() { Export = export, DefaultsObject = defaultScgObj });
					}
					else if (chestClasses.TryGetValue(export.ClassName, out UObject? defaultObj))
					{
						chestObjectList.Add(new() { Export = export, DefaultsObject = defaultObj });
					}
					else if (lookups.Tablets.ContainsKey(export.ClassName))
					{
						tabletObjectList.Add(export);
					}
				}
			}

			poiObjects = poiObjectList;
			tabletObjects = tabletObjectList;
			spawnerObjects = spawnerObjectList;
			chestObjects = chestObjectList;

			return true;
		}

		private void ProcessPois(MapPoiLookups lookups, IReadOnlyList<FObjectExport> poiObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {poiObjects.Count} POIs...");
			foreach (FObjectExport poiObject in poiObjects)
			{
				int? index = null;
				UObject? brush = null;

				UObject obj = poiObject.ExportObject.Value;
				foreach (FPropertyTag property in obj.Properties)
				{
					switch (property.Name.Text)
					{
						case "ParamInt":
							index = property.Tag?.GetValue<int>();
							break;
						case "BrushComponent":
							brush = property.Tag?.GetValue<FPackageIndex>()?.ResolvedObject?.Object?.Value;
							break;
					}
				}

				if (!index.HasValue) continue;

				if (!lookups.IndexLookup.TryGetValue(index.Value, out MapPoi? poi))
				{
					continue;
				}

				if (brush is null)
				{
					logger.Log(LogLevel.Warning, $"Failed to locate POI {index}");
					continue;
				}

				FPropertyTag? locationProperty = brush.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Log(LogLevel.Warning, $"Failed to locate POI {index}");
					continue;
				}
				poi.Location = locationProperty.Tag!.GetValue<FVector>();
				poi.MapLocation = GameUtil.WorldToMap(poi.Location);
			}
		}

		private void ProcessTablets(MapPoiLookups lookups, IReadOnlyList<FObjectExport> tabletObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {tabletObjects.Count} tablets...");
			foreach (FObjectExport tabletObject in tabletObjects)
			{
				if (!lookups.Tablets.TryGetValue(tabletObject.ClassName, out MapPoi? poi))
				{
					logger.Log(LogLevel.Warning, "Tablet object data missing. This should not happen.");
					continue;
				}

				UObject obj = tabletObject.ExportObject.Value;
				FPropertyTag? rootComponentProperty = obj.Properties.FirstOrDefault(p => p.Name.Text.Equals("RootComponent"));
				UObject? rootComponent = rootComponentProperty?.Tag?.GetValue<FPackageIndex>()?.Load();
				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Log(LogLevel.Warning, "Failed to locate tablet POI");
					continue;
				}

				poi.Location = locationProperty.Tag!.GetValue<FVector>();
				poi.MapLocation = GameUtil.WorldToMap(poi.Location);
			}
		}

		private void ProcessSpawners(MapPoiLookups lookups, IReadOnlyList<ObjectWithDefaults> spawnerObjects, Logger logger)
		{
			Dictionary<string, MultiSpawnData?> spawnDataCache = new();

			logger.Log(LogLevel.Information, $"Processing {spawnerObjects.Count} spawners...");
			foreach (ObjectWithDefaults spawnerObject in spawnerObjects)
			{
				FObjectExport export = spawnerObject.Export;
				UObject obj = export.ExportObject.Value;

				List<UBlueprintGeneratedClass> scgClasses = new();
				USceneComponent? rootComponent = null;
				float? spawnInterval = null;
				void searchProperties(UObject searchObj)
				{
					foreach (FPropertyTag property in searchObj.Properties)
					{
						switch (property.Name.Text)
						{
							case "SCGClass":
							case "TanChaYouZaiGuaiData":
								if (scgClasses.Count == 0)
								{
									UBlueprintGeneratedClass? scgClass = property.Tag?.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>();
									if (scgClass is not null)
									{
										scgClasses.Add(scgClass);
									}
									else
									{
										logger.Log(LogLevel.Debug, $"[{export.ObjectName}] Spawner has explicitly set data to null.");
									}
								}
								break;
							case "SCGJianGeShiJian":
								spawnInterval = property.Tag!.GetValue<float>();
								break;
							case "ShuaGuaiQiWithRand":
								if (scgClasses.Count == 0)
								{
									UScriptArray? array = property.Tag?.GetValue<UScriptArray>();
									if (array is null) continue;

									foreach (FPropertyTagType item in array.Properties)
									{
										FStructFallback? sf = item.GetValue<FStructFallback>();
										if (sf is null) continue;

										FPropertyTag? scgProp = sf.Properties.FirstOrDefault(p => p.Name.Text.Equals("SCGClass"));
										if (scgProp is null) continue;

										UBlueprintGeneratedClass? scgClass = scgProp.Tag?.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>();
										if (scgClass is not null)
										{
											scgClasses.Add(scgClass);
										}
										else
										{
											logger.Log(LogLevel.Debug, $"[{export.ObjectName}] Spawner has explicitly set data to null.");
										}
									}
								}
								break;
							case "RootComponent":
								if (rootComponent is null)
								{
									rootComponent = property.Tag?.GetValue<FPackageIndex>()?.Load<USceneComponent>();
								}
								break;
						}
					}
				}

				searchProperties(obj);
				if ((scgClasses.Count == 0 || rootComponent is null || !spawnInterval.HasValue) && obj.Class is UBlueprintGeneratedClass objClass)
				{
					BlueprintHeirarchy.SearchInheritance(objClass, (current) =>
					{
						UObject? currentObj = current.ClassDefaultObject.Load();
						if (currentObj is null) return true;

						searchProperties(currentObj);
						return scgClasses.Count > 0 && rootComponent is not null;
					});
				}

				if (!spawnInterval.HasValue)
				{
					spawnInterval = 10.0f;
				}

				string spawnDataKey = string.Join(',', scgClasses.Select(c => c.Name));
				MultiSpawnData? spawnData = null;
				if (!spawnDataCache.TryGetValue(spawnDataKey, out spawnData))
				{
					spawnData = SpawnMinerUtil.LoadSpawnData(scgClasses, logger, export.ObjectName.Text, spawnerObject.DefaultsObject);
					spawnDataCache.Add(spawnDataKey, spawnData);
				}
				if (spawnData is null)
				{
					continue;
				}
				string poiName = string.Join(", ", spawnData.NpcNames);

				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Log(LogLevel.Warning, $"[{export.ObjectName}] Failed to find location for spawn point");
					continue;
				}

				FVector location = locationProperty.Tag!.GetValue<FVector>();

				NpcData firstNpc = spawnData.NpcData.First().Value;
				NpcCategory layerType = SpawnMinerUtil.GetNpcCategory(firstNpc);

				SpawnLayerInfo layerInfo;
				SpawnLayerGroup group;
				string type;
				bool male, female;

				void applyLayerTypeAndSex(bool onlyBabies)
				{
					layerInfo = lookups.SpawnLayerMap[layerType];
					group = SpawnLayerGroup.Npc;
					type = layerInfo.Name;
					male = false;
					female = false;

					switch (layerType)
					{
						case NpcCategory.Animal:
							group = SpawnLayerGroup.Animal;
							if (poiName.Contains(','))
							{
								type = "(Multiple)";
							}
							else
							{
								type = poiName;
							}
							break;
						case NpcCategory.Human:
							group = SpawnLayerGroup.Human;
							type = spawnData.ClanType.ToEn();
							break;
						case NpcCategory.Lamas:
						case NpcCategory.Cats:
						case NpcCategory.Ostrich:
							group = SpawnLayerGroup.BabyAnimal;
							type = poiName;
							break;
					}

					foreach (WeightedValue<NpcData> npcData in spawnData.NpcData)
					{
						if (!onlyBabies && spawnData.IsMixedAge && npcData.Value.IsBaby) continue;
						if (onlyBabies && !npcData.Value.IsBaby) continue;

						EXingBieType sex = npcData.Value.Sex;
						if (sex == EXingBieType.CHARACTER_XINGBIE_NAN)
						{
							male = true;
						}
						else if (sex == EXingBieType.CHARACTER_XINGBIE_NV)
						{
							female = true;
						}
						else if (sex == EXingBieType.CHARACTER_XINGBIE_WEIZHI)
						{
							male = true;
							female = true;
						}
					}
				}
				applyLayerTypeAndSex(false);

				string levelText = (spawnData.MinLevel == spawnData.MaxLevel) ? spawnData.MinLevel.ToString() : $"{spawnData.MinLevel} - {spawnData.MaxLevel}";

				string? tribeStatus = null;
				if (spawnData.Statuses.Any())
				{
					tribeStatus = string.Join(", ", spawnData.Statuses.Select(wv => $"{wv.Value.ToEn()} ({wv.Weight:0%})"));
				}

				string? occupation = null;
				if (spawnData.Occupations.Any())
				{
					occupation = string.Join(", ", spawnData.Occupations.Select(wv => $"{wv.Value.ToEn()} ({wv.Weight:0%})"));
				}

				MapPoi poi = new()
				{
					GroupIndex = group,
					Type = type,
					Title = poiName,
					Name = layerInfo.Name,
					Description = $"Level {levelText}",
					Male = male,
					Female = female,
					TribeStatus = tribeStatus,
					Occupation = occupation,
					SpawnCount = spawnData.SpawnCount,
					SpawnInterval = spawnInterval.Value,
					Location = location,
					MapLocation = GameUtil.WorldToMap(location),
					Icon = layerInfo.Icon,
					LootId = firstNpc.Loot,
					Loot2Id = firstNpc.ExtraLoot
				};

				lookups.Spawners.Add(poi);

				if (spawnData.IsMixedAge)
				{
					WeightedValue<NpcData>[] babyData = spawnData.NpcData.Where(d => d.Value.IsBaby).ToArray();

					layerType = SpawnMinerUtil.GetNpcCategory(babyData[0].Value);
					applyLayerTypeAndSex(true);

					SpawnMinerUtil.CalculateLevels(babyData, false, out int minLevel, out int maxLevel);

					levelText = (minLevel == maxLevel) ? minLevel.ToString() : $"{minLevel} - {maxLevel}";

					poi = new(poi)
					{
						GroupIndex = group,
						Type = type,
						Name = layerInfo.Name,
						Description = $"Level {levelText}",
						Male = male,
						Female = female,
						SpawnCount = babyData.Sum(b => b.Value.SpawnCount),
						Icon = layerInfo.Icon
					};

					lookups.Spawners.Add(poi);
				}
			}
		}

		private void ProcessChests(MapPoiLookups lookups, IReadOnlyList<ObjectWithDefaults> chestObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {chestObjects.Count} chests...");

			foreach (ObjectWithDefaults spawnerObject in chestObjects)
			{
				FObjectExport export = spawnerObject.Export;
				UObject obj = export.ExportObject.Value;

				string? lootId = null;
				string? poiName = null;
				string? openTip = null;
				FPackageIndex? lootItem = null;
				USceneComponent? rootComponent = null;
				void searchProperties(UObject searchObj)
				{
					UScriptArray? openCheckList = null;

					foreach (FPropertyTag property in searchObj.Properties)
					{
						switch (property.Name.Text)
						{
							case "BaoXiangDiaoLuoID":
								if (lootId is null)
								{
									lootId = property.Tag!.GetValue<FName>().Text;
								}
								break;
							case "JianZhuDisplayName":
								if (poiName is null)
								{
									poiName = GameUtil.ReadTextProperty(property);
								}
								break;
							case "OpenCheckDaoJuData":
								if (openTip is null)
								{
									openCheckList = property.Tag?.GetValue<UScriptArray>();
								}
								break;
							case "KaiQiJiaoHuDaoJuClass":
								if (lootItem is null)
								{
									lootItem = property.Tag?.GetValue<FPackageIndex>();
								}
								break;
							case "RootComponent":
								if (rootComponent is null)
								{
									rootComponent = property.Tag?.GetValue<FPackageIndex>()?.Load<USceneComponent>();
								}
								break;
						}
					}

					if (openCheckList is not null)
					{
						List<string> openTips = new();
						foreach (FPropertyTagType property in openCheckList.Properties)
						{
							FStructFallback? openCheckObj = property.GetValue<FStructFallback>();
							if (openCheckObj is null) continue;

							FPropertyTag? openTipProperty = openCheckObj.Properties.FirstOrDefault(p => p.Name.Text.Equals("NotOpenTips"));
							if (openTipProperty is null) continue;

							openTips.Add(GameUtil.ReadTextProperty(openTipProperty)!);
						}
						openTip = string.Join("<br />", openTips);
					}
				}

				searchProperties(obj);
				if ((lootId is null || poiName is null || openTip is null || rootComponent is null) && obj.Class is UBlueprintGeneratedClass objClass)
				{
					BlueprintHeirarchy.SearchInheritance(objClass, (current) =>
					{
						UObject? currentObj = current.ClassDefaultObject.Load();
						if (currentObj is null) return true;

						searchProperties(currentObj);
						return lootId is not null && poiName is not null && openTip is not null && rootComponent is not null;
					});
				}

				if (lootId is null && lootItem is null || poiName is null || rootComponent is null)
				{
					logger.Log(LogLevel.Warning, $"[{export.ObjectName}] Unable to load data for chest");
					continue;
				}

				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Log(LogLevel.Warning, $"[{export.ObjectName}] Failed to find location for chest");
					continue;
				}

				FVector location = locationProperty.Tag!.GetValue<FVector>();

				MapPoi poi = new()
				{
					GroupIndex = SpawnLayerGroup.Chest,
					Type = poiName,
					Title = poiName,
					Name = "Lootable Object",
					Description = openTip,
					Icon = lookups.LootIcon,
					Location = location,
					MapLocation = GameUtil.WorldToMap(location),
					LootId = lootId,
					LootItem = lootItem?.Name
				};

				lookups.Lootables.Add(poi);
			}
		}

		private void FindPoiTextures(MapPoiLookups lookups, IReadOnlyDictionary<ETanSuoDianType, UTexture2D> mapIcons, Logger logger)
		{
			foreach (var pair in lookups.TypeLookup)
			{
				if (!mapIcons.TryGetValue(pair.Key, out var texture))
				{
					continue;
				}
				foreach (MapPoi poi in pair.Value)
				{
					poi.Icon = texture;
				}
			}
		}

		private void WriteIcons(MapData mapData, Config config, Logger logger)
		{
			string outDir = Path.Combine(config.OutputDirectory, Name, "icons");

			HashSet<string> exported = new();
			foreach (var pair in mapData.POIs)
			{
				if (exported.Add(pair.Value[0].Icon.Name))
				{
					TextureExporter.ExportTexture(pair.Value[0].Icon, false, logger, outDir);
				}
				foreach (MapPoi poi in pair.Value)
				{
					if (poi.Achievement?.Icon is null) continue;

					if (exported.Add(poi.Achievement.Icon.Name))
					{
						TextureExporter.ExportTexture(poi.Achievement.Icon, false, logger, outDir);
					}
				}
			}
		}

		private void WriteCsv(MapData mapData, Config config, Logger logger)
		{
			WriteCsvPois(mapData, config, logger);
			WriteCsvLoot(mapData, config, logger);
		}

		private void WriteCsvPois(MapData mapData, Config config, Logger logger)
		{
			string? csvStr(string? value)
			{
				if (value is null || value.Equals("None")) return null;
				return $"\"{value.Replace("\"", "\"\"")}\"";
			}

			foreach (var pair in mapData.POIs)
			{
				string outPath = Path.Combine(config.OutputDirectory, Name, $"{pair.Key}.csv");
				using FileStream outFile = IOUtil.CreateFile(outPath, logger);
				using StreamWriter writer = new(outFile, Encoding.UTF8);

				writer.WriteLine("gpIdx,gpName,type,posX,posY,posZ,mapX,mapY,title,name,desc,extra,m,f,stat,occ,num,intr,loot,loot2,lootitem,icon,ach,achDesc,achIcon");

				foreach (MapPoi poi in pair.Value)
				{
					string spawnerSegment = ",,,,,,,,";
					string poiSegment = ",,";
					if (poi.GroupIndex == SpawnLayerGroup.PointOfInterest)
					{
						poiSegment = $"{csvStr(poi.Achievement?.Name)},{csvStr(poi.Achievement?.Description)},{csvStr(poi.Achievement?.Icon?.Name)}";
					}
					else
					{
						spawnerSegment = $"{poi.Male},{poi.Female},{csvStr(poi.TribeStatus)},{csvStr(poi.Occupation)},{poi.SpawnCount},{poi.SpawnInterval},{csvStr(poi.LootId)},{csvStr(poi.Loot2Id)},{csvStr(poi.LootItem)}";
					}
					writer.WriteLine($"{(int)poi.GroupIndex},{csvStr(GetGroupName(poi.GroupIndex))},{csvStr(poi.Type)},{poi.Location.X:0},{poi.Location.Y:0},{poi.Location.Z:0},{poi.MapLocation.X:0},{poi.MapLocation.Y:0},{csvStr(poi.Title)},{csvStr(poi.Name)},{csvStr(poi.Description)},{csvStr(poi.Extra)},{spawnerSegment},{csvStr(poi.Icon?.Name)},{poiSegment}");
				}
			}
		}

		private void WriteCsvLoot(MapData mapData, Config config, Logger logger)
		{
			string? csvStr(string? value)
			{
				if (value is null || value.Equals("None")) return null;
				return $"\"{value.Replace("\"", "\"\"")}\"";
			}

			string outPath = Path.Combine(config.OutputDirectory, Name, "loot.csv");
			using FileStream outFile = IOUtil.CreateFile(outPath, logger);
			using StreamWriter writer = new(outFile, Encoding.UTF8);

			writer.WriteLine("id,entry,item,chance,weight,min,max,quality,asset");

			foreach (var pair in mapData.Loot)
			{
				for (int e = 0; e < pair.Value.Entries.Count; ++e)
				{
					LootEntry entry = pair.Value.Entries[e];
					for (int i = 0; i < entry.Items.Count; ++i)
					{
						LootItem item = entry.Items[i];
						writer.WriteLine($"{csvStr(pair.Key)},{e},{i},{entry.Probability},{item.Weight},{item.Amount.LowerBound.Value},{item.Amount.UpperBound.Value},{(int)item.Quality},{csvStr(item.Asset)}");
					}
				}
			}
		}

		private void WriteSql(MapData mapData, TextWriter sqlWriter, Logger logger)
		{
			WriteSqlPois(mapData, sqlWriter, logger);
			sqlWriter.WriteLine();
			WriteSqlLoot(mapData, sqlWriter, logger);
		}

		private void WriteSqlPois(MapData mapData, TextWriter sqlWriter, Logger logger)
		{
			// Schema
			// create table `poi` (
			//     `gpIdx` int not null,
			//     `gpName` varchar(63) not null,
			//     `type` varchar(63) not null,
			//     `posX` float not null,
			//     `posY` float not null,
			//     `posZ` float not null,
			//     `mapX` int not null,
			//     `mapY` int not null,
			//     `title` varchar(127),
			//     `name` varchar(127),
			//     `desc` varchar(511),
			//     `extra` varchar(511),
			//     `m` bool,
			//     `f` bool,
			//     `stat` varchar(63),
			//     `occ` varchar(127),
			//     `num` int,
			//     `intr` float,
			//     `loot` varchar(127),
			//     `loot2` varchar(127),
			//     `lootitem` varchar(127),
			//     `icon` varchar(127),
			//     `ach` varchar(127),
			//     `achDesc` varchar(255),
			//     `achIcon` varchar(127)
			// )

			sqlWriter.WriteLine("truncate table `poi`;");

			string dbStr(string? value)
			{
				if (value is null || value.Equals("None")) return "null";
				return $"'{value.Replace("\'", "\'\'")}'";
			}

			string dbBool(bool value)
			{
				return value ? "true" : "false";
			}

			foreach (var pair in mapData.POIs)
			{
				foreach (MapPoi poi in pair.Value)
				{
					// This is because some ancient tablets come from dungeons or pyramids instead of spawning in the world.
					if (poi.Location == FVector.ZeroVector) continue;

					string spawnerSegment = "null, null, null, null, null, null, null, null, null";
					string poiSegment = "null, null, null";
					if (poi.GroupIndex == SpawnLayerGroup.PointOfInterest)
					{
						poiSegment = $"{dbStr(poi.Achievement?.Name)}, {dbStr(poi.Achievement?.Description)}, {dbStr(poi.Achievement?.Icon?.Name)}";
					}
					else
					{
						spawnerSegment = $"{dbBool(poi.Male)}, {dbBool(poi.Female)}, {dbStr(poi.TribeStatus)}, {dbStr(poi.Occupation)}, {poi.SpawnCount}, {poi.SpawnInterval}, {dbStr(poi.LootId)}, {dbStr(poi.Loot2Id)}, {dbStr(poi.LootItem)}";
					}

					sqlWriter.WriteLine($"insert into `poi` values ({(int)poi.GroupIndex}, {dbStr(GetGroupName(poi.GroupIndex))}, {dbStr(poi.Type)}, {poi.Location.X:0}, {poi.Location.Y:0}, {poi.Location.Z:0}, {poi.MapLocation.X:0}, {poi.MapLocation.Y:0}, {dbStr(poi.Title)}, {dbStr(poi.Name)}, {dbStr(poi.Description)}, {dbStr(poi.Extra)}, {spawnerSegment}, {dbStr(poi.Icon?.Name)}, {poiSegment});");
				}
			}
		}

		private void WriteSqlLoot(MapData mapData, TextWriter sqlWriter, Logger logger)
		{
			// create table `loot` (
			//     `id` varchar(127) not null,
			//     `entry` int not null,
			//     `item` int not null,
			//     `chance` int not null,
			//     `weight` int not null,
			//     `min` int not null,
			//     `max` int not null,
			//     `quality` int not null,
			//     `asset` varchar(127) not null,
			//     primary key (`id`, `entry`, `item`)
			// )

			sqlWriter.WriteLine("truncate table `loot`;");

			string dbStr(string? value)
			{
				if (value is null || value.Equals("None")) return "null";
				return $"'{value.Replace("\'", "\'\'")}'";
			}

			foreach (var pair in mapData.Loot)
			{
				for (int e = 0; e < pair.Value.Entries.Count; ++e)
				{
					LootEntry entry = pair.Value.Entries[e];
					for (int i = 0; i < entry.Items.Count; ++i)
					{
						LootItem item = entry.Items[i];
						sqlWriter.WriteLine($"insert into `loot` values ({dbStr(pair.Key)}, {e}, {i}, {entry.Probability}, {item.Weight}, {item.Amount.LowerBound.Value}, {item.Amount.UpperBound.Value}, {(int)item.Quality}, {dbStr(item.Asset)});");
					}
				}
			}
		}

		private class MapData
		{
			public IReadOnlyDictionary<string, List<MapPoi>> POIs { get; }

			public IReadOnlyDictionary<string, LootData> Loot { get; }

			public MapData(IReadOnlyDictionary<string, List<MapPoi>> pois, IReadOnlyDictionary<string, LootData> loot)
			{
				POIs = pois;
				Loot = loot;
			}
		}

		private class MapPoiLookups
		{
			public IDictionary<int, MapPoi> IndexLookup { get; } = new Dictionary<int, MapPoi>();

			public IDictionary<ETanSuoDianType, List<MapPoi>> TypeLookup { get; } = new Dictionary<ETanSuoDianType, List<MapPoi>>();

			public IDictionary<string, MapPoi> Tablets { get; } = new Dictionary<string, MapPoi>();

			public IDictionary<string, LootData> Loot { get; } = new Dictionary<string, LootData>();

			public IDictionary<NpcCategory, SpawnLayerInfo> SpawnLayerMap { get; } = new Dictionary<NpcCategory, SpawnLayerInfo>((int)NpcCategory.Count);

			public IList<MapPoi> Spawners { get; } = new List<MapPoi>();

			public IList<MapPoi> Lootables { get; } = new List<MapPoi>();

			public UTexture2D LootIcon { get; set; } = null!;

			public IReadOnlyDictionary<string, List<MapPoi>> GetAllPois()
			{
				Dictionary<string, List<MapPoi>> result = new();

				foreach (MapPoi poi in IndexLookup.Values.Concat(Tablets.Values).Concat(Spawners).Concat(Lootables))
				{
					if (!result.TryGetValue(poi.Icon.Name, out List<MapPoi>? list))
					{
						list = new();
						result.Add(poi.Icon.Name, list);
					}
					list.Add(poi);
				}

				return result;
			}
		}

		private class MapPoi : ICloneable
		{
			public SpawnLayerGroup GroupIndex { get; set; }
			public string Type { get; set; } = null!;
			public string? Title { get; set; }
			public string? Name { get; set; }
			public string? Description { get; set; }
			public string? Extra { get; set; }
			public bool Male { get; set; }
			public bool Female { get; set; }
			public string? TribeStatus { get; set; }
			public string? Occupation { get; set; }
			public int SpawnCount { get; set; }
			public float SpawnInterval { get; set; }
			public string? LootId { get; set; }
			public string? Loot2Id { get; set; }
			public string? LootItem { get; set; }
			public FVector Location { get; set; }
			public FVector2D MapLocation { get; set; }
			public UTexture2D Icon { get; set; } = null!;
			public AchievementData? Achievement { get; set; }

			public MapPoi()
			{
			}

			public MapPoi(MapPoi other)
			{
				GroupIndex = other.GroupIndex;
				Type = other.Type;
				Title = other.Title;
				Name = other.Name;
				Description = other.Description;
				Extra = other.Extra;
				Male = other.Male;
				Female = other.Female;
				TribeStatus = other.TribeStatus;
				Occupation = other.Occupation;
				SpawnCount = other.SpawnCount;
				SpawnInterval = other.SpawnInterval;
				Location = other.Location;
				MapLocation = other.MapLocation;
				Icon = other.Icon;
				Achievement = other.Achievement;
			}

			public object Clone()
			{
				return new MapPoi(this);
			}

			public override string ToString()
			{
				return $"{Title}: {Name} [{Location}]";
			}
		}

		private struct ObjectWithDefaults
		{
			public FObjectExport Export;
			public UObject? DefaultsObject;

			public override string ToString()
			{
				return Export.ObjectName.Text;
			}
		}

		private struct LootData
		{
			public List<LootEntry> Entries;

			public LootData()
			{
				Entries = new();
			}

			public override string? ToString()
			{
				return $"{Entries.Count} entries";
			}
		}

		private struct LootEntry
		{
			public int Probability;
			public List<LootItem> Items;

			public LootEntry()
			{
				Probability = 0;
				Items = new();
			}

			public override string ToString()
			{
				return $"{Probability}, {Items.Count} items";
			}
		}

		private struct LootItem
		{
			public int Weight;
			public TRange<float> Amount;
			public EDaoJuPinZhi Quality;
			public string Asset;

			public LootItem()
			{
				Weight = 0;
				Amount = new TRange<float>();
				Quality = EDaoJuPinZhi.EDJPZ_Level1;
				Asset = null!;
			}

			public override string ToString()
			{
				return $"{Weight}, {Amount.LowerBound.Value}-{Amount.UpperBound.Value} {Asset} (Quality {(int)Quality})";
			}
		}

		private struct SpawnLayerInfo
		{
			public string Name;
			public UTexture2D Icon;

			public override string ToString()
			{
				return Name;
			}
		}

		private enum SpawnLayerGroup
		{
			Unset,
			PointOfInterest,
			BabyAnimal,
			Animal,
			Human,
			Npc,
			Chest
		}

		private enum EDaoJuPinZhi
		{
			EDJPZ_Level1,
			EDJPZ_Level2,
			EDJPZ_Level3,
			EDJPZ_Level4,
			EDJPZ_Level5,
			EDJPZ_Level6,
			EDJPZ_Max
		}

		private enum ETanSuoDianType
		{
			ETSD_TYPE_NOT_DEFINE,
			ETSD_TYPE_JINZITA,
			ETSD_TYPE_YIJI,
			ETSD_TYPE_DIXIA_YIJI,
			ETSD_TYPE_YEWAI_YIJI,
			ETSD_TYPE_YEWAI_YIZHI,
			ETSD_TYPE_BULUO_CHENGZHAI_BIG,
			ETSD_TYPE_BULUO_CHENGZHAI_MIDDLE,
			ETSD_TYPE_BULUO_CHENGZHAI_SMALL,
			ETSD_TYPE_CHAOXUE,
			ETSD_TYPE_KUANGCHUANG_BIG,
			ETSD_TYPE_KUANGCHUANG_MIDDLE,
			ETSD_TYPE_DIXIACHENG,
			ETSD_TYPE_CHUANSONGMEN,
			ETSD_TYPE_KUANGCHUANG_SMALL,
			ETSD_TYPE_SHEN_MIAO
		};

		private static string GetTitle(ETanSuoDianType type)
		{
			return type switch
			{
				ETanSuoDianType.ETSD_TYPE_NOT_DEFINE => "Unknown",
				ETanSuoDianType.ETSD_TYPE_JINZITA => "Ancient Pyramid",
				ETanSuoDianType.ETSD_TYPE_YIJI => "Holy Ruins",
				ETanSuoDianType.ETSD_TYPE_DIXIA_YIJI => "Ancient Ruins Dungeon",
				ETanSuoDianType.ETSD_TYPE_YEWAI_YIJI => "Ancient Ruins",
				ETanSuoDianType.ETSD_TYPE_YEWAI_YIZHI => "Ancient Ruins",
				ETanSuoDianType.ETSD_TYPE_BULUO_CHENGZHAI_BIG => "Barbarian Fortress",
				ETanSuoDianType.ETSD_TYPE_BULUO_CHENGZHAI_MIDDLE => "Barbarian Barrack",
				ETanSuoDianType.ETSD_TYPE_BULUO_CHENGZHAI_SMALL => "Barbarian Camp",
				ETanSuoDianType.ETSD_TYPE_CHAOXUE => "Beast Lair",
				ETanSuoDianType.ETSD_TYPE_KUANGCHUANG_BIG => "Large Mine",
				ETanSuoDianType.ETSD_TYPE_KUANGCHUANG_MIDDLE => "Mine",
				ETanSuoDianType.ETSD_TYPE_DIXIACHENG => "Ancient Dungeon",
				ETanSuoDianType.ETSD_TYPE_CHUANSONGMEN => "Mysterious Portal",
				ETanSuoDianType.ETSD_TYPE_KUANGCHUANG_SMALL => "Small Mine",
				ETanSuoDianType.ETSD_TYPE_SHEN_MIAO => "Mysterious Ruins",
				_ => "Unknown"
			};
		}

		private static string GetType(ETanSuoDianType type)
		{
			return type switch
			{
				ETanSuoDianType.ETSD_TYPE_NOT_DEFINE => "Unknown",
				ETanSuoDianType.ETSD_TYPE_JINZITA => "Ancient Pyramid",
				ETanSuoDianType.ETSD_TYPE_YIJI => "Ruins (Holy)",
				ETanSuoDianType.ETSD_TYPE_DIXIA_YIJI => "Dungeon (Ruins)",
				ETanSuoDianType.ETSD_TYPE_YEWAI_YIJI => "Ruins (Large)",
				ETanSuoDianType.ETSD_TYPE_YEWAI_YIZHI => "Ruins",
				ETanSuoDianType.ETSD_TYPE_BULUO_CHENGZHAI_BIG => "Barbarian Fortress",
				ETanSuoDianType.ETSD_TYPE_BULUO_CHENGZHAI_MIDDLE => "Barbarian Barrack",
				ETanSuoDianType.ETSD_TYPE_BULUO_CHENGZHAI_SMALL => "Barbarian Camp",
				ETanSuoDianType.ETSD_TYPE_CHAOXUE => "Beast Lair",
				ETanSuoDianType.ETSD_TYPE_KUANGCHUANG_BIG => "Mine (Large)",
				ETanSuoDianType.ETSD_TYPE_KUANGCHUANG_MIDDLE => "Mine",
				ETanSuoDianType.ETSD_TYPE_DIXIACHENG => "Dungeon",
				ETanSuoDianType.ETSD_TYPE_CHUANSONGMEN => "Mysterious Portal",
				ETanSuoDianType.ETSD_TYPE_KUANGCHUANG_SMALL => "Mine (Small)",
				ETanSuoDianType.ETSD_TYPE_SHEN_MIAO => "Ruins (Mysterious)",
				_ => "Unknown"
			};
		}

		private static string GetGroupName(SpawnLayerGroup group)
		{
			return group switch
			{
				SpawnLayerGroup.Unset => "",
				SpawnLayerGroup.PointOfInterest => "Point of Interest",
				SpawnLayerGroup.BabyAnimal => "Baby Animal Spawn",
				SpawnLayerGroup.Animal => "Animal Spawn",
				SpawnLayerGroup.Human => "Human Spawn",
				SpawnLayerGroup.Npc => "Other NPC Spawn",
				SpawnLayerGroup.Chest => "Lootable Objects",
				_ => ""
			};
		}
	}
}
