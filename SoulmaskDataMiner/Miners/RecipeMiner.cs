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
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using SoulmaskDataMiner.Data;
using SoulmaskDataMiner.GameData;
using SoulmaskDataMiner.IO;

namespace SoulmaskDataMiner.Miners
{
	internal class RecipeMiner : SubclassMinerBase
	{
		public override string Name => "Recipe";

		protected override string NameProperty => "PeiFangName";

		protected override string? DescriptionProperty => "TeShuPeiFangYiWen";

		protected override string? IconProperty => "PeiFangIcon";

		protected override IReadOnlySet<string>? AdditionalPropertyNames => sAdditionalPropertyNames;

		private static readonly HashSet<string> sAdditionalPropertyNames;

		private const string BaseClassName_Formula = "HPeiFangBase";

		static RecipeMiner()
		{
			sAdditionalPropertyNames =
			[
				"PeiFangUniqueID",
				"PeiFangDengJi",
				"PeiFangMakeTime",
				"MakeCompleteAddExp",
				"MakeProficiencyType",
				"MakeAddProficiencyExp",
				"DemandDaoJu",
				"ProduceDaoJu",
				"MatchGongZuoTaiData",
				"HiddenRecipeInGameMode"
			];
		}

		public override bool Run(IProviderManager providerManager, Config config, Logger logger, ISqlWriter sqlWriter)
		{
			IEnumerable<ObjectInfo> formulaObjects = FindObjects(BaseClassName_Formula.AsEnumerable());
			Dictionary<EProficiency, Dictionary<WorkbenchInfo, List<RecipeInfo>>> recipeMap = new();
			foreach (ObjectInfo formulaObject in formulaObjects)
			{
				RecipeInfo? recipe = RecipeInfo.Create(formulaObject, logger);
				if (recipe is null)
				{
					continue;
				}

				Dictionary<WorkbenchInfo, List<RecipeInfo>>? wbMap;
				if (!recipeMap.TryGetValue(recipe.ProficiencyType, out wbMap))
				{
					wbMap = new();
					recipeMap.Add(recipe.ProficiencyType, wbMap);
				}

				foreach (WorkbenchInfo workbench in recipe.Workbenches)
				{
					List<RecipeInfo>? recipes;
					if (!wbMap.TryGetValue(workbench, out recipes))
					{
						recipes = new();
						wbMap.Add(workbench, recipes);
					}
					recipes.Add(recipe);
				}
			}

			foreach (List<RecipeInfo> recipes in recipeMap.Values.SelectMany(m => m.Values))
			{
				recipes.Sort();
			}

			WriteCsv(recipeMap, config, logger);
			//WriteSql(recipeMap, sqlWriter, logger);

			return true;
		}

		private void WriteCsv(Dictionary<EProficiency, Dictionary<WorkbenchInfo, List<RecipeInfo>>> recipeMap, Config config, Logger logger)
		{
			foreach (var profPair in recipeMap)
			{
				string outPath = Path.Combine(config.OutputDirectory, Name, $"{profPair.Key}.csv");
				using (FileStream outFile = IOUtil.CreateFile(outPath, logger))
				using (StreamWriter writer = new(outFile))
				{
					writer.WriteLine("id,bench,name,description,icon,level,time,exp,profexp,inputs,output,hiddenmodes");
					foreach (var benchPair in profPair.Value)
					{
						foreach (RecipeInfo r in benchPair.Value)
						{
							string inputs = string.Join(" + ", r.InputItems.Select(i => $"{i.Quantity} [{string.Join(" | ", i.Names)}]"));
							string hiddenInModes = string.Join(", ", r.HiddenInGameModes);
							writer.WriteLine($"{CsvStr(r.UniqueID)},{CsvStr(benchPair.Key.Name)},{CsvStr(r.Name)},{CsvStr(r.Description)},{CsvStr(r.Icon.Name)},{r.Level},{r.CraftTime},{r.ExpGain},{r.ProficiencyExpGain},{CsvStr(inputs)},{CsvStr(r.OutputItem)},{CsvStr(hiddenInModes)}");
						}
					}
				}
			}
		}

		private void WriteSql(Dictionary<EProficiency, Dictionary<WorkbenchInfo, List<RecipeInfo>>> recipeMap, ISqlWriter sqlWriter, Logger logger)
		{
			// Schema
			// create table `recipe`
			// (
			//   TODO
			// )

			sqlWriter.WriteStartTable("recipe");

			foreach (var profPair in recipeMap)
			{
				foreach (var benchPair in profPair.Value)
				{
					// TODO
				}
			}
			sqlWriter.WriteEndTable();
		}

