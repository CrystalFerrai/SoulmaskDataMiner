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
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Utility for gathering data about the game's procedural dungeons
	/// </summary>
	internal class DungeonUtil
	{
		private readonly string mLevelName;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="levelName">The name of the level to load dungeon data for</param>
		public DungeonUtil(string levelName = "Level01_Main")
		{
			mLevelName = levelName;
		}

		/// <summary>
		/// Loads data about procedural dungeons
		/// </summary>
		/// <returns>Dungeon data, mapped by entrance actor class names</returns>
		public IReadOnlyDictionary<string, DungeonData>? LoadDungeonData(IProviderManager providerManager, Logger logger)
		{
			IReadOnlyDictionary<string, DungeonConfig>? configMap = LoadDungeonConfig(providerManager, logger);
			if (configMap is null) return null;

			FindDungeonEntrances(providerManager, configMap, logger);

			return configMap.ToDictionary(p => p.Value.Entrance.AssetName, p => CreateDungeonData(p.Value));
		}

		private IReadOnlyDictionary<string, DungeonConfig>? LoadDungeonConfig(IProviderManager providerManager, Logger logger)
		{
			if (!providerManager.SingletonManager.GameSingleton.TryGetPropertyValue("AllDiXiaChengConfigMap", out FStructFallback obj))
			{
				return null;
			}

			UScriptMap? dungeonDataMap = obj.Properties[0].Tag?.GetValue<UScriptMap>();
			if (dungeonDataMap is null) return null;

			UScriptArray? dungeonArray = dungeonDataMap.Properties.FirstOrDefault(p => p.Key.GetValue<string>()!.Equals(mLevelName)).Value?.GetValue<FStructFallback>()?.Properties[0].Tag?.GetValue<UScriptArray>();
			if (dungeonArray is null) return null;

			Dictionary<string, DungeonConfig> result = new();
			foreach (FPropertyTagType item in dungeonArray.Properties)
			{
				string? name = null;
				DungeonConfig config = new();

				FStructFallback itemObj = item.GetValue<FStructFallback>()!;
				foreach (FPropertyTag property in itemObj.Properties)
				{
					switch (property.Name.Text)
					{
						case "Name":
							name = property.Tag!.GetValue<string>()!;
							break;
						case "Title":
							config.Title = GameUtil.ReadTextProperty(property)!;
							break;
						case "Desc":
							config.Description = GameUtil.ReadTextProperty(property)!;
							break;
						case "MaxCount":
							config.MaxCount = property.Tag!.GetValue<int>();
							break;
						case "MaxTimeSeconds":
							config.MaxTimeSeconds = property.Tag!.GetValue<float>();
							break;
						case "MaxPlayers":
							config.MaxPlayers = property.Tag!.GetValue<int>();
							break;
						case "SGFTheme":
							config.ThemeAsset = property.Tag!.GetValue<FSoftObjectPath>()!;
							break;
						case "MaxRetryTimes":
							config.MaxRetryTimes = property.Tag!.GetValue<int>();
							break;
					}
				}

				if (name is null)
				{
					logger.Log(LogLevel.Warning, "Unable to read dungeon config");
					continue;
				}

				result.Add(name, config);
			}

			return result;
		}

		private void FindDungeonEntrances(IProviderManager providerManager, IReadOnlyDictionary<string, DungeonConfig> configMap, Logger logger)
		{
			foreach (var filePair in providerManager.Provider.Files)
			{
				if (!filePair.Key.StartsWith("WS/Content/Blueprints/JianZhu/GameFunction/")) continue;
				if (!filePair.Key.EndsWith(".uasset")) continue;
				if (!filePair.Key.Contains("DiXiaCheng")) continue;

				Package package = (Package)providerManager.Provider.LoadPackage(filePair.Value);
				UObject? defaultsObj = GameUtil.FindBlueprintDefaultsObject(package);
				if (defaultsObj is null) continue;

				UScriptMap? functionMap = defaultsObj.Properties.FirstOrDefault(p => p.Name.Text.Equals("GameFunctionExecutionMap"))?.Tag?.GetValue<UScriptMap>();
				if (functionMap is null || functionMap.Properties.Count == 0) continue;

				var funcData = functionMap.Properties.First();
				UScriptArray? execList = funcData.Value?.GetValue<FStructFallback>()?.Properties[0].Tag?.GetValue<UScriptArray>();
				if (execList is null || execList.Properties.Count == 0) continue;

				EJianZhuGameFunctionType funcType = EJianZhuGameFunctionType.EJZGFT_NOT_DEFINE;
				string? name = null;

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
						case "ExecutePara1":
							name = property.Tag?.GetValue<string>();
							break;
					}
				}

				if (funcType != EJianZhuGameFunctionType.EJZGFT_ENTRY_DIXIACHENG)
				{
					continue;
				}

				if (name is null)
				{
					logger.Log(LogLevel.Warning, $"Dungeon entry game function \"{defaultsObj.Class}\" is missing a parameter");
					continue;
				}

				if (!configMap.TryGetValue(name, out DungeonConfig? dungeonConfig))
				{
					continue;
				}

				FPackageIndex recipeAsset = funcData.Key.GetValue<FPackageIndex>()!;
				UBlueprintGeneratedClass? recipeClass = recipeAsset.Load<UBlueprintGeneratedClass>();
				UObject? recipeObj = recipeClass?.ClassDefaultObject.Load();
				if (recipeObj is null)
				{
					logger.Log(LogLevel.Warning, $"Dungeon entry recipe class \"{recipeAsset.Name}\" could not be loaded");
					continue;
				}

				DungeonEntrance entrance = new() { AssetName = defaultsObj.Class!.Name, ItemCost = new() };
				UScriptArray? recipeItemArray = null;
				foreach (FPropertyTag property in recipeObj.Properties)
				{
					switch (property.Name.Text)
					{
						case "PeiFangDengJi":
							entrance.Level = property.Tag!.GetValue<int>();
							break;
						case "DemandDaoJu":
							recipeItemArray = property.Tag?.GetValue<UScriptArray>();
							break;
						case "DemandMianJuNengLiang":
							entrance.MaskEnergyCost = property.Tag!.GetValue<int>();
							break;
					}
				}

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
							entrance.ItemCost.Add(new() { ItemClass = componentItem.GetValue<FPackageIndex>()!.Name, Count = count });
						}
					}
				}

				dungeonConfig.Entrance = entrance;
			}
		}

		private DungeonData CreateDungeonData(DungeonConfig dungeonConfig)
		{
			return new(
				dungeonConfig.Title,
				dungeonConfig.Description,
				dungeonConfig.Entrance.Level,
				dungeonConfig.MaxCount,
				dungeonConfig.MaxTimeSeconds,
				dungeonConfig.MaxPlayers,
				dungeonConfig.MaxRetryTimes,
				dungeonConfig.ThemeAsset,
				dungeonConfig.Entrance.ItemCost,
				dungeonConfig.Entrance.MaskEnergyCost);
		}

		private class DungeonConfig
		{
			public string Title { get; set; }
			public string Description { get; set; }
			public int MaxCount { get; set; }
			public float MaxTimeSeconds { get; set; }
			public int MaxPlayers { get; set; }
			public int MaxRetryTimes { get; set; }
			public FSoftObjectPath ThemeAsset { get; set; }
			public DungeonEntrance Entrance { get; set; }

			public DungeonConfig()
			{
				Title = null!;
				Description = null!;
			}
		}

		private struct DungeonEntrance
		{
			public string AssetName;
			public int Level;
			public List<RecipeComponent> ItemCost;
			public int MaskEnergyCost;
		}
	}

	/// <summary>
	/// Properties of a procedural dungeon
	/// </summary>
	internal class DungeonData
	{
		/// <summary>
		/// The display name
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// The description
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// The intended level to enter
		/// </summary>
		public int Level { get; }

		/// <summary>
		/// Maximum concurrent instances
		/// </summary>
		public int MaxCount { get; }

		/// <summary>
		/// Maximum time after opening an instance until it is closed
		/// </summary>
		public float MaxTimeSeconds { get; }

		/// <summary>
		/// Max players in an instance
		/// </summary>
		public int MaxPlayers { get; }

		/// <summary>
		/// Unknown
		/// </summary>
		public int MaxRetryTimes { get; }

		/// <summary>
		/// Asset containing data about the dungeon
		/// </summary>
		public FSoftObjectPath ThemeAsset { get; }

		/// <summary>
		/// Item cost for entering
		/// </summary>
		public IReadOnlyList<RecipeComponent> EntranceItemCost { get; }
		
		/// <summary>
		/// Mask energy cost for entering
		/// </summary>
		public int EntranceMaskEnergyCost { get; }

		public DungeonData(
			string title,
			string description,
			int level,
			int maxCount,
			float maxTimeSeconds,
			int maxPlayers,
			int maxRetryTimes,
			FSoftObjectPath themeAsset,
			IReadOnlyList<RecipeComponent> entranceItemCost,
			int entranceMaskEnergyCost)
		{
			Title = title;
			Description = description;
			Level = level;
			MaxCount = maxCount;
			MaxTimeSeconds = maxTimeSeconds;
			MaxPlayers = maxPlayers;
			MaxRetryTimes = maxRetryTimes;
			ThemeAsset = themeAsset;
			EntranceItemCost = entranceItemCost;
			EntranceMaskEnergyCost = entranceMaskEnergyCost;
		}
	}

	/// <summary>
	/// A component of a recipe
	/// </summary>
	internal struct RecipeComponent
	{
		/// <summary>
		/// The required item
		/// </summary>
		public string ItemClass;

		/// <summary>
		/// How many of the item is required
		/// </summary>
		public int Count;
	}
}
