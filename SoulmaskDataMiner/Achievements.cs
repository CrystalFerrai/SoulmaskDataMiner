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

using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;

namespace SoulmaskDataMiner
{
	/// <summary>
	/// Contains data about all achievements, aka trip milestones, from the game
	/// </summary>
	internal class Achievements
	{
		/// <summary>
		/// All achievements, mapped by blueprint class name
		/// </summary>
		public IReadOnlyDictionary<string, AchievementData> AllAchievements { get; private set; }

		/// <summary>
		/// Maps big achievement types to achievement types to individual achievements
		/// </summary>
		public IReadOnlyDictionary<EChengJiuBigType, IReadOnlyDictionary<EChengJiuType, IReadOnlyList<AchievementData>>> AchievementMap { get; private set; }

		/// <summary>
		/// Maps collect IDs to achievements which require them (used by location discovery achievements)
		/// </summary>
		public IReadOnlyDictionary<int, AchievementData> CollectMap { get; private set; }

		private Achievements()
		{
			AllAchievements = null!;
			AchievementMap = null!;
			CollectMap = null!;
		}

		/// <summary>
		/// Loads achievement data
		/// </summary>
		/// <param name="provider">The provider to load from</param>
		/// <param name="logger">For logging warnings and errors</param>
		/// <returns>The loaded data, or null if data could not be loaded</returns>
		public static Achievements? Load(IFileProvider provider, Logger logger)
		{
			UObject? achiementListObj = LoadDefaultsObject("WS/Content/Blueprints/ZiYuanGuanLi/BP_ChengJiuConfig.uasset", provider, logger);
			if (achiementListObj is null)
			{
				logger.LogError("Error loading game achievement list asset");
				return null;
			}

			Achievements instance = new();

			foreach (FPropertyTag property in achiementListObj.Properties)
			{
				switch (property.Name.Text)
				{
					case "ChengJiuBigTypeMap":
						{
							UScriptMap? map = property.Tag?.GetValue<FStructFallback>()?.Properties[0].Tag?.GetValue<UScriptMap>();
							if (map is null)
							{
								logger.LogError("Failed to load achievement map");
								return null;
							}
							instance.LoadAchiementMap(map, logger);
						}
						break;
				}
			}

			return instance;
		}

		private void LoadAchiementMap(UScriptMap sourceMap, Logger logger)
		{
			Dictionary<EquatablePackageIndex, AchievementData> allAchievements = new();
			Dictionary<EChengJiuBigType, IReadOnlyDictionary<EChengJiuType, IReadOnlyList<AchievementData>>> bigAchievementMap = new();
			foreach (var sourcePair in sourceMap.Properties)
			{
				if (!GameUtil.TryParseEnum(sourcePair.Key, out EChengJiuBigType bigType))
				{
					logger.Log(LogLevel.Warning, $"Failed to parse \"{sourcePair.Key.GetValue<FText>()?.Text}\" as {nameof(EChengJiuBigType)}");
					continue;
				}

				UScriptMap? sourceSubMap = sourcePair.Value?.GetValue<FStructFallback>()?.Properties[0].Tag?.GetValue<UScriptMap>();
				if (sourceSubMap is null)
				{
					logger.Log(LogLevel.Warning, $"Failed to read data for big achievement type {bigType}");
					continue;
				}

				IReadOnlyDictionary<EChengJiuType, IReadOnlyList<AchievementData>>? roAchievementMap;
				if (!bigAchievementMap.TryGetValue(bigType, out roAchievementMap))
				{
					roAchievementMap = new Dictionary<EChengJiuType, IReadOnlyList<AchievementData>>();
					bigAchievementMap.Add(bigType, roAchievementMap);
				}
				Dictionary<EChengJiuType, IReadOnlyList<AchievementData>> achievementMap = (Dictionary<EChengJiuType, IReadOnlyList<AchievementData>>)roAchievementMap;

				foreach (var subPair in sourceSubMap.Properties)
				{
					if (!GameUtil.TryParseEnum(subPair.Key, out EChengJiuType type))
					{
						logger.Log(LogLevel.Warning, $"Failed to parse \"{subPair.Key.GetValue<FText>()?.Text}\" as {nameof(EChengJiuType)}");
						continue;
					}

					UScriptArray? sourceList = subPair.Value?.GetValue<FStructFallback>()?.Properties[0].Tag?.GetValue<UScriptArray>();
					if (sourceList is null)
					{
						logger.Log(LogLevel.Warning, $"Failed to read data for achievement type {type}");
						continue;
					}

					IReadOnlyList<AchievementData>? roAchievementList;
					if (!achievementMap.TryGetValue(type, out roAchievementList))
					{
						roAchievementList = new List<AchievementData>();
						achievementMap.Add(type, roAchievementList);
					}
					List<AchievementData> achievementList = (List<AchievementData>)roAchievementList;

					foreach (FPropertyTagType item in sourceList.Properties)
					{
						FPackageIndex? pi = item.GetValue<FPackageIndex>();
						if (pi is null)
						{
							logger.Log(LogLevel.Warning, "Unable to read achievement data from achievement list");
							continue;
						}

						AchievementData? achievement;
						if (!allAchievements.TryGetValue(pi, out achievement))
						{
							achievement = new(pi);
							allAchievements.Add(pi, achievement);
						}

						achievementList.Add(achievement);
					}
				}
			}

			Dictionary<EquatablePackageIndex, AchievementData> allAchievementsClone = new(allAchievements);
			foreach (AchievementData achievement in allAchievementsClone.Values)
			{
				achievement.LoadData(allAchievements, logger);
			}

			Dictionary<int, AchievementData> collectMap = new();
			foreach (AchievementData achievement in allAchievements.Values)
			{
				if (achievement.CollectList is null) continue;

				foreach (int item in achievement.CollectList)
				{
					if (collectMap.ContainsKey(item))
					{
						logger.Log(LogLevel.Warning, $"More than one achievement contains collect item {item}. This is not currently supported by this program.");
						continue;
					}

					collectMap.Add(item, achievement);
				}
			}

			AllAchievements = allAchievements.ToDictionary(p => p.Key.Value.Name, p => p.Value);
			AchievementMap = bigAchievementMap;
			CollectMap = collectMap;
		}

