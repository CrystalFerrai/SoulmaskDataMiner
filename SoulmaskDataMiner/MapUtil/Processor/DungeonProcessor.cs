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

using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using SoulmaskDataMiner.GameData;
using System.Text;
using System.Threading.Tasks;

namespace SoulmaskDataMiner.MapUtil.Processor
{
	/// <summary>
	/// Map point of interest processor for dungeon locations
	/// </summary>
	internal class DungeonProcessor : ProcessorBase
	{
		public DungeonProcessor(MapData mapData)
			: base(mapData)
		{
		}

		public void Process(MapPoiDatabase poiDatabase, IReadOnlyList<FObjectExport> dungeonObjects, Logger logger)
		{
			logger.Information($"Processing {dungeonObjects.Count} dungeons...");

			foreach (FObjectExport export in dungeonObjects)
			{
				USceneComponent? rootComponent = export.ExportObject.Value.Properties.FirstOrDefault(p => p.Name.Text.Equals("RootComponent"))?.Tag?.GetValue<FPackageIndex>()?.Load<USceneComponent>();
				FPropertyTag? locationProperty = rootComponent?.Properties.FirstOrDefault(p => p.Name.Text.Equals("RelativeLocation"));
				if (locationProperty is null)
				{
					logger.Warning($"Failed to locate dungeon entrance {export.ObjectName}");
					continue;
				}

				DungeonData dungeonData = poiDatabase.DungeonMap[export.ClassName];
				FVector location = locationProperty.Tag!.GetValue<FVector>();

				foreach (MapPoi dungeonPoi in poiDatabase.DungeonPois)
				{
					if (!dungeonPoi.Location.HasValue) continue;

					FVector distance = location - dungeonPoi.Location.Value;
					if (distance.SizeSquared() < 400000000.0f) // 200 meters
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
								NpcCategory category = SpawnDataUtil.GetNpcCategory(firstNpc);
								if (category != NpcCategory.Mechanical)
								{
									logger.Warning($"Unhandled NPC type {category}");
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
							foreach (ChestData chest in dungeonData.Chests)
							{
								if (chest.LootItem is not null)
								{
									logger.Warning($"Dungeon chest {chest.Name} contains a LootItem. Support for this needs to be implemented.");
								}

								builder.Append("{");
								builder.Append($"\"name\":\"{chest.Name}\"");

								if (chest.AvailableGameModes.Count > 0)
								{
									logger.Warning("Dungeon chest is not available in all game modes. This has not been encountered before.");

									byte mask = 0;
									foreach (ECustomGameMode mode in chest.AvailableGameModes)
									{
										mask |= mode.CreateMask();
									}
									builder.Append($",\"modes\":{mask}");
								}
								else
								{
									builder.Append($",\"modes\":{GameEnumExtensions.AllGameModesMask}");
								}

								byte remainingModes = GameEnumExtensions.AllGameModesMask;
								builder.Append(",\"loot\":[");
								foreach (var pair in chest.GameModeLootIds)
								{
									byte mask = pair.Key.CreateMask();
									remainingModes &= (byte)~mask;
									builder.Append($"{{\"m\":{mask},\"l\":\"{pair.Value}\"}},");
								}
								if (remainingModes == 0)
								{
									--builder.Length; // Remove trailing comma
								}
								else
								{
									builder.Append($"{{\"m\":{remainingModes},\"l\":{(chest.LootId is null ? "null" : $"\"{chest.LootId}\"")}}}");
								}
								builder.Append("]");

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
	}
}
