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

namespace SoulmaskDataMiner.Miners
{
	/// <summary>
	/// Gathers information about item classes
	/// </summary>
	internal class ItemMiner : SubclassMinerBase
	{
		public override string Name => "Item";

		protected override string NameProperty => "Name";

		private const string BaseClassName = "HDaoJuBase";

		public override bool Run(IProviderManager providerManager, Config config, Logger logger)
		{
			BlueprintHeirarchy blueprintHeirarchy = BlueprintHeirarchy.GetOrLoad(providerManager, logger);

			string[] baseClassNames = new string[]
			{
				"HDaoJuBase",
					"HDaoJuZhuangBei",
						"HDaoJuWuQi",
							"HDaoJu_SheJiWuQi",
							"HDaoJu_TouZhi_WuQi",
							"HDaoJuShuiTong",
							"HDaoJu_SheJiWuQi",
							"HDaoJu_TouZhi_WuQi",
							"HDaoJuShuiTong",
					"HDaoJu_ZiDan",
					"HDaoJuXiaoHao",
						"HDaoJuChuCaoJi",
						"HDaoJuFeiLiao",
						"HDaoJuFunction",
						"HDaoJuMianJu",
						"HDaoJuShaChongJi",
						"HDaoJuShiWu",
					"HDaoJuDianChi",
					"HDaoJuHongJingShi",
					"HDaoJuJianZhu",
					"HDaoJuJianZhuPingTai",
					"HDaoJuShuiPing",
					"HDaoJuZhaoMingMoKuai"
			};

			ProcessClasses(blueprintHeirarchy, baseClassNames, "item", config, logger);

			return true;
		}
	}
}
