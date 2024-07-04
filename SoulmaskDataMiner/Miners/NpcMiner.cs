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
	/// Gathers information about NPC classes
	/// </summary>
	internal class NpcMiner : SubclassMinerBase
	{
		public override string Name => "Npc";

		protected override string NameProperty => "MoRenMingZi";

		private const string BaseClassName_NonHuman = "HCharacterDongWu";
		private const string BaseClassName_Human = "HCharacterRen";
		private const string BaseClassName_Other = "HCharacterBase";

		public override bool Run(IProviderManager providerManager, Config config, Logger logger)
		{
			BlueprintHeirarchy blueprintHeirarchy = BlueprintHeirarchy.GetOrLoad(providerManager, logger);

			ProcessClasses(blueprintHeirarchy, BaseClassName_NonHuman.AsEnumerable(), "nonhuman", config, logger);
			ProcessClasses(blueprintHeirarchy, BaseClassName_Human.AsEnumerable(), "human", config, logger);
			ProcessClasses(blueprintHeirarchy, BaseClassName_Other.AsEnumerable(), "other", config, logger);

			return true;
		}
	}
}