		private class RecipeInfo : IComparable<RecipeInfo>
		{
			public string UniqueID { get; }
			public string Name { get; }
			public string? Description { get; }
			public UTexture2D Icon { get; }
			public int Level { get; }
			public float CraftTime { get; }
			public int ExpGain { get; }
			public EProficiency ProficiencyType { get; }
			public float ProficiencyExpGain { get; }
			public IReadOnlyList<RecipeIngredient> InputItems { get; }
			public string OutputItem { get; }
			public IReadOnlyList<WorkbenchInfo> Workbenches { get; }
			public IReadOnlyList<ECustomGameMode> HiddenInGameModes { get; }

			private RecipeInfo(
				string uniqueId,
				string name,
				string? description,
				UTexture2D icon,
				int level,
				float craftTime,
				int expGain,
				EProficiency proficiencyType,
				float proficiencyExpGain,
				IReadOnlyList<RecipeIngredient> inputItems,
				string outputItem,
				IReadOnlyList<WorkbenchInfo> workbenches,
				IReadOnlyList<ECustomGameMode> hiddenInGameModes)
			{
				UniqueID = uniqueId;
				Name = name;
				Description = description;
				Icon = icon;
				Level = level;
				CraftTime = craftTime;
				ExpGain = expGain;
				ProficiencyType = proficiencyType;
				ProficiencyExpGain = proficiencyExpGain;
				InputItems = inputItems;
				OutputItem = outputItem;
				Workbenches = workbenches;
				HiddenInGameModes = hiddenInGameModes;
			}

			public static RecipeInfo? Create(ObjectInfo info, Logger logger)
			{
				string? uniqueId = null;
				int level = 0;
				float craftTime = 0.0f;
				int expGain = 0;
				EProficiency proficiencyType = EProficiency.Max;
				float proficiencyExpGain = 0.0f;
				List<RecipeIngredient> inputItems = new();
				string? outputItem = null;
				List<WorkbenchInfo> workbenches = new();
				List<ECustomGameMode> hiddenInGameModes = new();

				foreach (var pair in info.AdditionalProperties!)
				{
					switch (pair.Key)
					{
						case "PeiFangUniqueID":
							uniqueId = pair.Value.Tag!.GetValue<FName>().Text;
							break;
						case "PeiFangDengJi":
							level = pair.Value.Tag!.GetValue<int>();
							break;
						case "PeiFangMakeTime":
							craftTime = pair.Value.Tag!.GetValue<float>();
							break;
						case "MakeCompleteAddExp":
							expGain = pair.Value.Tag!.GetValue<int>();
							break;
						case "MakeProficiencyType":
							DataUtil.TryParseEnum(pair.Value, out proficiencyType);
							break;
						case "MakeAddProficiencyExp":
							proficiencyExpGain = pair.Value.Tag!.GetValue<float>();
							break;
						case "DemandDaoJu":
							{
								UScriptArray? ingredientArray = pair.Value.Tag?.GetValue<UScriptArray>();
								if (ingredientArray is not null)
								{
									foreach (FPropertyTagType ingredientItem in ingredientArray.Properties)
									{
										FStructFallback? ingredientStruct = ((FStructFallback?)ingredientItem.GetValue<FScriptStruct>()?.StructType);
										RecipeIngredient? ingredient = null;
										if (ingredientStruct is not null)
										{
											ingredient = RecipeIngredient.Load(ingredientStruct);
										}
										if (ingredient is null)
										{
											logger.Warning($"Unable to read ingredient for recipe \"{info.Name}\"");
											continue;
										}
										inputItems.Add(ingredient);
									}
								}
							}
							break;
						case "ProduceDaoJu":
							outputItem = pair.Value.Tag?.GetValue<FPackageIndex>()?.Name;
							break;
						case "MatchGongZuoTaiData":
							{
								UScriptArray? benchArray = pair.Value.Tag?.GetValue<UScriptArray>();
								if (benchArray is not null)
								{
									foreach (FPropertyTagType benchItem in benchArray.Properties)
									{
										UScriptArray? benchMatchList = ((FStructFallback?)benchItem.GetValue<FScriptStruct>()?.StructType)?.Properties.FirstOrDefault(p => p.Name.Text.Equals("MustMatchGongZuoTaiList"))?.Tag?.GetValue<UScriptArray>();
										if (benchMatchList is null) continue;

										foreach (FPropertyTagType benchMatchItem in benchMatchList.Properties)
										{
											UObject? benchObject = benchMatchItem.GetValue<FPackageIndex>()?.Load<UBlueprintGeneratedClass>()?.ClassDefaultObject.Load();
											WorkbenchInfo? workbench = null;
											if (benchObject is not null)
											{
												workbench = WorkbenchInfo.Load(benchObject);
											}
											if (workbench is null)
											{
												logger.Log(LogLevel.Verbose, $"Unable to read workbench for recipe \"{info.Name}\"");
												continue;
											}
											workbenches.Add(workbench);
										}
									}
								}
							}
							break;
						case "HiddenRecipeInGameMode":
							{
								UScriptArray? hiddenModeArray = pair.Value.Tag?.GetValue<UScriptArray>();
								if (hiddenModeArray is not null)
								{
									foreach (FPropertyTagType hiddenModeItem in hiddenModeArray.Properties)
									{
										if (DataUtil.TryParseEnum(hiddenModeItem, out ECustomGameMode mode))
										{
											hiddenInGameModes.Add(mode);
										}
									}
								}
							}
							break;
					}
				}

				if (proficiencyType == EProficiency.Max)
				{
					// "Max" appears for things like portal activations and boss summons
					return null;
				}

				if (info.Name is null || info.Icon is null || uniqueId is null || outputItem is null)
				{
					logger.Log(LogLevel.Verbose, $"Missing required property for recipe \"{info.Name}\"");
					return null;
				}

				return new(
					uniqueId,
					info.Name,
					info.Description,
					info.Icon,
					level,
					craftTime,
					expGain,
					proficiencyType,
					proficiencyExpGain,
					inputItems,
					outputItem,
					workbenches,
					hiddenInGameModes);
			}

