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

using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using SoulmaskDataMiner.Data;
using SoulmaskDataMiner.GameData;

namespace SoulmaskDataMiner.MapUtil
{
	/// <summary>
	/// Used by MapMiner to manage data for all points of interest for a map
	/// </summary>
	internal class MapPoiDatabase
	{
		public MapPoiStaticData StaticData { get; }

		public string MapName { get; }

		public IDictionary<int, MapPoi> IndexLookup { get; }

		public IDictionary<ETanSuoDianType, List<MapPoi>> TypeLookup { get; }

		public IList<MapPoi> DungeonPois { get; }

		public IList<MapPoi> ArenaPois { get; }

		public IDictionary<string, MapPoi> Tablets { get; }

		public IList<MapPoi> RespawnPoints { get; }

		public IReadOnlyDictionary<string, DungeonData> DungeonMap { get; set; }

		public IList<MapPoi> Spawners { get; }

		public IList<MapPoi> Lootables { get; }

		public IList<MapPoi> Ores { get; }

		public IList<MapPoi> WorldBosses { get; }

		public IList<MapPoi> MinePlatforms { get; }

		public IList<MapPoi> MineralVeins { get; }

		// These are references to main POIs, not their own unique instances
		public IList<MapPoi> Dungeons { get; }

		public ISet<UTexture2D> AdditionalIconsToExport { get; }

		public MapPoiDatabase(MapPoiStaticData staticData, string mapName)
		{
			StaticData = staticData;
			MapName = mapName;
			IndexLookup = new Dictionary<int, MapPoi>();
			TypeLookup = new Dictionary<ETanSuoDianType, List<MapPoi>>();
			DungeonPois = new List<MapPoi>();
			ArenaPois = new List<MapPoi>();
			Tablets = new Dictionary<string, MapPoi>();
			RespawnPoints = new List<MapPoi>();
			DungeonMap = null!;
			Spawners = new List<MapPoi>();
			Lootables = new List<MapPoi>();
			Ores = new List<MapPoi>();
			WorldBosses = new List<MapPoi>();
			MinePlatforms = new List<MapPoi>();
			MineralVeins = new List<MapPoi>();
			Dungeons = new List<MapPoi>();
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
				.Concat(WorldBosses)
				.Concat(MinePlatforms)
				.Concat(MineralVeins))
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

	/// <summary>
	/// Data related to map points of interest which is shared accross maps
	/// </summary>
	internal class MapPoiStaticData
	{
		public LootDatabase Loot { get; }

		public IReadOnlyDictionary<string, UObject> MapIntel { get; }

		public IReadOnlyDictionary<ETanSuoDianType, UTexture2D> MapIcons { get; }

		public IReadOnlyDictionary<NpcCategory, SpawnLayerInfo> SpawnLayerMap { get; }

		public UTexture2D RespawnIcon { get; set; }

		public UTexture2D LootIcon { get; set; }

		public UTexture2D BossIcon { get; set; }

		public UTexture2D MinePlatformIcon { get; set; }

		public UTexture2D EventIcon { get; set; }

		private MapPoiStaticData(
			LootDatabase loot,
			IReadOnlyDictionary<string, UObject> mapIntel,
			IReadOnlyDictionary<ETanSuoDianType, UTexture2D> mapIcons,
			IReadOnlyDictionary<NpcCategory, SpawnLayerInfo> spawnLayerMap,
			UTexture2D respawnIcon,
			UTexture2D lootIcon,
			UTexture2D bossIcon,
			UTexture2D minePlatformIcon,
			UTexture2D eventIcon)
		{
			Loot = loot;
			MapIntel = mapIntel;
			MapIcons = mapIcons;
			SpawnLayerMap = spawnLayerMap;
			RespawnIcon = respawnIcon;
			LootIcon = lootIcon;
			BossIcon = bossIcon;
			MinePlatformIcon = minePlatformIcon;
			EventIcon = eventIcon;
		}

