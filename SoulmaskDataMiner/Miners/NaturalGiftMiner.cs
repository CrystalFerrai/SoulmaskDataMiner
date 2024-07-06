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
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.UObject;
using System.Text;

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Mines data about natural gifts (talents)
	/// </summary>
	internal class NaturalGiftMiner : IDataMiner
	{
		public string Name => "Gift";

		public bool Run(IProviderManager providerManager, Config config, Logger logger)
		{
			if (!providerManager.Provider.TryFindGameFile("WS/Content/Blueprints/DataTable/NaturalGift/DT_GiftZongBiao.uasset", out GameFile file))
			{
				logger.LogError("Unable to locate natural gift data table.");
				return false;
			}

			Package package = (Package)providerManager.Provider.LoadPackage(file);
			UDataTable table = (UDataTable)package.ExportMap[0].ExportObject.Value;

			Dictionary<ENaturalGiftSource, List<GiftData>> gifts = new();

			foreach (var row in table.RowMap)
			{
				int id;
				if (!int.TryParse(row.Key.Text, out id))
				{
					logger.Log(LogLevel.Warning, $"Natural gift table row key is not a valid integer: {row.Key.Text}");
					continue;
				}

				bool isGood = IsGood(id);

				int level = 0;
				ENaturalGiftSource source = ENaturalGiftSource.Normal;
				string? title = null, description = null;

				foreach (FPropertyTag property in row.Value.Properties)
				{
					switch (property.Name.Text)
					{
						case "Star":
							level = property.Tag!.GetValue<int>();
							break;
						case "NGEffectSource":
							source = ParseSource(property.Tag!.GetValue<FName>()!.Text, id, logger);
							break;
						case "Title":
							title = property.Tag!.GetValue<FText>()!.Text;
							break;
						case "Desc":
							description = property.Tag!.GetValue<FText>()!.Text;
							break;
					}
				}

				List<GiftData>? list;
				if (!gifts.TryGetValue(source, out list))
				{
					list = new();
					gifts.Add(source, list);
				}
				list.Add(new()
				{
					ID = id,
					Level = level,
					IsGood = isGood,
					Title = title,
					Description = description
				});
			}

			foreach (var pair in gifts)
			{
				pair.Value.Sort();

				string outPathGood = Path.Combine(config.OutputDirectory, Name, $"Good_{TranslateGiftSource(pair.Key)}.csv");
				string outPathBad = Path.Combine(config.OutputDirectory, Name, $"Bad_{TranslateGiftSource(pair.Key)}.csv");
				using FileStream streamGood = IOUtil.CreateFile(outPathGood, logger);
				using FileStream streamBad = IOUtil.CreateFile(outPathBad, logger);
				using StreamWriter writerGood = new(streamGood, Encoding.UTF8);
				using StreamWriter writerBad = new(streamBad, Encoding.UTF8);

				writerGood.WriteLine("Id,Level,Title,Description");
				writerBad.WriteLine("Id,Level,Title,Description");

				foreach (GiftData gift in pair.Value)
				{
					StreamWriter writer = gift.IsGood ? writerGood : writerBad;
					writer.WriteLine($"{gift.ID},{gift.Level},\"{gift.Title}\",\"{gift.Description}\"");
				}
			}

			return true;
		}

		private static ENaturalGiftSource ParseSource(string text, int id, Logger logger)
		{
			text = text[(text.LastIndexOf(':') + 1)..];
			if (!Enum.TryParse<ENaturalGiftSource>(text, out ENaturalGiftSource result))
			{
				logger.Log(LogLevel.Warning, $"Unable to parse gift source \"{text}\" for gift {id}");
			}
			return result;
		}

		private static bool IsGood(int id)
		{
			return id < 200000
				|| id >= 300000 && id < 510000
				|| id >= 600000 && id < 900000 && id != 600051 && id != 600054 && id != 600055 && id != 600056;
		}

		private static string TranslateGiftSource(ENaturalGiftSource source)
		{
			return source switch
			{
				ENaturalGiftSource.Normal => "Normal",
				ENaturalGiftSource.BornChuShen => "Origin",
				ENaturalGiftSource.BornBuLuoCiTiao => "Tribe",
				ENaturalGiftSource.ChengHao => "Title",
				ENaturalGiftSource.JingLi => "Experience",
				ENaturalGiftSource.XiHao => "Preference",
				ENaturalGiftSource.XingGe => "Personality",
				ENaturalGiftSource.GuanXi => "Relationship",
				_ => "Unknown"
			};
		}

		private struct GiftData : IComparable<GiftData>
		{
			public int ID;
			public int Level;

			public bool IsGood;

			public string? Title;
			public string? Description;

			public int CompareTo(GiftData other)
			{
				return ID.CompareTo(other.ID);
			}

			public override string ToString()
			{
				return $"[{ID}] ({(IsGood ? "Good" : "Bad")}) {Title}";
			}
		}

		private enum ENaturalGiftSource
		{
			Normal,
			BornChuShen,
			BornBuLuoCiTiao,
			ChengHao,
			JingLi,
			XiHao,
			XingGe,
			GuanXi
		};
	}
}