			public int CompareTo(RecipeInfo? other)
			{
				if (other is null) return 1;

				int nameComp = Name.CompareTo(other.Name);
				if (nameComp != 0) return nameComp;

				return UniqueID.CompareTo(other.UniqueID);
			}

			public override string ToString()
			{
				return $"[{UniqueID}] {Name}";
			}
		}

		private class RecipeIngredient
		{
			public IReadOnlyList<string> Names { get; }
			public int Quantity { get; }

			public RecipeIngredient(IReadOnlyList<string> names, int quantity)
			{
				Names = names;
				Quantity = quantity;
			}

			public static RecipeIngredient? Load(IPropertyHolder data)
			{
				List<string>? names = new();
				int quantity = 1;
				foreach (FPropertyTag property in data.Properties)
				{
					switch (property.Name.Text)
					{
						case "DemandDaoJu":
							{
								UScriptArray? itemArray = property.Tag?.GetValue<UScriptArray>();
								if (itemArray is not null)
								{
									foreach (FPropertyTagType item in itemArray.Properties)
									{
										string? itemName = item.GetValue<FPackageIndex>()?.Name;
										if (itemName is not null)
										{
											names.Add(itemName);
										}
									}
								}
							}
							break;
						case "DemandCount":
							quantity = property.Tag!.GetValue<int>();
							break;
					}
				}

				return names.Count == 0 ? null : new(names, quantity);
			}

			public override string ToString()
			{
				return string.Join(" | ", Names);
			}
		}

		private class WorkbenchInfo
		{
			public static WorkbenchInfo Unknown { get; } = new WorkbenchInfo("Unknown", null);

			public string Name { get; }
			public UTexture2D? Icon { get; }

			public WorkbenchInfo(string name, UTexture2D? icon)
			{
				Name = name;
				Icon = icon;
			}

			public static WorkbenchInfo? Load(IPropertyHolder data)
			{
				string? name = null;
				UTexture2D? icon = null;
				foreach (FPropertyTag property in data.Properties)
				{
					switch (property.Name.Text)
					{
						case "JianZhuDisplayName":
							name = DataUtil.ReadTextProperty(property);
							break;
						case "JianZhuIcon":
							icon = DataUtil.ReadTextureProperty(property);
							break;
					}
				}

				return name is null ? null : new(name, icon);
			}

			public override int GetHashCode()
			{
				return Name.GetHashCode();
			}

			public override bool Equals(object? obj)
			{
				return obj is WorkbenchInfo other && Name.Equals(other.Name);
			}

			public override string ToString()
			{
				return Name;
			}
		}
	}
}
