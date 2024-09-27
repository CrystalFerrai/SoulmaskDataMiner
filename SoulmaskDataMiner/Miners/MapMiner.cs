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
	[RequireHeirarchy(true), RequireLootDatabase(true)]
	internal class MapMiner : MinerBase
	{
		public override string Name => "Map";

		private static readonly MapData sMapData;

		static MapMiner()
		{
			sMapData = new();
		}

		public override bool Run(IProviderManager providerManager, Config config, Logger logger, ISqlWriter sqlWriter)
		{
			logger.Log(LogLevel.Information, "Exporting map images...");
			if (!ExportMapImages(providerManager, config, logger))
			{
				return false;
			}

			logger.Log(LogLevel.Information, "<<< Begin processing map >>>");
			MapInfo? mapData = ProcessMap(providerManager, logger);
			if (mapData is null)
			{
				return false;
			}
			logger.Log(LogLevel.Information, "<<< Finished processing map >>>");

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

		private MapInfo? ProcessMap(IProviderManager providerManager, Logger logger)
		{
			logger.Log(LogLevel.Information, "Loading dependencies...");

			UObject? mapIntel = LoadMapIntel(providerManager, logger);
			if (mapIntel is null) return null;

			IReadOnlyDictionary<ETanSuoDianType, UTexture2D>? mapIcons = GetMapIcons(mapIntel, logger);
			if (mapIcons is null) return null;

			MapPoiDatabase? poiDatabase = GetPois(providerManager, mapIntel, providerManager.Achievements, logger);
			if (poiDatabase is null) return null;

			poiDatabase.LootIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/ChuShenTianFu/ChengHao/ChengHao_poxiangren.uasset", logger)!;
			if (poiDatabase.LootIcon is null) return null;

			poiDatabase.RespawnIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/fuhuodian.uasset", logger)!;
			if (poiDatabase.RespawnIcon is null) return null;

			poiDatabase.BossIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/hud/dusuicon.uasset", logger)!;
			if (poiDatabase.BossIcon is null) return null;

			DungeonUtil dungeonUtil = new();
			poiDatabase.DungeonMap = dungeonUtil.LoadDungeonData(providerManager, logger)!;
			if (poiDatabase.DungeonMap is null) return null;

			if (!FindTabletData(providerManager, poiDatabase, providerManager.Achievements, logger))
			{
				return null;
			}

			if (!LoadSpawnLayers(providerManager, poiDatabase, logger))
			{
				return null;
			}

			if (!FindMapObjects(providerManager, logger, poiDatabase,
				out IReadOnlyList<FObjectExport>? poiObjects,
				out IReadOnlyList<FObjectExport>? tabletObjects,
				out IReadOnlyList<FObjectExport>? respawnObjects,
				out IReadOnlyList<ObjectWithDefaults>? spawnerObjects,
				out IReadOnlyList<FObjectExport>? barracksObjects,
				out IReadOnlyList<ObjectWithDefaults>? chestObjects,
				out IReadOnlyList<FObjectExport>? dungeonObjects,
				out IReadOnlyList<FObjectExport>? gamefunctionObjects))
			{
				return null;
			}

			FoliageUtil foliageUtil = new(sMapData);
			IReadOnlyDictionary<EProficiency, IReadOnlyDictionary<string, FoliageData>>? foliageData = foliageUtil.LoadFoliage(providerManager, logger);
			if (foliageData is null) return null;

			ProcessPois(poiDatabase, poiObjects, logger);
			ProcessTablets(poiDatabase, tabletObjects, logger);
			ProcessRespawnPoints(poiDatabase, respawnObjects, logger);
			ProcessSpawners(poiDatabase, spawnerObjects, barracksObjects, logger);
			ProcessChests(poiDatabase, chestObjects, logger);
			ProcessFoliage(poiDatabase, foliageData, logger);
			ProcessDungeons(poiDatabase, dungeonObjects, logger);
			ProcessWorldBosses(poiDatabase, gamefunctionObjects, logger);

			FindPoiTextures(poiDatabase, mapIcons, logger);

			return new(poiDatabase.GetAllPois(), poiDatabase.AdditionalIconsToExport.ToArray());
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

		private MapPoiDatabase? GetPois(IProviderManager providerManager, UObject mapIntel, Achievements achievements, Logger logger)
		{
			foreach (FPropertyTag property in mapIntel.Properties)
			{
				if (!property.Name.Text.Equals("AllTanSuoDianInfoMap")) continue;

				MapPoiDatabase poiDatabase = new(providerManager.LootDatabase);

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
						Key = index,
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
					if (poi.Title is null)
					{
						poi.Title = GetTitle(poiType.Value);
					}

					if (achievements.CollectMap.TryGetValue(index, out AchievementData? achievement))
					{
						poi.Achievement = achievement;
					}

					poiDatabase.IndexLookup.Add(index, poi);
					if (!poiDatabase.TypeLookup.TryGetValue(poiType.Value, out List<MapPoi>? list))
					{
						list = new();
						poiDatabase.TypeLookup.Add(poiType.Value, list);
					}
					list.Add(poi);

					if (poiType == ETanSuoDianType.ETSD_TYPE_DIXIACHENG)
					{
						poiDatabase.DungeonPois.Add(poi);
					}
				}

				return poiDatabase;
			}

			return null;
		}

		private bool FindTabletData(IProviderManager providerManager, MapPoiDatabase poiDatabase, Achievements achievements, Logger logger)
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

				string idStr = className.Substring(className.Length - 5, 3);
				int key;
				if (!int.TryParse(idStr, out key))
				{
					logger.Log(LogLevel.Warning, $"Unable to parse tablet id from class name {className}");
					key = -1;
				}

				MapPoi tabletData = new()
				{
					Key = key >= 0 ? (key + 100000) : null,
					GroupIndex = SpawnLayerGroup.PointOfInterest,
					Type = "Tablet (Ancient)",
					Title = "Ancient Tablet",
					Achievement = ancientAchievement
				};
				List<string> unlocks = new();
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
						case "AutoGetKeJiList":
							{
								UScriptArray list = property.Tag!.GetValue<UScriptArray>()!;
								foreach (FPropertyTagType item in list.Properties)
								{
									UObject? unlockNode = item.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("KeJiSubNodeClass"))?.Tag?.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>()?.ClassDefaultObject.Load();
									if (unlockNode is null) continue;

									UScriptArray? unlockRecipeList = unlockNode.Properties.FirstOrDefault(p => p.Name.Text.Equals("KeJiPeiFangList"))?.Tag?.GetValue<UScriptArray>();
									if (unlockRecipeList is null) continue;

									foreach (FPropertyTagType recipe in unlockRecipeList.Properties)
									{
										UObject? unlockRecipe = recipe.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>()?.ClassDefaultObject.Load();
										if (unlockRecipe is null) continue;

										FPackageIndex? unlockItem = unlockRecipe.Properties.FirstOrDefault(p => p.Name.Text.Equals("ProduceDaoJu"))?.Tag?.GetValue<FPackageIndex>();
										if (unlockItem is null) continue;

										unlocks.Add(unlockItem.Name);
									}
								}
							}
							break;
					}
				}
				if (tabletData.Icon is null)
				{
					logger.Log(LogLevel.Warning, $"Unable to find all data for tablet POI {className}");
					continue;
				}

				if (unlocks.Count > 0)
				{
					tabletData.Unlocks = $"[{string.Join(',', unlocks.Select(u => $"\"{u}\""))}]";
				}

				poiDatabase.Tablets.Add(className, tabletData);
			}

			return poiDatabase.Tablets.Any();
		}

		private bool LoadSpawnLayers(IProviderManager providerManager, MapPoiDatabase poiDatabase, Logger logger)
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

				poiDatabase.SpawnLayerMap[(NpcCategory)i] = new SpawnLayerInfo() { Name = ((NpcCategory)i).ToString(), Icon = icon };
			}

			UTexture2D? lamasIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/dongwutubiao/ditubiaoji_yangtuo.uasset", logger);
			UTexture2D? catsIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/dongwutubiao/ditubiaoji_baozi.uasset", logger);
			UTexture2D? ostrichIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/dongwutubiao/ditubiaoji_tuoniao.uasset", logger);
			UTexture2D? turkeyIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/dongwutubiao/ditubiaoji_huoji.uasset", logger);
			UTexture2D? capybaraIcon = GameUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/dongwutubiao/ditubiaoji_shuitun.uasset", logger);
			if (lamasIcon is null || catsIcon is null || ostrichIcon is null || turkeyIcon is null || capybaraIcon is null)
			{
				logger.LogError("Failed to load spawner icon texture.");
				return false;
			}

			const string babyAnimalSpawnName = "Baby Animal Spawn";
			poiDatabase.SpawnLayerMap[NpcCategory.Lamas] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = lamasIcon };
			poiDatabase.SpawnLayerMap[NpcCategory.Cats] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = catsIcon };
			poiDatabase.SpawnLayerMap[NpcCategory.Ostrich] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = ostrichIcon };
			poiDatabase.SpawnLayerMap[NpcCategory.Turkey] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = turkeyIcon };
			poiDatabase.SpawnLayerMap[NpcCategory.Capybara] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = capybaraIcon };

			return true;
		}

		private bool FindMapObjects(IProviderManager providerManager, Logger logger, MapPoiDatabase poiDatabase,
			[NotNullWhen(true)] out IReadOnlyList<FObjectExport>? poiObjects,
			[NotNullWhen(true)] out IReadOnlyList<FObjectExport>? tabletObjects,
			[NotNullWhen(true)] out IReadOnlyList<FObjectExport>? respawnObjects,
			[NotNullWhen(true)] out IReadOnlyList<ObjectWithDefaults>? spawnerObjects,
			[NotNullWhen(true)] out IReadOnlyList<FObjectExport>? barracksObjects,
			[NotNullWhen(true)] out IReadOnlyList<ObjectWithDefaults>? chestObjects,
			[NotNullWhen(true)] out IReadOnlyList<FObjectExport>? dungeonObjects,
			[NotNullWhen(true)] out IReadOnlyList<FObjectExport>? gamefunctionObjects)
		{
			poiObjects = null;
			respawnObjects = null;
			tabletObjects = null;
			spawnerObjects = null;
			barracksObjects = null;
			chestObjects = null;
			dungeonObjects = null;
			gamefunctionObjects = null;

			Package[] gameplayPackages = new Package[2];
			{
				if (!providerManager.Provider.TryFindGameFile("WS/Content/Maps/Level01/Level01_Hub/Level01_GamePlay.umap", out GameFile file))
				{
					logger.LogError("Unable to load asset Level01_GamePlay.");
					return false;
				}
				gameplayPackages[0] = (Package)providerManager.Provider.LoadPackage(file);
			}
			{
				if (!providerManager.Provider.TryFindGameFile("WS/Content/Maps/Level01/Level01_Hub/Level01_GamePlay2.umap", out GameFile file))
				{
					logger.LogError("Unable to load asset Level01_GamePlay2.");
					return false;
				}
				gameplayPackages[1] = (Package)providerManager.Provider.LoadPackage(file);
			}
			Package mainPackage;
			{
				if (!providerManager.Provider.TryFindGameFile("WS/Content/Maps/Level01/Level01_Main.umap", out GameFile file))
				{
					logger.LogError("Unable to load asset Level01_Main.");
					return false;
				}
				mainPackage = (Package)providerManager.Provider.LoadPackage(file);
			}

			const string poiClass = "HVolumeChuFaQi";

			const string respawnClass = "HPlayerStart";

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
				spawnerBpClasses.AddRange(BlueprintHeirarchy.Instance.GetDerivedClasses(searchClass));
			}

			Dictionary<string, UObject?> spawnerClasses = spawnerBaseClasses.ToDictionary(c => c, c => (UObject?)null);
			foreach (BlueprintClassInfo bpClass in spawnerBpClasses)
			{
				UBlueprintGeneratedClass? exportObj = (UBlueprintGeneratedClass?)bpClass.Export?.ExportObject.Value;
				FPropertyTag? scgClassProperty = exportObj?.ClassDefaultObject.Load()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("SCGClass"));
				UObject? defaultScgObj = scgClassProperty?.Tag?.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>()?.ClassDefaultObject.Load();
				spawnerClasses.Add(bpClass.Name, defaultScgObj);
			}

			const string barracksBaseClass = "HBuLuoGuanLiQi";

			HashSet<string> barracksClasses = new(BlueprintHeirarchy.Instance.GetDerivedClasses(barracksBaseClass).Select(c => c.Name));

			const string chestBaseClass = "HJianZhuBaoXiang";

			List<BlueprintClassInfo> chestBpClasses = new(BlueprintHeirarchy.Instance.GetDerivedClasses(chestBaseClass));

			Dictionary<string, UObject?> chestClasses = new();
			foreach (BlueprintClassInfo bpClass in chestBpClasses)
			{
				UBlueprintGeneratedClass? exportObj = (UBlueprintGeneratedClass?)bpClass.Export?.ExportObject.Value;
				UObject? defaultObj = exportObj?.ClassDefaultObject.Load();
				chestClasses.Add(bpClass.Name, defaultObj);
			}

			const string gameFunctionBaseClass = "HJianZhuGameFunction";
			HashSet<string> gameFunctionClasses = new(BlueprintHeirarchy.Instance.GetDerivedClasses(gameFunctionBaseClass).Select(c => c.Name));

			List<FObjectExport> poiObjectList = new();
			List<FObjectExport> respawnObjectList = new();
			List<FObjectExport> tabletObjectList = new();
			List<ObjectWithDefaults> spawnerObjectList = new();
			List<FObjectExport> barracksObjectList = new();
			List<ObjectWithDefaults> chestObjectList = new();
			List<FObjectExport> dungeonObjectList = new();
			List<FObjectExport> gameFunctionObjectList = new();

			logger.Log(LogLevel.Information, "Scanning for objects...");
			foreach (Package package in gameplayPackages)
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
					else if (barracksClasses.Contains(export.ClassName))
					{
						barracksObjectList.Add(export);
					}
					else if (chestClasses.TryGetValue(export.ClassName, out UObject? defaultObj))
					{
						chestObjectList.Add(new() { Export = export, DefaultsObject = defaultObj });
					}
					else if (poiDatabase.Tablets.ContainsKey(export.ClassName))
					{
						tabletObjectList.Add(export);
					}
					else if (poiDatabase.DungeonMap.ContainsKey(export.ClassName))
					{
						dungeonObjectList.Add(export);
					}
					else if (gameFunctionClasses.Contains(export.ClassName))
					{
						gameFunctionObjectList.Add(export);
					}
				}
			}
			{
				logger.Log(LogLevel.Debug, mainPackage.Name);
				foreach (FObjectExport export in mainPackage.ExportMap)
				{
					if (export.ClassName.Equals(respawnClass))
					{
						respawnObjectList.Add(export);
					}
				}
			}

			poiObjects = poiObjectList;
			respawnObjects = respawnObjectList;
			tabletObjects = tabletObjectList;
			spawnerObjects = spawnerObjectList;
			barracksObjects = barracksObjectList;
			chestObjects = chestObjectList;
			dungeonObjects = dungeonObjectList;
			gamefunctionObjects = gameFunctionObjectList;

			return true;
		}

		private void ProcessPois(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> poiObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {poiObjects.Count} POIs...");
			foreach (FObjectExport poiObject in poiObjects)
			{
				int? index = null;
				UObject? rootComponent = null;

				UObject obj = poiObject.ExportObject.Value;
				foreach (FPropertyTag property in obj.Properties)
				{
					switch (property.Name.Text)
					{
						case "ParamInt":
							index = property.Tag?.GetValue<int>();
							break;
						case "RootComponent":
							rootComponent = property.Tag?.GetValue<FPackageIndex>()?.ResolvedObject?.Object?.Value;
							break;
					}
				}

				if (!index.HasValue) continue;

				if (!poiDatabase.IndexLookup.TryGetValue(index.Value, out MapPoi? poi))
				{
					continue;
				}

				if (rootComponent is null)
				{
					logger.Log(LogLevel.Warning, $"Failed to locate POI {index}");
					continue;
				}

				FPropertyTag? locationProperty = rootComponent.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Log(LogLevel.Warning, $"Failed to locate POI {index}");
					continue;
				}
				poi.Location = locationProperty.Tag!.GetValue<FVector>();
				poi.MapLocation = WorldToMap(poi.Location.Value);
			}
		}

		private void ProcessRespawnPoints(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> respawnObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {respawnObjects.Count} respawn points...");

			foreach (FObjectExport respawnObject in respawnObjects)
			{
				string? name = null;
				float radius = 0.0f;
				USceneComponent? rootComponent = null;

				UObject obj = respawnObject.ExportObject.Value;
				foreach (FPropertyTag property in obj.Properties)
				{
					switch (property.Name.Text)
					{
						case "Name":
							name = GameUtil.ReadTextProperty(property);
							break;
						case "Radius":
							radius = property.Tag!.GetValue<float>();
							break;
						case "RootComponent":
							rootComponent = property.Tag?.GetValue<FPackageIndex>()?.Load<USceneComponent>();
							break;
					}
				}

				if (name is null || rootComponent is null)
				{
					logger.Log(LogLevel.Warning, "Respawn point properties not found");
					continue;
				}

				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Log(LogLevel.Warning, "Failed to locate respawn point");
					continue;
				}

				FVector location = locationProperty.Tag!.GetValue<FVector>();

				MapPoi poi = new()
				{
					GroupIndex = SpawnLayerGroup.PointOfInterest,
					Type = "Respawn Point",
					Title = "Respawn Point",
					Name = name,
					Description = radius > 0.0f ? $"Radius: {radius}" : null,
					Location = location,
					MapLocation = WorldToMap(location),
					Icon = poiDatabase.RespawnIcon
				};

				poiDatabase.RespawnPoints.Add(poi);
			}
		}

		private void ProcessTablets(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> tabletObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {tabletObjects.Count} tablets...");
			foreach (FObjectExport tabletObject in tabletObjects)
			{
				if (!poiDatabase.Tablets.TryGetValue(tabletObject.ClassName, out MapPoi? poi))
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
				poi.MapLocation = WorldToMap(poi.Location.Value);
			}
		}

		private void ProcessSpawners(MapPoiDatabase poiDatabase, IReadOnlyList<ObjectWithDefaults> spawnerObjects, IReadOnlyList<FObjectExport> barracksObjects, Logger logger)
		{
			// Process barracks

			HashSet<string> barracksSpawnerNames = new();
			foreach (FObjectExport barracksObject in barracksObjects)
			{
				foreach (FPropertyTag property in barracksObject.ExportObject.Value.Properties)
				{
					switch (property.Name.Text)
					{
						case "SGQArray":
							{
								UScriptArray sgqArray = property.Tag!.GetValue<UScriptArray>()!;
								foreach (FPropertyTagType item in sgqArray.Properties)
								{
									barracksSpawnerNames.Add(item.GetValue<FPackageIndex>()!.Name);
								}
							}
							break;
						case "AssocJingBaoQiList":
							{
								UScriptArray sirenList = property.Tag!.GetValue<UScriptArray>()!;
								foreach (FPropertyTagType item in sirenList.Properties)
								{
									UObject? siren = item.GetValue<FPackageIndex>()!.Load();
									if (siren is null) continue;

									FPropertyTag? sirenSpawnerProp = siren.Properties.FirstOrDefault(p => p.Name.Text.Equals("WaiYuanShuaiGuaiQi"));
									if (sirenSpawnerProp is null) continue;

									barracksSpawnerNames.Add(sirenSpawnerProp.Tag!.GetValue<FPackageIndex>()!.Name);
								}
							}
							break;
					}
				}
			}

			// Process spawners

			Dictionary<string, SpawnData?> spawnDataCache = new();

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

				if (barracksSpawnerNames.Contains(export.ObjectName.Text))
				{
					spawnInterval = -1.0f;
				}

				string spawnDataKey = string.Join(',', scgClasses.Select(c => c.Name));
				SpawnData? spawnData = null;
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
					layerInfo = poiDatabase.SpawnLayerMap[layerType];
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
						case NpcCategory.Turkey:
						case NpcCategory.Capybara:
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

				string? equipment = null;
				if (spawnData.EquipmentClasses is not null || spawnData.WeaponClasses is not null)
				{
					StringBuilder equipBuilder = new("{");

					if (spawnData.EquipmentClasses is not null)
					{
						foreach (var pair in spawnData.EquipmentClasses)
						{
							equipBuilder.Append($"\"{pair.Key}\":\"{pair.Value}\",");
						}
					}

					if (spawnData.WeaponClasses is not null)
					{
						foreach (var pair in spawnData.WeaponClasses)
						{
							equipBuilder.Append($"\"{pair.Key}\":\"{pair.Value}\",");
						}
					}

					if (equipBuilder.Length > 1)
					{
						equipBuilder.Length -= 1; // Remove trailing comma
					}
					equipBuilder.Append("}");

					equipment = equipBuilder.ToString();
				}

				string? lootId = null;
				string? lootMap = null;
				string? collectMap = null;

				void applyAnimalLoot(bool onlyBabies)
				{
					string firstClass = firstNpc.CharacterClass.Name;
					bool isMultiAnimal = false;

					Dictionary<string, CollectionData> collectionMap = new();
					foreach (NpcData npc in spawnData.NpcData.Select(d => d.Value))
					{
						if (!firstClass.Equals(npc.CharacterClass.Name))
						{
							isMultiAnimal = true;
						}

						if (collectionMap.ContainsKey(npc.CharacterClass.Name)) continue;

						BlueprintHeirarchy.SearchInheritance(npc.CharacterClass, (current) =>
						{
							if (poiDatabase.Loot.CollectionMap.TryGetValue(current.Name, out CollectionData collectionData))
							{
								collectionMap.Add(npc.CharacterClass.Name, collectionData);
								return true;
							}
							return false;
						});
					}

					if (collectionMap.Count > 0)
					{
						StringBuilder collectMapBuilder = new("[");
						foreach (var pair in collectionMap)
						{
							collectMapBuilder.Append("{");

							NpcData npc = spawnData.NpcData.First(wv => wv.Value.CharacterClass.Name.Equals(pair.Key)).Value;
							
							if (isMultiAnimal)
							{
								collectMapBuilder.Append($"\"name\":\"{npc.Name}\",");
							}

							if (npc.IsBaby && !spawnData.IsMixedAge || onlyBabies)
							{
								if (pair.Value.Baby is not null) collectMapBuilder.Append($"\"base\":\"{pair.Value.Baby}\",");
								else if (pair.Value.Hit is not null) collectMapBuilder.Append($"\"base\":\"{pair.Value.Hit}\",");
							}
							else
							{
								if (pair.Value.Hit is not null) collectMapBuilder.Append($"\"base\":\"{pair.Value.Hit}\",");
								if (pair.Value.FinalHit is not null) collectMapBuilder.Append($"\"bonus\":\"{pair.Value.FinalHit}\",");
							}
							collectMapBuilder.Append($"\"amount\":{pair.Value.Amount}");
							collectMapBuilder.Append("},");
						}
						if (collectMapBuilder.Length > 1)
						{
							collectMapBuilder.Length -= 1; // Remove trailing comma
						}
						collectMapBuilder.Append("]");

						collectMap = collectMapBuilder.ToString();
					}
				}

				if (group == SpawnLayerGroup.Animal || group == SpawnLayerGroup.BabyAnimal)
				{
					applyAnimalLoot(false);
				}
				else
				{
					lootId = firstNpc.SpawnerLoot ?? firstNpc.CharacterLoot;
					lootMap = LootMapToString(spawnData, lootId);
					if (lootMap is not null) lootId = null;
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
					Equipment = equipment,
					SpawnCount = spawnData.SpawnCount,
					SpawnInterval = spawnInterval.Value,
					Location = location,
					MapLocation = WorldToMap(location),
					Icon = layerInfo.Icon,
					LootId = lootId,
					LootMap = lootMap,
					CollectMap = collectMap
				};

				poiDatabase.Spawners.Add(poi);

				if (spawnData.IsMixedAge)
				{
					WeightedValue<NpcData>[] babyData = spawnData.NpcData.Where(d => d.Value.IsBaby).ToArray();

					layerType = SpawnMinerUtil.GetNpcCategory(babyData[0].Value);
					applyLayerTypeAndSex(true);

					SpawnMinerUtil.CalculateLevels(babyData, false, out int minLevel, out int maxLevel);

					levelText = (minLevel == maxLevel) ? minLevel.ToString() : $"{minLevel} - {maxLevel}";

					collectMap = null;
					applyAnimalLoot(true);

					poi = new(poi)
					{
						GroupIndex = group,
						Type = type,
						Name = layerInfo.Name,
						Description = $"Level {levelText}",
						Male = male,
						Female = female,
						SpawnCount = babyData.Sum(b => b.Value.SpawnCount),
						Icon = layerInfo.Icon,
						CollectMap = collectMap
					};

					poiDatabase.Spawners.Add(poi);
				}
			}
		}

		private void ProcessChests(MapPoiDatabase poiDatabase, IReadOnlyList<ObjectWithDefaults> chestObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {chestObjects.Count} chests...");

			foreach (ObjectWithDefaults chestObject in chestObjects)
			{
				FObjectExport export = chestObject.Export;
				UObject obj = export.ExportObject.Value;

				int respawnTime = -1;
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
							case "ShuaXinTime":
								if (respawnTime < 0)
								{
									respawnTime = property.Tag!.GetValue<int>();
								}
								break;
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
				if ((respawnTime < 0 || lootId is null || poiName is null || openTip is null || rootComponent is null) && obj.Class is UBlueprintGeneratedClass objClass)
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
					Extra = openTip,
					Icon = poiDatabase.LootIcon,
					Location = location,
					MapLocation = WorldToMap(location),
					LootId = lootId,
					LootItem = lootItem?.Name,
					SpawnInterval = respawnTime > 0 ? respawnTime : 0
				};

				poiDatabase.Lootables.Add(poi);
			}
		}

		private void ProcessFoliage(MapPoiDatabase poiDatabase, IReadOnlyDictionary<EProficiency, IReadOnlyDictionary<string, FoliageData>> foliageData, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {foliageData.Count} ore clusters...");

			foreach (var map in foliageData)
			{
				// Max = hand, CaiKuang = mining
				if (map.Key != EProficiency.Max && map.Key != EProficiency.CaiKuang) continue;

				bool isOre = map.Key == EProficiency.CaiKuang;

				foreach (var pair in map.Value)
				{
					FoliageData foliage = pair.Value;
					if (foliage.Locations is null) continue;

					string collectMap;
					{
						StringBuilder collectMapBuilder = new("[{");
						if (foliage.HitLootName is not null) collectMapBuilder.Append($"\"base\":\"{foliage.HitLootName}\",");
						if (foliage.FinalHitLootName is not null) collectMapBuilder.Append($"\"bonus\":\"{foliage.FinalHitLootName}\",");
						collectMapBuilder.Append($"\"amount\":{foliage.Amount}");
						collectMapBuilder.Append("}]");
						collectMap = collectMapBuilder.ToString();
					}

					string? toolClass = foliage.SuggestedToolClass;
					float spawnInterval = foliage.RespawnTime;

					foreach (Cluster location in foliage.Locations)
					{
						string nameText = isOre
							? (location.Count == 1 ? $"{location.Count} deposit" : $"{location.Count} deposits")
							: (location.Count == 1 ? $"Collectible object" : $"{location.Count} objects");

						MapPoi poi = new()
						{
							GroupIndex = isOre ? SpawnLayerGroup.Ore : SpawnLayerGroup.Pickup,
							Type = foliage.Name,
							Title = foliage.Name,
							Name = nameText,
							Description = toolClass,
							SpawnCount = location.Count,
							SpawnInterval = spawnInterval,
							CollectMap = collectMap,
							MapLocation = WorldToMap(new(location.CenterX, location.CenterY, 0.0f)),
							MapRadius = sMapData.WorldToImage(location.CalculateRadius()),
							Icon = foliage.Icon
						};

						if (!isOre)
						{
							poi.Location = new FVector(location.CenterX, location.CenterY, location.CenterZ);
						}

						poiDatabase.Ores.Add(poi);
					}
				}
			}
		}

		private void ProcessDungeons(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> dungeonObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, $"Processing {dungeonObjects.Count} dungeons...");

			foreach (FObjectExport export in dungeonObjects)
			{
				USceneComponent? rootComponent = export.ExportObject.Value.Properties.FirstOrDefault(p => p.Name.Text.Equals("RootComponent"))?.Tag?.GetValue<FPackageIndex>()?.Load<USceneComponent>();
				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Log(LogLevel.Warning, $"Failed to locate dungeon entrance {export.ObjectName}");
					continue;
				}

				DungeonData dungeonData = poiDatabase.DungeonMap[export.ClassName];
				FVector location = locationProperty.Tag!.GetValue<FVector>();

				foreach (MapPoi dungeonPoi in poiDatabase.DungeonPois)
				{
					FVector v = location - dungeonPoi.Location!.Value;
					if (v.SizeSquared() < 400000000.0f) // 200 meters
					{
						StringBuilder builder = new("{");

						builder.Append($"\"title\":\"{dungeonData.Title}\"");
						builder.Append($",\"desc\":\"{dungeonData.Description}\"");
						builder.Append($",\"level\":{dungeonData.Level}");
						builder.Append($",\"count\":{dungeonData.MaxCount}");
						builder.Append($",\"time\":{dungeonData.MaxTimeSeconds}");
						builder.Append($",\"players\":{dungeonData.MaxPlayers}");
						builder.Append($",\"retry\":{dungeonData.MaxRetryTimes}");

						builder.Append(",\"items\":[");
						if (dungeonData.EntranceItemCost.Count > 0)
						{
							foreach (RecipeComponent item in dungeonData.EntranceItemCost)
							{
								builder.Append($"{{\"i\":\"{item.ItemClass}\",\"c\":{item.Count}}},");
							}
							builder.Length -= 1; // Remove trailing comma
						}
						builder.Append("]");

						builder.Append($",\"mask\":{dungeonData.EntranceMaskEnergyCost}");

						builder.Append(",\"spawns\":[");
						if (dungeonData.Spawners.Count > 0)
						{
							foreach (SpawnData spawner in dungeonData.Spawners)
							{
								NpcData firstNpc = spawner.NpcData.First().Value;
								NpcCategory category = SpawnMinerUtil.GetNpcCategory(firstNpc);
								if (category != NpcCategory.Mechanical)
								{
									logger.Log(LogLevel.Warning, $"Unhandled NPC type {category}");
									continue;
								}

								string levelText = (spawner.MinLevel == spawner.MaxLevel) ? spawner.MinLevel.ToString() : $"{spawner.MinLevel} - {spawner.MaxLevel}";

								Dictionary<string, List<NpcData>> lootMap = new();
								foreach (var npc in spawner.NpcData)
								{
									string lootId = npc.Value.SpawnerLoot ?? npc.Value.CharacterLoot!;
									if (!lootMap.TryGetValue(lootId, out List<NpcData>? list))
									{
										list = new();
										lootMap.Add(lootId, list);
									}	
									list.Add(npc.Value);
								}

								foreach (var pair in lootMap)
								{
									builder.Append("{");
									builder.Append($"\"name\":\"{string.Join(", ", pair.Value.Select(npc => npc.Name))}\"");
									builder.Append($",\"level\":\"{levelText}\"");
									builder.Append($",\"loot\":\"{pair.Key}\"");
									builder.Append("},");
								}
							}
							builder.Length -= 1; // Remove trailing comma
						}
						builder.Append("]");

						builder.Append(",\"chests\":[");
						if (dungeonData.Chests.Count > 0)
						{
							foreach (DungeonChestData chest in dungeonData.Chests)
							{
								builder.Append("{");
								builder.Append($"\"name\":\"{chest.ChestName}\"");
								builder.Append($",\"loot\":{(chest.LootId is null ? "null" : $"\"{chest.LootId}\"")}");
								builder.Append("},");
							}
							builder.Length -= 1; // Remove trailing comma
						}
						builder.Append("]");

						builder.Append("}");

						dungeonPoi.DungeonInfo = builder.ToString();
						break;
					}
				}

				Package themePackage = (Package)dungeonData.ThemeAsset.Load().Owner!;
				foreach (FObjectExport themeExport in themePackage.ExportMap)
				{
					if (poiDatabase.Tablets.TryGetValue(themeExport.ClassName, out MapPoi? tabletPoi))
					{
						tabletPoi.InDungeon = true;
						tabletPoi.MapLocation = WorldToMap(location);
					}
				}
			}
		}

		private void ProcessWorldBosses(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> gameFunctionObjects, Logger logger)
		{
			logger.Log(LogLevel.Information, "Processing world bosses...");

			foreach (FObjectExport export in gameFunctionObjects)
			{
				UObject obj = export.ExportObject.Value;

				UBlueprintGeneratedClass? objClass = export.ClassIndex.Load<UBlueprintGeneratedClass>();
				UObject? defaultsObj = objClass?.ClassDefaultObject.Load();
				if (defaultsObj is null) continue;

				UScriptMap? functionMap = defaultsObj.Properties.FirstOrDefault(p => p.Name.Text.Equals("GameFunctionExecutionMap"))?.Tag?.GetValue<UScriptMap>();
				if (functionMap is null || functionMap.Properties.Count == 0) continue;

				string? bossName = null;
				List<BossData> bosses = new();
				foreach (var pair in functionMap.Properties)
				{
					EJianZhuGameFunctionType funcType = EJianZhuGameFunctionType.EJZGFT_NOT_DEFINE;
					FPackageIndex? npcIndex = null;

					UScriptArray? execList = pair.Value?.GetValue<FStructFallback>()?.Properties[0].Tag?.GetValue<UScriptArray>();
					if (execList is null || execList.Properties.Count == 0) continue;

					FStructFallback execFunc = execList.Properties.First().GetValue<FStructFallback>()!;
					foreach (FPropertyTag property in execFunc.Properties)
					{
						switch (property.Name.Text)
						{
							case "FunctionType":
								if (GameUtil.TryParseEnum(property, out EJianZhuGameFunctionType value))
								{
									funcType = value;
								}
								break;
							case "ExecuteActorClass":
								npcIndex = property.Tag?.GetValue<FPackageIndex>();
								break;
						}
					}

					if (funcType != EJianZhuGameFunctionType.EJZGFT_SUMMON_NPC)
					{
						continue;
					}

					if (npcIndex is null)
					{
						logger.Log(LogLevel.Warning, $"Boss summon function in class {export.ObjectName} is missing an NPC class.");
						continue;
					}

					UBlueprintGeneratedClass npcClass = npcIndex.Load<UBlueprintGeneratedClass>()!;

					string? npcName = null;
					List<FPackageIndex> growthComponentIndices = new();
					BlueprintHeirarchy.SearchInheritance(npcClass, (current =>
					{
						UObject? npcObj = current?.ClassDefaultObject.Load();
						if (npcObj is null)
						{
							return false;
						}

						foreach (FPropertyTag property in npcObj.Properties)
						{
							switch (property.Name.Text)
							{
								case "MoRenMingZi":
									if (npcName is null)
									{
										npcName = GameUtil.ReadTextProperty(property);
									}
									break;
								case "ChengZhangComponent":
									{
										FPackageIndex? growthComponentIndex = property.Tag?.GetValue<FPackageIndex>();
										if (growthComponentIndex is not null)
										{
											growthComponentIndices.Add(growthComponentIndex);
										}
									}
									break;
							}
						}

						return false;
					}));

					if (npcName is null)
					{
						logger.Log(LogLevel.Warning, $"Boss defined by class {npcClass.Name} is missing a name.");
						continue;
					}

					if (growthComponentIndices.Count == 0)
					{
						logger.Log(LogLevel.Warning, $"Boss defined by class {npcClass.Name} is missing a growth component.");
						continue;
					}

					if (bossName is null)
					{
						bossName = npcName.Substring(npcName.IndexOf(" ") + 1);
					}

					int level = 0;
					UDataTable? statTable = null;
					foreach (FPackageIndex growthComponentIndex in growthComponentIndices)
					{
						UObject growthComponent = growthComponentIndex.Load()!;

						foreach (FPropertyTag property in growthComponent.Properties)
						{
							switch (property.Name.Text)
							{
								case "AttrMetaDataDT":
									if (statTable is null)
									{
										statTable = property.Tag?.GetValue<FPackageIndex>()?.Load<UDataTable>();
									}
									break;
								case "NeedLevel":
									if (level == 0)
									{
										level = property.Tag!.GetValue<int>();
									}
									break;
							}
						}
					}

					if (level == 0 || statTable is null)
					{
						logger.Log(LogLevel.Warning, $"Boss defined by class {npcClass.Name} is missing growth data.");
						continue;
					}

					int maxHealth = (int)statTable.RowMap.FirstOrDefault(r => r.Key.Text.Equals("HSuperCommonSet.MaxHealth")).Value.Properties.FirstOrDefault(p => p.Name.Text.Equals("BaseValue"))!.Tag!.GetValue<float>();

					UBlueprintGeneratedClass recipeClass = pair.Key.GetValue<FPackageIndex>()!.Load<UBlueprintGeneratedClass>()!;
					UObject recipeObj = recipeClass.ClassDefaultObject.Load()!;

					UTexture2D? recipeIcon = null;
					int requiredLevel = 0;
					UScriptArray? recipeItemArray = null;
					int maskEnergyCost = 0;
					foreach (FPropertyTag property in recipeObj.Properties)
					{
						switch (property.Name.Text)
						{
							case "PeiFangIcon":
								recipeIcon = GameUtil.ReadTextureProperty(property);
								break;
							case "PeiFangDengJi":
								requiredLevel = property.Tag!.GetValue<int>();
								break;
							case "DemandDaoJu":
								recipeItemArray = property.Tag?.GetValue<UScriptArray>();
								break;
							case "DemandMianJuNengLiang":
								maskEnergyCost = property.Tag!.GetValue<int>();
								break;
						}
					}

					List<RecipeComponent> recipeItems = new();

					if (recipeItemArray is not null)
					{
						foreach (FPropertyTagType item in recipeItemArray.Properties)
						{
							UScriptArray? itemsArray = null;
							int count = 0;

							FStructFallback itemObj = item.GetValue<FStructFallback>()!;
							foreach (FPropertyTag property in itemObj.Properties)
							{
								switch (property.Name.Text)
								{
									case "DemandDaoJu":
										itemsArray = property.Tag?.GetValue<UScriptArray>();
										break;
									case "DemandCount":
										count = property.Tag!.GetValue<int>();
										break;
								}
							}

							if (itemsArray is null || itemsArray.Properties.Count == 0) continue;
							if (count == 0) continue;

							foreach (FPropertyTagType componentItem in itemsArray.Properties)
							{
								recipeItems.Add(new() { ItemClass = componentItem.GetValue<FPackageIndex>()!.Name, Count = count });
							}
						}
					}

					string summonRecipe = "{}";
					if (recipeItems.Count > 0 || maskEnergyCost > 0)
					{
						StringBuilder builder = new("{");

						builder.Append("\"items\":[");
						foreach (RecipeComponent item in recipeItems)
						{
							builder.Append($"{{\"i\":\"{item.ItemClass}\",\"c\":{item.Count}}},");
						}
						builder.Length -= 1; // Remove trailing comma
						builder.Append("]");

						builder.Append($",\"mask\":{maskEnergyCost}");

						builder.Append("}");

						summonRecipe = builder.ToString();
					}

					string loot = "{}";
					{
						CollectionData? collectionData = null;
						BlueprintHeirarchy.SearchInheritance(npcClass, (current) =>
						{
							if (poiDatabase.Loot.CollectionMap.TryGetValue(current.Name, out CollectionData value))
							{
								collectionData = value;
								return true;
							}
							return false;
						});

						if (collectionData.HasValue)
						{
							StringBuilder collectMapBuilder = new("{");

							if (collectionData.Value.Hit is not null) collectMapBuilder.Append($"\"base\":\"{collectionData.Value.Hit}\",");
							if (collectionData.Value.FinalHit is not null) collectMapBuilder.Append($"\"bonus\":\"{collectionData.Value.FinalHit}\",");

							collectMapBuilder.Append($"\"amount\":{collectionData.Value.Amount}");
							collectMapBuilder.Append("}");

							loot = collectMapBuilder.ToString();
						}
					}

					bosses.Add(new() { Name = npcName, Level = level, MaxHealth = maxHealth, SummonRecipe = summonRecipe, Loot = loot, Icon = recipeIcon });
					
					if (recipeIcon is not null) poiDatabase.AdditionalIconsToExport.Add(recipeIcon);
				}

				if (bosses.Count == 0) continue;

				UObject? rootComponent = obj.Properties.FirstOrDefault(p => p.Name.Text.Equals("RootComponent"))?.Tag?.GetValue<FPackageIndex>()?.Load();
				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Log(LogLevel.Warning, $"Failed to find location for world boss: {bossName}");
					continue;
				}
				FVector location = locationProperty.Tag!.GetValue<FVector>();

				string bossData;
				{
					StringBuilder builder = new("[");
					foreach (BossData boss in bosses)
					{
						builder.Append("{");
						builder.Append($"\"name\":\"{boss.Name}\"");
						builder.Append($",\"level\":{boss.Level}");
						builder.Append($",\"health\":{boss.MaxHealth}");
						builder.Append($",\"icon\":{(boss.Icon?.Name is null ? "null" : $"\"{boss.Icon.Name}\"")}");
						builder.Append($",\"summon\":{boss.SummonRecipe}");
						builder.Append($",\"loot\":{boss.Loot}");
						builder.Append("},");
					}
					builder.Length -= 1; // Remove trailing comma
					builder.Append("]");

					bossData = builder.ToString();
				}

				MapPoi poi = new()
				{
					GroupIndex = SpawnLayerGroup.PointOfInterest,
					Type = "World Boss",
					Title = bossName,
					Name = "World boss summoning altar",
					BossInfo = bossData,
					Location = location,
					MapLocation = WorldToMap(location),
					Icon = poiDatabase.BossIcon
				};

				poiDatabase.WorldBosses.Add(poi);
			}
		}

		private struct BossData
		{
			public string Name;
			public int Level;
			public int MaxHealth;
			public string SummonRecipe;
			public string Loot;
			public UTexture2D? Icon;
		}

		private void FindPoiTextures(MapPoiDatabase poiDatabase, IReadOnlyDictionary<ETanSuoDianType, UTexture2D> mapIcons, Logger logger)
		{
			foreach (var pair in poiDatabase.TypeLookup)
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

		private void WriteIcons(MapInfo mapData, Config config, Logger logger)
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

			foreach (UTexture2D icon in mapData.AdditionalMapIcons)
			{
				if (exported.Add(icon.Name))
				{
					TextureExporter.ExportTexture(icon, false, logger, outDir);
				}
			}
		}

		private void WriteCsv(MapInfo mapData, Config config, Logger logger)
		{
			string valOrNull(float value)
			{
				return value == 0.0f ? "" : value.ToString();
			}

			foreach (var pair in mapData.POIs)
			{
				string outPath = Path.Combine(config.OutputDirectory, Name, $"{pair.Key}.csv");
				using FileStream outFile = IOUtil.CreateFile(outPath, logger);
				using StreamWriter writer = new(outFile, Encoding.UTF8);

				writer.WriteLine("gpIdx,gpName,key,type,posX,posY,posZ,mapX,mapY,mapR,title,name,desc,extra,m,f,stat,occ,num,intr,loot,lootitem,lootmap,equipmap,collectmap,unlocks,icon,ach,achDesc,achIcon,inDun,dunInfo,bossInfo");

				foreach (MapPoi poi in pair.Value)
				{
					string spawnerSegment = ",,,,,,,,,,";
					string poiSegment = ",,";
					if (poi.GroupIndex == SpawnLayerGroup.PointOfInterest)
					{
						poiSegment = $"{CsvStr(poi.Achievement?.Name)},{CsvStr(poi.Achievement?.Description)},{CsvStr(poi.Achievement?.Icon?.Name)}";
					}
					else
					{
						spawnerSegment = $"{poi.Male},{poi.Female},{CsvStr(poi.TribeStatus)},{CsvStr(poi.Occupation)},{poi.SpawnCount},{poi.SpawnInterval},{CsvStr(poi.LootId)},{CsvStr(poi.LootItem)},{CsvStr(poi.LootMap)},{CsvStr(poi.Equipment)},{CsvStr(poi.CollectMap)}";
					}

					string posSegment = "null, null, null";
					if (poi.Location.HasValue)
					{
						posSegment = $"{poi.Location.Value.X:0},{poi.Location.Value.Y:0},{poi.Location.Value.Z:0}";
					}

					writer.WriteLine(
						$"{(int)poi.GroupIndex},{CsvStr(GetGroupName(poi.GroupIndex))},{poi.Key},{CsvStr(poi.Type)},{posSegment},{poi.MapLocation.X:0},{poi.MapLocation.Y:0},{valOrNull(poi.MapRadius)},{CsvStr(poi.Title)},{CsvStr(poi.Name)},{CsvStr(poi.Description)},{CsvStr(poi.Extra)}," +
						$"{spawnerSegment},{CsvStr(poi.Unlocks)},{CsvStr(poi.Icon?.Name)},{poiSegment},{poi.InDungeon},{CsvStr(poi.DungeonInfo)},{CsvStr(poi.BossInfo)}");
				}
			}
		}

		private void WriteSql(MapInfo mapData, ISqlWriter sqlWriter, Logger logger)
		{
			// Schema
			// create table `poi` (
			//   `gpIdx` int not null,
			//   `gpName` varchar(63) not null,
			//   `key` int,
			//   `type` varchar(63) not null,
			//   `posX` float,
			//   `posY` float,
			//   `posZ` float,
			//   `mapX` int not null,
			//   `mapY` int not null,
			//   `mapX2` int,
			//   `mapY2` int,
			//   `title` varchar(127),
			//   `name` varchar(127),
			//   `desc` varchar(511),
			//   `extra` varchar(511),
			//   `m` bool,
			//   `f` bool,
			//   `stat` varchar(63),
			//   `occ` varchar(127),
			//   `num` int,
			//   `intr` float,
			//   `loot` varchar(127),
			//   `lootitem` varchar(127),
			//   `lootmap` varchar(255),
			//   `equipmap` varchar(2047),
			//   `collectmap` varchar(511),
			//   `unlocks` varchar(255),
			//   `icon` varchar(127),
			//   `ach` varchar(127),
			//   `achDesc` varchar(255),
			//   `achIcon` varchar(127),
			//   `inDun` bool,
			//   `dunInfo` varchar(1535),
			//   `bossInfo` varchar(1535)
			// )

			string valOrNull(float value)
			{
				return value == 0.0f ? "null" : value.ToString();
			}

			sqlWriter.WriteStartTable("poi");

			foreach (var pair in mapData.POIs)
			{
				foreach (MapPoi poi in pair.Value)
				{
					// This is because some ancient tablets come from dungeons or pyramids instead of spawning in the world.
					if (poi.Location == FVector.ZeroVector) continue;

					string spawnerSegment = "null, null, null, null, null, null, null, null, null, null, null";
					string poiSegment = "null, null, null";
					if (poi.GroupIndex == SpawnLayerGroup.PointOfInterest)
					{
						poiSegment = $"{DbStr(poi.Achievement?.Name)}, {DbStr(poi.Achievement?.Description)}, {DbStr(poi.Achievement?.Icon?.Name)}";
					}
					else
					{
						spawnerSegment = $"{DbBool(poi.Male)}, {DbBool(poi.Female)}, {DbStr(poi.TribeStatus)}, {DbStr(poi.Occupation)}, {poi.SpawnCount}, {poi.SpawnInterval}, {DbStr(poi.LootId)}, {DbStr(poi.LootItem)}, {DbStr(poi.LootMap)}, {DbStr(poi.Equipment)}, {DbStr(poi.CollectMap)}";
					}

					string posSegment = "null, null, null";
					if (poi.Location.HasValue)
					{
						posSegment = $"{poi.Location.Value.X:0}, {poi.Location.Value.Y:0}, {poi.Location.Value.Z:0}";
					}

					sqlWriter.WriteRow(
						$"{(int)poi.GroupIndex}, {DbStr(GetGroupName(poi.GroupIndex))}, {DbVal(poi.Key)}, {DbStr(poi.Type)}, {posSegment}, {poi.MapLocation.X:0}, {poi.MapLocation.Y:0}, {valOrNull(poi.MapRadius)}, {DbStr(poi.Title)}, {DbStr(poi.Name)}, {DbStr(poi.Description)}, {DbStr(poi.Extra)}, " +
						$"{spawnerSegment}, {DbStr(poi.Unlocks)}, {DbStr(poi.Icon?.Name)}, {poiSegment}, {DbBool(poi.InDungeon)}, {DbStr(poi.DungeonInfo)}, {DbStr(poi.BossInfo)}");
				}
			}

			sqlWriter.WriteEndTable();
		}

		private static FVector2D WorldToMap(FVector world)
		{
			return sMapData.WorldToImage(world);
		}

		private string? LootMapToString(SpawnData spawner, string? firstLootId)
		{
			if (spawner.NpcData.Skip(1).Any(d => (d.Value.SpawnerLoot ?? d.Value.CharacterLoot) != firstLootId))
			{
				// Multiple loot tables referenced
				StringBuilder lootMapBuilder = new("{");
				foreach (NpcData npc in spawner.NpcData.Select(d => d.Value))
				{
					string? loot = npc.SpawnerLoot ?? npc.CharacterLoot;
					lootMapBuilder.Append($"\"{npc.Name}\": \"{loot}\",");
				}
				lootMapBuilder.Length -= 1; // Remove trailing comma
				lootMapBuilder.Append("}");

				return lootMapBuilder.ToString();
			}
			return null;
		}

		private class MapInfo
		{
			public IReadOnlyDictionary<string, List<MapPoi>> POIs { get; }

			public IReadOnlyList<UTexture2D> AdditionalMapIcons { get; }

			public MapInfo(IReadOnlyDictionary<string, List<MapPoi>> pois, IReadOnlyList<UTexture2D> additionalMapIcons)
			{
				POIs = pois;
				AdditionalMapIcons = additionalMapIcons;
			}
		}

		private class MapPoiDatabase
		{
			public LootDatabase Loot { get; }

			public IDictionary<int, MapPoi> IndexLookup { get; }

			public IDictionary<ETanSuoDianType, List<MapPoi>> TypeLookup { get; }

			public IList<MapPoi> DungeonPois { get; }

			public IDictionary<string, MapPoi> Tablets { get; }

			public IList<MapPoi> RespawnPoints { get; }

			public IDictionary<NpcCategory, SpawnLayerInfo> SpawnLayerMap { get; }

			public IReadOnlyDictionary<string, DungeonData> DungeonMap { get; set; }

			public IList<MapPoi> Spawners { get; }

			public IList<MapPoi> Lootables { get; }

			public IList<MapPoi> Ores { get; }

			public IList<MapPoi> WorldBosses { get; }

			// These are references to main POIs, not their own unique instances
			public IList<MapPoi> Dungeons { get; }

			public UTexture2D RespawnIcon { get; set; }

			public UTexture2D LootIcon { get; set; }

			public UTexture2D BossIcon { get; set; }

			public ISet<UTexture2D> AdditionalIconsToExport { get; }

			public MapPoiDatabase(LootDatabase loot)
			{
				Loot = loot;
				IndexLookup = new Dictionary<int, MapPoi>();
				TypeLookup = new Dictionary<ETanSuoDianType, List<MapPoi>>();
				DungeonPois = new List<MapPoi>();
				Tablets = new Dictionary<string, MapPoi>();
				RespawnPoints = new List<MapPoi>();
				SpawnLayerMap = new Dictionary<NpcCategory, SpawnLayerInfo>((int)NpcCategory.Count);
				DungeonMap = null!;
				Spawners = new List<MapPoi>();
				Lootables = new List<MapPoi>();
				Ores = new List<MapPoi>();
				WorldBosses = new List<MapPoi>();
				Dungeons = new List<MapPoi>();
				RespawnIcon = null!;
				LootIcon = null!;
				BossIcon = null!;
				AdditionalIconsToExport = new HashSet<UTexture2D>();
			}

			public IReadOnlyDictionary<string, List<MapPoi>> GetAllPois()
			{
				Dictionary<string, List<MapPoi>> result = new();

				foreach (MapPoi poi in 
					IndexLookup.Values
					.Concat(Tablets.Values)
					.Concat(RespawnPoints)
					.Concat(Spawners)
					.Concat(Lootables)
					.Concat(Ores)
					.Concat(WorldBosses))
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
			public int? Key { get; set; }
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
			public string? Equipment { get; set; }
			public int SpawnCount { get; set; }
			public float SpawnInterval { get; set; }
			public string? LootId { get; set; }
			public string? LootItem { get; set; }
			public string? LootMap { get; set; }
			public string? CollectMap { get; set; }
			public string? Unlocks { get; set; }
			public FVector? Location { get; set; }
			public FVector2D MapLocation { get; set; }
			public float MapRadius { get; set; }
			public UTexture2D Icon { get; set; } = null!;
			public AchievementData? Achievement { get; set; }
			public bool InDungeon { get; set; }
			public string? DungeonInfo { get; set; }
			public string? BossInfo { get; set; }

			public MapPoi()
			{
			}

			public MapPoi(MapPoi other)
			{
				Key = other.Key;
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
				Equipment = other.Equipment;
				SpawnCount = other.SpawnCount;
				SpawnInterval = other.SpawnInterval;
				LootId = other.LootId;
				LootItem = other.LootItem;
				LootMap = other.LootMap;
				CollectMap = other.CollectMap;
				Unlocks = other.Unlocks;
				Location = other.Location;
				MapLocation = other.MapLocation;
				MapRadius = other.MapRadius;
				Icon = other.Icon;
				Achievement = other.Achievement;
				InDungeon = other.InDungeon;
				DungeonInfo = other.DungeonInfo;
				BossInfo = other.BossInfo;
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
			Chest,
			Pickup,
			Ore
		}

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
				SpawnLayerGroup.Pickup => "Collectible Objects",
				SpawnLayerGroup.Chest => "Lootable Objects",
				SpawnLayerGroup.Ore => "Ore Deposits",
				_ => ""
			};
		}
	}
}