		public static MapPoiStaticData? Build(IReadOnlyDictionary<string, string> mapNameToLevelPath, IProviderManager providerManager, Logger logger)
		{
			IReadOnlyDictionary<string, UObject>? mapIntel = LoadMapIntel(mapNameToLevelPath, providerManager, logger);
			if (mapIntel is null)
			{
				logger.Error("Failed to load map intel.");
				return null;
			}

			IReadOnlyDictionary<ETanSuoDianType, UTexture2D>? mapIcons = GetMapIcons(mapIntel, logger);
			if (mapIcons is null)
			{
				logger.Error("Failed to load map icons.");
				return null;
			}

			UTexture2D? lootIcon = DataUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/ChuShenTianFu/ChengHao/ChengHao_poxiangren.uasset", logger)!;
			if (lootIcon is null)
			{
				logger.Error("Failed to load loot icon.");
				return null;
			}

			UTexture2D? respawnIcon = DataUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/fuhuodian.uasset", logger)!;
			if (respawnIcon is null)
			{
				logger.Error("Failed to load respawn icon.");
				return null;
			}

			UTexture2D? bossIcon = DataUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/hud/dusuicon.uasset", logger)!;
			if (bossIcon is null)
			{
				logger.Error("Failed to load boss icon.");
				return null;
			}

			UTexture2D? minePlatformIcon = DataUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/MianJuJiNeng/xinban/kuangmaitance3.uasset", logger)!;
			if (minePlatformIcon is null)
			{
				logger.Error("Failed to load mine platform icon.");
				return null;
			}

			UTexture2D? eventIcon = DataUtil.LoadFirstTexture(providerManager.Provider, "WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/lueduo.uasset", logger)!;
			if (eventIcon is null)
			{
				logger.Error("Failed to load event icon.");
				return null;
			}

			IReadOnlyDictionary<NpcCategory, SpawnLayerInfo>? spawnLayers = LoadSpawnLayers(providerManager, logger);
			if (spawnLayers is null)
			{
				// LoadSpawnLayers prints its own error messages, so we don't need one here
				return null;
			}

			return new(providerManager.LootDatabase, mapIntel, mapIcons, spawnLayers, respawnIcon, lootIcon, bossIcon, minePlatformIcon, eventIcon);
		}

		private static IReadOnlyDictionary<string, UObject>? LoadMapIntel(IReadOnlyDictionary<string, string> mapNameToLevelPath, IProviderManager providerManager, Logger logger)
		{
			if (!providerManager.SingletonManager.ResourceManager.TryGetPropertyValue("MapQingBaoConfigClassMap", out UScriptMap? mapIntelClassMap))
			{
				logger.Error("Failed to read map intel class map");
				return null;
			}

			Dictionary<string, UObject> mapIntelMap = new();
			foreach (var pair in mapIntelClassMap.Properties)
			{
				string mapName = pair.Key.GetValue<string>()!;
				if (!mapNameToLevelPath.ContainsKey(mapName)) continue;

				FPackageIndex classIndex = pair.Value!.GetValue<FPackageIndex>()!;
				UBlueprintGeneratedClass mapIntelClass = (UBlueprintGeneratedClass)classIndex.ResolvedObject!.Load()!;
				mapIntelMap.Add(mapName, mapIntelClass.ClassDefaultObject.Load()!);
			}

			return mapIntelMap.Count > 0 ? mapIntelMap : null;
		}

		private static IReadOnlyDictionary<ETanSuoDianType, UTexture2D>? GetMapIcons(IReadOnlyDictionary<string, UObject> mapIntel, Logger logger)
		{
			Dictionary<ETanSuoDianType, UTexture2D> mapIcons = new();

			foreach (var intelPair in mapIntel)
			{
				foreach (FPropertyTag property in intelPair.Value.Properties)
				{
					if (!property.Name.Text.Equals("AllTanSuoDianIconMap")) continue;

					UScriptMap iconMap = property.Tag!.GetValue<FStructFallback>()!.Properties[0].Tag!.GetValue<UScriptMap>()!;
					foreach (var pair in iconMap.Properties)
					{
						ETanSuoDianType iconType;
						if (!DataUtil.TryParseEnum<ETanSuoDianType>(pair.Key.GetValue<FName>(), out iconType))
						{
							logger.Warning($"Unable to parse icon type {pair.Key.GetValue<FName>().Text}");
							continue;
						}

						UTexture2D? icon = DataUtil.ReadTextureProperty(pair.Value);
						if (icon is null)
						{
							logger.Warning($"Unable to load icon for type {iconType}");
							continue;
						}

						if (mapIcons.TryGetValue(iconType, out UTexture2D? existingIcon))
						{
							if (!icon.Name.Equals(existingIcon.Name))
							{
								logger.Warning($"Duplicate icon for type {iconType}. Existing: {existingIcon.Name}, New: {icon.Name}");
							}
							continue;
						}

						mapIcons.Add(iconType, icon);
					}
				}
			}

			return mapIcons.Count > 0 ? mapIcons : null;
		}