		private static UObject? LoadDefaultsObject(string assetPath, IFileProvider provider, Logger logger)
		{
			if (!provider.TryFindGameFile(assetPath, out GameFile file))
			{
				logger.LogError($"Unable to load asset {Path.GetFileNameWithoutExtension(assetPath)}.");
				return null;
			}

			Package package = (Package)provider.LoadPackage(file);
			return GameUtil.FindBlueprintDefaultsObject(package);
		}
	}

	/// <summary>
	/// Data about a game achievement
	/// </summary>
	internal class AchievementData
	{
		private FPackageIndex mPackageIndex;

		public EChengJiuBigType BigAchievementType { get; private set; }

		public EChengJiuType AchievementType { get; private set; }

		public string? Name { get; private set; }

		public string? Description { get; private set; }

		public UTexture2D? Icon { get; private set; }

		public string? SteamAchievementId { get; private set; }

		public IReadOnlyList<int>? CollectList { get; private set; }

		public IReadOnlyList<AchievementData>? SubAchievements { get; private set; }

		public AchievementData(FPackageIndex packageIndex)
		{
			mPackageIndex = packageIndex;
		}

		public void LoadData(Dictionary<EquatablePackageIndex, AchievementData> allAchievements, Logger logger)
		{
			UObject? defaultsObj = mPackageIndex.Load<UBlueprintGeneratedClass>()?.ClassDefaultObject.Load();
			if (defaultsObj is null)
			{
				logger.Log(LogLevel.Warning, "Unable to load data for achievement");
				return;
			}

			foreach (FPropertyTag property in defaultsObj.Properties)
			{
				switch (property.Name.Text)
				{
					case "ChengJiuBigType":
						{
							if (GameUtil.TryParseEnum(property, out EChengJiuBigType value))
							{
								BigAchievementType = value;
							}
						}
						break;
					case "ChengJiuType":
						{
							if (GameUtil.TryParseEnum(property, out EChengJiuType value))
							{
								AchievementType = value;
							}
						}
						break;
					case "CollectIntParaList":
						{
							UScriptArray? items = property.Tag?.GetValue<UScriptArray>();
							if (items is null)
							{
								break;
							}

							List<int> collectList = new();

							foreach (FPropertyTagType item in items.Properties)
							{
								collectList.Add(item.GetValue<int>());
							}

							CollectList = collectList;
						}
						break;
					case "SubChengJiuList":
						{
							UScriptArray? items = property.Tag?.GetValue<UScriptArray>();
							if (items is null)
							{
								break;
							}

							List<AchievementData> subAchievements = new();

							foreach (FPropertyTagType item in items.Properties)
							{
								FPackageIndex? pi = item.GetValue<FPackageIndex>();
								if (pi is null) continue;

								AchievementData? subAchievement;
								if (!allAchievements.TryGetValue(pi, out subAchievement))
								{
									subAchievement = new(pi);
									allAchievements.Add(pi, subAchievement);
									subAchievement.LoadData(allAchievements, logger);
								}
								subAchievements.Add(subAchievement);
							}

							SubAchievements = subAchievements;
						}
						break;
					case "ChengJiuName":
						Name = GameUtil.ReadTextProperty(property);
						break;
					case "ChengJiuTiaoJian":
						Description = GameUtil.ReadTextProperty(property);
						break;
					case "TextureIcon":
						Icon = GameUtil.ReadTextureProperty(property);
						break;
					case "SteamChengJiuID":
						SteamAchievementId = property.Tag?.GetValue<string>();
						break;
				}
			}
		}

		public override string? ToString()
		{
			return Name;
		}
	}
}