		private static IReadOnlyDictionary<NpcCategory, SpawnLayerInfo>? LoadSpawnLayers(IProviderManager providerManager, Logger logger)
		{
			string[] texturePaths = new string[]
			{
				"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji1.uasset", // Unknown
				"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji.uasset",  // Animal
				"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji2.uasset", // Mechanical
				"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji3.uasset", // Human
				"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji4.uasset", // Boat
				"WS/Content/UI/resource/JianYingIcon/DiTuBiaoJiIcon/shitubiaoji.uasset"   // Firefly
			};

			Dictionary<NpcCategory, SpawnLayerInfo> result = new();

			for (int i = 0; i < texturePaths.Length; ++i)
			{
				UTexture2D? icon = DataUtil.LoadFirstTexture(providerManager.Provider, texturePaths[i], logger);
				if (icon is null)
				{
					logger.Error("Failed to load spawner icon texture.");
					return null;
				}

				result[(NpcCategory)i] = new SpawnLayerInfo() { Name = ((NpcCategory)i).ToString(), Icon = icon };
			}

			UScriptMap? animalConfigMap = providerManager.SingletonManager.GameSingleton.Properties.FirstOrDefault(p => p.Name.Text.Equals("DongWuConfigExMap"))?.Tag?.GetValue<UScriptMap>();
			if (animalConfigMap is null)
			{
				logger.Error("Failed to load animal config map from game singleton");
				return null;
			}

			Dictionary<string, FPropertyTag> animalIconMap = new();
			foreach (var pair in animalConfigMap.Properties)
			{
				string className = pair.Key.GetValue<FSoftObjectPath>().AssetPathName.Text;
				className = className.Substring(className.LastIndexOf('.') + 1);
				FPropertyTag? iconProperty = pair.Value?.GetValue<FStructFallback>()?.Properties[0];
				if (className is null || iconProperty is null) continue;

				animalIconMap.Add(className, iconProperty);
			}

			UTexture2D? loadTexture(string className)
			{
				if (animalIconMap.TryGetValue(className, out FPropertyTag? iconProperty))
				{
					string? iconPath = iconProperty?.Tag?.GetValue<FPackageIndex>()?.ResolvedObject?.GetPathName();
					if (iconPath is null) return null;

					// Swap to the map marker version of the texture
					iconPath = iconPath.Replace("xunyang", "ditubiaoji");
					iconPath = iconPath.Substring(0, iconPath.LastIndexOf('.'));
					iconPath += ".uasset";

					return DataUtil.LoadFirstTexture(providerManager.Provider, iconPath, logger);
				}
				return null;
			}

			UTexture2D? alpacaIcon = loadTexture("BP_DongWu_YangTuo_C");
			UTexture2D? bisonIcon = loadTexture("BP_DongWu_YeNiu_C");
			UTexture2D? boarIcon = loadTexture("BP_DongWu_YeZhu_C");
			UTexture2D? camelIcon = loadTexture("BP_Monster_Dromedary_C");
			UTexture2D? capybaraIcon = loadTexture("BP_DongWu_ShuiTun_C");
			UTexture2D? chickenIcon = loadTexture("BP_Monster_Chicken_C");
			UTexture2D? donkeyIcon = loadTexture("BP_Monster_Ass_C");
			UTexture2D? eagleIcon = loadTexture("BP_DongWu_JiaoDiao_C");
			UTexture2D? elephantIcon = loadTexture("BP_DongWu_DaXiang_C");
			UTexture2D? flamingoIcon = loadTexture("BP_Monster_Flamingo_Egg_C");
			UTexture2D? giraffeIcon = loadTexture("BP_Monster_Giraffe_C");
			UTexture2D? hippopotamusIcon = loadTexture("BP_Monster_Hippopotamus_C");
			UTexture2D? jaguarIcon = loadTexture("BP_DongWu_XueBao_C");
			UTexture2D? leopardIcon = loadTexture("BP_DongWu_BaoZi_C");
			UTexture2D? lizardIcon = loadTexture("BP_DongWu_QiuYuXi_C");
			UTexture2D? llamaIcon = loadTexture("BP_DongWu_DaYangTuo_C");
			UTexture2D? longhornIcon = loadTexture("BP_Monster_SangaCattle_C");
			UTexture2D? mooseIcon = loadTexture("BP_DongWu_TuoLu_C");
			UTexture2D? ostrichIcon = loadTexture("BP_DongWu_TuoNiao_C");
			UTexture2D? rhinoIcon = loadTexture("BP_Monster_Rhinoceros_C");
			UTexture2D? tortoiseIcon = loadTexture("BP_DongWu_XiangGui_C");
			UTexture2D? turkeyIcon = loadTexture("BP_DongWu_HuoJi_C");
			if (alpacaIcon is null ||
				bisonIcon is null ||
				boarIcon is null ||
				camelIcon is null ||
				capybaraIcon is null ||
				chickenIcon is null ||
				donkeyIcon is null ||
				eagleIcon is null ||
				elephantIcon is null ||
				flamingoIcon is null ||
				giraffeIcon is null ||
				hippopotamusIcon is null ||
				jaguarIcon is null ||
				leopardIcon is null ||
				lizardIcon is null ||
				llamaIcon is null ||
				longhornIcon is null ||
				mooseIcon is null ||
				ostrichIcon is null ||
				rhinoIcon is null ||
				tortoiseIcon is null ||
				turkeyIcon is null)
			{
				logger.Error("Failed to load spawner icon texture.");
				return null;
			}

			const string babyAnimalSpawnName = "Baby Animal Spawn";
			result[NpcCategory.Alpaca] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = alpacaIcon };
			result[NpcCategory.Bison] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = bisonIcon };
			result[NpcCategory.Boar] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = boarIcon };
			result[NpcCategory.Camel] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = camelIcon };
			result[NpcCategory.Capybara] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = capybaraIcon };
			result[NpcCategory.Chicken] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = chickenIcon };
			result[NpcCategory.Donkey] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = donkeyIcon };
			result[NpcCategory.Eagle] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = eagleIcon };
			result[NpcCategory.Elephant] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = elephantIcon };
			result[NpcCategory.Flamingo] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = flamingoIcon };
			result[NpcCategory.Giraffe] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = giraffeIcon };
			result[NpcCategory.Hippopotamus] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = hippopotamusIcon };
			result[NpcCategory.Jaguar] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = jaguarIcon };
			result[NpcCategory.Leopard] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = leopardIcon };
			result[NpcCategory.Lizard] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = lizardIcon };
			result[NpcCategory.Llama] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = llamaIcon };
			result[NpcCategory.Longhorn] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = longhornIcon };
			result[NpcCategory.Moose] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = mooseIcon };
			result[NpcCategory.Ostrich] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = ostrichIcon };
			result[NpcCategory.Rhino] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = rhinoIcon };
			result[NpcCategory.Tortoise] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = tortoiseIcon };
			result[NpcCategory.Turkey] = new SpawnLayerInfo() { Name = babyAnimalSpawnName, Icon = turkeyIcon };

			return result;
		}
	}

	/// <summary>
	/// Represents a point of interest on a map
	/// </summary>
	internal class MapPoi : ICloneable
	{
		public byte? GameModeMask { get; set; }
		public int? Key { get; set; }
		public SpawnLayerGroup GroupIndex { get; set; }
		public string Type { get; set; } = null!;
		public string? Title { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Extra { get; set; }
		public string? Region { get; set; }
		public NpcCategory? NpcCategory { get; set; }
		public bool Male { get; set; }
		public bool Female { get; set; }
		public string? TribeStatus { get; set; }
		public string? Occupation { get; set; }
		public int? ClanType { get; set; }
		public string? ClanAreas { get; set; }
		public string? ClanOccupations { get; set; }
		public string? Equipment { get; set; }
		public int SpawnCount { get; set; }
		public int SpawnCountMax { get; set; }
		public float SpawnInterval { get; set; }
		public float PlayerExclusionRadius { get; set; }
		public float BuildingExclusionRadius { get; set; }
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
		public string? ArenaInfo { get; set; }

		public MapPoi()
		{
		}

		public MapPoi(MapPoi other)
		{
			GameModeMask = other.GameModeMask;
			Key = other.Key;
			GroupIndex = other.GroupIndex;
			Type = other.Type;
			Title = other.Title;
			Name = other.Name;
			Description = other.Description;
			Extra = other.Extra;
			Region = other.Region;
			NpcCategory = other.NpcCategory;
			Male = other.Male;
			Female = other.Female;
			TribeStatus = other.TribeStatus;
			Occupation = other.Occupation;
			ClanType = other.ClanType;
			ClanAreas = other.ClanAreas;
			ClanOccupations = other.ClanOccupations;
			Equipment = other.Equipment;
			SpawnCount = other.SpawnCount;
			SpawnCountMax = other.SpawnCountMax;
			SpawnInterval = other.SpawnInterval;
			PlayerExclusionRadius = other.PlayerExclusionRadius;
			BuildingExclusionRadius = other.BuildingExclusionRadius;
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
			ArenaInfo = other.ArenaInfo;
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

	internal static class MapPoiLoader
	{
		public static MapPoiDatabase? Load(string mapName, MapPoiStaticData mapPoiStaticData, Achievements achievements, Logger logger)
		{
			FPropertyTag? poiMapProperty = mapPoiStaticData.MapIntel[mapName].Properties.FirstOrDefault(p => p.Name.Text.Equals("AllTanSuoDianInfoMap"));
			if (poiMapProperty is null)
			{
				return null;
			}

			Dictionary<int, string> pointToRegionMap = new();

			UScriptMap? intelMapProperty = mapPoiStaticData.MapIntel[mapName].Properties.FirstOrDefault(p => p.Name.Text.Equals("MapQingBaoMap"))?
				.Tag?.GetValue<FStructFallback>()?.Properties.FirstOrDefault(p => p.Name.Text.Equals("MapQingBaoMap"))?.Tag?.GetValue<UScriptMap>();
			if (intelMapProperty is not null)
			{
				foreach (var pair in intelMapProperty.Properties)
				{
					FStructFallback valueStruct = pair.Value!.GetValue<FStructFallback>()!;
					string? areaName = null;
					UScriptMap? pointMap = null;
					foreach (FPropertyTag valueProperty in valueStruct.Properties)
					{
						switch (valueProperty.Name.Text)
						{
							case "AreaName":
								areaName = DataUtil.ReadTextProperty(valueProperty);
								break;
							case "TanSuoPointMap":
								pointMap = valueProperty.Tag?.GetValue<UScriptMap>();
								break;
						}
					}

					if (areaName is null || pointMap is null)
					{
						logger.Debug("MapQingBaoMap contains an item with missing data");
						continue;
					}

					foreach (var pointPair in pointMap.Properties)
					{
						int pointVal = pointPair.Key.GetValue<int>();
						if (!pointToRegionMap.TryAdd(pointVal, areaName))
						{
							logger.Debug($"MapQingBaoMap area \"{areaName}\" contains a point that is already present in the region map: {pointVal}");
						}
					}
				}
			}

			MapPoiDatabase poiDatabase = new(mapPoiStaticData, mapName);

			UScriptMap poiMap = poiMapProperty.Tag!.GetValue<FStructFallback>()!.Properties[0].Tag!.GetValue<UScriptMap>()!;
			foreach (var pair in poiMap.Properties)
			{
				int index = pair.Key.GetValue<int>();
				string? region;
				if (!pointToRegionMap.TryGetValue(index, out region))
				{
					region = null;
				}

				FStructFallback? poiProperties = pair.Value?.GetValue<FStructFallback>();
				if (poiProperties is null)
				{
					logger.Warning($"Failed to load data for POI {index}");
					continue;
				}

				ETanSuoDianType? poiType = null;
				MapPoi poi = new()
				{
					Key = index,
					GroupIndex = SpawnLayerGroup.PointOfInterest,
					Region = region
				};
				foreach (FPropertyTag poiProperty in poiProperties.Properties)
				{
					switch (poiProperty.Name.Text)
					{
						case "TSDType":
							if (DataUtil.TryParseEnum(poiProperty, out ETanSuoDianType value))
							{
								poiType = value;
							}
							break;
						case "TSDName":
							poi.Title = DataUtil.ReadTextProperty(poiProperty);
							break;
						case "TSDBossName":
							poi.Name = DataUtil.ReadTextProperty(poiProperty);
							break;
						case "TSDDesc":
							poi.Description = DataUtil.ReadTextProperty(poiProperty);
							break;
						case "TSDDesc1":
							poi.Extra = DataUtil.ReadTextProperty(poiProperty);
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
					logger.Warning($"Failed to locate type for POI {index}");
					continue;
				}

				poi.Type = MapStringUtil.GetType(poiType.Value);
				if (poi.Title is null)
				{
					poi.Title = MapStringUtil.GetTitle(poiType.Value);
				}

				// HACK: Location achievements only seem to apply to Level01_Main, but i could find nothing in the data indicating why
				if (mapName == "Level01_Main" && achievements.CollectMap.TryGetValue(index, out AchievementData? achievement))
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
				else if (poiType == ETanSuoDianType.ETSD_TYPE_ARENA)
				{
					poiDatabase.ArenaPois.Add(poi);
				}
			}

			return poiDatabase;
		}
	}

	internal struct SpawnLayerInfo
	{
		public string Name;
		public UTexture2D Icon;

		public override string ToString()
		{
			return Name;
		}
	}

	internal enum SpawnLayerGroup
	{
		Unset,
		PointOfInterest,
		BabyAnimal,
		Animal,
		Human,
		Event,
		Npc,
		Chest,
		Pickup,
		Ore,
		MineralVein
	}

	internal static class MapStringUtil
	{
		public static string GetTitle(ETanSuoDianType type)
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
				ETanSuoDianType.ETSD_TYPE_ARENA => "Arena",
				ETanSuoDianType.ETSD_TYPE_CAVE => "Cave",
				ETanSuoDianType.ETSD_TYPE_UNDERGROUNDPALACE => "Ancient Dungeon",
				ETanSuoDianType.ETSD_TYPE_WORLDBOSS => "World Boss",
				ETanSuoDianType.ETSD_TYPE_MYSTERYISLAND => "Mysterious Island",
				ETanSuoDianType.ETSD_TYPE_SHIPCAMP => "Airship Camp",
				_ => "Unknown"
			};
		}

		public static string GetType(ETanSuoDianType type)
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
				ETanSuoDianType.ETSD_TYPE_ARENA => "Arena",
				ETanSuoDianType.ETSD_TYPE_CAVE => "Cave",
				ETanSuoDianType.ETSD_TYPE_UNDERGROUNDPALACE => "Ancient Dungeon",
				ETanSuoDianType.ETSD_TYPE_WORLDBOSS => "World Boss",
				ETanSuoDianType.ETSD_TYPE_MYSTERYISLAND => "Mysterious Island",
				ETanSuoDianType.ETSD_TYPE_SHIPCAMP => "Airship Camp",
				_ => "Unknown"
			};
		}

		public static string GetGroupName(SpawnLayerGroup group)
		{
			return group switch
			{
				SpawnLayerGroup.Unset => "",
				SpawnLayerGroup.PointOfInterest => "Point of Interest",
				SpawnLayerGroup.BabyAnimal => "Baby Animal Spawn",
				SpawnLayerGroup.Animal => "Animal Spawn",
				SpawnLayerGroup.Human => "Human Spawn",
				SpawnLayerGroup.Event => "Event Spawn",
				SpawnLayerGroup.Npc => "Other Spawn",
				SpawnLayerGroup.Pickup => "Collectible Objects",
				SpawnLayerGroup.Chest => "Lootable Objects",
				SpawnLayerGroup.Ore => "Ore Deposits",
				SpawnLayerGroup.MineralVein => "Mineral Veins",
				_ => ""
			};
		}
	}
}
